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
    private Button? _p1PointTab;
    private Button? _p1PartnerTab;
    private Button? _p2PointTab;
    private Button? _p2PartnerTab;

    private int _p1PickSlot = 0;
    private int _p2PickSlot = 0;
    private int _knownCatalogVersion = -1;
    private string _knownP1Key = "";
    private string _knownP2Key = "";

    private static bool Is2v2Mode =>
        string.Equals(EngineServices.Current?.Match.CurrentMatchMode, "2v2", StringComparison.OrdinalIgnoreCase) ||
        (SteamLobbyManager.IsInMatchLobby && SteamLobbyManager.Is2v2Lobby);

    public void Initialize()
    {
        Dispose();
        _buttons.Clear();
        _p1PickSlot = 0;
        _p2PickSlot = 0;
        _knownCatalogVersion = -1;
        _knownP1Key = "";
        _knownP2Key = "";

        _p1PointTab = new Button(0.03f, 0.104f, 0.215f, 0.032f, "SLOT 1: POINT", _ => _p1PickSlot = 0)
        {
            FontSizePct = 0.014f,
            AccentColor = new Vector3(0.30f, 0.72f, 1.0f)
        };
        _p1PartnerTab = new Button(0.255f, 0.104f, 0.215f, 0.032f, "SLOT 2: PARTNER", _ => _p1PickSlot = 1)
        {
            FontSizePct = 0.014f,
            AccentColor = new Vector3(0.25f, 0.88f, 0.78f)
        };
        _p2PointTab = new Button(0.53f, 0.104f, 0.215f, 0.032f, "SLOT 1: POINT", _ => _p2PickSlot = 0)
        {
            FontSizePct = 0.014f,
            AccentColor = new Vector3(1.0f, 0.35f, 0.35f)
        };
        _p2PartnerTab = new Button(0.755f, 0.104f, 0.215f, 0.032f, "SLOT 2: PARTNER", _ => _p2PickSlot = 1)
        {
            FontSizePct = 0.014f,
            AccentColor = new Vector3(0.75f, 0.45f, 1.0f)
        };

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
                    bool is2v2 = Is2v2Mode;
                    if (SteamLobbyManager.IsInMatchLobby)
                    {
                        Player? localPlayer = SteamLobbyManager.LocalSlot == 0 ? ctx.Data.Player1 : ctx.Data.Player2;
                        Character? localChar = localPlayer?.PrimaryCharacter;
                        Character? localPartner = localPlayer?.PartnerCharacter;
                        if (localChar == null || (is2v2 && localPartner == null)) return;

                        SteamLobbyManager.SetLocalSelection(
                            localChar.CharacterId,
                            localChar.ScriptHash,
                            localChar.BaseScriptHash,
                            ready: true,
                            workshopId: localChar.WorkshopId,
                            partnerCharId: is2v2 ? localPartner!.CharacterId : "",
                            partnerScriptHash: is2v2 ? localPartner!.ScriptHash : "",
                            partnerBaseScriptHash: is2v2 ? localPartner!.BaseScriptHash : "",
                            partnerWorkshopId: is2v2 ? localPartner!.WorkshopId : 0UL);

                        if (SteamLobbyManager.HandshakeState == ModHandshakeState.Verified &&
                            SteamLobbyManager.PeerReady &&
                            SteamLobbyManager.IsHost)
                        {
                            SteamLobbyManager.TryRequestMatchStart();
                        }
                        return;
                    }

                    bool p1Ready = ctx.Data.Player1?.PrimaryCharacter != null && (!is2v2 || ctx.Data.Player1?.PartnerCharacter != null);
                    bool p2Ready = ctx.Data.Player2?.PrimaryCharacter != null && (!is2v2 || ctx.Data.Player2?.PartnerCharacter != null);
                    if (p1Ready && p2Ready)
                    {
                        SceneManager.LoadMatchScene();
                        if (ctx.Data.Services != null)
                            ctx.Data.Services.Match.ResetMatch(ctx.Data.Player1!, ctx.Data.Player2!, ctx.Data.Services.Projectiles);
                        else
                            PhysicsEngine.ResetMatch(ctx.Data.Player1!, ctx.Data.Player2!);
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

    private void AssignFighter(ButtonContext ctx, int slot, string fighterId)
    {
        if (SteamLobbyManager.IsInMatchLobby && slot != SteamLobbyManager.LocalSlot)
            return;

        Player? target = slot == 0 ? ctx.Data.Player1 : ctx.Data.Player2;
        if (target == null) return;

        bool is2v2 = Is2v2Mode;
        if (is2v2)
        {
            int pickSlot = slot == 0 ? _p1PickSlot : _p2PickSlot;
            if (pickSlot == 0)
            {
                Character primary = ctx.Data.LuaEngine.CreateCharacter(fighterId, slot: slot);
                Character? existingPartner = target.PartnerCharacter;
                target.SetTeam(primary, existingPartner);
                if (existingPartner == null)
                {
                    if (slot == 0) _p1PickSlot = 1;
                    else _p2PickSlot = 1;
                }
            }
            else
            {
                Character partner = ctx.Data.LuaEngine.CreateCharacter(fighterId, slot: slot + 2);
                target.SetPartnerCharacter(partner);
            }

            if (SteamLobbyManager.IsInMatchLobby && target.PrimaryCharacter != null)
            {
                Character pChar = target.PrimaryCharacter;
                Character? partChar = target.PartnerCharacter;
                SteamLobbyManager.SetLocalSelection(
                    pChar.CharacterId,
                    pChar.ScriptHash,
                    pChar.BaseScriptHash,
                    ready: false,
                    workshopId: pChar.WorkshopId,
                    partnerCharId: partChar?.CharacterId ?? "",
                    partnerScriptHash: partChar?.ScriptHash ?? "",
                    partnerBaseScriptHash: partChar?.BaseScriptHash ?? "",
                    partnerWorkshopId: partChar?.WorkshopId ?? 0UL);
            }
            return;
        }

        Character character = ctx.Data.LuaEngine.CreateCharacter(fighterId, slot: slot);
        target.ChangeCharacter(character);

        if (SteamLobbyManager.IsInMatchLobby)
        {
            SteamLobbyManager.SetLocalSelection(
                character.CharacterId,
                character.ScriptHash,
                character.BaseScriptHash,
                ready: false,
                workshopId: character.WorkshopId);
        }
    }

    private void SyncDescriptionPanels(GameData data)
    {
        bool is2v2 = Is2v2Mode;
        Character? c1 = data.Player1?.PrimaryCharacter;
        Character? c1Partner = data.Player1?.PartnerCharacter;
        if (_p1DescPanel != null)
        {
            string key = $"{c1?.Name ?? ""}|{c1?.ScriptHash ?? ""}|{c1Partner?.Name ?? ""}|{c1Partner?.ScriptHash ?? ""}|{_p1PickSlot}";
            if (!string.Equals(_knownP1Key, key, StringComparison.Ordinal))
            {
                _knownP1Key = key;
                if (is2v2)
                {
                    Character? focused = (_p1PickSlot == 1 ? c1Partner : c1) ?? c1 ?? c1Partner;
                    _p1DescPanel.Title = $"P1: {c1?.Name.ToUpperInvariant() ?? "?"} + {c1Partner?.Name.ToUpperInvariant() ?? "?"}";
                    _p1DescPanel.Text = focused?.Description ?? "Pick Slot 1 (Point) and Slot 2 (Partner).";
                }
                else
                {
                    _p1DescPanel.Title = c1 != null ? $"P1: {c1.Name.ToUpperInvariant()}" : "PLAYER 1 FIGHTER";
                    _p1DescPanel.Text = c1?.Description ?? "Select a fighter on the left roster.";
                }
            }
            Character? sel1 = is2v2 ? (_p1PickSlot == 1 ? c1Partner : c1) : c1;
            if (sel1 == null && _p1Roster?.SelectedFighterId != null)
            {
                _p1Roster.SetSelectedFighter(null);
            }
            else if (sel1 != null)
            {
                _p1Roster?.SetSelectedFighter(sel1.CharacterId);
            }
        }

        Character? c2 = data.Player2?.PrimaryCharacter;
        Character? c2Partner = data.Player2?.PartnerCharacter;
        if (_p2DescPanel != null)
        {
            string key = $"{c2?.Name ?? ""}|{c2?.ScriptHash ?? ""}|{c2Partner?.Name ?? ""}|{c2Partner?.ScriptHash ?? ""}|{_p2PickSlot}";
            if (!string.Equals(_knownP2Key, key, StringComparison.Ordinal))
            {
                _knownP2Key = key;
                if (is2v2)
                {
                    Character? focused = (_p2PickSlot == 1 ? c2Partner : c2) ?? c2 ?? c2Partner;
                    _p2DescPanel.Title = $"P2: {c2?.Name.ToUpperInvariant() ?? "?"} + {c2Partner?.Name.ToUpperInvariant() ?? "?"}";
                    _p2DescPanel.Text = focused?.Description ?? "Pick Slot 1 (Point) and Slot 2 (Partner).";
                }
                else
                {
                    _p2DescPanel.Title = c2 != null ? $"P2: {c2.Name.ToUpperInvariant()}" : "PLAYER 2 FIGHTER";
                    _p2DescPanel.Text = c2?.Description ?? "Select a fighter on the right roster.";
                }
            }
            Character? sel2 = is2v2 ? (_p2PickSlot == 1 ? c2Partner : c2) : c2;
            if (sel2 == null && _p2Roster?.SelectedFighterId != null)
            {
                _p2Roster.SetSelectedFighter(null);
            }
            else if (sel2 != null)
            {
                _p2Roster?.SetSelectedFighter(sel2.CharacterId);
            }
        }
    }

    public void Update(ButtonContext context)
    {
        if (_knownCatalogVersion != ModManager.CatalogVersion)
            RefreshRosters();

        bool is2v2 = Is2v2Mode;
        if (is2v2)
        {
            if (_p1PointTab != null)
            {
                _p1PointTab.IsSelected = _p1PickSlot == 0;
                _p1PointTab.Text = $"1: {context.Data.Player1?.PrimaryCharacter?.Name.ToUpperInvariant() ?? "POINT"}";
            }
            if (_p1PartnerTab != null)
            {
                _p1PartnerTab.IsSelected = _p1PickSlot == 1;
                _p1PartnerTab.Text = $"2: {context.Data.Player1?.PartnerCharacter?.Name.ToUpperInvariant() ?? "PARTNER"}";
            }
            if (_p2PointTab != null)
            {
                _p2PointTab.IsSelected = _p2PickSlot == 0;
                _p2PointTab.Text = $"1: {context.Data.Player2?.PrimaryCharacter?.Name.ToUpperInvariant() ?? "POINT"}";
            }
            if (_p2PartnerTab != null)
            {
                _p2PartnerTab.IsSelected = _p2PickSlot == 1;
                _p2PartnerTab.Text = $"2: {context.Data.Player2?.PartnerCharacter?.Name.ToUpperInvariant() ?? "PARTNER"}";
            }
        }

        if (SteamLobbyManager.IsInMatchLobby)
        {
            if (SteamLobbyManager.LocalSlot == 0)
            {
                if (is2v2)
                {
                    _p1PointTab?.Update(context);
                    _p1PartnerTab?.Update(context);
                }
                _p1Roster?.Update(context);
            }
            else
            {
                if (is2v2)
                {
                    _p2PointTab?.Update(context);
                    _p2PartnerTab?.Update(context);
                }
                _p2Roster?.Update(context);
            }

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
            if (is2v2)
            {
                _p1PointTab?.Update(context);
                _p1PartnerTab?.Update(context);
                _p2PointTab?.Update(context);
                _p2PartnerTab?.Update(context);
            }
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
        bool is2v2 = Is2v2Mode;
        int headerSize = Math.Max(16, (int)(sh * 0.04f));
        int infoSize = Math.Max(14, (int)(sh * (is2v2 ? 0.025f : 0.03f)));

        string p1Name;
        string p2Name;

        if (SteamLobbyManager.IsInMatchLobby)
        {
            if (is2v2)
            {
                p1Name = SteamLobbyManager.LocalSlot == 0
                    ? $"{SteamManager.LocalPersonaName}: {data.Player1?.PrimaryCharacter?.Name ?? "?"} + {data.Player1?.PartnerCharacter?.Name ?? "?"}"
                    : $"{SteamLobbyManager.PeerPersonaName}: {FormatPeerPair(SteamLobbyManager.PeerCharId, SteamLobbyManager.PeerPartnerCharId)}";

                p2Name = SteamLobbyManager.LocalSlot == 1
                    ? $"{SteamManager.LocalPersonaName}: {data.Player2?.PrimaryCharacter?.Name ?? "?"} + {data.Player2?.PartnerCharacter?.Name ?? "?"}"
                    : $"{SteamLobbyManager.PeerPersonaName}: {FormatPeerPair(SteamLobbyManager.PeerCharId, SteamLobbyManager.PeerPartnerCharId)}";
            }
            else
            {
                p1Name = SteamLobbyManager.LocalSlot == 0
                    ? $"{SteamManager.LocalPersonaName}: {data.Player1?.CurrentCharacter?.Name ?? "Selecting"}"
                    : $"{SteamLobbyManager.PeerPersonaName}: {(!string.IsNullOrEmpty(SteamLobbyManager.PeerCharId) ? SteamLobbyManager.PeerCharId.ToUpperInvariant() : "Selecting")}";

                p2Name = SteamLobbyManager.LocalSlot == 1
                    ? $"{SteamManager.LocalPersonaName}: {data.Player2?.CurrentCharacter?.Name ?? "Selecting"}"
                    : $"{SteamLobbyManager.PeerPersonaName}: {(!string.IsNullOrEmpty(SteamLobbyManager.PeerCharId) ? SteamLobbyManager.PeerCharId.ToUpperInvariant() : "Selecting")}";
            }
        }
        else if (is2v2)
        {
            p1Name = $"{data.Player1?.PrimaryCharacter?.Name ?? "None"} + {data.Player1?.PartnerCharacter?.Name ?? "None"}";
            p2Name = $"{data.Player2?.PrimaryCharacter?.Name ?? "None"} + {data.Player2?.PartnerCharacter?.Name ?? "None"}";
        }
        else
        {
            p1Name = data.Player1?.CurrentCharacter?.Name ?? "None";
            p2Name = data.Player2?.CurrentCharacter?.Name ?? "None";
        }

        Raylib.DrawText(SteamLobbyManager.IsInMatchLobby ? $"P1 - {p1Name}" : $"P1: {p1Name}", (int)(sw * 0.03f), (int)(sh * 0.068f), infoSize, Color.DARKBLUE);
        Raylib.DrawText(SteamLobbyManager.IsInMatchLobby ? $"P2 - {p2Name}" : $"P2: {p2Name}", (int)(sw * 0.53f), (int)(sh * 0.068f), infoSize, Color.MAROON);

        if (is2v2)
        {
            _p1PointTab?.Draw();
            _p1PartnerTab?.Draw();
            _p2PointTab?.Draw();
            _p2PartnerTab?.Draw();
        }

        _p1Roster?.Draw();
        _p2Roster?.Draw();
        _p1DescPanel?.Draw();
        _p2DescPanel?.Draw();

        bool p1Ready = data.Player1?.PrimaryCharacter != null && (!is2v2 || data.Player1?.PartnerCharacter != null);
        bool p2Ready = data.Player2?.PrimaryCharacter != null && (!is2v2 || data.Player2?.PartnerCharacter != null);

        if (SteamLobbyManager.IsInMatchLobby)
        {
            Color badgeCol = SteamLobbyManager.HandshakeState switch
            {
                ModHandshakeState.Verified        => Color.DARKGREEN,
                ModHandshakeState.MismatchBlocked => Color.RED,
                ModHandshakeState.SyncingMod      => Color.ORANGE,
                _                                 => Color.DARKBLUE
            };

            string handshakeLabel = SteamLobbyManager.HandshakeState.ToString().ToUpperInvariant();
            ulong syncWsId = SteamLobbyManager.ActiveSyncWorkshopId;
            if (SteamLobbyManager.HandshakeState == ModHandshakeState.SyncingMod && syncWsId != 0UL)
            {
                int pct = (int)Math.Round(SteamWorkshopManager.GetDownloadProgress(syncWsId) * 100f);
                handshakeLabel = $"SYNCING WORKSHOP MOD ({pct}%)";
            }

            string statusMsg = $"STEAM HANDSHAKE: {handshakeLabel}  |  {SteamLobbyManager.StatusText}";
            int fontPx = Math.Max(13, (int)(sh * 0.022f));
            int swText = Raylib.MeasureText(statusMsg, fontPx);
            Raylib.DrawText(statusMsg, (sw - swText) / 2, (int)(sh * 0.758f), fontPx, badgeCol);
        }
        else if (p1Ready && p2Ready)
        {
            string readyMsg = is2v2 ? "2v2 TEAMS READY!" : "FIGHTERS READY!";
            int w = Raylib.MeasureText(readyMsg, headerSize);
            Raylib.DrawText(readyMsg, (sw - w) / 2, (int)(sh * 0.755f), headerSize, Color.DARKGREEN);
        }

        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Draw();
        }
    }

    private static string FormatPeerPair(string primaryId, string partnerId)
    {
        string a = string.IsNullOrEmpty(primaryId) ? "Selecting" : primaryId.ToUpperInvariant();
        string b = string.IsNullOrEmpty(partnerId) ? "Partner" : partnerId.ToUpperInvariant();
        return $"{a} + {b}";
    }

    public void Dispose()
    {
        _p1PointTab?.Dispose();
        _p1PointTab = null;

        _p1PartnerTab?.Dispose();
        _p1PartnerTab = null;

        _p2PointTab?.Dispose();
        _p2PointTab = null;

        _p2PartnerTab?.Dispose();
        _p2PartnerTab = null;

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
