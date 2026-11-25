using System;
using System.Diagnostics;

public sealed class OnlineMatchSession
{
    public const int DefaultInputDelayFrames = 2;
    public const bool DefaultRollbackEnabled = true;
    public const int MaxRollbackFrames = 8;
    private const int InputRingSize = 256;
    private const int SnapshotRingSize = MaxRollbackFrames + 2;
    private const int ChecksumIntervalTicks = 60;
    private const int MaxChecksumsTracked = 32;

    private struct InputSlot
    {
        public int Tick;
        public ushort Bitmask;
        public bool HasConfirmed;
        public ushort AppliedBitmask;
        public bool WasSimulated;
    }

    private readonly InputSlot[] _localInputs = new InputSlot[InputRingSize];
    private readonly InputSlot[] _remoteInputs = new InputSlot[InputRingSize];
    private readonly SimulationSnapshot[] _snapshots = new SimulationSnapshot[SnapshotRingSize];
    private readonly NetTickInput[] _sendBundleScratch = new NetTickInput[4];
    private readonly (int tick, uint checksum)[] _localChecksums = new (int, uint)[MaxChecksumsTracked];
    private readonly (int tick, uint checksum)[] _remoteChecksums = new (int, uint)[MaxChecksumsTracked];

    private ushort _prevLocalApplied;
    private ushort _prevRemoteApplied;
    private ushort _lastConfirmedRemoteMask;
    private int _latestRemoteConfirmedTick = -1;
    private int _pendingRollbackTick = -1;
    private int _pingIntervalCounter = 0;

    public bool IsActive { get; private set; }
    public ulong PeerSteamId { get; private set; }
    public int LocalSlot { get; private set; }
    public int RemoteSlot => LocalSlot == 0 ? 1 : 0;
    public int InputDelayFrames { get; private set; } = DefaultInputDelayFrames;
    public bool RollbackEnabled { get; private set; } = DefaultRollbackEnabled;

    public int CurrentTick { get; private set; }
    public int PingMs { get; private set; }
    public int RollbackCount { get; private set; }
    public bool IsStalledWaitingForPeer { get; private set; }
    public bool DesyncDetected { get; private set; }
    public int DesyncCount { get; private set; }

    public OnlineMatchSession()
    {
        for (int i = 0; i < _snapshots.Length; i++)
            _snapshots[i] = new SimulationSnapshot();
    }

    public void Begin(ulong peerSteamId, int localSlot, int inputDelayFrames = DefaultInputDelayFrames, bool rollbackEnabled = DefaultRollbackEnabled)
    {
        PeerSteamId = peerSteamId;
        LocalSlot = localSlot;
        InputDelayFrames = Math.Clamp(inputDelayFrames, 0, 6);
        RollbackEnabled = rollbackEnabled;
        CurrentTick = 0;
        PingMs = 0;
        RollbackCount = 0;
        IsStalledWaitingForPeer = false;
        DesyncDetected = false;
        DesyncCount = 0;
        _prevLocalApplied = 0;
        _prevRemoteApplied = 0;
        _lastConfirmedRemoteMask = 0;
        _latestRemoteConfirmedTick = -1;
        _pendingRollbackTick = -1;
        _pingIntervalCounter = 0;

        Array.Clear(_localInputs);
        Array.Clear(_remoteInputs);
        Array.Clear(_localChecksums);
        Array.Clear(_remoteChecksums);

        // Pre-seed initial input delay frames cleanly with neutral inputs so tick 0..Delay-1 never stall
        for (int t = 0; t < InputDelayFrames; t++)
        {
            SetConfirmedInput(_localInputs, t, 0);
            SetConfirmedInput(_remoteInputs, t, 0);
        }

        IsActive = peerSteamId != 0UL;
    }

    public void Disconnect()
    {
        IsActive = false;
        IsStalledWaitingForPeer = false;
        PeerSteamId = 0UL;
    }

    public void PollNonMatchTraffic(EngineServices services)
    {
        if (IsActive) return;
        SteamNetTransport.PollAll(null, services);
    }

    public void SampleHardwareEdgesForRenderFrame(EngineServices services)
    {
        if (!IsActive || GameState.CurrentState != State.Playing) return;
        Player? localPlayer = LocalSlot == 0 ? services.Player1 : services.Player2;
        localPlayer?.LatchOnlineHardwareEdges(bindingsIndex: 0);
    }

    public void StepFixedTick(GameData data)
    {
        if (!IsActive || data.Player1 == null || data.Player2 == null || data.Services == null)
            return;

        SteamNetTransport.PollAll(this, data.Services);

        if (++_pingIntervalCounter >= 30)
        {
            _pingIntervalCounter = 0;
            SteamNetTransport.SendPing(PeerSteamId, Stopwatch.GetTimestamp());
        }

        Player p1 = data.Player1;
        Player p2 = data.Player2;
        Player localPlayer = LocalSlot == 0 ? p1 : p2;
        PhysicsEngine physics = data.Services.Physics;
        MatchManager match = data.Services.Match;
        ProjectileManager projectiles = data.Services.Projectiles;

        // 1. Sample local input and schedule at CurrentTick + InputDelayFrames
        int targetInputTick = CurrentTick + InputDelayFrames;
        if (!_localInputs[targetInputTick % InputRingSize].HasConfirmed ||
            _localInputs[targetInputTick % InputRingSize].Tick != targetInputTick)
        {
            ushort mask = localPlayer.SampleHardwareInputBitmask(bindingsIndex: 0);
            SetConfirmedInput(_localInputs, targetInputTick, mask);
        }

        // Send redundant recent inputs (last 4 ticks) on unreliable channel
        SendRecentInputBundle(targetInputTick);

        // 2. Execute Phase B rollback if any previously predicted remote input turned out to differ
        if (RollbackEnabled && _pendingRollbackTick >= 0 && _pendingRollbackTick < CurrentTick)
        {
            PerformRollback(data, p1, p2, physics, match, projectiles);
        }
        _pendingRollbackTick = -1;

        // 3. Check synchronization window for CurrentTick
        bool remoteConfirmedForCurrent = TryGetConfirmedMask(_remoteInputs, CurrentTick, out ushort confirmedRemoteMask);

        if (!remoteConfirmedForCurrent)
        {
            int framesAhead = CurrentTick - (_latestRemoteConfirmedTick + 1);
            if (!RollbackEnabled || framesAhead >= MaxRollbackFrames)
            {
                IsStalledWaitingForPeer = true;
                return;
            }
        }
        IsStalledWaitingForPeer = false;

        // 4. Snapshot exact deterministic state BEFORE simulating CurrentTick
        ushort localMaskForTick = GetConfirmedOrLast(_localInputs, CurrentTick, 0);
        ushort remoteMaskForTick = remoteConfirmedForCurrent ? confirmedRemoteMask : _lastConfirmedRemoteMask;

        ushort p1Prev = LocalSlot == 0 ? _prevLocalApplied : _prevRemoteApplied;
        ushort p2Prev = LocalSlot == 0 ? _prevRemoteApplied : _prevLocalApplied;
        _snapshots[CurrentTick % SnapshotRingSize].Capture(CurrentTick, p1, p2, match, projectiles, p1Prev, p2Prev);

        RecordAppliedMask(_localInputs, CurrentTick, localMaskForTick);
        RecordAppliedMask(_remoteInputs, CurrentTick, remoteMaskForTick);

        // 5. Apply P1 and P2 network inputs and advance 1 deterministic 60Hz tick
        ApplyInputsAndStep(p1, p2, localMaskForTick, remoteMaskForTick, p1Prev, p2Prev, physics, data, suppressPresentation: false);

        _prevLocalApplied = localMaskForTick;
        _prevRemoteApplied = remoteMaskForTick;

        // 6. Validate state checksum periodically
        if (CurrentTick > 0 && (CurrentTick % ChecksumIntervalTicks) == 0)
        {
            uint checksum = SimulationSnapshot.ComputeStateChecksum(p1, p2, match, projectiles);
            StoreChecksum(_localChecksums, CurrentTick, checksum);
            CompareChecksumIfBothPresent(CurrentTick);
        }

        CurrentTick++;
    }

    private void PerformRollback(
        GameData data,
        Player p1,
        Player p2,
        PhysicsEngine physics,
        MatchManager match,
        ProjectileManager projectiles)
    {
        int earliestAvailableSnap = Math.Max(0, CurrentTick - MaxRollbackFrames);
        int restoreTick = Math.Max(_pendingRollbackTick, earliestAvailableSnap);

        SimulationSnapshot snap = _snapshots[restoreTick % SnapshotRingSize];
        if (snap.Tick != restoreTick)
            return;

        snap.Restore(p1, p2, match, projectiles);
        ushort prevP1 = snap.PrevP1Bitmask;
        ushort prevP2 = snap.PrevP2Bitmask;

        for (int t = restoreTick; t < CurrentTick; t++)
        {
            ushort localMask = GetConfirmedOrLast(_localInputs, t, 0);
            ushort remoteMask = TryGetConfirmedMask(_remoteInputs, t, out ushort confRemote)
                ? confRemote
                : _lastConfirmedRemoteMask;

            if (t > restoreTick)
            {
                _snapshots[t % SnapshotRingSize].Capture(t, p1, p2, match, projectiles, prevP1, prevP2);
            }

            RecordAppliedMask(_localInputs, t, localMask);
            RecordAppliedMask(_remoteInputs, t, remoteMask);

            ApplyInputsAndStep(p1, p2, localMask, remoteMask, prevP1, prevP2, physics, data, suppressPresentation: true);

            prevP1 = LocalSlot == 0 ? localMask : remoteMask;
            prevP2 = LocalSlot == 0 ? remoteMask : localMask;

            if (t > 0 && (t % ChecksumIntervalTicks) == 0)
            {
                uint checksum = SimulationSnapshot.ComputeStateChecksum(p1, p2, match, projectiles);
                StoreChecksum(_localChecksums, t, checksum);
                CompareChecksumIfBothPresent(t);
            }
        }

        _prevLocalApplied = LocalSlot == 0 ? prevP1 : prevP2;
        _prevRemoteApplied = LocalSlot == 0 ? prevP2 : prevP1;
        RollbackCount++;
    }

    private void ApplyInputsAndStep(
        Player p1,
        Player p2,
        ushort localMask,
        ushort remoteMask,
        ushort prevP1,
        ushort prevP2,
        PhysicsEngine physics,
        GameData data,
        bool suppressPresentation)
    {
        ushort p1Mask = LocalSlot == 0 ? localMask : remoteMask;
        ushort p2Mask = LocalSlot == 0 ? remoteMask : localMask;

        p1.ApplyNetworkInputBitmask(p1Mask, prevP1);
        p2.ApplyNetworkInputBitmask(p2Mask, prevP2);

        physics.StepDeterministicTick(p1, p2, data, suppressPresentation);
    }

    private void SendRecentInputBundle(int latestTick)
    {
        int count = 0;
        for (int i = 0; i < 4; i++)
        {
            int tick = latestTick - i;
            if (tick < 0) break;

            ref InputSlot slot = ref _localInputs[tick % InputRingSize];
            if (slot.HasConfirmed && slot.Tick == tick)
            {
                _sendBundleScratch[count++] = new NetTickInput
                {
                    Tick = tick,
                    InputBitmask = slot.Bitmask
                };
            }
        }

        int latestChecksumTick = -1;
        uint latestChecksumVal = 0u;
        int lastCheckIdx = -1;
        for (int i = 0; i < _localChecksums.Length; i++)
        {
            if (_localChecksums[i].tick > latestChecksumTick)
            {
                latestChecksumTick = _localChecksums[i].tick;
                latestChecksumVal = _localChecksums[i].checksum;
                lastCheckIdx = i;
            }
        }
        _ = lastCheckIdx;

        SteamNetTransport.SendInputBundle(
            PeerSteamId,
            latestTick,
            _latestRemoteConfirmedTick,
            latestChecksumTick,
            latestChecksumVal,
            _sendBundleScratch.AsSpan(0, count));
    }

    public void OnRemoteInputBundle(
        ulong senderId,
        int latestTick,
        int ackTick,
        int checksumTick,
        uint checksumValue,
        ReadOnlySpan<NetTickInput> inputs)
    {
        if (!IsActive || senderId != PeerSteamId) return;
        _ = latestTick;
        _ = ackTick;

        for (int i = 0; i < inputs.Length; i++)
        {
            int t = inputs[i].Tick;
            ushort mask = inputs[i].InputBitmask;
            if (t < 0) continue;

            ref InputSlot slot = ref _remoteInputs[t % InputRingSize];
            bool alreadySameTickConfirmed = slot.HasConfirmed && slot.Tick == t;

            if (!alreadySameTickConfirmed)
            {
                bool mispredicted = slot.WasSimulated && slot.Tick == t && slot.AppliedBitmask != mask;
                slot.Tick = t;
                slot.Bitmask = mask;
                slot.HasConfirmed = true;

                if (t >= _latestRemoteConfirmedTick)
                {
                    _latestRemoteConfirmedTick = t;
                    _lastConfirmedRemoteMask = mask;
                }

                if (mispredicted && t < CurrentTick)
                {
                    if (_pendingRollbackTick < 0 || t < _pendingRollbackTick)
                        _pendingRollbackTick = t;
                }
            }
        }

        if (checksumTick > 0)
        {
            StoreChecksum(_remoteChecksums, checksumTick, checksumValue);
            CompareChecksumIfBothPresent(checksumTick);
        }
    }

    public void OnPongReceived(ulong senderId, long echoTimestampTicks)
    {
        if (!IsActive || senderId != PeerSteamId) return;
        long elapsedTicks = Stopwatch.GetTimestamp() - echoTimestampTicks;
        double ms = (elapsedTicks * 1000.0) / Stopwatch.Frequency;
        if (ms >= 0 && ms < 10000)
        {
            PingMs = (int)Math.Round(ms);
        }
    }

    public void OnPeerDesyncNotice(ulong senderId, int tick, uint peerChecksum, uint peerSawChecksum)
    {
        if (!IsActive || senderId != PeerSteamId) return;
        _ = tick;
        _ = peerChecksum;
        _ = peerSawChecksum;
        DesyncDetected = true;
        DesyncCount++;
    }

    private void CompareChecksumIfBothPresent(int tick)
    {
        int idx = (tick / ChecksumIntervalTicks) % MaxChecksumsTracked;
        if (_localChecksums[idx].tick == tick && _remoteChecksums[idx].tick == tick)
        {
            if (_localChecksums[idx].checksum != _remoteChecksums[idx].checksum)
            {
                DesyncDetected = true;
                DesyncCount++;
                SteamNetTransport.SendDesyncNotice(PeerSteamId, tick, _localChecksums[idx].checksum, _remoteChecksums[idx].checksum);
            }
        }
    }

    private static void StoreChecksum((int tick, uint checksum)[] ring, int tick, uint checksum)
    {
        int idx = (tick / ChecksumIntervalTicks) % MaxChecksumsTracked;
        ring[idx] = (tick, checksum);
    }

    private static void SetConfirmedInput(InputSlot[] ring, int tick, ushort mask)
    {
        int idx = tick % InputRingSize;
        ring[idx] = new InputSlot
        {
            Tick = tick,
            Bitmask = mask,
            HasConfirmed = true,
            AppliedBitmask = ring[idx].Tick == tick ? ring[idx].AppliedBitmask : (ushort)0,
            WasSimulated = ring[idx].Tick == tick && ring[idx].WasSimulated
        };
    }

    private static void RecordAppliedMask(InputSlot[] ring, int tick, ushort appliedMask)
    {
        int idx = tick % InputRingSize;
        ring[idx].Tick = tick;
        ring[idx].AppliedBitmask = appliedMask;
        ring[idx].WasSimulated = true;
    }

    private static bool TryGetConfirmedMask(InputSlot[] ring, int tick, out ushort mask)
    {
        ref InputSlot slot = ref ring[tick % InputRingSize];
        if (slot.HasConfirmed && slot.Tick == tick)
        {
            mask = slot.Bitmask;
            return true;
        }
        mask = 0;
        return false;
    }

    private static ushort GetConfirmedOrLast(InputSlot[] ring, int tick, ushort fallback)
    {
        ref InputSlot slot = ref ring[tick % InputRingSize];
        return (slot.HasConfirmed && slot.Tick == tick) ? slot.Bitmask : fallback;
    }
}
