using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

public class MainMenuScreen : IUIScreen
{
    private enum LobbyQueueMode
    {
        None,
        Queue1v1,
        Queue2v2
    }

    private const float ToastDurationSeconds = 2.6f;

    private readonly List<WorldButton> _worldButtons = new();
    private PaginatedRosterWidget? _roster;
    private Button? _descPanel;
    private Button? _launchQueueButton;
    private Button? _cancelQueueButton;
    private Button? _leaderboardPanel;
    private Button? _closeLeaderboardButton;

    private WorldButton? _modFilterBtn;
    private WorldButton? _queue1v1Btn;
    private WorldButton? _queue2v2Btn;
    private WorldButton? _leaderboardsBtn;
    private WorldButton? _debugHitboxBtn;
    private WorldButton? _dummyModeBtn;
    private WorldButton? _labDummyBtn;
    private WorldButton? _labBlockBtn;
    private WorldButton? _labAutoHealBtn;

    private LobbyQueueMode _queueMode = LobbyQueueMode.None;
    private bool _showLeaderboards = false;
    private int _knownCatalogVersion = -1;
    private string _knownDescCharName = "";
    private string _knownDescCharHash = "";
    private string _toastText = "";
    private float _toastTimer = 0f;
    private int _lastDummyHp = -1;
    private int _lastP1Combo = -1;
    private string _cachedDummyInfo = "";

    public void Initialize()
    {
        Dispose();
        _worldButtons.Clear();
        _queueMode = LobbyQueueMode.None;
        _showLeaderboards = false;
        _knownCatalogVersion = -1;
        _knownDescCharName = "";
        _knownDescCharHash = "";
        _toastText = "";
        _toastTimer = 0f;
        _lastDummyHp = -1;
        _lastP1Combo = -1;
        _cachedDummyInfo = "";

        BuildOfflineRoomButtons();
        BuildOnlineCenterButtons();
        BuildDeveloperAlcoveButtons();
        BuildTrainingLabButtons();
        BuildHudWidgets();
    }

    private void BuildOfflineRoomButtons()
    {
        WorldButton settingsBtn = CreateWorldButton(
            "settings", "SETTINGS", "OPTIONS & KEYS",
            new Vector2(-0.75f, 0.37f), new Vector2(0.20f, 0.09f),
            new Vector3(0.45f, 0.78f, 1.0f), isToggle: false,
            _ => GameState.SetState(State.Settings));

        WorldButton exitBtn = CreateWorldButton(
            "exit", "EXIT", "QUIT GAME",
            new Vector2(-0.25f, 0.37f), new Vector2(0.18f, 0.09f),
            new Vector3(0.95f, 0.32f, 0.36f), isToggle: false,
            _ => GameState.SetState(State.Exit));

        WorldButton local1v1Btn = CreateWorldButton(
            "local_1v1", "LOCAL 1v1", "VERSUS SELECT",
            new Vector2(-0.79f, 0.505f), new Vector2(0.20f, 0.095f),
            new Vector3(1.0f, 0.62f, 0.28f), isToggle: false,
            ctx => OnLaunchLocalPvp(ctx, is2v2: false));

        WorldButton local2v2Btn = CreateWorldButton(
            "local_2v2", "LOCAL 2v2", "TAG VERSUS",
            new Vector2(-0.50f, 0.505f), new Vector2(0.20f, 0.095f),
            new Vector3(0.95f, 0.48f, 0.72f), isToggle: false,
            ctx => OnLaunchLocalPvp(ctx, is2v2: true));

        WorldButton vsAiBtn = CreateWorldButton(
            "vs_ai", "VS AI", "QUICK START",
            new Vector2(-0.21f, 0.505f), new Vector2(0.20f, 0.095f),
            new Vector3(0.25f, 0.90f, 0.48f), isToggle: false,
            OnLaunchVsAi);

        _worldButtons.AddRange(new[]
        {
            settingsBtn, exitBtn,
            local1v1Btn, local2v2Btn, vsAiBtn
        });
    }

    private void BuildOnlineCenterButtons()
    {
        _leaderboardsBtn = CreateWorldButton(
            "leaderboards", "LEADERBOARDS", "RANKINGS",
            new Vector2(0.50f, 0.37f), new Vector2(0.23f, 0.095f),
            new Vector3(1.0f, 0.80f, 0.25f), isToggle: true,
            _ =>
            {
                _showLeaderboards = !_showLeaderboards;
                if (_showLeaderboards)
                    ShowToast("SEASON 1 LEADERBOARDS");
            });

        _queue1v1Btn = CreateWorldButton(
            "play_1v1", "ONLINE 1v1", "QUEUE 1v1",
            new Vector2(0.22f, 0.505f), new Vector2(0.21f, 0.095f),
            new Vector3(0.28f, 0.78f, 1.0f), isToggle: true,
            _ => ToggleQueueMode(LobbyQueueMode.Queue1v1));

        _queue2v2Btn = CreateWorldButton(
            "play_2v2", "ONLINE 2v2", "QUEUE 2v2",
            new Vector2(0.78f, 0.505f), new Vector2(0.21f, 0.095f),
            new Vector3(0.68f, 0.45f, 1.0f), isToggle: true,
            _ => ToggleQueueMode(LobbyQueueMode.Queue2v2));

        _worldButtons.AddRange(new[]
        {
            _leaderboardsBtn, _queue1v1Btn, _queue2v2Btn
        });
    }

    private void BuildDeveloperAlcoveButtons()
    {
        _debugHitboxBtn = CreateWorldButton(
            "dev_hitboxes", "DEBUG HITBOXES", Character.ShowDebugBoxes ? "ON" : "OFF",
            new Vector2(1.23f, 0.37f), new Vector2(0.21f, 0.095f),
            new Vector3(0.0f, 1.0f, 0.95f), isToggle: true,
            _ =>
            {
                Character.ShowDebugBoxes = !Character.ShowDebugBoxes;
                ShowToast($"DEBUG HITBOXES: {(Character.ShowDebugBoxes ? "ON" : "OFF")}");
            });

        WorldButton aiTrainBtn = CreateWorldButton(
            "dev_ai_train", "AI VS AI", "TURBO TRAIN",
            new Vector2(1.50f, 0.37f), new Vector2(0.21f, 0.095f),
            new Vector3(1.0f, 0.18f, 0.85f), isToggle: false,
            OnLaunchAiTraining);

        _dummyModeBtn = CreateWorldButton(
            "dev_dummy_mode", "DUMMY MODE", "PASSIVE",
            new Vector2(1.77f, 0.37f), new Vector2(0.21f, 0.095f),
            new Vector3(0.38f, 1.0f, 0.30f), isToggle: true,
            ToggleDummyMode);

        WorldButton reloadModsBtn = CreateWorldButton(
            "dev_reload_mods", "RELOAD MODS", "HOT REFRESH",
            new Vector2(1.23f, 0.505f), new Vector2(0.21f, 0.095f),
            new Vector3(1.0f, 0.72f, 0.12f), isToggle: false,
            OnReloadMods);

        _modFilterBtn = CreateWorldButton(
            "mod_filter", "MOD FILTER", GetFilterLabel(ModManager.ActiveModFilter),
            new Vector2(1.50f, 0.505f), new Vector2(0.21f, 0.095f),
            new Vector3(1.0f, 0.75f, 0.25f), isToggle: true,
            OnCycleModFilter);

        WorldButton resetWeightsBtn = CreateWorldButton(
            "dev_reset_weights", "RESET AI WEIGHTS", "CLEAR RL MODEL",
            new Vector2(1.77f, 0.505f), new Vector2(0.21f, 0.095f),
            new Vector3(1.0f, 0.25f, 0.30f), isToggle: false,
            _ =>
            {
                MacroTacticalModel.SharedInstance.ResetWeights();
                ShowToast("AI WEIGHTS RESET TO PRETRAINED");
            });

        _worldButtons.AddRange(new[]
        {
            _debugHitboxBtn, aiTrainBtn, _dummyModeBtn,
            reloadModsBtn, _modFilterBtn, resetWeightsBtn
        });
    }

    private void BuildTrainingLabButtons()
    {
        _labDummyBtn = CreateWorldButton(
            "lab_dummy", "DUMMY", "PASSIVE",
            new Vector2(0.11f, 1.32f), new Vector2(0.175f, 0.082f),
            new Vector3(0.38f, 1.0f, 0.30f), isToggle: true,
            ToggleDummyMode,
            allowCombatTrigger: false);

        _labBlockBtn = CreateWorldButton(
            "lab_block", "BLOCK", "OFF",
            new Vector2(0.305f, 1.32f), new Vector2(0.175f, 0.082f),
            new Vector3(0.35f, 0.75f, 1.0f), isToggle: true,
            ctx =>
            {
                Player? dummy = ctx.Data.Player2;
                if (dummy == null) return;
                dummy.DummyBlock = dummy.DummyBlock switch
                {
                    DummyBlockMode.Off   => DummyBlockMode.Stand,
                    DummyBlockMode.Stand => DummyBlockMode.Crouch,
                    _                    => DummyBlockMode.Off
                };
                ShowToast($"DUMMY BLOCK: {GetDummyBlockLabel(dummy.DummyBlock)}");
            },
            allowCombatTrigger: false);

        _labAutoHealBtn = CreateWorldButton(
            "lab_autoheal", "AUTO-HEAL", "ON",
            new Vector2(0.50f, 1.32f), new Vector2(0.175f, 0.082f),
            new Vector3(0.25f, 0.92f, 0.55f), isToggle: true,
            ctx =>
            {
                Player? dummy = ctx.Data.Player2;
                if (dummy == null) return;
                dummy.AutoHealEnabled = !dummy.AutoHealEnabled;
                if (dummy.AutoHealEnabled && dummy.Health < Player.MAX_HEALTH)
                {
                    dummy.Health = Player.MAX_HEALTH;
                }
                ShowToast($"AUTO-HEAL: {(dummy.AutoHealEnabled ? "ON" : "OFF")}");
            },
            allowCombatTrigger: false);

        WorldButton resetPosBtn = CreateWorldButton(
            "lab_reset", "RESET POSITIONS", "HP & SPAWN",
            new Vector2(0.695f, 1.32f), new Vector2(0.175f, 0.082f),
            new Vector3(1.0f, 0.72f, 0.18f), isToggle: false,
            OnResetTrainingLabPositions,
            allowCombatTrigger: false);

        WorldButton returnLobbyBtn = CreateWorldButton(
            "lab_return", "RETURN TO LOBBY", "CENTER HUB",
            new Vector2(0.89f, 1.32f), new Vector2(0.175f, 0.082f),
            new Vector3(0.45f, 0.85f, 1.0f), isToggle: false,
            OnReturnFromTrainingLab,
            allowCombatTrigger: false);

        _worldButtons.AddRange(new[]
        {
            _labDummyBtn, _labBlockBtn, _labAutoHealBtn,
            resetPosBtn, returnLobbyBtn
        });
    }

    private void ToggleDummyMode(ButtonContext ctx)
    {
        Player? dummy = ctx.Data.Player2;
        if (dummy == null) return;
        dummy.IsPassiveDummy = !dummy.IsPassiveDummy;
        dummy.SetAIDifficulty(AIDifficulty.Hard);
        string modeName = dummy.IsPassiveDummy ? "PASSIVE" : "AI (HARD)";
        ShowToast($"SPARRING DUMMY: {modeName}");
    }

    private void OnResetTrainingLabPositions(ButtonContext ctx)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        float labGroundY = (1.0f + PhysicsEngine.GROUND_Y_PCT) * sh;

        Player? p1 = ctx.Data.Player1;
        Player? p2 = ctx.Data.Player2;

        if (p1 != null)
        {
            float halfH = (p1.CurrentCharacter?.Height ?? sh * 0.20f) * 0.5f;
            p1.ResetState(faceRight: true);
            p1.Position = new Vector2(sw * 0.32f, labGroundY - halfH);
            p1.PreviousPosition = p1.Position;
        }

        if (p2 != null)
        {
            float halfH = (p2.CurrentCharacter?.Height ?? sh * 0.20f) * 0.5f;
            p2.ResetState(faceRight: false);
            p2.Position = new Vector2(sw * 0.68f, labGroundY - halfH);
            p2.PreviousPosition = p2.Position;
        }

        ctx.Data.Services?.Projectiles.Clear();
        VFXManager.Clear();
        ShowToast("TRAINING POSITIONS RESET");
    }

    private void OnReturnFromTrainingLab(ButtonContext ctx)
    {
        Player? p1 = ctx.Data.Player1;
        if (p1 == null) return;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        float halfH = (p1.CurrentCharacter?.Height ?? sh * 0.20f) * 0.5f;

        p1.Velocity = Vector2.Zero;
        p1.PlatformDropTimer = 0f;
        p1.IsOnPlatform = false;
        p1.IsGrounded = true;
        p1.Position = new Vector2(sw * 0.50f, PhysicsEngine.GroundY - halfH);
        p1.PreviousPosition = p1.Position;

        VFXManager.SpawnPreset(VFXPreset.Shockwave, p1.Position, p1.FacingRight);
        ShowToast("RETURNED TO CENTER HUB");
    }

    private static string GetDummyBlockLabel(DummyBlockMode mode) => mode switch
    {
        DummyBlockMode.Stand  => "STAND",
        DummyBlockMode.Crouch => "CROUCH",
        _                     => "OFF"
    };

    private void BuildHudWidgets()
    {
        _roster = new PaginatedRosterWidget(
            xPct: 0.015f,
            yPct: 0.015f,
            widthPct: 0.585f,
            heightPct: 0.175f,
            columns: 6,
            rows: 1
        );
        TryLoadOptionalUiTexture("roster_panel.png", p => _roster.LoadPanelTexture(p));

        _descPanel = new Button(
            xPct: 0.615f,
            yPct: 0.015f,
            widthPct: 0.37f,
            heightPct: 0.235f,
            text: "Select a fighter to view archetype and move list."
        )
        .WithTitle("FIGHTER INFO")
        .WithFontSize(0.017f)
        .WithScrolling();
        TryLoadOptionalUiTexture("desc_panel.png", p => _descPanel.LoadBackgroundTexture(p));

        _launchQueueButton = new Button(0.36f, 0.205f, 0.13f, 0.048f, "LAUNCH PvP", OnLaunchQueuedPvp)
        {
            FontSizePct = 0.018f,
            AccentColor = new Vector3(0.25f, 0.90f, 0.50f)
        };

        _cancelQueueButton = new Button(0.50f, 0.205f, 0.10f, 0.048f, "CANCEL", _ => ToggleQueueMode(LobbyQueueMode.None))
        {
            FontSizePct = 0.018f,
            AccentColor = new Vector3(0.95f, 0.35f, 0.35f)
        };

        _leaderboardPanel = new Button(
            xPct: 0.24f,
            yPct: 0.28f,
            widthPct: 0.52f,
            heightPct: 0.44f,
            text: "#1  APEX_VOID      — 2,480 ELO  (68W - 11L)\n" +
                  "#2  AERO_LANCER    — 2,395 ELO  (54W - 14L)\n" +
                  "#3  CHRONO_MAGE    — 2,310 ELO  (49W - 16L)\n" +
                  "#4  TIDAL_PRIEST   — 2,240 ELO  (41W - 19L)\n" +
                  "#5  TITAN_CRUSHER  — 2,185 ELO  (37W - 21L)\n\n" +
                  "Online matchmaking & seasonal rankings preview."
        )
        .WithTitle("SEASON 1 LEADERBOARDS")
        .WithFontSize(0.018f)
        .WithAccent(1.0f, 0.80f, 0.25f)
        .WithScrolling();

        _closeLeaderboardButton = new Button(0.43f, 0.735f, 0.14f, 0.048f, "CLOSE", _ => _showLeaderboards = false)
        {
            FontSizePct = 0.018f,
            AccentColor = new Vector3(1.0f, 0.80f, 0.25f)
        };
    }

    private static WorldButton CreateWorldButton(
        string id,
        string label,
        string subLabel,
        Vector2 worldPosPct,
        Vector2 sizePct,
        Vector3 accentColor,
        bool isToggle,
        Action<ButtonContext> onTrigger,
        bool allowCombatTrigger = true)
    {
        var btn = new WorldButton
        {
            Id = id,
            Label = label,
            SubLabel = subLabel,
            WorldPositionPct = worldPosPct,
            SizePct = sizePct,
            AccentColor = accentColor,
            IsToggle = isToggle,
            AllowCombatTrigger = allowCombatTrigger,
            OnTrigger = onTrigger
        };
        TryLoadOptionalUiTexture($"world_btn_{id}.png", p => btn.LoadTexture(p));
        return btn;
    }

    private static void TryLoadOptionalUiTexture(string fileName, Action<string> loader)
    {
        string? path = ModManager.ResolveFile("Assets", "UI", fileName);
        if (path != null)
            loader(path);
    }

    private static string GetFilterLabel(ModFilterMode mode) => mode switch
    {
        ModFilterMode.VanillaOnly    => "VANILLA ONLY",
        ModFilterMode.VanillaAndMods => "VANILLA + MODS",
        ModFilterMode.ModsOnly       => "MODS ONLY",
        _                            => "VANILLA + MODS"
    };

    private void OnCycleModFilter(ButtonContext ctx)
    {
        ModManager.ActiveModFilter = ModManager.ActiveModFilter switch
        {
            ModFilterMode.VanillaOnly    => ModFilterMode.VanillaAndMods,
            ModFilterMode.VanillaAndMods => ModFilterMode.ModsOnly,
            _                            => ModFilterMode.VanillaOnly
        };

        ShowToast($"FILTER: {GetFilterLabel(ModManager.ActiveModFilter)}");
        RefreshRoster(ctx.Data);
    }

    private void ToggleQueueMode(LobbyQueueMode requested)
    {
        _queueMode = (_queueMode == requested) ? LobbyQueueMode.None : requested;
        if (_queueMode != LobbyQueueMode.None)
        {
            string label = _queueMode == LobbyQueueMode.Queue1v1 ? "1v1" : "2v2";
            ShowToast($"QUEUED FOR {label} — KEEP SPARRING!");
        }
    }

    private void OnLaunchVsAi(ButtonContext ctx)
    {
        EngineServices? services = ctx.Data.Services ?? EngineServices.Current;
        if (services == null) return;

        string[] pool = GetPlayablePool(services);
        string p1Id = _roster?.SelectedFighterId ?? services.SelectedLobbyFighterId;
        if (!Array.Exists(pool, f => string.Equals(f, p1Id, StringComparison.OrdinalIgnoreCase)))
            p1Id = pool[0];

        string p2Id = pool[Random.Shared.Next(pool.Length)];
        services.StartMatchFromLobby(
            p1AI: false,
            p2AI: true,
            p1CharId: p1Id,
            p2CharId: p2Id,
            startImmediately: true,
            isTrainingMode: false
        );
        ctx.SetP1(services.Player1);
        ctx.SetP2(services.Player2);
    }

    private void OnLaunchLocalPvp(ButtonContext ctx, bool is2v2)
    {
        EngineServices? services = ctx.Data.Services ?? EngineServices.Current;
        if (services == null) return;

        string[] pool = GetPlayablePool(services);
        string p1Id = _roster?.SelectedFighterId ?? services.SelectedLobbyFighterId;
        if (!Array.Exists(pool, f => string.Equals(f, p1Id, StringComparison.OrdinalIgnoreCase)))
            p1Id = pool[0];

        if (is2v2)
        {
            ShowToast("LOCAL 2v2 — SELECT FIGHTERS");
        }

        services.StartMatchFromLobby(
            p1AI: false,
            p2AI: false,
            p1CharId: p1Id,
            p2CharId: null,
            startImmediately: false,
            isTrainingMode: false
        );
        ctx.SetP1(services.Player1);
        ctx.SetP2(services.Player2);
    }

    private void OnLaunchQueuedPvp(ButtonContext ctx)
    {
        EngineServices? services = ctx.Data.Services ?? EngineServices.Current;
        if (services == null) return;

        string[] pool = GetPlayablePool(services);
        string p1Id = _roster?.SelectedFighterId ?? services.SelectedLobbyFighterId;
        if (!Array.Exists(pool, f => string.Equals(f, p1Id, StringComparison.OrdinalIgnoreCase)))
            p1Id = pool[0];

        _queueMode = LobbyQueueMode.None;
        services.StartMatchFromLobby(
            p1AI: false,
            p2AI: false,
            p1CharId: p1Id,
            p2CharId: null,
            startImmediately: false,
            isTrainingMode: false
        );
        ctx.SetP1(services.Player1);
        ctx.SetP2(services.Player2);
    }

    private static void OnLaunchAiTraining(ButtonContext ctx)
    {
        EngineServices? services = ctx.Data.Services ?? EngineServices.Current;
        if (services == null) return;

        string[] pool = GetPlayablePool(services);
        var rng = Random.Shared;
        string p1Id = pool[rng.Next(pool.Length)];
        string p2Id = pool[rng.Next(pool.Length)];

        services.StartMatchFromLobby(
            p1AI: true,
            p2AI: true,
            p1CharId: p1Id,
            p2CharId: p2Id,
            startImmediately: true,
            isTrainingMode: true
        );
        ctx.SetP1(services.Player1);
        ctx.SetP2(services.Player2);
    }

    private void OnReloadMods(ButtonContext ctx)
    {
        ModManager.Initialize();
        ctx.Data.LuaEngine.ClearCharacterCache();
        LuaMoveTranslator.InitializeCache(ctx.Data.LuaEngine);
        _roster?.ReloadThumbnails();

        EngineServices? services = ctx.Data.Services ?? EngineServices.Current;
        string[] pool = GetPlayablePool(services);
        Player? p1 = ctx.Data.Player1;
        Player? p2 = ctx.Data.Player2;

        if (p1 != null)
        {
            string p1Id = services?.SelectedLobbyFighterId ?? _roster?.SelectedFighterId ?? pool[0];
            if (string.IsNullOrEmpty(ModManager.ResolveCharacterScript(p1Id)))
                p1Id = pool[0];

            p1.ChangeCharacter(ctx.Data.LuaEngine.CreateCharacter(p1Id, slot: 0));
            if (services != null) services.SelectedLobbyFighterId = p1Id;
            if (_roster != null) _roster.SelectedFighterId = p1Id;
        }

        if (p2?.CurrentCharacter != null)
        {
            string dummyId = Array.Exists(pool, f => string.Equals(f, "titan", StringComparison.OrdinalIgnoreCase))
                ? "titan"
                : pool[0];
            p2.ChangeCharacter(ctx.Data.LuaEngine.CreateCharacter(dummyId, slot: 1));
        }

        ctx.Data.SoundHandler?.ReloadCharacterBanks(p1, p2);
        _knownDescCharName = "";
        _knownDescCharHash = "";
        RefreshRoster(ctx.Data);
        ShowToast("MODS & SCRIPTS RELOADED!");
    }

    private static string[] GetPlayablePool(EngineServices? services)
    {
        if (services != null && services.FilteredFighters.Length > 0)
            return services.FilteredFighters;
        if (services != null && services.AvailableFighters.Length > 0)
            return services.AvailableFighters;
        return GUIEngine.RosterIds;
    }

    private void RefreshRoster(GameData data)
    {
        if (_roster == null) return;

        EngineServices? services = data.Services ?? EngineServices.Current;
        string[] fighters = services?.FilteredFighters
                         ?? ModManager.GetFilteredFighters(ModManager.ActiveModFilter);

        string selectedId = services?.SelectedLobbyFighterId ?? _roster.SelectedFighterId ?? "brawler";
        _roster.SetFighters(fighters, OnSelectLobbyFighter, selectedId);
        _knownCatalogVersion = ModManager.CatalogVersion;
    }

    private void OnSelectLobbyFighter(string fighterId, ButtonContext ctx)
    {
        EngineServices? services = ctx.Data.Services ?? EngineServices.Current;
        if (services != null)
            services.SelectedLobbyFighterId = fighterId;

        Player? p1 = ctx.Data.Player1;
        if (p1 != null)
        {
            Character loaded = ctx.Data.LuaEngine.CreateCharacter(fighterId, slot: 0);
            p1.ChangeCharacter(loaded);
            services?.PruneUnusedCharacterSpriteSheets();
            ctx.Data.SoundHandler?.ReloadCharacterBanks(p1, ctx.Data.Player2);
            VFXManager.SpawnPreset(VFXPreset.Shockwave, p1.Position, p1.FacingRight);
        }

        _knownDescCharName = "";
        _knownDescCharHash = "";
    }

    private void SyncDynamicLabels(GameData data)
    {
        if (_modFilterBtn != null)
        {
            _modFilterBtn.SubLabel = GetFilterLabel(ModManager.ActiveModFilter);
            _modFilterBtn.IsSelected = ModManager.ActiveModFilter != ModFilterMode.VanillaOnly;
        }

        if (_queue1v1Btn != null)
        {
            _queue1v1Btn.IsSelected = _queueMode == LobbyQueueMode.Queue1v1;
            _queue1v1Btn.SubLabel = _queue1v1Btn.IsSelected ? "IN QUEUE" : "QUEUE 1v1";
        }

        if (_queue2v2Btn != null)
        {
            _queue2v2Btn.IsSelected = _queueMode == LobbyQueueMode.Queue2v2;
            _queue2v2Btn.SubLabel = _queue2v2Btn.IsSelected ? "IN QUEUE" : "QUEUE 2v2";
        }

        if (_leaderboardsBtn != null)
        {
            _leaderboardsBtn.IsSelected = _showLeaderboards;
            _leaderboardsBtn.SubLabel = _showLeaderboards ? "VIEWING" : "RANKINGS";
        }

        if (_debugHitboxBtn != null)
        {
            _debugHitboxBtn.IsSelected = Character.ShowDebugBoxes;
            _debugHitboxBtn.SubLabel = Character.ShowDebugBoxes ? "ON" : "OFF";
        }

        if (_dummyModeBtn != null && data.Player2 != null)
        {
            bool fighting = !data.Player2.IsPassiveDummy;
            _dummyModeBtn.IsSelected = fighting;
            _dummyModeBtn.SubLabel = fighting ? "AI (HARD)" : "PASSIVE";
        }

        if (_labDummyBtn != null && data.Player2 != null)
        {
            bool fighting = !data.Player2.IsPassiveDummy;
            _labDummyBtn.IsSelected = fighting;
            _labDummyBtn.SubLabel = fighting ? "AI (HARD)" : "PASSIVE";
        }

        if (_labBlockBtn != null && data.Player2 != null)
        {
            _labBlockBtn.IsSelected = data.Player2.DummyBlock != DummyBlockMode.Off;
            _labBlockBtn.SubLabel = GetDummyBlockLabel(data.Player2.DummyBlock);
        }

        if (_labAutoHealBtn != null && data.Player2 != null)
        {
            _labAutoHealBtn.IsSelected = data.Player2.AutoHealEnabled;
            _labAutoHealBtn.SubLabel = data.Player2.AutoHealEnabled ? "ON" : "OFF";
        }

        Character? activeChar = data.Player1?.CurrentCharacter;
        if (_descPanel != null && activeChar != null)
        {
            if (!string.Equals(_knownDescCharName, activeChar.Name, StringComparison.Ordinal) ||
                !string.Equals(_knownDescCharHash, activeChar.ScriptHash, StringComparison.Ordinal))
            {
                _knownDescCharName = activeChar.Name;
                _knownDescCharHash = activeChar.ScriptHash;
                _descPanel.Title = activeChar.Name.ToUpperInvariant();
                _descPanel.Text = activeChar.Description;
            }
        }
    }

    private void ShowToast(string message)
    {
        _toastText = message;
        _toastTimer = ToastDurationSeconds;
    }

    public void Update(ButtonContext context)
    {
        float dt = Raylib.GetFrameTime();
        if (_toastTimer > 0f)
            _toastTimer = MathF.Max(0f, _toastTimer - dt);

        if (_knownCatalogVersion != ModManager.CatalogVersion)
            RefreshRoster(context.Data);

        SyncDynamicLabels(context.Data);

        _roster?.Update(context);
        _descPanel?.Update(context);

        bool pointerInBanner = false;
        if (_queueMode != LobbyQueueMode.None)
        {
            Rectangle bannerBounds = UIRenderer.GetPctRect(0.015f, 0.200f, 0.585f, 0.058f);
            pointerInBanner = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), bannerBounds);
            _launchQueueButton?.Update(context);
            _cancelQueueButton?.Update(context);
        }

        bool pointerInLeaderboards = false;
        if (_showLeaderboards)
        {
            Rectangle lbBounds = UIRenderer.GetPctRect(0.24f, 0.28f, 0.52f, 0.51f);
            pointerInLeaderboards = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), lbBounds);
            _leaderboardPanel?.Update(context);
            _closeLeaderboardButton?.Update(context);
        }

        bool hudCapturedPointer = (_roster?.CapturesPointer ?? false) ||
                                  (_descPanel?.CapturesPointer ?? false) ||
                                  pointerInBanner ||
                                  pointerInLeaderboards;

        for (int i = 0; i < _worldButtons.Count; i++)
        {
            _worldButtons[i].Update(context, dt, blockMouseInput: hudCapturedPointer);
        }
    }

    public void DrawWorld(GameData data)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        float groundY = PhysicsEngine.GroundY;

        DrawOfflineRoomDecor(sw, sh, groundY);
        DrawCenterRoomDecor(sw, sh, groundY);
        DrawDeveloperAlcoveDecor(sw, sh, groundY);
        DrawTrainingLabDecor(sw, sh);

        for (int i = 0; i < _worldButtons.Count; i++)
        {
            _worldButtons[i].DrawPanel(keepShaderBound: true);
        }
        ShaderManager.EndShader();

        for (int i = 0; i < _worldButtons.Count; i++)
        {
            _worldButtons[i].DrawOverlay();
        }

        DrawWorldFighterTags(data, sh);
    }

    private static void DrawOfflineRoomDecor(int sw, int sh, float groundY)
    {
        Rectangle offlineRect = new Rectangle(-sw, 0f, sw * 0.995f, groundY);
        Raylib.DrawRectangleRec(offlineRect, new Color(24, 14, 18, 125));

        int titleFont = Math.Max(18, (int)(sh * 0.034f));
        string header = "// OFFLINE MODES & SYSTEM //";
        int hw = UIRenderer.MeasureText(header, titleFont);
        UIRenderer.DrawTextShadowed(header, -sw * 0.50f - hw * 0.5f, sh * 0.255f, titleFont, new Color(255, 175, 95, 255));

        float leftPortalX = 0f;
        float dividerTop = sh * 0.28f;
        Raylib.DrawRectangle((int)(leftPortalX - 3f), (int)dividerTop, 6, (int)(groundY - dividerTop), new Color(255, 165, 80, 85));

        int signFont = Math.Max(12, (int)(sh * 0.018f));
        UIRenderer.DrawTextShadowed("< OFFLINE", leftPortalX + 10f, dividerTop - signFont - 4f, signFont, new Color(255, 185, 110, 220), shadowOffset: 1f);

        string backSign = "ONLINE HUB >";
        int bsw = UIRenderer.MeasureText(backSign, signFont);
        UIRenderer.DrawTextShadowed(backSign, leftPortalX - bsw - 10f, dividerTop - signFont - 4f, signFont, new Color(130, 210, 255, 220), shadowOffset: 1f);
    }

    private static void DrawCenterRoomDecor(int sw, int sh, float groundY)
    {
        int titleFont = Math.Max(16, (int)(sh * 0.028f));
        string header = "// ONLINE HUB & LEADERBOARDS //";
        int hw = UIRenderer.MeasureText(header, titleFont);
        UIRenderer.DrawTextShadowed(header, sw * 0.50f - hw * 0.5f, sh * 0.265f, titleFont, new Color(135, 210, 255, 245));

        float hatchLeft = sw * PhysicsEngine.LOBBY_HATCH_LEFT_PCT;
        float hatchRight = sw * PhysicsEngine.LOBBY_HATCH_RIGHT_PCT;
        float hatchW = hatchRight - hatchLeft;
        float hatchTop = groundY;
        float shaftBottom = sh * 1.02f;

        // Open drop-shaft beneath the one-way hatch platform
        Raylib.DrawRectangle((int)hatchLeft, (int)hatchTop, (int)hatchW, (int)(shaftBottom - hatchTop), new Color(8, 12, 20, 220));
        Raylib.DrawRectangleLinesEx(new Rectangle(hatchLeft, hatchTop, hatchW, shaftBottom - hatchTop), 2f, new Color(80, 255, 165, 160));

        int signFont = Math.Max(12, (int)(sh * 0.018f));
        string hatchLabel = "v TRAINING LAB";
        int hlw = UIRenderer.MeasureText(hatchLabel, signFont);
        UIRenderer.DrawTextShadowed(hatchLabel, (hatchLeft + hatchRight - hlw) * 0.5f, groundY - signFont - 12f, signFont, new Color(95, 255, 175, 240), shadowOffset: 1f);

        int subFont = Math.Max(10, (int)(sh * 0.014f));
        string hatchSub = "CROUCH TO DROP";
        int hsw = UIRenderer.MeasureText(hatchSub, subFont);
        UIRenderer.DrawTextShadowed(hatchSub, (hatchLeft + hatchRight - hsw) * 0.5f, groundY + 18f, subFont, new Color(170, 235, 200, 210), shadowOffset: 1f);
    }

    private static void DrawDeveloperAlcoveDecor(int sw, int sh, float groundY)
    {
        float portalX = sw * 1.00f;
        float dividerTop = sh * 0.28f;

        // Passable boundary marker between Center Hub and Right Developer Alcove
        Raylib.DrawRectangle((int)(portalX - 3f), (int)dividerTop, 6, (int)(groundY - dividerTop), new Color(0, 235, 220, 75));
        int signFont = Math.Max(12, (int)(sh * 0.018f));
        string devSign = "DEV >";
        int dsw = UIRenderer.MeasureText(devSign, signFont);
        UIRenderer.DrawTextShadowed(devSign, portalX - dsw - 10f, dividerTop - signFont - 4f, signFont, new Color(110, 220, 255, 210), shadowOffset: 1f);
        UIRenderer.DrawTextShadowed("< ONLINE", portalX + 10f, dividerTop - signFont - 4f, signFont, new Color(135, 210, 255, 210), shadowOffset: 1f);

        // Alcove floor & cyber grid
        Rectangle alcoveRect = new Rectangle(sw * 1.01f, 0f, sw * 0.99f, groundY);
        Raylib.DrawRectangleRec(alcoveRect, new Color(10, 12, 22, 160));

        int gridStep = Math.Max(40, (int)(sw * 0.06f));
        for (int gx = (int)(sw * 1.04f); gx < sw * 2; gx += gridStep)
        {
            Raylib.DrawLine(gx, (int)(sh * 0.12f), gx, (int)groundY, new Color(40, 210, 220, 28));
        }

        int devTitleSize = Math.Max(18, (int)(sh * 0.036f));
        string devHeader = "// DEVELOPER SANDBOX ALCOVE //";
        int hw = UIRenderer.MeasureText(devHeader, devTitleSize);
        UIRenderer.DrawText(devHeader, sw * 1.50f - hw * 0.5f, sh * 0.24f, devTitleSize, new Color(90, 250, 235, 255));

        int hintSize = Math.Max(13, (int)(sh * 0.019f));
        string devHint = "PUNCH OR CLICK TOOLS   |   <- WALK LEFT TO CENTER HUB";
        int dhw = UIRenderer.MeasureText(devHint, hintSize);
        UIRenderer.DrawText(devHint, sw * 1.50f - dhw * 0.5f, sh * 0.29f, hintSize, Color.LIGHTGRAY);
    }

    private static void DrawTrainingLabDecor(int sw, int sh)
    {
        float labTop = sh * 1.0f;
        float labGroundY = (1.0f + PhysicsEngine.GROUND_Y_PCT) * sh;

        // Training Lab chamber backdrop and lower floor
        Raylib.DrawRectangle(0, (int)labTop, sw, sh, new Color(10, 13, 22, 245));
        Raylib.DrawRectangle(0, (int)labGroundY, sw, (int)(sh * 2.0f - labGroundY), new Color(18, 22, 34, 255));

        // Training measurement grid
        int stepX = Math.Max(48, (int)(sw * 0.0625f));
        for (int gx = stepX; gx < sw; gx += stepX)
        {
            Raylib.DrawLine(gx, (int)(labTop + sh * 0.06f), gx, (int)labGroundY, new Color(65, 110, 155, 32));
        }
        int stepY = Math.Max(42, (int)(sh * 0.09f));
        for (int gy = (int)(labTop + sh * 0.12f); gy < (int)labGroundY; gy += stepY)
        {
            Raylib.DrawLine(0, gy, sw, gy, new Color(65, 110, 155, 24));
        }

        // Crisp glowing Training Lab floor edge
        Raylib.DrawLineEx(new Vector2(0f, labGroundY), new Vector2(sw, labGroundY), 3f, new Color(95, 255, 175, 210));

        // Hatch ceiling opening indicator
        float hatchLeft = sw * PhysicsEngine.LOBBY_HATCH_LEFT_PCT;
        float hatchRight = sw * PhysicsEngine.LOBBY_HATCH_RIGHT_PCT;
        Raylib.DrawRectangle((int)hatchLeft, (int)labTop, (int)(hatchRight - hatchLeft), (int)(sh * 0.03f), new Color(22, 48, 42, 180));
        Raylib.DrawRectangleLinesEx(new Rectangle(hatchLeft, labTop, hatchRight - hatchLeft, sh * 0.03f), 2f, new Color(95, 255, 175, 180));

        int titleFont = Math.Max(16, (int)(sh * 0.025f));
        string header = "// TRAINING LAB — MOUSE-CLICK DUMMY CONTROLS //";
        int hw = UIRenderer.MeasureText(header, titleFont);
        UIRenderer.DrawTextShadowed(header, sw * 0.50f - hw * 0.5f, labTop + sh * 0.24f, titleFont, new Color(110, 255, 190, 245));
    }

    private static void DrawWorldFighterTags(GameData data, int sh)
    {
        int tagFont = Math.Max(12, (int)(sh * 0.018f));

        if (data.Player1?.CurrentCharacter != null)
        {
            string p1Tag = "P1";
            int tw = UIRenderer.MeasureText(p1Tag, tagFont);
            float tx = data.Player1.Position.X - tw * 0.5f;
            float ty = data.Player1.Position.Y - data.Player1.CurrentCharacter.Height * 0.72f;
            UIRenderer.DrawTextShadowed(p1Tag, tx, ty, tagFont, Color.SKYBLUE, shadowOffset: 1f);
        }

        if (data.Player2?.CurrentCharacter != null)
        {
            string dummyTag = data.Player2.IsPassiveDummy ? "SPARRING DUMMY" : "DUMMY [AI HARD]";
            int tw = UIRenderer.MeasureText(dummyTag, tagFont);
            float tx = data.Player2.Position.X - tw * 0.5f;
            float ty = data.Player2.Position.Y - data.Player2.CurrentCharacter.Height * 0.72f;
            UIRenderer.DrawTextShadowed(dummyTag, tx, ty, tagFont, data.Player2.IsPassiveDummy ? Color.GOLD : Color.RED, shadowOffset: 1f);
        }
    }

    public void Draw(GameData data)
    {
        _roster?.Draw();
        _descPanel?.Draw();

        if (_queueMode != LobbyQueueMode.None)
            DrawQueueBanner();

        if (_showLeaderboards)
        {
            _leaderboardPanel?.Draw();
            _closeLeaderboardButton?.Draw();
        }

        DrawBottomLobbyBar(data);
    }

    private void DrawQueueBanner()
    {
        Rectangle banner = UIRenderer.GetPctRect(0.015f, 0.200f, 0.585f, 0.058f);
        Color bannerBorder = _queueMode == LobbyQueueMode.Queue1v1 ? Color.SKYBLUE : new Color(180, 115, 255, 255);
        Raylib.DrawRectangleRec(banner, new Color(18, 24, 38, 232));
        Raylib.DrawRectangleLinesEx(banner, 2f, bannerBorder);

        string modeText = _queueMode == LobbyQueueMode.Queue1v1
            ? "QUEUE ACTIVE: 1v1 PvP — KEEP SPARRING!"
            : "QUEUE ACTIVE: 2v2 MODE — KEEP SPARRING!";

        int sh = Raylib.GetScreenHeight();
        int fontSize = UIRenderer.FitFontSize(modeText, Math.Max(13, (int)(sh * 0.020f)), 11, banner.width * 0.54f);
        UIRenderer.DrawText(modeText, banner.x + 12f, banner.y + (banner.height - fontSize) * 0.5f, fontSize, bannerBorder);

        _launchQueueButton?.Draw();
        _cancelQueueButton?.Draw();
    }

    private void DrawBottomLobbyBar(GameData data)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        float groundY = PhysicsEngine.GroundY;

        if (data.Player2 != null && CameraManager.CurrentLobbyRoom == LobbyRoom.BottomTraining)
        {
            Rectangle hpRect = UIRenderer.GetPctRect(0.02f, 0.885f, 0.24f, 0.032f);
            float hpPct = data.Player2.Health / (float)Player.MAX_HEALTH;
            UIRenderer.DrawProgressBar(hpRect, hpPct, new UIBarStyle
            {
                BackgroundColor = new Color(35, 35, 42, 220),
                FillColor = data.Player2.IsPassiveDummy ? new Color(255, 195, 50, 255) : new Color(230, 65, 65, 255),
                BorderColor = Color.RAYWHITE,
                BorderThickness = 1.5f
            });

            int labelFont = Math.Max(12, (int)(sh * 0.018f));
            int dummyHp = data.Player2.Health;
            int p1Combo = data.Player1?.ComboCount ?? 0;
            if (dummyHp != _lastDummyHp || p1Combo != _lastP1Combo)
            {
                _lastDummyHp = dummyHp;
                _lastP1Combo = p1Combo;
                _cachedDummyInfo = p1Combo > 1
                    ? $"DUMMY HP: {dummyHp}/{Player.MAX_HEALTH}   |   {p1Combo} HIT COMBO!"
                    : $"DUMMY HP: {dummyHp}/{Player.MAX_HEALTH}";
            }
            UIRenderer.DrawTextShadowed(_cachedDummyInfo, hpRect.x, hpRect.y + hpRect.height + 5f, labelFont, Color.RAYWHITE, shadowOffset: 1f);
        }

        int hintFont = Math.Max(12, (int)(sh * 0.018f));
        string hint = CameraManager.CurrentLobbyRoom switch
        {
            LobbyRoom.LeftOffline    => "OFFLINE MODES: ATTACK OR CLICK   |   WALK RIGHT FOR ONLINE HUB",
            LobbyRoom.RightDev       => "DEV ALCOVE: JUMP PLATFORMS TO HIT TOOLS   |   WALK LEFT FOR ONLINE HUB",
            LobbyRoom.BottomTraining => "TRAINING LAB: CLICK TOP WALL TO CONFIGURE DUMMY   |   ATTACK TO TEST",
            _                        => "WALK LEFT: OFFLINE   |   WALK RIGHT: DEV   |   CENTER HATCH + CROUCH: TRAINING LAB"
        };
        int hw = UIRenderer.MeasureText(hint, hintFont);
        UIRenderer.DrawTextShadowed(hint, sw * 0.985f - hw, groundY + (sh - groundY - hintFont) * 0.5f, hintFont, Color.LIGHTGRAY, shadowOffset: 1f);

        if (_toastTimer > 0f && !string.IsNullOrEmpty(_toastText))
        {
            int toastFont = Math.Max(16, (int)(sh * 0.026f));
            int tw = UIRenderer.MeasureText(_toastText, toastFont);
            int boxW = tw + 36;
            int boxH = toastFont + 16;
            int bx = (sw - boxW) / 2;
            int by = (int)(sh * 0.27f);
            Raylib.DrawRectangle(bx, by, boxW, boxH, new Color(16, 20, 32, 235));
            Raylib.DrawRectangleLines(bx, by, boxW, boxH, Color.GOLD);
            UIRenderer.DrawText(_toastText, bx + 18, by + 8, toastFont, Color.GOLD);
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < _worldButtons.Count; i++)
        {
            _worldButtons[i].Dispose();
        }
        _worldButtons.Clear();

        _roster?.Dispose();
        _roster = null;

        _descPanel?.Dispose();
        _descPanel = null;

        _launchQueueButton?.Dispose();
        _launchQueueButton = null;

        _cancelQueueButton?.Dispose();
        _cancelQueueButton = null;

        _leaderboardPanel?.Dispose();
        _leaderboardPanel = null;

        _closeLeaderboardButton?.Dispose();
        _closeLeaderboardButton = null;
    }
}
