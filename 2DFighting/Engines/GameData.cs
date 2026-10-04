public readonly struct GameData
{
    public Player Player1 { get; }
    public Player Player2 { get; }
    public Timer Timer { get; }
    public LuaEngine LuaEngine { get; }
    public AppConfig AppConfig { get; }


    public GameData(Player player1, Player player2, Timer gameTimer, LuaEngine LuaEngineInstance, AppConfig appConfig )
    {
        Player1 = player1;
        Player2 = player2;
        Timer = gameTimer;
        LuaEngine = LuaEngineInstance;
        AppConfig = appConfig;
    }
}