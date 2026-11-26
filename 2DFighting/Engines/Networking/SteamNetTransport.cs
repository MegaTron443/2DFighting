using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Steamworks;

public static class SteamNetTransport
{
    private const int ChannelInputs = 0;
    private const int ChannelControl = 1;
    private const int MaxMessagesPerPoll = 64;
    private const int ModChunkSizeBytes = 40 * 1024;
    private const int SendBufferSizeBytes = 1024 * 1024;

    private static readonly IntPtr[] _msgPtrs = new IntPtr[MaxMessagesPerPoll];
    private static readonly HashSet<ulong> _acceptedPeers = new();
    private static byte[] _rxScratch = new byte[64 * 1024];
    private static readonly byte[] _inputSendBuf = new byte[64];

    private static Callback<SteamNetworkingMessagesSessionRequest_t>? _sessionRequestCallback;
    private static Callback<SteamNetworkingMessagesSessionFailed_t>? _sessionFailedCallback;
    private static int _nextTransferId = 1;
    private static long _lastInputSendErrorLogTimestamp = 0;

    public static bool IsPeerSessionActive(ulong peerSteamId) =>
        peerSteamId != 0UL && _acceptedPeers.Contains(peerSteamId);

    public static void Initialize()
    {
        if (!SteamManager.IsInitialized) return;

        _acceptedPeers.Clear();
        _lastInputSendErrorLogTimestamp = 0;
        SetGlobalConfigInt32(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize, SendBufferSizeBytes);
        _sessionRequestCallback = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
        _sessionFailedCallback = Callback<SteamNetworkingMessagesSessionFailed_t>.Create(OnSessionFailed);
    }

    public static void Shutdown()
    {
        if (SteamManager.IsInitialized && _acceptedPeers.Count > 0)
        {
            ulong[] peers = new ulong[_acceptedPeers.Count];
            _acceptedPeers.CopyTo(peers);
            for (int i = 0; i < peers.Length; i++)
            {
                CloseSessionWithPeer(peers[i]);
            }
        }

        _acceptedPeers.Clear();
        _sessionRequestCallback?.Dispose();
        _sessionRequestCallback = null;
        _sessionFailedCallback?.Dispose();
        _sessionFailedCallback = null;
    }

    public static void CloseSessionWithPeer(ulong peerSteamId)
    {
        if (peerSteamId == 0UL) return;

        _acceptedPeers.Remove(peerSteamId);
        if (!SteamManager.IsInitialized) return;

        var identity = CreateIdentity(peerSteamId);
        SteamNetworkingMessages.CloseSessionWithUser(ref identity);
    }

    private static unsafe void SetGlobalConfigInt32(ESteamNetworkingConfigValue configValue, int value)
    {
        int val = value;
        SteamNetworkingUtils.SetConfigValue(
            configValue,
            ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
            IntPtr.Zero,
            ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
            (IntPtr)(&val));
    }

    private static void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t callback)
    {
        ulong senderId = callback.m_identityRemote.GetSteamID().m_SteamID;
        if (SteamLobbyManager.IsPeerAuthorized(senderId))
        {
            bool accepted = SteamNetworkingMessages.AcceptSessionWithUser(ref callback.m_identityRemote);
            if (accepted)
            {
                _acceptedPeers.Add(senderId);
                Console.WriteLine($"[SteamNetTransport] Accepted P2P session with peer {senderId}.");
                SteamLobbyManager.OnPeerSessionEstablished(senderId);
            }
            else
            {
                Console.WriteLine($"[SteamNetTransport] AcceptSessionWithUser returned false for peer {senderId}.");
            }
        }
    }

    private static void OnSessionFailed(SteamNetworkingMessagesSessionFailed_t callback)
    {
        ulong peerId = callback.m_info.m_identityRemote.GetSteamID().m_SteamID;
        string debugInfo = callback.m_info.m_szEndDebug ?? string.Empty;
        Console.WriteLine($"[SteamNetTransport] Session failed for peer {peerId}: reason={callback.m_info.m_eEndReason} ({debugInfo})");

        SteamNetworkingMessages.CloseSessionWithUser(ref callback.m_info.m_identityRemote);
        _acceptedPeers.Remove(peerId);
        SteamLobbyManager.OnPeerSessionFailed(peerId);
    }

    public static unsafe void SendInputBundle(
        ulong peerSteamId,
        int latestTick,
        int ackTick,
        int checksumTick,
        uint checksumValue,
        ReadOnlySpan<NetTickInput> recentInputs)
    {
        if (!SteamManager.IsInitialized || peerSteamId == 0UL) return;

        int count = Math.Min(4, recentInputs.Length);
        int offset = 0;

        _inputSendBuf[offset++] = (byte)NetMessageType.InputBundle;
        WriteInt32(_inputSendBuf, ref offset, latestTick);
        WriteInt32(_inputSendBuf, ref offset, ackTick);
        WriteInt32(_inputSendBuf, ref offset, checksumTick);
        WriteUInt32(_inputSendBuf, ref offset, checksumValue);
        _inputSendBuf[offset++] = (byte)count;

        for (int i = 0; i < count; i++)
        {
            WriteInt32(_inputSendBuf, ref offset, recentInputs[i].Tick);
            WriteUInt16(_inputSendBuf, ref offset, recentInputs[i].InputBitmask);
        }

        var identity = CreateIdentity(peerSteamId);
        EResult result;
        fixed (byte* pData = _inputSendBuf)
        {
            result = SteamNetworkingMessages.SendMessageToUser(
                ref identity,
                (IntPtr)pData,
                (uint)offset,
                Constants.k_nSteamNetworkingSend_UnreliableNoNagle | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession,
                ChannelInputs);
        }

        if (result == EResult.k_EResultNoConnection || result == EResult.k_EResultInvalidState)
        {
            long now = Stopwatch.GetTimestamp();
            if (now - _lastInputSendErrorLogTimestamp >= Stopwatch.Frequency)
            {
                _lastInputSendErrorLogTimestamp = now;
                Console.WriteLine($"[SteamNetTransport] SendInputBundle to {peerSteamId} returned {result}; closing stale session.");
            }
            CloseSessionWithPeer(peerSteamId);
        }
    }

    public static void SendFighterSelection(
        ulong peerSteamId,
        string charId,
        string scriptHash,
        string baseScriptHash,
        bool ready,
        bool gaEnabled,
        ulong workshopId = 0UL)
    {
        using var ms = new MemoryStream(160);
        using var bw = new BinaryWriter(ms, Encoding.UTF8);
        bw.Write((byte)NetMessageType.FighterSelection);
        bw.Write(charId ?? string.Empty);
        bw.Write(scriptHash ?? string.Empty);
        bw.Write(baseScriptHash ?? string.Empty);
        bw.Write(ready);
        bw.Write(gaEnabled);
        bw.Write(workshopId);
        bw.Flush();
        SendReliable(peerSteamId, ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    public static void SendModScriptRequest(ulong peerSteamId, string charId, string expectedHash)
    {
        using var ms = new MemoryStream(96);
        using var bw = new BinaryWriter(ms, Encoding.UTF8);
        bw.Write((byte)NetMessageType.ModScriptRequest);
        bw.Write(charId ?? string.Empty);
        bw.Write(expectedHash ?? string.Empty);
        bw.Flush();
        SendReliable(peerSteamId, ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    public static void SendModScriptBundle(
        ulong peerSteamId,
        string charId,
        string scriptHash,
        string baseScriptHash,
        string luaSource,
        string? balanceJson)
    {
        byte[] compressed;
        using (var rawMs = new MemoryStream())
        {
            using (var gzip = new GZipStream(rawMs, CompressionLevel.Fastest, leaveOpen: true))
            using (var bw = new BinaryWriter(gzip, Encoding.UTF8))
            {
                bw.Write(charId ?? string.Empty);
                bw.Write(scriptHash ?? string.Empty);
                bw.Write(baseScriptHash ?? string.Empty);
                bw.Write(luaSource ?? string.Empty);
                bw.Write(!string.IsNullOrEmpty(balanceJson));
                if (!string.IsNullOrEmpty(balanceJson))
                    bw.Write(balanceJson);
            }
            compressed = rawMs.ToArray();
        }

        int transferId = _nextTransferId++;
        int totalChunks = Math.Max(1, (compressed.Length + ModChunkSizeBytes - 1) / ModChunkSizeBytes);

        for (int chunkIdx = 0; chunkIdx < totalChunks; chunkIdx++)
        {
            int start = chunkIdx * ModChunkSizeBytes;
            int len = Math.Min(ModChunkSizeBytes, compressed.Length - start);

            using var pkt = new MemoryStream(len + 64);
            using var pw = new BinaryWriter(pkt, Encoding.UTF8);
            pw.Write((byte)NetMessageType.ModScriptChunk);
            pw.Write(transferId);
            pw.Write(charId ?? string.Empty);
            pw.Write((ushort)chunkIdx);
            pw.Write((ushort)totalChunks);
            pw.Write(len);
            pw.Write(compressed, start, len);
            pw.Flush();
            SendReliable(peerSteamId, pkt.GetBuffer().AsSpan(0, (int)pkt.Length));
        }
    }

    public static void SendModScriptAck(ulong peerSteamId, string charId, string scriptHash, bool accepted)
    {
        using var ms = new MemoryStream(96);
        using var bw = new BinaryWriter(ms, Encoding.UTF8);
        bw.Write((byte)NetMessageType.ModScriptAck);
        bw.Write(charId ?? string.Empty);
        bw.Write(scriptHash ?? string.Empty);
        bw.Write(accepted);
        bw.Flush();
        SendReliable(peerSteamId, ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    public static void SendMatchStart(ulong peerSteamId, uint matchSeed, int inputDelayFrames, bool rollbackEnabled)
    {
        using var ms = new MemoryStream(16);
        using var bw = new BinaryWriter(ms, Encoding.UTF8);
        bw.Write((byte)NetMessageType.MatchStart);
        bw.Write(matchSeed);
        bw.Write(inputDelayFrames);
        bw.Write(rollbackEnabled);
        bw.Flush();
        SendReliable(peerSteamId, ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    public static void SendPing(ulong peerSteamId, long timestampMs)
    {
        using var ms = new MemoryStream(16);
        using var bw = new BinaryWriter(ms, Encoding.UTF8);
        bw.Write((byte)NetMessageType.Ping);
        bw.Write(timestampMs);
        bw.Flush();
        SendReliable(peerSteamId, ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    public static void SendPong(ulong peerSteamId, long echoTimestampMs)
    {
        using var ms = new MemoryStream(16);
        using var bw = new BinaryWriter(ms, Encoding.UTF8);
        bw.Write((byte)NetMessageType.Pong);
        bw.Write(echoTimestampMs);
        bw.Flush();
        SendReliable(peerSteamId, ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    public static void SendDesyncNotice(ulong peerSteamId, int tick, uint localChecksum, uint remoteChecksum)
    {
        using var ms = new MemoryStream(16);
        using var bw = new BinaryWriter(ms, Encoding.UTF8);
        bw.Write((byte)NetMessageType.DesyncNotice);
        bw.Write(tick);
        bw.Write(localChecksum);
        bw.Write(remoteChecksum);
        bw.Flush();
        SendReliable(peerSteamId, ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    private static unsafe void SendReliable(ulong peerSteamId, ReadOnlySpan<byte> payload)
    {
        if (!SteamManager.IsInitialized || peerSteamId == 0UL || payload.IsEmpty) return;

        var identity = CreateIdentity(peerSteamId);
        EResult result;
        fixed (byte* pData = payload)
        {
            result = SteamNetworkingMessages.SendMessageToUser(
                ref identity,
                (IntPtr)pData,
                (uint)payload.Length,
                Constants.k_nSteamNetworkingSend_ReliableNoNagle | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession,
                ChannelControl);
        }

        if (result == EResult.k_EResultNoConnection || result == EResult.k_EResultInvalidState)
        {
            Console.WriteLine($"[SteamNetTransport] SendReliable to {peerSteamId} returned {result}; closing stale session.");
            CloseSessionWithPeer(peerSteamId);
        }
        else if (result != EResult.k_EResultOK)
        {
            Console.WriteLine($"[SteamNetTransport] SendReliable to {peerSteamId} failed: {result}");
        }
    }

    public static void PollAll(OnlineMatchSession? onlineSession, EngineServices? services)
    {
        if (!SteamManager.IsInitialized) return;

        PollChannel(ChannelInputs, (sender, data, len) => DispatchMessage(sender, data, len, onlineSession, services));
        PollChannel(ChannelControl, (sender, data, len) => DispatchMessage(sender, data, len, onlineSession, services));
    }

    private static void PollChannel(int channel, Action<ulong, byte[], int> handler)
    {
        int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(channel, _msgPtrs, _msgPtrs.Length);
        for (int i = 0; i < count; i++)
        {
            IntPtr ptr = _msgPtrs[i];
            if (ptr == IntPtr.Zero) continue;

            try
            {
                SteamNetworkingMessage_t msg = SteamNetworkingMessage_t.FromIntPtr(ptr);
                ulong senderId = msg.m_identityPeer.GetSteamID().m_SteamID;
                int size = msg.m_cbSize;
                if (size > 0 && msg.m_pData != IntPtr.Zero)
                {
                    if (senderId != 0UL && SteamLobbyManager.IsPeerAuthorized(senderId))
                    {
                        if (_acceptedPeers.Add(senderId))
                        {
                            SteamLobbyManager.OnPeerSessionEstablished(senderId);
                        }
                    }

                    if (size > _rxScratch.Length)
                        _rxScratch = new byte[Math.Max(size, _rxScratch.Length * 2)];

                    Marshal.Copy(msg.m_pData, _rxScratch, 0, size);
                    handler(senderId, _rxScratch, size);
                }
            }
            finally
            {
                SteamNetworkingMessage_t.Release(ptr);
            }
        }
    }

    private static void DispatchMessage(
        ulong senderId,
        byte[] buffer,
        int length,
        OnlineMatchSession? onlineSession,
        EngineServices? services)
    {
        if (length < 1) return;

        NetMessageType type = (NetMessageType)buffer[0];
        using var ms = new MemoryStream(buffer, 1, length - 1, writable: false);
        using var br = new BinaryReader(ms, Encoding.UTF8);

        switch (type)
        {
            case NetMessageType.InputBundle:
            {
                if (length < 18 || onlineSession == null) return;
                int latestTick = br.ReadInt32();
                int ackTick = br.ReadInt32();
                int checksumTick = br.ReadInt32();
                uint checksumVal = br.ReadUInt32();
                byte entryCount = br.ReadByte();

                Span<NetTickInput> inputs = stackalloc NetTickInput[Math.Min((int)entryCount, 8)];
                int actual = 0;
                for (int i = 0; i < entryCount && ms.Position + 6 <= ms.Length; i++)
                {
                    int t = br.ReadInt32();
                    ushort m = br.ReadUInt16();
                    if (actual < inputs.Length)
                    {
                        inputs[actual++] = new NetTickInput { Tick = t, InputBitmask = m };
                    }
                }
                onlineSession.OnRemoteInputBundle(senderId, latestTick, ackTick, checksumTick, checksumVal, inputs[..actual]);
                break;
            }

            case NetMessageType.FighterSelection:
            {
                string charId = br.ReadString();
                string scriptHash = br.ReadString();
                string baseHash = br.ReadString();
                bool ready = br.ReadBoolean();
                bool gaEnabled = br.ReadBoolean();
                ulong workshopId = (ms.Position + 8 <= ms.Length) ? br.ReadUInt64() : 0UL;
                SteamLobbyManager.OnRemoteFighterSelection(senderId, charId, scriptHash, baseHash, ready, gaEnabled, workshopId);
                break;
            }

            case NetMessageType.ModScriptRequest:
            {
                string charId = br.ReadString();
                string expectedHash = br.ReadString();
                SteamLobbyManager.OnModScriptRequestReceived(senderId, charId, expectedHash);
                break;
            }

            case NetMessageType.ModScriptChunk:
            {
                int transferId = br.ReadInt32();
                string charId = br.ReadString();
                ushort chunkIdx = br.ReadUInt16();
                ushort totalChunks = br.ReadUInt16();
                int chunkLen = br.ReadInt32();
                if (chunkLen >= 0 && ms.Position + chunkLen <= ms.Length)
                {
                    byte[] bytes = br.ReadBytes(chunkLen);
                    SteamLobbyManager.OnModScriptChunkReceived(senderId, transferId, charId, chunkIdx, totalChunks, bytes, services);
                }
                break;
            }

            case NetMessageType.ModScriptAck:
            {
                string charId = br.ReadString();
                string scriptHash = br.ReadString();
                bool accepted = br.ReadBoolean();
                SteamLobbyManager.OnModScriptAckReceived(senderId, charId, scriptHash, accepted);
                break;
            }

            case NetMessageType.MatchStart:
            {
                uint seed = br.ReadUInt32();
                int inputDelay = br.ReadInt32();
                bool rollback = br.ReadBoolean();
                SteamLobbyManager.OnMatchStartReceived(senderId, seed, inputDelay, rollback);
                break;
            }

            case NetMessageType.Ping:
            {
                long ts = br.ReadInt64();
                SendPong(senderId, ts);
                break;
            }

            case NetMessageType.Pong:
            {
                long echoTs = br.ReadInt64();
                onlineSession?.OnPongReceived(senderId, echoTs);
                break;
            }

            case NetMessageType.DesyncNotice:
            {
                int tick = br.ReadInt32();
                uint peerChecksum = br.ReadUInt32();
                uint peerSawChecksum = br.ReadUInt32();
                onlineSession?.OnPeerDesyncNotice(senderId, tick, peerChecksum, peerSawChecksum);
                break;
            }
        }
    }

    private static SteamNetworkingIdentity CreateIdentity(ulong steamId)
    {
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID64(steamId);
        return identity;
    }

    private static void WriteInt32(byte[] buf, ref int offset, int value)
    {
        buf[offset++] = (byte)(value & 0xFF);
        buf[offset++] = (byte)((value >> 8) & 0xFF);
        buf[offset++] = (byte)((value >> 16) & 0xFF);
        buf[offset++] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteUInt32(byte[] buf, ref int offset, uint value)
    {
        buf[offset++] = (byte)(value & 0xFF);
        buf[offset++] = (byte)((value >> 8) & 0xFF);
        buf[offset++] = (byte)((value >> 16) & 0xFF);
        buf[offset++] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteUInt16(byte[] buf, ref int offset, ushort value)
    {
        buf[offset++] = (byte)(value & 0xFF);
        buf[offset++] = (byte)((value >> 8) & 0xFF);
    }
}
