using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

public static class GUIEngine
{
    private static readonly Dictionary<State, List<Button>> ButtonsByState = new();
    private static readonly Dictionary<string, Texture2D> RosterThumbnails = new();
    private static readonly string[] AvailableFighters = new[] { "brawler", "lancer", "titan" };
    private static int sw => Raylib.GetScreenWidth();
    private static int sh => Raylib.GetScreenHeight();

    static GUIEngine()
    {
        LoadRosterThumbnails();
        InitializeButtons();
    }
    
    private static void LoadRosterThumbnails()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;

        foreach (var fighter in AvailableFighters)
        {
            string path = Path.Combine(baseDir, "Characters", "assets", fighter, "Roaster.png");
            
            if (!File.Exists(path))
            {
                path = Path.Combine(baseDir, "Assets", "Characters", fighter, "Roaster.png");
            }

            if (File.Exists(path))
            {
                RosterThumbnails[fighter] = Raylib.LoadTexture(path);
            }
        }
    }

    private static Texture2D? GetThumbnail(string characterKey)
    {
        return RosterThumbnails.TryGetValue(characterKey, out var tex) ? tex : null;
    }

    private static void InitializeButtons()
    {
        ButtonsByState[State.MainMenu] = new List<Button>
        {
            Button.Create("Player vs AI")
                .WithSize(0.28f, 0.07f)
                .At(0.5f, 0.40f)
                .Action(ctx => SetupGame(ctx, isAI: true)),

            Button.Create("Player vs Player")
                .WithSize(0.28f, 0.07f)
                .At(0.5f, 0.48f)
                .Action(ctx => SetupGame(ctx, isAI: false)),

            Button.Create("Exit")
                .WithSize(0.28f, 0.07f)
                .At(0.5f, 0.56f)
                .Action(_ => GameState.SetState(State.Exit))
        };

        void SetupGame(ButtonContext ctx, bool isAI)
        {
            Vector2 spawn_p1 = new Vector2(sw * 0.1f, PhysicsEngine.GroundY * 0.9f);
            Vector2 spawn_p2 = new Vector2(sw * 0.9f, PhysicsEngine.GroundY * 0.9f);

            Player p1 = Player.Create("Player 1", 0, spawn_p1, ctx.Data.AppConfig, isAI: false);
            Player p2 = Player.Create("Player 2", 1, spawn_p2, ctx.Data.AppConfig, isAI: isAI);

            p1.SetOpponent(p2);
            p2.SetOpponent(p1);

            ctx.SetP1(p1);
            ctx.SetP2(p2);
            GameState.SetState(State.ChoiceMenu);
        }

        var choiceButtons = new List<Button>();

        var p1Grid = new UIGrid(xPct: 0.05f, yPct: 0.20f, widthPct: 0.4f, heightPct: 0.6f, columns: 6, rows: 5)
            .WithAlignment(TextAlignment.Bottom);
        foreach (var fighter in AvailableFighters)
        {
            string id = fighter;
            p1Grid.Add(char.ToUpper(id[0]) + id[1..], ctx =>
            {
                if (ctx.Data.Player1 != null)
                {
                    Character character = ctx.Data.LuaEngine.CreateCharacter(id);
                    ctx.Data.Player1.ChangeCharacter(character);
                }
            }, GetThumbnail(id));
        }

        var p2Grid = new UIGrid(xPct: 0.55f, yPct: 0.20f, widthPct: 0.4f, heightPct: 0.6f, columns: 6, rows: 5)
            .WithAlignment(TextAlignment.Bottom);
        foreach (var fighter in AvailableFighters)
        {
            string id = fighter;
            p2Grid.Add(char.ToUpper(id[0]) + id[1..], ctx =>
            {
                if (ctx.Data.Player2 != null)
                {
                    Character character = ctx.Data.LuaEngine.CreateCharacter(id);
                    ctx.Data.Player2.ChangeCharacter(character);
                }
            }, GetThumbnail(id));
        }

        choiceButtons.AddRange(p1Grid.Build());
        choiceButtons.AddRange(p2Grid.Build());

        choiceButtons.Add(
            Button.Create("START MATCH")
                  .WithSize(0.22f, 0.08f)
                  .At(0.5f, 0.85f)
                  .Action(ctx =>
                  {
                      if (ctx.Data.Player1?.CurrentCharacter != null && ctx.Data.Player2?.CurrentCharacter != null)
                      {
                          PhysicsEngine.ResetMatch(ctx.Data.Player1, ctx.Data.Player2);
                          GameState.SetState(State.Playing);
                      }
                  })
        );

        ButtonsByState[State.ChoiceMenu] = choiceButtons;

        ButtonsByState[State.Paused] = new List<Button>
        {
            Button.Create("Resume")
                  .WithSize(0.25f, 0.08f)
                  .At(0.5f, 0.41f)
                  .Action(ctx =>
                  {
                      GameState.SetState(State.Playing);
                  }), 
            Button.Create("Quit to Menu")
                  .WithSize(0.25f, 0.08f)
                  .At(0.5f, 0.5f)
                  .Action(ctx =>
                  {
                      GameState.SetState(State.MainMenu);
                  })
        };

        ButtonsByState[State.GameOver] = new List<Button>
        {
            Button.Create("Main Menu")
                  .WithSize(0.25f, 0.08f)
                  .At(0.5f, 0.5f)
                  .Action(ctx =>
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
            case State.ChoiceMenu:
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

    }

    public static void GameOver()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.KEY_ENTER))
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
            case State.ChoiceMenu:
                DrawChoiceMenu(data);
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
        
    }

    private static void DrawChoiceMenu(GameData data)
    {
        int headerSize = (int)(sh * 0.04f);
        int infoSize = (int)(sh * 0.03f);
        float p2_text_margin = 0.2f*sh;

        string p1Name = data.Player1?.CurrentCharacter?.Name ?? "None";
        string p2Name = data.Player2?.CurrentCharacter?.Name ?? "None";

        Raylib.DrawText($"P1: {p1Name}", (int)(sw * 0.05f), (int)(sh * 0.1f), infoSize, Color.DARKGRAY);
        Raylib.DrawText($"P2: {p2Name}", (int)((sw * 0.95f)-p2_text_margin), (int)(sh * 0.1f), infoSize, Color.DARKGRAY);

        if (data.Player1?.CurrentCharacter != null && data.Player2?.CurrentCharacter != null)
        {
            string readyMsg = "FIGHTERS READY!";
            int w = Raylib.MeasureText(readyMsg, headerSize);
            Raylib.DrawText(readyMsg, (sw - w) / 2, (int)(sh * 0.76f), headerSize, Color.DARKGREEN);
        }
    }

    private static void DrawPlaying(GameData data)
    {
        CombatHUD.Draw(data);
        Raylib.DrawLine(0, (int)PhysicsEngine.GroundY, sw, (int)PhysicsEngine.GroundY, Color.BLACK);
    }

    private static void DrawPause(GameData data)
    {

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
        int titleSize = (int)(sh * 0.06f);
        int winnerSize = (int)(sh * 0.04f);
        int subSize = (int)(sh * 0.03f);

        string title = "GAME OVER";
        int titleW = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, (sw - titleW) / 2, (int)(sh * 0.24f), titleSize, Color.RED);

        if (!string.IsNullOrEmpty(PhysicsEngine.MatchWinnerText))
        {
            int winW = Raylib.MeasureText(PhysicsEngine.MatchWinnerText, winnerSize);
            Raylib.DrawText(PhysicsEngine.MatchWinnerText, (sw - winW) / 2, (int)(sh * 0.33f), winnerSize, Color.DARKGREEN);
        }

        string hint = "Press Enter or Click Below to Return to Menu";
        int hintW = Raylib.MeasureText(hint, subSize);
        Raylib.DrawText(hint, (sw - hintW) / 2, (int)(sh * 0.41f), subSize, Color.DARKGRAY);
    }
}