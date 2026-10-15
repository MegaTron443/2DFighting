public struct AnimationClip
{
    public int StartFrame;
    public int FrameCount;
    public float Speed;
}

public class Animation
{
    public AnimationState State { get; private set; } = AnimationState.Idle;
    public int CurrentFrame { get; private set; } = 0;
    private float _timer = 0f;

    public void SetState(AnimationState newState)
    {
        if (State != newState)
        {
            State = newState;
            CurrentFrame = 0;
            _timer = 0f;
        }
    }

    public void Update(float dt, Dictionary<string, AnimationClip> clips)
    {
        string key = State switch
        {
            AnimationState.Walking => "walk",
            AnimationState.Jumping => "jump",
            AnimationState.Attacking => "attack1",
            _ => "idle"
        };

        if (!clips.TryGetValue(key, out var clip) || clip.FrameCount <= 0)
            return;

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
        string key = State switch
        {
            AnimationState.Walking => "walk",
            AnimationState.Jumping => "jump",
            AnimationState.Attacking => "attack1",
            _ => "idle"
        };

        if (clips.TryGetValue(key, out var clip) && clip.FrameCount > 0)
        {
            return clip.StartFrame + (CurrentFrame % clip.FrameCount);
        }
        return 0;
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