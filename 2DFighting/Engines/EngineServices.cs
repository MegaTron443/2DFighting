using System;
using System.Numerics;
using Raylib_cs;

public class EngineServices
{
    public static readonly string[] DefaultFighters =
    {
        "brawler", "lancer", "pyromancer", "phantom", "titan", "chronomancer", "gravity", "priest"
    };

    public static EngineServices? Current { get; private set; }

    public AppConfig Config { get; }
    public IPlayerIdentityProvider Identity { get; private set; }
    public Timer Timer { get; }
    public LuaEngine Lua { get; }
    public PhysicsEngine Physics { get; }
    public OnlineMatchSession OnlineSession { get; } = new();

    public MatchManager Match => Physics.Match;
    public ProjectileManager Projectiles => Physics.Projectiles;
    public CombatResolver Combat => Physics.Combat;

    public SoundHandler? Sound { get; set; }
    public Player? Player1 { get; set; }
    public Player? Player2 { get; set; }

    public bool IsLobbySession { get; private set; } = false;
    public string SelectedLobbyFighterId { get; set; } = "brawler";

    public string[] AvailableFighters => ModManager.AvailableFighters.Length > 0
        ? ModManager.AvailableFighters
        : DefaultFighters;

    public string[] FilteredFighters
    {
        get
        {
            string[] filtered = ModManager.GetFilteredFighters(ModManager.ActiveModFilter);
            if (filtered.Length > 0) return filtered;
            return ModManager.ActiveModFilter == ModFilterMode.ModsOnly ? Array.Empty<string>() : DefaultFighters;
        }
    }

    public EngineServices(AppConfig config)
    {
        Config = config;
        Identity = new LocalIdentityProvider(config);
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
        SteamManager.Initialize();
        if (SteamManager.IsInitialized)
        {
            Identity = new SteamIdentityProvider(Config);
        }
        LeaderboardDatabase.Initialize();
        ShaderManager.Initialize();
        PostFXManager.Initialize();
        SceneManager.Initialize();
        CameraManager.Initialize();
        LuaMoveTranslator.InitializeCache(Lua);
        UIRenderer.InitializeFont();
        GUIEngine.Initialize();
    }

    public GameData CreateGameData() => new(Player1, Player2, Timer, Lua, Config, Sound, this);

    public void EnsureMatchSoundHandler()
    {
        if (GameState.IsLiveArena && Sound == null &&
            Player1?.CurrentCharacter != null && Player2?.CurrentCharacter != null)
        {
            Sound = new SoundHandler(CreateGameData());
            if (GameState.CurrentState == State.Playing)
            {
                Sound.PlaySound(SoundType.RoundStart);
            }
        }
    }

    public void SyncSessionForCurrentState()
    {
        if (SteamLobbyManager.ShouldEnterCharacterSelect && GameState.CurrentState == State.MainMenu)
        {
            SteamLobbyManager.ConsumeEnterCharacterSelectFlag();
            string mode = string.Equals(SteamLobbyManager.QueuedMode, "2v2", StringComparison.OrdinalIgnoreCase)
                ? "2v2"
                : "1v1_Online";
            EnterOnlineCharacterSelect(mode);
        }

        if (SteamLobbyManager.PendingStart is PendingOnlineMatchStart pending &&
            GameState.CurrentState == State.ChoiceMenu)
        {
            SteamLobbyManager.ConsumePendingStart();
            StartVerifiedOnlineMatch(pending.MatchSeed, pending.InputDelayFrames, pending.RollbackEnabled);
        }

        if (GameState.CurrentState == State.GameOver)
        {
            OnlineSession.Disconnect();
            if (SteamLobbyManager.IsInMatchLobby)
                SteamLobbyManager.LeaveLobby();
            DisposeMatchSession();
            SteamRichPresence.UpdateForSession(this);
            return;
        }

        if (GameState.CurrentState == State.MainMenu)
        {
            OnlineSession.Disconnect();
            if (SteamLobbyManager.State == LobbySearchState.Playing)
                SteamLobbyManager.LeaveLobby();
            if (!IsLobbySession && (Player1 != null || Player2 != null || Sound != null))
            {
                DisposeMatchSession();
            }
            EnsureLobbySession();
        }

        SteamRichPresence.UpdateForSession(this);
    }

    public void EnsureLobbySession()
    {
        if (IsLobbySession && Player1?.CurrentCharacter != null && Player2?.CurrentCharacter != null)
            return;

        Match.IsTrainingMode = false;
        SceneManager.LoadLobbyScene();

        string[] fighters = FilteredFighters;
        string[] fallbackPool = fighters.Length > 0 ? fighters : AvailableFighters;

        if (!Array.Exists(fallbackPool, f => string.Equals(f, SelectedLobbyFighterId, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedLobbyFighterId = fallbackPool[0];
        }

        string dummyId = Array.Exists(AvailableFighters, f => string.Equals(f, "titan", StringComparison.OrdinalIgnoreCase))
            ? "titan"
            : fallbackPool[^1];

        int sw = PhysicsEngine.LogicalWidth;
        int sh = PhysicsEngine.LogicalHeight;
        Vector2 spawnP1 = new Vector2(sw * 0.30f, PhysicsEngine.GroundY * 0.86f);
        Vector2 spawnP2 = new Vector2(sw * 0.65f, sh + (PhysicsEngine.GroundY * 0.86f));

        Player1?.Dispose();
        Player2?.Dispose();

        Player p1 = Player.Create("Player 1", 0, spawnP1, Config, isAI: false);
        Player p2 = Player.Create("Sparring Dummy", 1, spawnP2, Config, isAI: true);
        p2.IsPassiveDummy = true;
        p2.SetAIDifficulty(AIDifficulty.Hard);
        p1.SetOpponent(p2);
        p2.SetOpponent(p1);

        p1.ChangeCharacter(Lua.CreateCharacter(SelectedLobbyFighterId, slot: 0));
        p2.ChangeCharacter(Lua.CreateCharacter(dummyId, slot: 1));
        p1.ResetState(faceRight: true);
        p2.ResetState(faceRight: false);

        Player1 = p1;
        Player2 = p2;
        PruneUnusedCharacterSpriteSheets();

        Projectiles.Clear();
        VFXManager.Clear();
        PostFXManager.ResetAll();
        Match.HitstopTimer = 0f;
        CameraManager.Reset();

        Sound?.Dispose();
        Sound = new SoundHandler(CreateGameData());
        IsLobbySession = true;
    }

    public void StartMatchFromLobby(
        bool p1AI,
        bool p2AI,
        string? p1CharId,
        string? p2CharId,
        bool startImmediately,
        bool isTrainingMode,
        string matchMode = "1v1_Local")
    {
        OnlineSession.Disconnect();
        DisposeMatchSession(unloadTextures: false);
        Match.IsTrainingMode = isTrainingMode;
        Match.CurrentMatchMode = string.IsNullOrWhiteSpace(matchMode) ? "1v1_Local" : matchMode;
        SceneManager.LoadMatchScene();

        int sw = PhysicsEngine.LogicalWidth;
        Vector2 spawnP1 = new Vector2(sw * 0.15f, PhysicsEngine.GroundY * 0.9f);
        Vector2 spawnP2 = new Vector2(sw * 0.85f, PhysicsEngine.GroundY * 0.9f);

        Player p1 = Player.Create("Player 1", 0, spawnP1, Config, isAI: p1AI);
        Player p2 = Player.Create("Player 2", 1, spawnP2, Config, isAI: p2AI);
        p1.SetOpponent(p2);
        p2.SetOpponent(p1);

        if (!string.IsNullOrEmpty(p1CharId))
            p1.ChangeCharacter(Lua.CreateCharacter(p1CharId, slot: 0));

        if (!string.IsNullOrEmpty(p2CharId))
            p2.ChangeCharacter(Lua.CreateCharacter(p2CharId, slot: 1));

        Player1 = p1;
        Player2 = p2;
        PruneUnusedCharacterSpriteSheets();

        if (startImmediately)
        {
            Match.ResetMatch(p1, p2, Projectiles);
            GameState.SetState(State.Playing);
        }
        else
        {
            GameState.SetState(State.ChoiceMenu);
        }
    }

    public void EnterOnlineCharacterSelect(string matchMode = "1v1_Online")
    {
        OnlineSession.Disconnect();
        DisposeMatchSession(unloadTextures: false);
        Match.IsTrainingMode = false;
        Match.CurrentMatchMode = string.IsNullOrWhiteSpace(matchMode) ? "1v1_Online" : matchMode;
        SceneManager.LoadMatchScene();

        int sw = PhysicsEngine.LogicalWidth;
        Vector2 spawnP1 = new Vector2(sw * 0.15f, PhysicsEngine.GroundY * 0.9f);
        Vector2 spawnP2 = new Vector2(sw * 0.85f, PhysicsEngine.GroundY * 0.9f);

        Player p1 = Player.Create("Player 1", 0, spawnP1, Config, isAI: false);
        Player p2 = Player.Create("Player 2", 1, spawnP2, Config, isAI: false);
        p1.SetOpponent(p2);
        p2.SetOpponent(p1);

        string initialId = SelectedLobbyFighterId;
        if (!Array.Exists(FilteredFighters, f => string.Equals(f, initialId, StringComparison.OrdinalIgnoreCase)) &&
            FilteredFighters.Length > 0)
        {
            initialId = FilteredFighters[0];
        }

        Character localChar = Lua.CreateCharacter(initialId, slot: SteamLobbyManager.LocalSlot);
        if (SteamLobbyManager.LocalSlot == 0)
            p1.ChangeCharacter(localChar);
        else
            p2.ChangeCharacter(localChar);

        Player1 = p1;
        Player2 = p2;
        PruneUnusedCharacterSpriteSheets();

        SteamLobbyManager.SetLocalSelection(
            localChar.CharacterId,
            localChar.ScriptHash,
            localChar.BaseScriptHash,
            ready: false,
            workshopId: localChar.WorkshopId);

        GameState.SetState(State.ChoiceMenu);
    }

    public void StartVerifiedOnlineMatch(uint matchSeed, int inputDelayFrames, bool rollbackEnabled)
    {
        if (Player1 == null || Player2 == null) return;

        int localSlot = SteamLobbyManager.LocalSlot;
        int peerSlot = SteamLobbyManager.PeerSlot;
        Player localPlayer = localSlot == 0 ? Player1 : Player2;
        Player peerPlayer = peerSlot == 0 ? Player1 : Player2;

        if (localPlayer.CurrentCharacter == null && !string.IsNullOrEmpty(SteamLobbyManager.LocalCharId))
        {
            localPlayer.ChangeCharacter(Lua.CreateCharacter(SteamLobbyManager.LocalCharId, slot: localSlot));
        }

        if (!string.IsNullOrEmpty(SteamLobbyManager.PeerCustomScriptPath) &&
            File.Exists(SteamLobbyManager.PeerCustomScriptPath))
        {
            bool applyStored = SteamLobbyManager.PeerCustomBalanceGenome == null && BalanceOverrideStore.IsEnabled;
            Character peerChar = Lua.CreateCharacterFromPath(SteamLobbyManager.PeerCustomScriptPath, peerSlot, applyStoredBalance: applyStored);
            if (SteamLobbyManager.PeerCustomBalanceGenome != null)
                CharacterLoader.ApplyGenome(peerChar, SteamLobbyManager.PeerCustomBalanceGenome);
            peerPlayer.ChangeCharacter(peerChar);
        }
        else if (peerPlayer.CurrentCharacter == null && !string.IsNullOrEmpty(SteamLobbyManager.PeerCharId))
        {
            peerPlayer.ChangeCharacter(Lua.CreateCharacter(SteamLobbyManager.PeerCharId, slot: peerSlot));
        }

        if (Player1.CurrentCharacter == null || Player2.CurrentCharacter == null)
            return;

        Match.IsTrainingMode = false;
        SceneManager.LoadMatchScene();
        Match.ResetMatch(Player1, Player2, Projectiles, Identity, matchSeed);
        Match.Telemetry.SetSlotIdentity(localSlot, Identity.SteamId, Identity.DisplayName);
        Match.Telemetry.SetSlotIdentity(peerSlot, SteamLobbyManager.PeerSteamId, SteamLobbyManager.PeerPersonaName);
        PruneUnusedCharacterSpriteSheets();

        OnlineSession.Begin(SteamLobbyManager.PeerSteamId, localSlot, inputDelayFrames, rollbackEnabled);
        GameState.SetState(State.Playing);
    }

    public void CleanupMatchSession()
    {
        if (GameState.CurrentState == State.GameOver ||
            (GameState.CurrentState == State.MainMenu && !IsLobbySession))
        {
            DisposeMatchSession();
        }
    }

    public void PruneUnusedCharacterSpriteSheets()
    {
        var activePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Player1?.CurrentCharacter?.CollectTexturePaths(activePaths);
        Player2?.CurrentCharacter?.CollectTexturePaths(activePaths);
        CharacterLoader.UnloadUnusedSpriteSheets(activePaths);
    }

    public void DisposeMatchSession(bool unloadTextures = true)
    {
        IsLobbySession = false;
        Match.Telemetry.StopTracking();
        LeaderboardDatabase.Flush();
        if (Player1 != null || Player2 != null || Sound != null)
        {
            Sound?.Dispose();
            Sound = null;
            Player1?.Dispose();
            Player2?.Dispose();
            Player1 = null;
            Player2 = null;
            if (unloadTextures)
            {
                Character.UnloadAllSpriteSheets();
            }
            Projectiles.Clear();
            VFXManager.Clear();
            PostFXManager.ResetAll();
            Match.HitstopTimer = 0f;
            CameraManager.Reset();
        }
    }

    public void DisposeAll()
    {
        OnlineSession.Disconnect();
        SteamManager.Shutdown();
        GeneticBalanceTrainer.StopAndWait(1500);
        LeaderboardDatabase.Flush();
        MacroTacticalModel.SharedInstance.SaveWeights();
        Player1?.Dispose();
        Player2?.Dispose();
        Player1 = null;
        Player2 = null;
        GUIEngine.Dispose();
        UIRenderer.DisposeFont();
        VFXManager.Dispose();
        PostFXManager.Dispose();
        SceneManager.Dispose();
        ShaderManager.Dispose();
        SharedTextures.Dispose();
        Character.UnloadAllSpriteSheets();
        Sound?.Dispose();
        Sound = null;

        if (Current == this) Current = null;
    }
}
