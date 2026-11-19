public class GameState
{
    public static State CurrentState { get; private set; } = State.MainMenu;
    public static State PreviousState { get; private set; } = State.MainMenu;
    public static bool IsLiveArena => CurrentState == State.Playing || CurrentState == State.MainMenu;

    public static void SetState(State state)
    {
        PreviousState = CurrentState;
        CurrentState = state;
    }

    
}

public enum State
{
    MainMenu,
    ChoiceMenu,
    Settings,
    Playing,
    Paused,
    GameOver,
    Exit,
}