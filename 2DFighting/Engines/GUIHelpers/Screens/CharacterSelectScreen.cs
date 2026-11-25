using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

public class CharacterSelectScreen : IUIScreen
{
    private readonly List<Button> _buttons = new();
    private PaginatedRosterWidget? _p1Roster;
    private PaginatedRosterWidget? _p2Roster;
    private Button? _p1DescPanel;
    private Button? _p2DescPanel;

    private int _knownCatalogVersion = -1;
    private string _knownP1Key = "";
    private string _knownP2Key = "";

    public void Initialize()
    {
        Dispose();
        _buttons.Clear();
        _knownCatalogVersion = -1;
        _knownP1Key = "";
        _knownP2Key = "";

        _p1Roster = new PaginatedRosterWidget(0.03f, 0.14f, 0.44f, 0.34f, columns: 4, rows: 2)
        {
            AccentColor = new Vector3(0.30f, 0.72f, 1.0f)
        };
        _p2Roster = new PaginatedRosterWidget(0.53f, 0.14f, 0.44f, 0.34f, columns: 4, rows: 2)
        {
            AccentColor = new Vector3(1.0f, 0.35f, 0.35f)
        };

        _p1DescPanel = new Button(0.03f, 0.50f, 0.44f, 0.24f, "Select a fighter on the left roster.")
            .WithTitle("PLAYER 1 FIGHTER")
            .WithFontSize(0.017f)
            .WithAccent(0.30f, 0.72f, 1.0f)
            .WithScrolling();

        _p2DescPanel = new Button(0.53f, 0.50f, 0.44f, 0.24f, "Select a fighter on the right roster.")
            .WithTitle("PLAYER 2 FIGHTER")
            .WithFontSize(0.017f)
            .WithAccent(1.0f, 0.35f, 0.35f)
            .WithScrolling();

        _buttons.Add(
            Button.Create("START MATCH")
                .WithSize(0.24f, 0.08f)
                .At(0.50f, 0.84f)
                .WithAccent(0.25f, 0.90f, 0.48f)
                .Action(ctx =>
                {
                    if (SteamLobbyManager.IsInMatchLobby)
                    {
                        Player? localPlayer = SteamLobbyManager.LocalSlot == 0 ? ctx.Data.Player1 : ctx.Data.Player2;
                        Character? localChar = localPlayer?.CurrentCharacter;
                        if (localChar == null) return;

                        SteamLobbyManager.SetLocalSelection(
                            localChar.CharacterId,
                            localChar.ScriptHash,
                            localChar.BaseScriptHash,
                            ready: true);

                        if (SteamLobbyManager.HandshakeState == ModHandshakeState.Verified &&
                            SteamLobbyManager.PeerReady &&
                            SteamLobbyManager.IsHost)
                        {
                            SteamLobbyManager.TryRequestMatchStart();
                        }
                        return;
                    }

                    if (ctx.Data.Player1?.CurrentCharacter != null && ctx.Data.Player2?.CurrentCharacter != null)
                    {
                        SceneManager.LoadMatchScene();
                        if (ctx.Data.Services != null)
                            ctx.Data.Services.Match.ResetMatch(ctx.Data.Player1, ctx.Data.Player2, ctx.Data.Services.Projectiles);
                        else
                            PhysicsEngine.ResetMatch(ctx.Data.Player1, ctx.Data.Player2);
                        ctx.Data.Services?.PruneUnusedCharacterSpriteSheets();
                        GameState.SetState(State.Playing);
                    }
                })
        );

        _buttons.Add(
            Button.Create("BACK TO LOBBY")
                .WithSize(0.18f, 0.06f)
                .At(0.14f, 0.84f)
                .WithAccent(0.60f, 0.65f, 0.80f)
                .Action(_ =>
                {
                    if (SteamLobbyManager.IsInMatchLobby)
                        SteamLobbyManager.LeaveLobby();
                    GameState.SetState(State.MainMenu);
                })
        );
    }

    private void RefreshRosters()
    {
        string[] fighters = EngineServices.Current?.FilteredFighters
                         ?? EngineServices.Current?.AvailableFighters
                         ?? GUIEngine.RosterIds;

        bool isOnline = SteamLobbyManager.IsInMatchLobby;
        if (!isOnline || SteamLobbyManager.LocalSlot == 0)
            _p1Roster?.SetFighters(fighters, (id, ctx) => AssignFighter(ctx, slot: 0, id));
        else
            _p1Roster?.SetFighters(fighters, (_, _) => { });

        if (!isOnline || SteamLobbyManager.LocalSlot == 1)
            _p2Roster?.SetFighters(fighters, (id, ctx) => AssignFighter(ctx, slot: 1, id));
        else
            _p2Roster?.SetFighters(fighters, (_, _) => { });

        _knownCatalogVersion = ModManager.CatalogVersion;
    }

    private static void AssignFighter(ButtonContext ctx, int slot, string fighterId)
    {
        if (SteamLobbyManager.IsInMatchLobby && slot != SteamLobbyManager.LocalSlot)
            return;

        Player? target = slot == 0 ? ctx.Data.Player1 : ctx.Data.Player2;
        if (target == null) return;

        Character character = ctx.Data.LuaEngine.CreateCharacter(fighterId, slot: slot);
        target.ChangeCharacter(character);

        if (SteamLobbyManager.IsInMatchLobby)
        {
            SteamLobbyManager.SetLocalSelection(
                character.CharacterId,
                character.ScriptHash,
                character.BaseScriptHash,
                ready: false);
        }
    }

    private void SyncDescriptionPanels(GameData data)
    {
        Character? c1 = data.Player1?.CurrentCharacter;
        if (_p1DescPanel != null)
        {
            string key = c1 != null ? $"{c1.Name}|{c1.ScriptHash}" : "";
            if (!string.Equals(_knownP1Key, key, StringComparison.Ordinal))
            {
                _knownP1Key = key;
                _p1DescPanel.Title = c1 != null ? $"P1: {c1.Name.ToUpperInvariant()}" : "PLAYER 1 FIGHTER";
                _p1DescPanel.Text = c1?.Description ?? "Select a fighter on the left roster.";
            }
            if (c1 == null && _p1Roster?.SelectedFighterId != null)
            {
                _p1Roster.SetSelectedFighter(null);
            }
            else if (c1 != null && data.Services != null && _p1Roster?.SelectedFighterId == null)
            {
                _p1Roster?.SetSelectedFighter(data.Services.SelectedLobbyFighterId);
            }
        }

        Character? c2 = data.Player2?.CurrentCharacter;
        if (_p2DescPanel != null)
        {
            string key = c2 != null ? $"{c2.Name}|{c2.ScriptHash}" : "";
            if (!string.Equals(_knownP2Key, key, StringComparison.Ordinal))
            {
                _knownP2Key = key;
                _p2DescPanel.Title = c2 != null ? $"P2: {c2.Name.ToUpperInvariant()}" : "PLAYER 2 FIGHTER";
                _p2DescPanel.Text = c2?.Description ?? "Select a fighter on the right roster.";
            }
            if (c2 == null && _p2Roster?.SelectedFighterId != null)
            {
                _p2Roster.SetSelectedFighter(null);
            }
        }
    }

    public void Update(ButtonContext context)
    {
        if (_knownCatalogVersion != ModManager.CatalogVersion)
            RefreshRosters();

        if (SteamLobbyManager.IsInMatchLobby)
        {
            if (SteamLobbyManager.LocalSlot == 0)
                _p1Roster?.Update(context);
            else
                _p2Roster?.Update(context);

            // Auto-launch once host & guest are both ready and mod hash is verified
            if (SteamLobbyManager.IsHost &&
                SteamLobbyManager.LocalReady &&
                SteamLobbyManager.PeerReady &&
                SteamLobbyManager.HandshakeState == ModHandshakeState.Verified)
            {
                SteamLobbyManager.TryRequestMatchStart();
            }
        }
        else
        {
            _p1Roster?.Update(context);
            _p2Roster?.Update(context);
        }

        _p1DescPanel?.Update(context);
        _p2DescPanel?.Update(context);
        SyncDescriptionPanels(context.Data);

        if (_buttons.Count > 0 && SteamLobbyManager.IsInMatchLobby)
        {
            _buttons[0].Text = SteamLobbyManager.LocalReady ? "READY (WAITING...)" : "LOCK IN / READY";
        }
        else if (_buttons.Count > 0)
        {
            _buttons[0].Text = "START MATCH";
        }

        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Update(context);
        }
    }

    public void Draw(GameData data)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int headerSize = Math.Max(16, (int)(sh * 0.04f));
        int infoSize = Math.Max(14, (int)(sh * 0.03f));

        string p1Name = SteamLobbyManager.IsInMatchLobby
            ? (SteamLobbyManager.LocalSlot == 0
                ? $"{SteamManager.LocalPersonaName}: {data.Player1?.CurrentCharacter?.Name ?? "Selecting"}"
                : $"{SteamLobbyManager.PeerPersonaName}: {(!string.IsNullOrEmpty(SteamLobbyManager.PeerCharId) ? SteamLobbyManager.PeerCharId.ToUpperInvariant() : "Selecting")}")
            : (data.Player1?.CurrentCharacter?.Name ?? "None");

        string p2Name = SteamLobbyManager.IsInMatchLobby
            ? (SteamLobbyManager.LocalSlot == 1
                ? $"{SteamManager.LocalPersonaName}: {data.Player2?.CurrentCharacter?.Name ?? "Selecting"}"
                : $"{SteamLobbyManager.PeerPersonaName}: {(!string.IsNullOrEmpty(SteamLobbyManager.PeerCharId) ? SteamLobbyManager.PeerCharId.ToUpperInvariant() : "Selecting")}")
            : (data.Player2?.CurrentCharacter?.Name ?? "None");

        Raylib.DrawText(SteamLobbyManager.IsInMatchLobby ? $"P1 - {p1Name}" : $"P1: {p1Name}", (int)(sw * 0.03f), (int)(sh * 0.07f), infoSize, Color.DARKBLUE);
        Raylib.DrawText(SteamLobbyManager.IsInMatchLobby ? $"P2 - {p2Name}" : $"P2: {p2Name}", (int)(sw * 0.53f), (int)(sh * 0.07f), infoSize, Color.MAROON);

        _p1Roster?.Draw();
        _p2Roster?.Draw();
        _p1DescPanel?.Draw();
        _p2DescPanel?.Draw();

        if (SteamLobbyManager.IsInMatchLobby)
        {
            Color badgeCol = SteamLobbyManager.HandshakeState switch
            {
                ModHandshakeState.Verified        => Color.DARKGREEN,
                ModHandshakeState.MismatchBlocked => Color.RED,
                ModHandshakeState.SyncingMod      => Color.ORANGE,
                _                                 => Color.DARKBLUE
            };
            string statusMsg = $"STEAM HANDSHAKE: {SteamLobbyManager.HandshakeState.ToString().ToUpperInvariant()}  |  {SteamLobbyManager.StatusText}";
            int swText = Raylib.MeasureText(statusMsg, Math.Max(13, (int)(sh * 0.022f)));
            Raylib.DrawText(statusMsg, (sw - swText) / 2, (int)(sh * 0.758f), Math.Max(13, (int)(sh * 0.022f)), badgeCol);
        }
        else if (data.Player1?.CurrentCharacter != null && data.Player2?.CurrentCharacter != null)
        {
            string readyMsg = "FIGHTERS READY!";
            int w = Raylib.MeasureText(readyMsg, headerSize);
            Raylib.DrawText(readyMsg, (sw - w) / 2, (int)(sh * 0.755f), headerSize, Color.DARKGREEN);
        }

        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Draw();
        }
    }

    public void Dispose()
    {
        _p1Roster?.Dispose();
        _p1Roster = null;

        _p2Roster?.Dispose();
        _p2Roster = null;

        _p1DescPanel?.Dispose();
        _p1DescPanel = null;

        _p2DescPanel?.Dispose();
        _p2DescPanel = null;
    }
}
