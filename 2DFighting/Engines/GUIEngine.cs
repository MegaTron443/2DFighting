using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

public static class GUIEngine
{
    private static readonly Dictionary<State, List<Button>> ButtonsByState = new();

    static GUIEngine()
    {
        InitializeButtons();
    }

    public static Rectangle ToPixelRect(float xPct, float yPct, float widthPct, float heightPct)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        return new Rectangle(sw * xPct, sh * yPct, sw * widthPct, sh * heightPct);
    }

    private static void InitializeButtons()
    {
        ButtonsByState[State.MainMenu] = new List<Button>
        {
            new Button(0.06f, 0.36f, 0.28f, 0.07f, "Select P1: Brawler", (ctx) =>
            {
                int sw = Raylib.GetScreenWidth();
                Vector2 spawn = new Vector2(sw * 0.1f, PhysicsEngine.GroundY * 0.9f);
                Player p1 = new Player("Player 1", isAI: false, playerIndex: 0, spawn, ctx.Data.AppConfig);
                p1.ChangeCharacter(ctx.Data.LuaEngine.CreateCharacter("brawler"));
                ctx.SetP1(p1);
            }),

            new Button(0.06f, 0.45f, 0.28f, 0.07f, "Select P2: Ninja", (ctx) =>
            {
                int sw = Raylib.GetScreenWidth();
                Vector2 spawn = new Vector2(sw * 0.9f - 60f, PhysicsEngine.GroundY * 0.9f);
                Player p2 = new Player("Player 2", isAI: false, playerIndex: 1, spawn, ctx.Data.AppConfig);
                p2.ChangeCharacter(ctx.Data.LuaEngine.CreateCharacter("brawler"));
                ctx.SetP2(p2);
            }),

            new Button(0.06f, 0.55f, 0.28f, 0.08f, "Start Match", (ctx) =>
            {
                if (ctx.Data.Player1?.CurrentCharacter != null && ctx.Data.Player2?.CurrentCharacter != null)
                {
                    GameState.SetState(State.Playing);
                }
            })
        };

        ButtonsByState[State.Paused] = new List<Button>
        {
            new Button(0.38f, 0.43f, 0.25f, 0.07f, "Resume", (ctx) =>
            {
                GameState.SetState(State.Playing);
            }),
            new Button(0.38f, 0.52f, 0.25f, 0.07f, "Quit to Menu", (ctx) =>
            {
                GameState.SetState(State.MainMenu);
            })
        };

        ButtonsByState[State.GameOver] = new List<Button>
        {
            new Button(0.38f, 0.46f, 0.25f, 0.08f, "Main Menu", (ctx) =>
            {
                GameState.SetState(State.MainMenu);
            })
        };
    }

    public static void Update(ref Player? p1, ref Player? p2, GameData data)
    {
        HandleButtons(ref p1, ref p2, data);

        switch (GameState.CurrentState)
        {
            case State.MainMenu:
                MainMenu(ref p1, ref p2, data);
                break;
            case State.Playing:
                break;
            case State.Paused:
                break;
            case State.GameOver:
                GameOver();
                break;
        }
    }

    private static void HandleButtons(ref Player? p1, ref Player? p2, GameData data)
    {
        if (ButtonsByState.TryGetValue(GameState.CurrentState, out var buttons))
        {
            Player? currentP1 = p1;
            Player? currentP2 = p2;
            ButtonContext context = new ButtonContext(
                data,
                (newP1) => currentP1 = newP1,
                (newP2) => currentP2 = newP2
            );

            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].Update(context);
            }

            p1 = currentP1;
            p2 = currentP2;
        }
    }

    private static void MainMenu(ref Player? p1, ref Player? p2, GameData data)
    {
        int sw = Raylib.GetScreenWidth();
        Vector2 p1Spawn = new Vector2(sw * 0.1f, PhysicsEngine.GroundY * 0.9f);
        Vector2 p2Spawn = new Vector2(sw * 0.9f - 60f, PhysicsEngine.GroundY * 0.9f);

        if (Raylib.IsKeyPressed(KeyboardKey.KEY_ONE))
        {
            p1 = new Player("Player 1", isAI: false, playerIndex: 0, p1Spawn, data.AppConfig);
            Character brawler = data.LuaEngine.CreateCharacter("brawler");
            p1.ChangeCharacter(brawler);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.KEY_TWO))
        {
            p2 = new Player("Player 2", isAI: false, playerIndex: 1, p2Spawn, data.AppConfig);
            Character brawler2 = data.LuaEngine.CreateCharacter("brawler");
            p2.ChangeCharacter(brawler2);
        }

        if (p1?.CurrentCharacter != null && p2?.CurrentCharacter != null)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.KEY_ENTER))
            {
                GameState.SetState(State.Playing);
            }
        }
    }

    public static void GameOver()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.KEY_ENTER) || Raylib.IsKeyPressed(KeyboardKey.KEY_R))
        {
            GameState.SetState(State.MainMenu);
        }
    }

    public static void Draw(GameData data)
    {
        switch (GameState.CurrentState)
        {
            case State.MainMenu:
                DrawMainMenu(data);
                break;
            case State.Playing:
                DrawPlaying(data);
                break;
            case State.Paused:
                DrawPause(data);
                break;
            case State.GameOver:
                DrawGameOver(data);
                break;
        }

        if (ButtonsByState.TryGetValue(GameState.CurrentState, out var buttons))
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].Draw();
            }
        }
    }

    private static void DrawMainMenu(GameData data)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        int fontSize = (int)(sh * 0.033f);

        string p1Status = data.Player1?.CurrentCharacter != null ? data.Player1.CurrentCharacter.Name : "None (Press 1)";
        string p2Status = data.Player2?.CurrentCharacter != null ? data.Player2.CurrentCharacter.Name : "None (Press 2)";

        Raylib.DrawText($"Player 1: {p1Status}", (int)(sw * 0.06f), (int)(sh * 0.16f), fontSize, Color.BLACK);
        Raylib.DrawText($"Player 2: {p2Status}", (int)(sw * 0.06f), (int)(sh * 0.22f), fontSize, Color.BLACK);

        if (data.Player1?.CurrentCharacter != null && data.Player2?.CurrentCharacter != null)
        {
            Raylib.DrawText("Ready to Fight! (Click Start Match or Press Enter)", (int)(sw * 0.06f), (int)(sh * 0.30f), fontSize, Color.DARKGREEN);
        }
        else
        {
            Raylib.DrawText("Select both fighters to continue", (int)(sw * 0.06f), (int)(sh * 0.30f), fontSize, Color.GRAY);
        }
    }

    private static void DrawPlaying(GameData data)
    {
        DrawHealthBar(data);
    }

    private static void DrawHealthBar(GameData data)
    {
        if (data.Player1 == null || data.Player2 == null) return;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        float p1HealthPercent = Math.Clamp(data.Player1.Health / 100f, 0f, 1f);
        float p2HealthPercent = Math.Clamp(data.Player2.Health / 100f, 0f, 1f);

        int barWidth = (int)(sw * 0.30f);
        int barHeight = (int)(sh * 0.05f);

        int p1BarX = (int)(sw * 0.10f);
        int p2BarX = (int)(sw * 0.90f) - barWidth;
        int barY = (int)(sh * 0.05f);

        int p1Fill = (int)(barWidth * p1HealthPercent);
        int p2Fill = (int)(barWidth * p2HealthPercent);

        Raylib.DrawRectangle(p1BarX, barY, barWidth, barHeight, Color.GRAY);
        Raylib.DrawRectangle(p2BarX, barY, barWidth, barHeight, Color.GRAY);

        Raylib.DrawRectangle(p1BarX, barY, p1Fill, barHeight, Color.RED);
        Raylib.DrawRectangle(p2BarX + (barWidth - p2Fill), barY, p2Fill, barHeight, Color.BLUE);
    }

    private static void DrawPause(GameData data)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        int titleSize = (int)(sh * 0.05f);
        int subSize = (int)(sh * 0.03f);

        string title = "Game Paused";
        int titleW = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, (sw - titleW) / 2, (int)(sh * 0.25f), titleSize, Color.BLACK);

        string hint = $"Press {data.AppConfig.Controls.Pause} to resume";
        int hintW = Raylib.MeasureText(hint, subSize);
        Raylib.DrawText(hint, (sw - hintW) / 2, (int)(sh * 0.34f), subSize, Color.DARKGRAY);
    }

    private static void DrawGameOver(GameData data)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        int titleSize = (int)(sh * 0.06f);
        int subSize = (int)(sh * 0.03f);

        string title = "GAME OVER";
        int titleW = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, (sw - titleW) / 2, (int)(sh * 0.28f), titleSize, Color.RED);

        string hint = "Press Enter or Click Below to Return to Menu";
        int hintW = Raylib.MeasureText(hint, subSize);
        Raylib.DrawText(hint, (sw - hintW) / 2, (int)(sh * 0.38f), subSize, Color.DARKGRAY);
    }
}

public class Button
{
    public float XPct { get; set; }
    public float YPct { get; set; }
    public float WidthPct { get; set; }
    public float HeightPct { get; set; }
    
    public string Text { get; set; }
    public float FontSizePct { get; set; } = 0.035f;
    public Action<ButtonContext>? OnClick { get; set; }

    public Color NormalColor { get; set; } = Color.LIGHTGRAY;
    public Color HoverColor { get; set; } = Color.GRAY;
    public Color TextColor { get; set; } = Color.BLACK;

    public bool IsHovered { get; private set; }

    public Button(float xPct, float yPct, float widthPct, float heightPct, string text, Action<ButtonContext>? onClick = null)
    {
        XPct = xPct;
        YPct = yPct;
        WidthPct = widthPct;
        HeightPct = heightPct;
        Text = text;
        OnClick = onClick;
    }

    public Rectangle GetPixelBounds()
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        return new Rectangle(sw * XPct, sh * YPct, sw * WidthPct, sh * HeightPct);
    }

    public void Update(ButtonContext context)
    {
        Rectangle bounds = GetPixelBounds();
        Vector2 mousePos = Raylib.GetMousePosition();
        IsHovered = Raylib.CheckCollisionPointRec(mousePos, bounds);

        if (IsHovered && Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_LEFT))
        {
            OnClick?.Invoke(context);
        }
    }

    public void Draw()
    {
        Rectangle bounds = GetPixelBounds();
        int fontSize = (int)(Raylib.GetScreenHeight() * FontSizePct);

        Color currentBg = IsHovered ? HoverColor : NormalColor;
        Raylib.DrawRectangleRec(bounds, currentBg);
        Raylib.DrawRectangleLinesEx(bounds, 2, Color.DARKGRAY);

        int textWidth = Raylib.MeasureText(Text, fontSize);
        int textX = (int)(bounds.x + (bounds.width - textWidth) / 2f);
        int textY = (int)(bounds.y + (bounds.height - fontSize) / 2f);

        Raylib.DrawText(Text, textX, textY, fontSize, TextColor);
    }
}

public class ButtonContext
{
    public GameData Data { get; }
    public Action<Player?> SetP1 { get; }
    public Action<Player?> SetP2 { get; }

    public ButtonContext(GameData data, Action<Player?> setP1, Action<Player?> setP2)
    {
        Data = data;
        SetP1 = setP1;
        SetP2 = setP2;
    }
}