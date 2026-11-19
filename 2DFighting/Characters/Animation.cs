using System;
using System.Collections.Generic;
using System.Numerics;

public struct KeyframeStep
{
    public int AtlasFrame;
    public int HoldTicks;
    public bool SpawnAfterimage;
    public Vector2 WeaponOffsetPct;
    public float WeaponAngleDeg;
}

public class AnimationClip
{
    public int StartFrame;
    public int FrameCount;
    public float Speed = 8f;
    public List<KeyframeStep>? Keyframes;
}

public class Animation
{
    public AnimationState State { get; private set; } = AnimationState.Idle;
    public string? ActiveCustomClip { get; private set; } = null;
    public int CurrentFrame { get; private set; } = 0;

    private float _timer = 0f;
    private int _tickCounter = 0;

    public void SetState(AnimationState newState, string? customClipName = null)
    {
        if (State != newState || ActiveCustomClip != customClipName)
        {
            State = newState;
            ActiveCustomClip = customClipName;
            CurrentFrame = 0;
            _timer = 0f;
            _tickCounter = 0;
        }
    }

    public string ResolveClipKey(Dictionary<string, AnimationClip> clips)
    {
        if (!string.IsNullOrEmpty(ActiveCustomClip) && clips.ContainsKey(ActiveCustomClip))
            return ActiveCustomClip;

        string key = State switch
        {
            AnimationState.Walking => "walk",
            AnimationState.Jumping => "jump",
            AnimationState.Attacking => "attack1",
            _ => "idle"
        };

        return key;
    }

    public void Update(float dt, Dictionary<string, AnimationClip> clips)
    {
        string key = ResolveClipKey(clips);
        if (!clips.TryGetValue(key, out var clip))
            return;

        if (clip.Keyframes != null && clip.Keyframes.Count > 0)
        {
            _tickCounter++;
            int safeIdx = CurrentFrame % clip.Keyframes.Count;
            int hold = Math.Max(1, clip.Keyframes[safeIdx].HoldTicks);

            if (_tickCounter >= hold)
            {
                _tickCounter = 0;
                CurrentFrame = (CurrentFrame + 1) % clip.Keyframes.Count;
            }
            return;
        }

        if (clip.FrameCount <= 0) return;

        _timer += dt * clip.Speed;
        if (_timer >= 1f)
        {
            int steps = (int)_timer;
            _timer -= steps;
            CurrentFrame = (CurrentFrame + steps) % clip.FrameCount;
        }
    }

    public int GetAtlasFrameIndex(Dictionary<string, AnimationClip> clips)
    {
        string key = ResolveClipKey(clips);
        if (!clips.TryGetValue(key, out var clip))
            return 0;

        if (clip.Keyframes != null && clip.Keyframes.Count > 0)
        {
            return clip.Keyframes[CurrentFrame % clip.Keyframes.Count].AtlasFrame;
        }

        if (clip.FrameCount > 0)
        {
            return clip.StartFrame + (CurrentFrame % clip.FrameCount);
        }
        return 0;
    }

    public KeyframeStep? GetCurrentKeyframe(Dictionary<string, AnimationClip> clips)
    {
        string key = ResolveClipKey(clips);
        if (clips.TryGetValue(key, out var clip) && clip.Keyframes != null && clip.Keyframes.Count > 0)
        {
            return clip.Keyframes[CurrentFrame % clip.Keyframes.Count];
        }
        return null;
    }
}

public enum AnimationState
{
    Idle,
    Blocking,
    Walking,
    Jumping,
    Attacking,
    TakingDamage,
    Dying
}