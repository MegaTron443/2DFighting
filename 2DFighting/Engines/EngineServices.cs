using System;

public class EngineServices
{
    public static readonly string[] DefaultFighters =
    {
        "brawler", "lancer", "pyromancer", "phantom", "titan", "chronomancer", "gravity"
    };

    public static EngineServices? Current { get; private set; }

    public AppConfig Config { get; }
    public Timer Timer { get; }
    public LuaEngine Lua { get; }
    public PhysicsEngine Physics { get; }

    public MatchManager Match => Physics.Match;
    public ProjectileManager Projectiles => Physics.Projectiles;
    public CombatResolver Combat => Physics.Combat;

    public SoundHandler? Sound { get; set; }
    public Player? Player1 { get; set; }
    public Player? Player2 { get; set; }

    public string[] AvailableFighters => ModManager.AvailableFighters.Length > 0
        ? ModManager.AvailableFighters
        : DefaultFighters;

    public EngineServices(AppConfig config)
    {
        Config = config;
        Timer = new Timer();
        Lua = new LuaEngine();
        Physics = new PhysicsEngine();
        Current = this;
    }

    public void InitializeAll()
    {
        Physics.Initialize();
        Timer.Initialize();
        Lua.Initialize();

        ModManager.Initialize();
        ShaderManager.Initialize();
        PostFXManager.Initialize();
        LuaMoveTranslator.InitializeCache(Lua);
        GUIEngine.Initialize();
    }

    public GameData CreateGameData() => new(Player1, Player2, Timer, Lua, Config, Sound, this);

    public void EnsureMatchSoundHandler()
    {
        if (GameState.CurrentState == State.Playing && Sound == null &&
            Player1?.CurrentCharacter != null && Player2?.CurrentCharacter != null)
        {
            Sound = new SoundHandler(CreateGameData());
            Sound.PlaySound(SoundType.RoundStart);
        }
    }

    public void CleanupMatchSession()
    {
        if ((GameState.CurrentState == State.GameOver || GameState.CurrentState == State.MainMenu) &&
            (Player1 != null || Player2 != null || Sound != null))
        {
            Sound?.Dispose();
            Sound = null;
            Player1 = null;
            Player2 = null;
        }
    }

    public void DisposeAll()
    {
        MacroTacticalModel.SharedInstance.SaveWeights();
        GUIEngine.Dispose();
        VFXManager.Dispose();
        PostFXManager.Dispose();
        ShaderManager.Dispose();
        Character.UnloadAllSpriteSheets();
        Sound?.Dispose();
        Sound = null;

        if (Current == this) Current = null;
    }
}
