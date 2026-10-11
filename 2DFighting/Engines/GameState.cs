public class GameState
{
    public static State CurrentState { get; private set; } = State.MainMenu;

    public static void SetState(State state)
    {
        CurrentState = state;
    }
}

public enum State
{
    MainMenu,
    ChoiceMenu,
    Playing,
    Paused,
    GameOver,
    Exit,
}