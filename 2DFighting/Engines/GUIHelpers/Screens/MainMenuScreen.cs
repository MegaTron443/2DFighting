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

    private WorldButton? _modFilterBtn;
    private WorldButton? _queue1v1Btn;
    private WorldButton? _queue2v2Btn;
    private WorldButton? _debugHitboxBtn;
    private WorldButton? _dummyModeBtn;

    private LobbyQueueMode _queueMode = LobbyQueueMode.None;
    private int _knownCatalogVersion = -1;
    private string _knownDescCharKey = "";
    private string _toastText = "";
    private float _toastTimer = 0f;

    public void Initialize()
    {
        Dispose();
        _worldButtons.Clear();
        _queueMode = LobbyQueueMode.None;
        _knownCatalogVersion = -1;
        _knownDescCharKey = "";
        _toastText = "";
        _toastTimer = 0f;

        BuildMainArenaButtons();
        BuildDeveloperAlcoveButtons();
        BuildHudWidgets();
    }

    private void BuildMainArenaButtons()
    {
        WorldButton settingsBtn = CreateWorldButton(
            "settings", "SETTINGS", "OPTIONS & KEYS",
            new Vector2(0.16f, 0.37f), new Vector2(0.17f, 0.09f),
            new Vector3(0.45f, 0.78f, 1.0f), isToggle: false,
            _ => GameState.SetState(State.Settings));

        _modFilterBtn = CreateWorldButton(
            "mod_filter", "MOD FILTER", GetFilterLabel(ModManager.ActiveModFilter),
            new Vector2(0.50f, 0.37f), new Vector2(0.21f, 0.09f),
            new Vector3(1.0f, 0.75f, 0.25f), isToggle: true,
            OnCycleModFilter);

        WorldButton exitBtn = CreateWorldButton(
            "exit", "EXIT", "QUIT GAME",
            new Vector2(0.84f, 0.37f), new Vector2(0.16f, 0.09f),
            new Vector3(0.95f, 0.32f, 0.36f), isToggle: false,
            _ => GameState.SetState(State.Exit));

        _queue1v1Btn = CreateWorldButton(
            "play_1v1", "PLAY 1v1", "QUEUE 1v1",
            new Vector2(0.21f, 0.505f), new Vector2(0.19f, 0.095f),
            new Vector3(0.28f, 0.78f, 1.0f), isToggle: true,
            _ => ToggleQueueMode(LobbyQueueMode.Queue1v1));

        _queue2v2Btn = CreateWorldButton(
            "play_2v2", "PLAY 2v2", "QUEUE 2v2",
            new Vector2(0.50f, 0.505f), new Vector2(0.19f, 0.095f),
            new Vector3(0.68f, 0.45f, 1.0f), isToggle: true,
            _ => ToggleQueueMode(LobbyQueueMode.Queue2v2));

        WorldButton vsAiBtn = CreateWorldButton(
            "vs_ai", "VS AI", "QUICK START",
            new Vector2(0.79f, 0.505f), new Vector2(0.19f, 0.095f),
            new Vector3(0.25f, 0.90f, 0.48f), isToggle: false,
            OnLaunchVsAi);

        _worldButtons.AddRange(new[]
        {
            settingsBtn, _modFilterBtn, exitBtn,
            _queue1v1Btn, _queue2v2Btn, vsAiBtn
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
            ctx =>
            {
                Player? dummy = ctx.Data.Player2;
                if (dummy == null) return;
                dummy.IsPassiveDummy = !dummy.IsPassiveDummy;
                dummy.SetAIDifficulty(AIDifficulty.Hard);
                string modeName = dummy.IsPassiveDummy ? "PASSIVE" : "FIGHT (HARD)";
                ShowToast($"SPARRING DUMMY: {modeName}");
            });

        WorldButton reloadModsBtn = CreateWorldButton(
            "dev_reload_mods", "RELOAD MODS", "HOT REFRESH",
            new Vector2(1.36f, 0.505f), new Vector2(0.21f, 0.095f),
            new Vector3(1.0f, 0.72f, 0.12f), isToggle: false,
            OnReloadMods);

        WorldButton resetWeightsBtn = CreateWorldButton(
            "dev_reset_weights", "RESET AI WEIGHTS", "CLEAR RL MODEL",
            new Vector2(1.64f, 0.505f), new Vector2(0.21f, 0.095f),
            new Vector3(1.0f, 0.25f, 0.30f), isToggle: false,
            _ =>
            {
                MacroTacticalModel.SharedInstance.ResetWeights();
                ShowToast("AI WEIGHTS RESET TO PRETRAINED");
            });

        _worldButtons.AddRange(new[]
        {
            _debugHitboxBtn, aiTrainBtn, _dummyModeBtn,
            reloadModsBtn, resetWeightsBtn
        });
    }

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
    }

    private static WorldButton CreateWorldButton(
        string id,
        string label,
        string subLabel,
        Vector2 worldPosPct,
        Vector2 sizePct,
        Vector3 accentColor,
        bool isToggle,
        Action<ButtonContext> onTrigger)
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
        _knownDescCharKey = "";
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
            ctx.Data.SoundHandler?.ReloadCharacterBanks(p1, ctx.Data.Player2);
            VFXManager.SpawnPreset(VFXPreset.Shockwave, p1.Position, p1.FacingRight);
        }

        _knownDescCharKey = "";
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

        if (_debugHitboxBtn != null)
        {
            _debugHitboxBtn.IsSelected = Character.ShowDebugBoxes;
            _debugHitboxBtn.SubLabel = Character.ShowDebugBoxes ? "ON" : "OFF";
        }

        if (_dummyModeBtn != null && data.Player2 != null)
        {
            bool fighting = !data.Player2.IsPassiveDummy;
            _dummyModeBtn.IsSelected = fighting;
            _dummyModeBtn.SubLabel = fighting ? "FIGHT (HARD)" : "PASSIVE";
        }

        Character? activeChar = data.Player1?.CurrentCharacter;
        if (_descPanel != null && activeChar != null)
        {
            string descKey = $"{activeChar.Name}|{activeChar.ScriptHash}";
            if (!string.Equals(_knownDescCharKey, descKey, StringComparison.Ordinal))
            {
                _knownDescCharKey = descKey;
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

        bool hudCapturedPointer = (_roster?.CapturesPointer ?? false) ||
                                  (_descPanel?.CapturesPointer ?? false) ||
                                  pointerInBanner;

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

        DrawDeveloperAlcoveDecor(sw, sh, groundY);

        for (int i = 0; i < _worldButtons.Count; i++)
        {
            _worldButtons[i].Draw();
        }

        DrawWorldFighterTags(data, sh);
    }

    private static void DrawDeveloperAlcoveDecor(int sw, int sh, float groundY)
    {
        float portalX = sw * 1.00f;
        float dividerTop = sh * 0.28f;

        // Passable boundary marker between Main Lobby and Hidden Developer Alcove
        Raylib.DrawRectangle((int)(portalX - 3f), (int)dividerTop, 6, (int)(groundY - dividerTop), new Color(0, 235, 220, 75));
        int signFont = Math.Max(12, (int)(sh * 0.018f));
        Raylib.DrawText("> DEV", (int)(portalX - 44), (int)(dividerTop - signFont - 4), signFont, new Color(110, 220, 255, 175));

        // Alcove floor & cyber grid (only visible when player crosses portal into alcove!)
        Rectangle alcoveRect = new Rectangle(sw * 1.01f, 0f, sw * 0.99f, groundY);
        Raylib.DrawRectangleRec(alcoveRect, new Color(10, 12, 22, 160));

        int gridStep = Math.Max(40, (int)(sw * 0.06f));
        for (int gx = (int)(sw * 1.04f); gx < sw * 2; gx += gridStep)
        {
            Raylib.DrawLine(gx, (int)(sh * 0.12f), gx, (int)groundY, new Color(40, 210, 220, 28));
        }

        int devTitleSize = Math.Max(18, (int)(sh * 0.036f));
        string devHeader = "// DEVELOPER SANDBOX ALCOVE //";
        int hw = Raylib.MeasureText(devHeader, devTitleSize);
        Raylib.DrawText(devHeader, (int)(sw * 1.50f - hw * 0.5f), (int)(sh * 0.16f), devTitleSize, new Color(90, 250, 235, 255));

        int hintSize = Math.Max(13, (int)(sh * 0.020f));
        string devHint = "PUNCH OR CLICK TOOLS   |   <- WALK LEFT TO RETURN TO LOBBY";
        int dhw = Raylib.MeasureText(devHint, hintSize);
        Raylib.DrawText(devHint, (int)(sw * 1.50f - dhw * 0.5f), (int)(sh * 0.22f), hintSize, Color.LIGHTGRAY);
    }

    private static void DrawWorldFighterTags(GameData data, int sh)
    {
        int tagFont = Math.Max(12, (int)(sh * 0.018f));

        if (data.Player1?.CurrentCharacter != null)
        {
            string p1Tag = "P1";
            int tw = Raylib.MeasureText(p1Tag, tagFont);
            int tx = (int)(data.Player1.Position.X - tw * 0.5f);
            int ty = (int)(data.Player1.Position.Y - data.Player1.CurrentCharacter.Height * 0.72f);
            Raylib.DrawText(p1Tag, tx + 1, ty + 1, tagFont, Color.BLACK);
            Raylib.DrawText(p1Tag, tx, ty, tagFont, Color.SKYBLUE);
        }

        if (data.Player2?.CurrentCharacter != null)
        {
            string dummyTag = data.Player2.IsPassiveDummy ? "SPARRING DUMMY" : "DUMMY [AI HARD]";
            int tw = Raylib.MeasureText(dummyTag, tagFont);
            int tx = (int)(data.Player2.Position.X - tw * 0.5f);
            int ty = (int)(data.Player2.Position.Y - data.Player2.CurrentCharacter.Height * 0.72f);
            Raylib.DrawText(dummyTag, tx + 1, ty + 1, tagFont, Color.BLACK);
            Raylib.DrawText(dummyTag, tx, ty, tagFont, data.Player2.IsPassiveDummy ? Color.GOLD : Color.RED);
        }
    }

    public void Draw(GameData data)
    {
        _roster?.Draw();
        _descPanel?.Draw();

        if (_queueMode != LobbyQueueMode.None)
            DrawQueueBanner();

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
        Raylib.DrawText(modeText, (int)(banner.x + 12f), (int)(banner.y + (banner.height - fontSize) * 0.5f), fontSize, bannerBorder);

        _launchQueueButton?.Draw();
        _cancelQueueButton?.Draw();
    }

    private void DrawBottomLobbyBar(GameData data)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        float groundY = PhysicsEngine.GroundY;

        if (data.Player2 != null)
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
            string dummyInfo = $"DUMMY HP: {data.Player2.Health}/{Player.MAX_HEALTH}";
            if (data.Player1 != null && data.Player1.ComboCount > 1)
                dummyInfo += $"   |   {data.Player1.ComboCount} HIT COMBO!";
            Raylib.DrawText(dummyInfo, (int)hpRect.x, (int)(hpRect.y + hpRect.height + 5f), labelFont, Color.RAYWHITE);
        }

        int hintFont = Math.Max(12, (int)(sh * 0.018f));
        string hint = CameraManager.IsInLobbyAlcove
            ? "DEV ALCOVE: JUMP PLATFORMS TO HIT TOOLS   |   TAP CROUCH TO DROP   |   WALK LEFT TO EXIT"
            : "JUMP PLATFORMS TO REACH HIGH BUTTONS   |   TAP CROUCH TO DROP   |   WALK RIGHT FOR DEV ROOM";
        int hw = Raylib.MeasureText(hint, hintFont);
        Raylib.DrawText(hint, (int)(sw * 0.985f - hw), (int)(groundY + (sh - groundY - hintFont) * 0.5f), hintFont, Color.LIGHTGRAY);

        if (_toastTimer > 0f && !string.IsNullOrEmpty(_toastText))
        {
            int toastFont = Math.Max(16, (int)(sh * 0.026f));
            int tw = Raylib.MeasureText(_toastText, toastFont);
            int boxW = tw + 36;
            int boxH = toastFont + 16;
            int bx = (sw - boxW) / 2;
            int by = (int)(sh * 0.27f);
            Raylib.DrawRectangle(bx, by, boxW, boxH, new Color(16, 20, 32, 235));
            Raylib.DrawRectangleLines(bx, by, boxW, boxH, Color.GOLD);
            Raylib.DrawText(_toastText, bx + 18, by + 8, toastFont, Color.GOLD);
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
    }
}
