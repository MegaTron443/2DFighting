using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

public class MainMenuScreen : IUIScreen
{
    private readonly List<Button> _buttons = new();

    public void Initialize()
    {
        _buttons.Clear();
        _buttons.AddRange(new[]
        {
            Button.Create("Player vs AI")
                .WithSize(0.28f, 0.07f)
                .At(0.5f, 0.33f)
                .Action(ctx =>
                {
                    SetTrainingMode(ctx, false);
                    SetupGame(ctx, p1AI: false, p2AI: true);
                }),

            Button.Create("Player vs Player")
                .WithSize(0.28f, 0.07f)
                .At(0.5f, 0.41f)
                .Action(ctx =>
                {
                    SetTrainingMode(ctx, false);
                    SetupGame(ctx, p1AI: false, p2AI: false);
                }),

            Button.Create("AI vs AI (Turbo Train)")
                .WithSize(0.28f, 0.07f)
                .At(0.5f, 0.49f)
                .Action(ctx =>
                {
                    SetTrainingMode(ctx, true);
                    SetupGame(ctx, p1AI: true, p2AI: true);
                }),

            Button.Create("Settings")
                .WithSize(0.28f, 0.07f)
                .At(0.5f, 0.57f)
                .Action(_ => GameState.SetState(State.Settings)),

            Button.Create("Exit")
                .WithSize(0.28f, 0.07f)
                .At(0.5f, 0.65f)
                .Action(_ => GameState.SetState(State.Exit))
        });
    }

    private static void SetTrainingMode(ButtonContext ctx, bool enabled)
    {
        if (ctx.Data.Services != null)
            ctx.Data.Services.Match.IsTrainingMode = enabled;
        else
            PhysicsEngine.IsTrainingMode = enabled;
    }

    private static void SetupGame(ButtonContext ctx, bool p1AI, bool p2AI)
    {
        int sw = Raylib.GetScreenWidth();
        Vector2 spawnP1 = new Vector2(sw * 0.1f, PhysicsEngine.GroundY * 0.9f);
        Vector2 spawnP2 = new Vector2(sw * 0.9f, PhysicsEngine.GroundY * 0.9f);
        Player p1 = Player.Create("Player 1", 0, spawnP1, ctx.Data.AppConfig, isAI: p1AI);
        Player p2 = Player.Create("Player 2", 1, spawnP2, ctx.Data.AppConfig, isAI: p2AI);
        p1.SetOpponent(p2);
        p2.SetOpponent(p1);

        bool isTraining = ctx.Data.Services?.Match.IsTrainingMode ?? PhysicsEngine.IsTrainingMode;
        if (isTraining)
        {
            string[] fighters = ctx.Data.Services?.AvailableFighters ?? GUIEngine.RosterIds;
            var rng = Random.Shared;
            p1.ChangeCharacter(ctx.Data.LuaEngine.CreateCharacter(fighters[rng.Next(fighters.Length)], slot: 0));
            p2.ChangeCharacter(ctx.Data.LuaEngine.CreateCharacter(fighters[rng.Next(fighters.Length)], slot: 1));
            if (ctx.Data.Services != null)
                ctx.Data.Services.Match.ResetMatch(p1, p2, ctx.Data.Services.Projectiles);
            else
                PhysicsEngine.ResetMatch(p1, p2);
            GameState.SetState(State.Playing);
        }
        else
        {
            GameState.SetState(State.ChoiceMenu);
        }

        ctx.SetP1(p1);
        ctx.SetP2(p2);
    }

    public void Update(ButtonContext context)
    {
        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Update(context);
        }
    }

    public void Draw(GameData data)
    {
        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Draw();
        }
    }
}
