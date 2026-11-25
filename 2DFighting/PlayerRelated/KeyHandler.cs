using System.Numerics;
using Raylib_cs;

public class KeyHandler
{
    private const int PLINK_WINDOW_FRAME_COUNT = 5;
    private const int CHORD_COMMIT_FRAME_COUNT = 3;
    private const int DEFAULT_MAX_OVERRIDE_FRAMES = 4;

    private readonly AppConfig _appConfig;
    private readonly Dictionary<int, Queue<(float time, int mask)>> _inputBuffers = new();

    private float _latchedDir = 0f;
    private bool _latchedJumpPressed = false;
    private bool _latchedCrouch = false;
    private bool _latchedCrouchPressed = false;
    private float _simTime = 0f;
    private ushort _pendingNetPresses = 0;

    public readonly struct Snapshot
    {
        public readonly float LatchedDir;
        public readonly bool LatchedJumpPressed;
        public readonly bool LatchedCrouch;
        public readonly bool LatchedCrouchPressed;
        public readonly float SimTime;
        public readonly (float time, int mask)[] BufferedEntries;

        public Snapshot(
            float latchedDir,
            bool latchedJumpPressed,
            bool latchedCrouch,
            bool latchedCrouchPressed,
            float simTime,
            (float time, int mask)[] bufferedEntries)
        {
            LatchedDir = latchedDir;
            LatchedJumpPressed = latchedJumpPressed;
            LatchedCrouch = latchedCrouch;
            LatchedCrouchPressed = latchedCrouchPressed;
            SimTime = simTime;
            BufferedEntries = bufferedEntries;
        }
    }

    public Snapshot CaptureSnapshot(int playerIndex)
    {
        (float time, int mask)[] entries = _inputBuffers.TryGetValue(playerIndex, out var buf)
            ? buf.ToArray()
            : Array.Empty<(float, int)>();
        return new Snapshot(_latchedDir, _latchedJumpPressed, _latchedCrouch, _latchedCrouchPressed, _simTime, entries);
    }

    public void RestoreSnapshot(int playerIndex, in Snapshot snapshot)
    {
        _latchedDir = snapshot.LatchedDir;
        _latchedJumpPressed = snapshot.LatchedJumpPressed;
        _latchedCrouch = snapshot.LatchedCrouch;
        _latchedCrouchPressed = snapshot.LatchedCrouchPressed;
        _simTime = snapshot.SimTime;

        if (!_inputBuffers.TryGetValue(playerIndex, out var buf))
        {
            buf = new Queue<(float time, int mask)>();
            _inputBuffers[playerIndex] = buf;
        }
        buf.Clear();
        for (int i = 0; i < snapshot.BufferedEntries.Length; i++)
            buf.Enqueue(snapshot.BufferedEntries[i]);
    }

    public void LatchOnlineHardwareEdges(int bindingsIndex = 0)
    {
        PlayerControls controls = bindingsIndex == 0 ? _appConfig.Controls.P1 : _appConfig.Controls.P2;
        if (Raylib.IsKeyPressed(controls.Jump))      _pendingNetPresses |= (ushort)NetInputBits.Jump;
        if (Raylib.IsKeyPressed(controls.Crouch))    _pendingNetPresses |= (ushort)NetInputBits.Crouch;
        if (Raylib.IsKeyPressed(controls.KeyTop))    _pendingNetPresses |= (ushort)NetInputBits.KeyTop;
        if (Raylib.IsKeyPressed(controls.KeyBottom)) _pendingNetPresses |= (ushort)NetInputBits.KeyBottom;
        if (Raylib.IsKeyPressed(controls.KeyLeft))   _pendingNetPresses |= (ushort)NetInputBits.KeyLeft;
        if (Raylib.IsKeyPressed(controls.KeyRight))  _pendingNetPresses |= (ushort)NetInputBits.KeyRight;
    }

    public ushort SampleHardwareInputBitmask(int bindingsIndex = 0)
    {
        PlayerControls controls = bindingsIndex == 0 ? _appConfig.Controls.P1 : _appConfig.Controls.P2;
        ushort mask = _pendingNetPresses;
        _pendingNetPresses = 0;

        if (Raylib.IsKeyDown(controls.Left))      mask |= (ushort)NetInputBits.Left;
        if (Raylib.IsKeyDown(controls.Right))     mask |= (ushort)NetInputBits.Right;
        if (Raylib.IsKeyDown(controls.Jump))      mask |= (ushort)NetInputBits.Jump;
        if (Raylib.IsKeyDown(controls.Crouch))    mask |= (ushort)NetInputBits.Crouch;
        if (Raylib.IsKeyDown(controls.KeyTop))    mask |= (ushort)NetInputBits.KeyTop;
        if (Raylib.IsKeyDown(controls.KeyBottom)) mask |= (ushort)NetInputBits.KeyBottom;
        if (Raylib.IsKeyDown(controls.KeyLeft))   mask |= (ushort)NetInputBits.KeyLeft;
        if (Raylib.IsKeyDown(controls.KeyRight))  mask |= (ushort)NetInputBits.KeyRight;

        return mask;
    }

    public void ApplyNetworkInputBitmask(ushort bitmask, ushort prevBitmask, int playerIndex)
    {
        NetInputBits curr = (NetInputBits)bitmask;
        NetInputBits prev = (NetInputBits)prevBitmask;
        NetInputBits rising = curr & ~prev;

        float dir = 0f;
        if ((curr & NetInputBits.Left) != 0) dir -= 1f;
        if ((curr & NetInputBits.Right) != 0) dir += 1f;
        _latchedDir = dir;

        if ((rising & NetInputBits.Jump) != 0)
            _latchedJumpPressed = true;

        _latchedCrouch = (curr & NetInputBits.Crouch) != 0;
        if ((rising & NetInputBits.Crouch) != 0)
            _latchedCrouchPressed = true;

        int pressedMask = 0;
        if ((rising & NetInputBits.KeyTop) != 0)    pressedMask |= 1 << 0;
        if ((rising & NetInputBits.KeyBottom) != 0) pressedMask |= 1 << 1;
        if ((rising & NetInputBits.KeyLeft) != 0)   pressedMask |= 1 << 2;
        if ((rising & NetInputBits.KeyRight) != 0)  pressedMask |= 1 << 3;

        if (!_inputBuffers.ContainsKey(playerIndex))
            _inputBuffers[playerIndex] = new Queue<(float time, int mask)>();

        if (pressedMask != 0)
            _inputBuffers[playerIndex].Enqueue((_simTime, pressedMask));
    }

    public KeyHandler(AppConfig appConfig)
    {
        _appConfig = appConfig;
    }

    public void PollHardwareInput(int playerIndex)
    {
        PlayerControls controls = playerIndex == 0 ? _appConfig.Controls.P1 : _appConfig.Controls.P2;

        float dir = 0;
        if (Raylib.IsKeyDown(controls.Right)) dir += 1;
        if (Raylib.IsKeyDown(controls.Left)) dir -= 1;
        _latchedDir = dir;

        if (Raylib.IsKeyPressed(controls.Jump))
        {
            _latchedJumpPressed = true;
        }

        _latchedCrouch = Raylib.IsKeyDown(controls.Crouch);
        if (Raylib.IsKeyPressed(controls.Crouch))
        {
            _latchedCrouchPressed = true;
        }

        int pressedThisFrame = 0;
        if (Raylib.IsKeyPressed(controls.KeyTop))    pressedThisFrame |= (1 << 0);
        if (Raylib.IsKeyPressed(controls.KeyBottom)) pressedThisFrame |= (1 << 1);
        if (Raylib.IsKeyPressed(controls.KeyLeft))   pressedThisFrame |= (1 << 2);
        if (Raylib.IsKeyPressed(controls.KeyRight))  pressedThisFrame |= (1 << 3);

        if (!_inputBuffers.ContainsKey(playerIndex))
            _inputBuffers[playerIndex] = new Queue<(float time, int mask)>();

        if (pressedThisFrame != 0)
        {
            _inputBuffers[playerIndex].Enqueue((_simTime, pressedThisFrame));
        }
    }

    public bool TryConsumeTechInput(int playerIndex, out float techDirection)
    {
        techDirection = _latchedDir;
        bool hasButtonPress = _latchedJumpPressed;

        if (_inputBuffers.TryGetValue(playerIndex, out var buffer) && buffer.Count > 0)
        {
            float currentTime = _simTime;
            float plinkWindowDuration = Timer.FixedDeltaTime * PLINK_WINDOW_FRAME_COUNT;
            while (buffer.Count > 0 && currentTime - buffer.Peek().time > plinkWindowDuration)
            {
                buffer.Dequeue();
            }
            if (buffer.Count > 0)
            {
                hasButtonPress = true;
            }
        }

        if (hasButtonPress)
        {
            _latchedJumpPressed = false;
            if (_inputBuffers.TryGetValue(playerIndex, out var b)) b.Clear();
            return true;
        }

        return false;
    }

    public void ClearBuffer(int playerIndex)
    {
        _latchedDir = 0f;
        _latchedJumpPressed = false;
        _latchedCrouch = false;
        _latchedCrouchPressed = false;
        if (_inputBuffers.TryGetValue(playerIndex, out var buffer))
        {
            buffer.Clear();
        }
    }

    public bool ConsumeBufferedInput(Character currentCharacter, Player player, int playerIndex)
    {
        if (currentCharacter == null) return true;

        float dir = _latchedDir;
        bool jumped = _latchedJumpPressed;
        bool crouchPressed = _latchedCrouchPressed;
        _latchedJumpPressed = false;
        _latchedCrouchPressed = false;

        if (crouchPressed && player.IsGrounded && player.IsOnPlatform)
        {
            player.RequestPlatformDrop();
        }
        else if (jumped)
        {
            currentCharacter.Jump(player);
        }

        player.IsCrouching = _latchedCrouch;
        if (_latchedCrouch)
        {
            dir = 0;
        }

        currentCharacter.Move(player, dir);

        if (!_inputBuffers.ContainsKey(playerIndex))
            _inputBuffers[playerIndex] = new Queue<(float time, int mask)>();

        var buffer = _inputBuffers[playerIndex];
        _simTime += Timer.FixedDeltaTime;
        float currentTime = _simTime;
        float frameDuration = Timer.FixedDeltaTime;
        float plinkWindowDuration = frameDuration * PLINK_WINDOW_FRAME_COUNT;
        float chordCommitDelay = frameDuration * CHORD_COMMIT_FRAME_COUNT;

        while (buffer.Count > 0 && currentTime - buffer.Peek().time > plinkWindowDuration)
        {
            buffer.Dequeue();
        }

        int combinedMask = 0;
        foreach (var entry in buffer)
        {
            combinedMask |= entry.mask;
        }

        bool hasInput = dir != 0 || jumped || combinedMask != 0;
        if (combinedMask == 0)
        {
            return !hasInput;
        }

        float oldestPressTime = buffer.Peek().time;
        bool commitWindowElapsed = (currentTime - oldestPressTime) >= chordCommitDelay;

        if (!commitWindowElapsed && HasReadySupersetChord(currentCharacter, combinedMask))
        {
            return !hasInput;
        }

        SkillData? bestSkill = null;
        int bestBitCount = 0;

        foreach (var skill in currentCharacter.Skills.Values)
        {
            if (skill.TriggerMask == 0) continue;

            if ((combinedMask & skill.TriggerMask) == skill.TriggerMask)
            {
                int bits = BitOperations.PopCount((uint)skill.TriggerMask);
                if (bits > bestBitCount)
                {
                    bestBitCount = bits;
                    bestSkill = skill;
                }
            }
        }

        if (bestSkill == null)
        {
            buffer.Clear();
            return !hasInput;
        }

        if (currentCharacter.SkillCooldowns.TryGetValue(bestSkill.Name, out float cd) && cd > 0f)
        {
            buffer.Clear();
            return !hasInput;
        }

        bool canExecute = false;
        if (currentCharacter.ActiveSkill == null)
        {
            canExecute = true;
        }
        else
        {
            int currentSkillBits = BitOperations.PopCount((uint)currentCharacter.ActiveSkill.TriggerMask);
            if (bestBitCount > currentSkillBits && !currentCharacter.HasHit)
            {
                int maxAllowedFrame = Math.Min(DEFAULT_MAX_OVERRIDE_FRAMES, currentCharacter.ActiveSkill.StartupFrames - 1);
                if (currentCharacter.CurrentActionFrame <= maxAllowedFrame)
                {
                    canExecute = true;
                }
            }
        }

        if (canExecute)
        {
            if (currentCharacter.ActiveSkill != null)
            {
                currentCharacter.SkillCooldowns[currentCharacter.ActiveSkill.Name] = 0f;
            }
            currentCharacter.ActiveSkill = null;
            currentCharacter.ExecuteSkill(player, bestSkill.Name);
        }

        buffer.Clear();
        return !hasInput;
    }

    private static bool HasReadySupersetChord(Character character, int combinedMask)
    {
        foreach (var skill in character.Skills.Values)
        {
            if (skill.TriggerMask == 0) continue;
            if ((skill.TriggerMask & combinedMask) == combinedMask && skill.TriggerMask != combinedMask)
            {
                if (!character.SkillCooldowns.TryGetValue(skill.Name, out float cd) || cd <= 0f)
                    return true;
            }
        }
        return false;
    }

    public void SimulateDirection(GameDirection direction)
    {
        switch (direction)
        {
            case GameDirection.Left:
                _latchedDir = -1f;
                _latchedCrouch = false;
                break;
            case GameDirection.Right:
                _latchedDir = 1f;
                _latchedCrouch = false;
                break;
            case GameDirection.Down:
                _latchedDir = 0f;
                _latchedCrouch = true;
                break;
            case GameDirection.DownLeft:
                _latchedDir = -1f;
                _latchedCrouch = true;
                break;
            case GameDirection.DownRight:
                _latchedDir = 1f;
                _latchedCrouch = true;
                break;
            case GameDirection.Up:
                _latchedDir = 0f;
                _latchedJumpPressed = true;
                _latchedCrouch = false;
                break;
            case GameDirection.UpRight:
                _latchedDir = 1f;
                _latchedJumpPressed = true;
                _latchedCrouch = false;
                break;
            case GameDirection.UpLeft:
                _latchedDir = -1f;
                _latchedJumpPressed = true;
                _latchedCrouch = false;
                break;
            case GameDirection.None:
            default:
                _latchedDir = 0f;
                _latchedCrouch = false;
                break;
        }
    }

    public void SimulateMaskPress(int mask, int playerIndex = 1)
    {
        if (mask == 0) return;
        _latchedCrouch = false;

        if (!_inputBuffers.ContainsKey(playerIndex))
        {
            _inputBuffers[playerIndex] = new Queue<(float time, int mask)>();
        }
        _inputBuffers[playerIndex].Enqueue((_simTime - (Timer.FixedDeltaTime * CHORD_COMMIT_FRAME_COUNT), mask));
    }
}