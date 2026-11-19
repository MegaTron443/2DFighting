using System.Collections.Generic;

public static class GUIEngine
{
    private static readonly Dictionary<State, IUIScreen> _screens = new();

    private static string[] AvailableFighters => ModManager.AvailableFighters.Length > 0
        ? ModManager.AvailableFighters
        : EngineServices.DefaultFighters;

    public static string[] RosterIds => AvailableFighters;

    public static void Initialize()
    {
        Dispose();

        _screens[State.MainMenu]   = new MainMenuScreen();
        _screens[State.ChoiceMenu] = new CharacterSelectScreen();
        _screens[State.Settings]   = new SettingsScreen();
        _screens[State.Playing]    = new PlayingHudScreen();
        _screens[State.Paused]     = new PauseOverlay();
        _screens[State.GameOver]   = new GameOverScreen();

        foreach (var screen in _screens.Values)
        {
            screen.Initialize();
        }
    }

    public static void Update(ref Player? p1, ref Player? p2, GameData data)
    {
        if (!_screens.TryGetValue(GameState.CurrentState, out IUIScreen? screen))
            return;

        Player? currentP1 = p1;
        Player? currentP2 = p2;
        ButtonContext context = new ButtonContext(
            data,
            newP1 => currentP1 = newP1,
            newP2 => currentP2 = newP2
        );

        screen.Update(context);

        p1 = currentP1;
        p2 = currentP2;
    }

    public static void Draw(GameData data)
    {
        if (_screens.TryGetValue(GameState.CurrentState, out IUIScreen? screen))
        {
            screen.Draw(data);
        }
    }

    public static void Dispose()
    {
        foreach (var screen in _screens.Values)
        {
            screen.Dispose();
        }
        _screens.Clear();
    }
}
