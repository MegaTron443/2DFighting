using System;
using System.Collections.Generic;
using System.IO;
using Raylib_cs;

public class CharacterSelectScreen : IUIScreen
{
    private readonly List<Button> _buttons = new();
    private readonly Dictionary<string, Texture2D> _rosterThumbnails = new();

    public void Initialize()
    {
        Dispose();
        _buttons.Clear();

        string[] fighters = EngineServices.Current?.AvailableFighters ?? GUIEngine.RosterIds;
        LoadRosterThumbnails(fighters);

        var p1Grid = BuildFighterGrid(fighters, xPct: 0.05f, slot: 0, isP1: true);
        var p2Grid = BuildFighterGrid(fighters, xPct: 0.55f, slot: 1, isP1: false);

        _buttons.AddRange(p1Grid.Build());
        _buttons.AddRange(p2Grid.Build());

        _buttons.Add(
            Button.Create("START MATCH")
                .WithSize(0.22f, 0.08f)
                .At(0.5f, 0.85f)
                .Action(ctx =>
                {
                    if (ctx.Data.Player1?.CurrentCharacter != null && ctx.Data.Player2?.CurrentCharacter != null)
                    {
                        if (ctx.Data.Services != null)
                            ctx.Data.Services.Match.ResetMatch(ctx.Data.Player1, ctx.Data.Player2, ctx.Data.Services.Projectiles);
                        else
                            PhysicsEngine.ResetMatch(ctx.Data.Player1, ctx.Data.Player2);
                        GameState.SetState(State.Playing);
                    }
                })
        );
    }

    private UIGrid BuildFighterGrid(string[] fighters, float xPct, int slot, bool isP1)
    {
        var grid = new UIGrid(xPct: xPct, yPct: 0.20f, widthPct: 0.4f, heightPct: 0.6f, columns: 6, rows: 5)
            .WithAlignment(TextAlignment.Bottom);

        foreach (var fighter in fighters)
        {
            string id = fighter;
            string label = char.ToUpper(id[0]) + id[1..];
            grid.Add(label, ctx =>
            {
                Player? target = isP1 ? ctx.Data.Player1 : ctx.Data.Player2;
                if (target != null)
                {
                    Character character = ctx.Data.LuaEngine.CreateCharacter(id, slot: slot);
                    target.ChangeCharacter(character);
                }
            }, GetThumbnail(id));
        }

        return grid;
    }

    private void LoadRosterThumbnails(string[] fighters)
    {
        foreach (var fighter in fighters)
        {
            string? path = ModManager.ResolveFile("Assets", "Characters", fighter, "Roaster.png")
                        ?? ModManager.ResolveFile("Characters", "assets", fighter, "Roaster.png");

            if (path != null && File.Exists(path) && !_rosterThumbnails.ContainsKey(fighter))
            {
                _rosterThumbnails[fighter] = Raylib.LoadTexture(path);
            }
        }
    }

    private Texture2D? GetThumbnail(string characterKey)
    {
        return _rosterThumbnails.TryGetValue(characterKey, out var tex) ? tex : null;
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
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int headerSize = (int)(sh * 0.04f);
        int infoSize = (int)(sh * 0.03f);
        float p2TextMargin = 0.2f * sh;

        string p1Name = data.Player1?.CurrentCharacter?.Name ?? "None";
        string p2Name = data.Player2?.CurrentCharacter?.Name ?? "None";

        Raylib.DrawText($"P1: {p1Name}", (int)(sw * 0.05f), (int)(sh * 0.1f), infoSize, Color.DARKGRAY);
        Raylib.DrawText($"P2: {p2Name}", (int)((sw * 0.95f) - p2TextMargin), (int)(sh * 0.1f), infoSize, Color.DARKGRAY);

        if (data.Player1?.CurrentCharacter != null && data.Player2?.CurrentCharacter != null)
        {
            string readyMsg = "FIGHTERS READY!";
            int w = Raylib.MeasureText(readyMsg, headerSize);
            Raylib.DrawText(readyMsg, (sw - w) / 2, (int)(sh * 0.76f), headerSize, Color.DARKGREEN);
        }

        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Draw();
        }
    }

    public void Dispose()
    {
        foreach (var tex in _rosterThumbnails.Values)
        {
            Raylib.UnloadTexture(tex);
        }
        _rosterThumbnails.Clear();
    }
}
