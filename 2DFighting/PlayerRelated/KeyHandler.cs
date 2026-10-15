using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

public class KeyHandler
{
    private const int PLINK_WINDOW_FRAME_COUNT = 4;
    private const int COMMIT_DELAY_FRAME_COUNT = 2;
    private const int DEFAULT_MAX_OVERRIDE_FRAMES = 5;

    private readonly AppConfig _appConfig;
    private readonly Dictionary<int, Queue<(float time, int mask)>> _inputBuffers = new();

    private float _latchedDir = 0f;
    private bool _latchedJumpPressed = false;
    private bool _latchedCrouch = false;
    private int _latchedHeldMask = 0;

    public KeyHandler(AppConfig appConfig)
    {
        _appConfig = appConfig;
    }

    /// <summary>
    /// Runs every RENDER FRAME (e.g., 144Hz/240Hz/uncapped) to catch mid-tick clicks.
    /// </summary>
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

        int pressedThisFrame = 0;
        if (Raylib.IsKeyPressed(controls.KeyTop))    pressedThisFrame |= (1 << 0);
        if (Raylib.IsKeyPressed(controls.KeyBottom)) pressedThisFrame |= (1 << 1);
        if (Raylib.IsKeyPressed(controls.KeyLeft))   pressedThisFrame |= (1 << 2);
        if (Raylib.IsKeyPressed(controls.KeyRight))  pressedThisFrame |= (1 << 3);

        int heldThisFrame = 0;
        if (Raylib.IsKeyDown(controls.KeyTop))       heldThisFrame |= (1 << 0);
        if (Raylib.IsKeyDown(controls.KeyBottom))    heldThisFrame |= (1 << 1);
        if (Raylib.IsKeyDown(controls.KeyLeft))      heldThisFrame |= (1 << 2);
        if (Raylib.IsKeyDown(controls.KeyRight))     heldThisFrame |= (1 << 3);
        _latchedHeldMask |= heldThisFrame;

        if (!_inputBuffers.ContainsKey(playerIndex))
            _inputBuffers[playerIndex] = new Queue<(float time, int mask)>();

        if (pressedThisFrame != 0)
        {
            _inputBuffers[playerIndex].Enqueue(((float)Raylib.GetTime(), pressedThisFrame));
        }
    }

    /// <summary>
    /// Runs every FIXED TICK (60Hz) to apply buffered sub-tick inputs to the simulation.
    /// </summary>
    public bool ConsumeBufferedInput(Character currentCharacter, Player player, int playerIndex)
    {
        if (currentCharacter == null) return true;

        float dir = _latchedDir;
        bool jumped = _latchedJumpPressed;
        _latchedJumpPressed = false;

        if (jumped)
        {
            currentCharacter.Jump(player);
        }

        player.IsCrouching = _latchedCrouch;
        if (_latchedCrouch)
        {
            dir = 0;
        }

        currentCharacter.Move(player, dir);

        int heldMask = _latchedHeldMask;
        _latchedHeldMask = 0;

        if (!_inputBuffers.ContainsKey(playerIndex))
            _inputBuffers[playerIndex] = new Queue<(float time, int mask)>();

        var buffer = _inputBuffers[playerIndex];
        float currentTime = (float)Raylib.GetTime();

        float frameDuration = Timer.FixedDeltaTime;
        float plinkWindowDuration = frameDuration * PLINK_WINDOW_FRAME_COUNT;
        float singleButtonCommitDelay = frameDuration * COMMIT_DELAY_FRAME_COUNT;

        while (buffer.Count > 0 && currentTime - buffer.Peek().time > plinkWindowDuration)
        {
            buffer.Dequeue();
        }

        int combinedMask = heldMask;
        foreach (var entry in buffer)
        {
            combinedMask |= entry.mask;
        }

        bool hasInput = dir != 0 || jumped || combinedMask != 0;
        if (combinedMask == 0)
        {
            return !hasInput;
        }

        SkillData? bestSkill = null;
        int bestBitCount = 0;

        foreach (var skill in currentCharacter.Skills.Values)
        {
            if (skill.TriggerMask == 0) continue;
            if (currentCharacter.SkillCooldowns.TryGetValue(skill.Name, out float cd) && cd > 0f)
                continue;

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
            return !hasInput;
        }

        float oldestPressTime = buffer.Count > 0 ? buffer.Peek().time : currentTime;
        bool isMultiButton = bestBitCount > 1;
        bool singleButtonCommitReady = (currentTime - oldestPressTime) >= singleButtonCommitDelay;

        bool canExecute = false;

        if (currentCharacter.ActiveSkill == null)
        {
            if (isMultiButton || singleButtonCommitReady)
            {
                canExecute = true;
            }
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
                else
                {
                    buffer.Clear();
                    return !hasInput;
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
            buffer.Clear();
        }

        return !hasInput;
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
        _latchedHeldMask |= mask;

        if (!_inputBuffers.ContainsKey(playerIndex))
        {
            _inputBuffers[playerIndex] = new Queue<(float time, int mask)>();
        }

        _inputBuffers[playerIndex].Enqueue(((float)Raylib.GetTime() - 0.1f, mask));
    }
}
