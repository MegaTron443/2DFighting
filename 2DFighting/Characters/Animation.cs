public class Animation
{
    public AnimationState State { get; private set; } = AnimationState.Idle;
    public Character? Character { get; private set; } = null;

    public void SetState(AnimationState newState)
    {
        State = newState;
    }

    public string GetAnimationFilePath()
    {
        if (Character != null)
        {
            return State switch
            {
                AnimationState.Idle => $"Animations/{Character.Name}/Idle.png",
                AnimationState.Walking => $"Animations/{Character.Name}/Walking.png",
                AnimationState.Jumping => $"Animations/{Character.Name}/Jumping.png",
                AnimationState.Attacking => $"Animations/{Character.Name}/Attacking.png",
                AnimationState.TakingDamage => $"Animations/{Character.Name}/TakingDamage.png",
                AnimationState.Dying => $"Animations/{Character.Name}/Dying.png",
                _ => $"Animations/{Character.Name}/Unknown.png"
            };
        }
        return "Animations/Unknown.png";
    }
    
}

public enum AnimationState
{
    Idle,
    Walking,
    Jumping,
    Attacking,
    TakingDamage,
    Dying
}