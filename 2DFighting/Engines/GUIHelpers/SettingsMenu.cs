using System;
using System.Collections.Generic;
using Raylib_cs;

public static class SettingsMenu
{
    private static readonly List<Button> _buttons = new();
    private static Action<KeyboardKey>? _pendingRebindSetter = null;
    private static string _pendingRebindLabel = "";

    private static readonly (int w, int h)[] Resolutions =
    {
        (800, 600),
        (1024, 768),
        (1280, 720),
        (1600, 900),
        (1920, 1080)
    };

    private static readonly int[] FpsOptions = { 60, 120, 144, 240 };

    public static bool IsWaitingForKey => _pendingRebindSetter != null;

    public static void Initialize()
    {
        _buttons.Clear();
        _pendingRebindSetter = null;

        _buttons.Add(Button.Create("-").WithSize(0.05f, 0.055f).At(0.56f, 0.17f).Action(ctx =>
        {
            var cfg = ctx.Data.AppConfig;
            cfg.Sound.Volume = Math.Clamp(cfg.Sound.Volume - 10, 0, 100);
            if (SoundHandler.Current != null)
                SoundHandler.Current.Volume = cfg.Sound.Volume / 100f;
            AppConfig.Save("config.json", cfg);
        }));

        _buttons.Add(Button.Create("+").WithSize(0.05f, 0.055f).At(0.76f, 0.17f).Action(ctx =>
        {
            var cfg = ctx.Data.AppConfig;
            cfg.Sound.Volume = Math.Clamp(cfg.Sound.Volume + 10, 0, 100);
            if (SoundHandler.Current != null)
                SoundHandler.Current.Volume = cfg.Sound.Volume / 100f;
            AppConfig.Save("config.json", cfg);
        }));

        _buttons.Add(Button.Create("<").WithSize(0.05f, 0.055f).At(0.56f, 0.24f).Action(ctx =>
        {
            var cfg = ctx.Data.AppConfig;
            cfg.AIDifficulty = cfg.AIDifficulty switch
            {
                AIDifficulty.Hard   => AIDifficulty.Medium,
                AIDifficulty.Medium => AIDifficulty.Easy,
                _                   => AIDifficulty.Hard
            };
            AppConfig.Save("config.json", cfg);
        }));

        _buttons.Add(Button.Create(">").WithSize(0.05f, 0.055f).At(0.76f, 0.24f).Action(ctx =>
        {
            var cfg = ctx.Data.AppConfig;
            cfg.AIDifficulty = cfg.AIDifficulty switch
            {
                AIDifficulty.Easy   => AIDifficulty.Medium,
                AIDifficulty.Medium => AIDifficulty.Hard,
                _                   => AIDifficulty.Easy
            };
            AppConfig.Save("config.json", cfg);
        }));

        _buttons.Add(Button.Create("<").WithSize(0.05f, 0.055f).At(0.56f, 0.31f).Action(ctx =>
        {
            CycleFps(ctx.Data.AppConfig, -1);
        }));

        _buttons.Add(Button.Create(">").WithSize(0.05f, 0.055f).At(0.76f, 0.31f).Action(ctx =>
        {
            CycleFps(ctx.Data.AppConfig, 1);
        }));

        _buttons.Add(Button.Create("<").WithSize(0.05f, 0.055f).At(0.56f, 0.38f).Action(ctx =>
        {
            CycleResolution(ctx.Data.AppConfig, -1);
        }));

        _buttons.Add(Button.Create(">").WithSize(0.05f, 0.055f).At(0.76f, 0.38f).Action(ctx =>
        {
            CycleResolution(ctx.Data.AppConfig, 1);
        }));

        _buttons.Add(Button.Create("Rebind Pause").WithSize(0.25f, 0.05f).At(0.66f, 0.45f).Action(ctx =>
        {
            StartRebind("Pause Key", key =>
            {
                ctx.Data.AppConfig.Controls.Pause = key;
                AppConfig.Save("config.json", ctx.Data.AppConfig);
            });
        }));

        BuildPlayerControlButtons(isP1: true, xCenterPct: 0.28f, startYPct: 0.56f);

        BuildPlayerControlButtons(isP1: false, xCenterPct: 0.72f, startYPct: 0.56f);

        _buttons.Add(
            Button.Create("BACK")
                .WithSize(0.24f, 0.065f)
                .At(0.50f, 0.92f)
                .Action(_ =>
                {
                    _pendingRebindSetter = null;
                    GameState.SetState(GameState.PreviousState);
                })
        );
    }

    private static void BuildPlayerControlButtons(bool isP1, float xCenterPct, float startYPct)
    {
        string prefix = isP1 ? "P1" : "P2";
        (string label, Action<AppConfig, KeyboardKey> setter)[] bindings =
        {
            ("Left",      (cfg, k) => (isP1 ? cfg.Controls.P1 : cfg.Controls.P2).Left = k),
            ("Right",     (cfg, k) => (isP1 ? cfg.Controls.P1 : cfg.Controls.P2).Right = k),
            ("Jump",      (cfg, k) => (isP1 ? cfg.Controls.P1 : cfg.Controls.P2).Jump = k),
            ("Crouch",    (cfg, k) => (isP1 ? cfg.Controls.P1 : cfg.Controls.P2).Crouch = k),
            ("KeyTop",    (cfg, k) => (isP1 ? cfg.Controls.P1 : cfg.Controls.P2).KeyTop = k),
            ("KeyBottom", (cfg, k) => (isP1 ? cfg.Controls.P1 : cfg.Controls.P2).KeyBottom = k),
            ("KeyLeft",   (cfg, k) => (isP1 ? cfg.Controls.P1 : cfg.Controls.P2).KeyLeft = k),
            ("KeyRight",  (cfg, k) => (isP1 ? cfg.Controls.P1 : cfg.Controls.P2).KeyRight = k),
        };

        for (int i = 0; i < bindings.Length; i++)
        {
            int col = i % 2;
            int row = i / 2;
            float btnX = (xCenterPct - 0.105f) + (col * 0.21f);
            float btnY = startYPct + (row * 0.068f);

            var (label, setter) = bindings[i];
            _buttons.Add(
                Button.Create($"{prefix} {label}")
                    .WithSize(0.195f, 0.055f)
                    .At(btnX, btnY)
                    .Action(ctx =>
                    {
                        StartRebind($"{prefix} {label}", key =>
                        {
                            setter(ctx.Data.AppConfig, key);
                            AppConfig.Save("config.json", ctx.Data.AppConfig);
                        });
                    })
            );
        }
    }

    private static void StartRebind(string label, Action<KeyboardKey> setter)
    {
        _pendingRebindLabel = label;
        _pendingRebindSetter = setter;
    }

    private static void CycleFps(AppConfig cfg, int dir)
    {
        int idx = Array.IndexOf(FpsOptions, cfg.Display.TargetFps);
        if (idx < 0) idx = 0;
        idx = (idx + dir + FpsOptions.Length) % FpsOptions.Length;

        cfg.Display.TargetFps = FpsOptions[idx];
        Raylib.SetTargetFPS(cfg.Display.TargetFps);
        AppConfig.Save("config.json", cfg);
    }

    private static void CycleResolution(AppConfig cfg, int dir)
    {
        int idx = 0;
        for (int i = 0; i < Resolutions.Length; i++)
        {
            if (Resolutions[i].w == cfg.Display.ScreenWidth && Resolutions[i].h == cfg.Display.ScreenHeight)
            {
                idx = i;
                break;
            }
        }

        idx = (idx + dir + Resolutions.Length) % Resolutions.Length;
        cfg.Display.ScreenWidth = Resolutions[idx].w;
        cfg.Display.ScreenHeight = Resolutions[idx].h;

        Raylib.SetWindowSize(cfg.Display.ScreenWidth, cfg.Display.ScreenHeight);
        AppConfig.Save("config.json", cfg);
    }

    public static void Update(ButtonContext context)
    {
        if (_pendingRebindSetter != null)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.KEY_ESCAPE))
            {
                _pendingRebindSetter = null;
                _pendingRebindLabel = "";
                return;
            }

            int keyInt = Raylib.GetKeyPressed();
            if (keyInt > 0)
            {
                _pendingRebindSetter((KeyboardKey)keyInt);
                _pendingRebindSetter = null;
                _pendingRebindLabel = "";
            }
            return;
        }

        SyncKeybindButtonLabels(context.Data.AppConfig);

        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Update(context);
        }
    }

    private static void SyncKeybindButtonLabels(AppConfig cfg)
    {
        if (_buttons.Count < 25) return;

        _buttons[8].Text = $"Pause: {FormatKey(cfg.Controls.Pause)}";

        var p1 = cfg.Controls.P1;
        _buttons[9].Text  = $"Left: {FormatKey(p1.Left)}";
        _buttons[10].Text = $"Right: {FormatKey(p1.Right)}";
        _buttons[11].Text = $"Jump: {FormatKey(p1.Jump)}";
        _buttons[12].Text = $"Crouch: {FormatKey(p1.Crouch)}";
        _buttons[13].Text = $"Top: {FormatKey(p1.KeyTop)}";
        _buttons[14].Text = $"Bot: {FormatKey(p1.KeyBottom)}";
        _buttons[15].Text = $"L-Atk: {FormatKey(p1.KeyLeft)}";
        _buttons[16].Text = $"R-Atk: {FormatKey(p1.KeyRight)}";

        var p2 = cfg.Controls.P2;
        _buttons[17].Text = $"Left: {FormatKey(p2.Left)}";
        _buttons[18].Text = $"Right: {FormatKey(p2.Right)}";
        _buttons[19].Text = $"Jump: {FormatKey(p2.Jump)}";
        _buttons[20].Text = $"Crouch: {FormatKey(p2.Crouch)}";
        _buttons[21].Text = $"Top: {FormatKey(p2.KeyTop)}";
        _buttons[22].Text = $"Bot: {FormatKey(p2.KeyBottom)}";
        _buttons[23].Text = $"L-Atk: {FormatKey(p2.KeyLeft)}";
        _buttons[24].Text = $"R-Atk: {FormatKey(p2.KeyRight)}";
    }

    private static string FormatKey(KeyboardKey key)
    {
        string s = key.ToString();
        return s.StartsWith("KEY_", StringComparison.OrdinalIgnoreCase) ? s[4..] : s;
    }

    public static void Draw(GameData data)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        var cfg = data.AppConfig;

        int titleSize = Math.Max(18, (int)(sh * 0.045f));
        int labelSize = Math.Max(14, (int)(sh * 0.026f));
        int sectionSize = Math.Max(14, (int)(sh * 0.028f));

        string title = "SETTINGS";
        int titleW = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, (sw - titleW) / 2, (int)(sh * 0.05f), titleSize, Color.BLACK);

        DrawRow(sw, sh, 0.17f, labelSize, "Sound Volume:", $"{cfg.Sound.Volume}%");
        DrawRow(sw, sh, 0.24f, labelSize, "AI Difficulty:", cfg.AIDifficulty.ToString());
        DrawRow(sw, sh, 0.31f, labelSize, "Target FPS:", $"{cfg.Display.TargetFps} FPS");
        DrawRow(sw, sh, 0.38f, labelSize, "Resolution:", $"{cfg.Display.ScreenWidth}x{cfg.Display.ScreenHeight}");
        DrawRow(sw, sh, 0.45f, labelSize, "Global Pause:", "");

        Raylib.DrawText("PLAYER 1 CONTROLS (Click to Rebind)", (int)(sw * 0.08f), (int)(sh * 0.50f), sectionSize, Color.DARKBLUE);
        Raylib.DrawText("PLAYER 2 CONTROLS (Click to Rebind)", (int)(sw * 0.52f), (int)(sh * 0.50f), sectionSize, Color.MAROON);

        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Draw();
        }

        if (_pendingRebindSetter != null)
        {
            Raylib.DrawRectangle(0, 0, sw, sh, new Color(0, 0, 0, 185));
            Rectangle modal = UIRenderer.GetPctRect(0.20f, 0.38f, 0.60f, 0.22f);
            Raylib.DrawRectangleRec(modal, new Color(35, 35, 35, 250));
            Raylib.DrawRectangleLinesEx(modal, 3f, Color.GOLD);

            string prompt = $"PRESS ANY KEY FOR [{_pendingRebindLabel.ToUpper()}]";
            string cancelHint = "(Press ESC to cancel)";

            int pw = Raylib.MeasureText(prompt, sectionSize);
            int cw = Raylib.MeasureText(cancelHint, labelSize);

            Raylib.DrawText(prompt, (sw - pw) / 2, (int)(sh * 0.45f), sectionSize, Color.GOLD);
            Raylib.DrawText(cancelHint, (sw - cw) / 2, (int)(sh * 0.52f), labelSize, Color.LIGHTGRAY);
        }
    }

    private static void DrawRow(int sw, int sh, float yPct, int fontSize, string label, string valueText)
    {
        int y = (int)(sh * yPct) - (fontSize / 2);
        Raylib.DrawText(label, (int)(sw * 0.22f), y, fontSize, Color.DARKGRAY);

        if (!string.IsNullOrEmpty(valueText))
        {
            int valW = Raylib.MeasureText(valueText, fontSize);
            int centerValX = (int)(sw * 0.66f) - (valW / 2);
            Raylib.DrawText(valueText, centerValX, y, fontSize, Color.BLACK);
        }
    }
}