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

    public KeyHandler(AppConfig appConfig)
    {
        _appConfig = appConfig;
    }

    /// <summary>
    /// Processes movement and combat inputs.
    /// </summary>
    /// <returns>True if no actions or movement keys are active (player is idle); otherwise false.</returns>
    public bool HandleInput(Character currentCharacter, Player player, int playerIndex)
    {
        if (currentCharacter == null) return true;

        PlayerControls controls = playerIndex == 0 ? _appConfig.Controls.P1 : _appConfig.Controls.P2;

        float dir = 0;
        if (Raylib.IsKeyDown(controls.Right)) dir += 1;
        if (Raylib.IsKeyDown(controls.Left)) dir -= 1;

        bool jumped = false;
        if (Raylib.IsKeyPressed(controls.Jump))
        {
            currentCharacter.Jump(player);
            jumped = true;
        }

        bool isCrouching = Raylib.IsKeyDown(controls.Crouch);
        player.IsCrouching = isCrouching;
        if (isCrouching)
        {
            dir = 0;
        }
        currentCharacter.Move(player, dir);

        int pressedThisFrame = 0;
        int heldThisFrame = 0;

        if (Raylib.IsKeyPressed(controls.KeyTop))    pressedThisFrame |= (1 << 0);
        if (Raylib.IsKeyPressed(controls.KeyBottom)) pressedThisFrame |= (1 << 1);
        if (Raylib.IsKeyPressed(controls.KeyLeft))   pressedThisFrame |= (1 << 2);
        if (Raylib.IsKeyPressed(controls.KeyRight))  pressedThisFrame |= (1 << 3);

        if (Raylib.IsKeyDown(controls.KeyTop))       heldThisFrame |= (1 << 0);
        if (Raylib.IsKeyDown(controls.KeyBottom))    heldThisFrame |= (1 << 1);
        if (Raylib.IsKeyDown(controls.KeyLeft))      heldThisFrame |= (1 << 2);
        if (Raylib.IsKeyDown(controls.KeyRight))     heldThisFrame |= (1 << 3);

        bool hasInput = dir != 0 
              || jumped 
              || pressedThisFrame != 0 
              || heldThisFrame != 0;

        if (!_inputBuffers.ContainsKey(playerIndex))
            _inputBuffers[playerIndex] = new Queue<(float time, int mask)>();

        var buffer = _inputBuffers[playerIndex];
        float currentTime = (float)Raylib.GetTime();

        float targetFps = Math.Max(1f, _appConfig.Display.TargetFps);
        float frameDuration = 1f / targetFps;
        float plinkWindowDuration = frameDuration * PLINK_WINDOW_FRAME_COUNT;
        float singleButtonCommitDelay = frameDuration * COMMIT_DELAY_FRAME_COUNT;

        if (pressedThisFrame != 0)
        {
            buffer.Enqueue((currentTime, pressedThisFrame));
        }

        while (buffer.Count > 0 && currentTime - buffer.Peek().time > plinkWindowDuration)
        {
            buffer.Dequeue();
        }

        int combinedMask = heldThisFrame;
        foreach (var entry in buffer)
        {
            combinedMask |= entry.mask;
        }

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
}