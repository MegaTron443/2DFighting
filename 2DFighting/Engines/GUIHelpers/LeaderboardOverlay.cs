using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

public sealed class LeaderboardOverlay : IDisposable
{
    private const float PanelXPct = 0.11f;
    private const float PanelYPct = 0.12f;
    private const float PanelWPct = 0.78f;
    private const float PanelHPct = 0.76f;
    private const int MaxDisplayedRows = 50;

    private readonly struct CachedRow
    {
        public readonly string Rank;
        public readonly string PlayerLabel;
        public readonly string Fighter;
        public readonly string Rating;
        public readonly string Record;
        public readonly string BestCombo;
        public readonly string Diversity;
        public readonly Color RankColor;
        public readonly Color RowColor;

        public CachedRow(
            string rank,
            string playerLabel,
            string fighter,
            string rating,
            string record,
            string bestCombo,
            string diversity,
            Color rankColor,
            Color rowColor)
        {
            Rank = rank;
            PlayerLabel = playerLabel;
            Fighter = fighter;
            Rating = rating;
            Record = record;
            BestCombo = bestCombo;
            Diversity = diversity;
            RankColor = rankColor;
            RowColor = rowColor;
        }
    }

    private readonly Button _vanillaTabButton;
    private readonly Button _vanillaAndModsTabButton;
    private readonly Button _modsOnlyTabButton;
    private readonly Button _closeButton;
    private readonly List<CachedRow> _rows = new();

    private ModFilterMode _selectedFilter = ModFilterMode.VanillaAndMods;
    private int _cachedVersion = -1;
    private ModFilterMode _cachedFilter = ModFilterMode.VanillaAndMods;
    private float _scrollOffsetPx = 0f;
    private string _footerSummary = "0 RANKED ENTRIES  |  0 RECENT MATCHES";

    public bool IsOpen { get; private set; }
    public bool CapturesPointer => IsOpen;

    public LeaderboardOverlay()
    {
        _vanillaTabButton = new Button(0.135f, 0.215f, 0.17f, 0.048f, "VANILLA ONLY", _ => SelectFilter(ModFilterMode.VanillaOnly))
        {
            FontSizePct = 0.016f,
            AccentColor = new Vector3(0.45f, 0.80f, 1.0f)
        };

        _vanillaAndModsTabButton = new Button(0.315f, 0.215f, 0.19f, 0.048f, "VANILLA + MODS", _ => SelectFilter(ModFilterMode.VanillaAndMods))
        {
            FontSizePct = 0.016f,
            AccentColor = new Vector3(1.0f, 0.80f, 0.25f)
        };

        _modsOnlyTabButton = new Button(0.515f, 0.215f, 0.16f, 0.048f, "MODS ONLY", _ => SelectFilter(ModFilterMode.ModsOnly))
        {
            FontSizePct = 0.016f,
            AccentColor = new Vector3(0.95f, 0.48f, 0.85f)
        };

        _closeButton = new Button(0.755f, 0.812f, 0.115f, 0.046f, "CLOSE", _ => Close())
        {
            FontSizePct = 0.017f,
            AccentColor = new Vector3(1.0f, 0.80f, 0.25f)
        };
    }

    public void Open(ModFilterMode activeFilter)
    {
        IsOpen = true;
        _selectedFilter = activeFilter;
        _scrollOffsetPx = 0f;
        RefreshCacheIfNeeded(force: true);
    }

    public void Close()
    {
        IsOpen = false;
    }

    public void Toggle(ModFilterMode activeFilter)
    {
        if (IsOpen) Close();
        else Open(activeFilter);
    }

    public void SelectFilter(ModFilterMode filter)
    {
        if (_selectedFilter == filter) return;
        _selectedFilter = filter;
        _scrollOffsetPx = 0f;
        RefreshCacheIfNeeded(force: true);
    }

    public void Update(ButtonContext context)
    {
        if (!IsOpen) return;

        if (Raylib.IsKeyPressed(KeyboardKey.KEY_ESCAPE))
        {
            Close();
            return;
        }

        RefreshCacheIfNeeded(force: false);

        _vanillaTabButton.IsSelected = _selectedFilter == ModFilterMode.VanillaOnly;
        _vanillaAndModsTabButton.IsSelected = _selectedFilter == ModFilterMode.VanillaAndMods;
        _modsOnlyTabButton.IsSelected = _selectedFilter == ModFilterMode.ModsOnly;

        _vanillaTabButton.Update(context);
        _vanillaAndModsTabButton.Update(context);
        _modsOnlyTabButton.Update(context);
        _closeButton.Update(context);

        float wheel = Raylib.GetMouseWheelMove();
        if (MathF.Abs(wheel) > 0.001f)
        {
            Rectangle panelRect = UIRenderer.GetPctRect(PanelXPct, PanelYPct, PanelWPct, PanelHPct);
            if (Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), panelRect))
            {
                float rowHeight = GetRowHeight();
                float listHeight = GetListRect(panelRect).height;
                float contentHeight = _rows.Count * rowHeight;
                float maxScroll = MathF.Max(0f, contentHeight - listHeight);
                _scrollOffsetPx = Math.Clamp(_scrollOffsetPx - wheel * rowHeight * 2.2f, 0f, maxScroll);
            }
        }
    }

    public void Draw()
    {
        if (!IsOpen) return;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        Raylib.DrawRectangle(0, 0, sw, sh, new Color(6, 9, 16, 185));

        Rectangle panel = UIRenderer.GetPctRect(PanelXPct, PanelYPct, PanelWPct, PanelHPct);
        Raylib.DrawRectangleRec(panel, new Color(16, 21, 34, 246));
        Raylib.DrawRectangleLinesEx(panel, 2.2f, new Color(255, 204, 64, 235));

        int titleFont = Math.Max(18, (int)(sh * 0.030f));
        int subFont = Math.Max(11, (int)(sh * 0.015f));
        UIRenderer.DrawTextShadowed("SEASON 1 LEADERBOARDS", panel.x + panel.width * 0.032f, panel.y + panel.height * 0.032f, titleFont, new Color(255, 214, 92, 255));

        string subtitle = "STEAM-READY TELEMETRY  |  SCROLL WHEEL TO BROWSE  |  ESC TO CLOSE";
        int subW = UIRenderer.MeasureText(subtitle, subFont);
        UIRenderer.DrawText(subtitle, panel.x + panel.width * 0.968f - subW, panel.y + panel.height * 0.044f, subFont, new Color(175, 192, 220, 230));

        _vanillaTabButton.Draw();
        _vanillaAndModsTabButton.Draw();
        _modsOnlyTabButton.Draw();

        Rectangle headerRect = new Rectangle(
            panel.x + panel.width * 0.032f,
            panel.y + panel.height * 0.21f,
            panel.width * 0.936f,
            MathF.Max(26f, sh * 0.042f));

        Raylib.DrawRectangleRec(headerRect, new Color(26, 35, 54, 240));
        Raylib.DrawRectangleLinesEx(headerRect, 1f, new Color(95, 130, 185, 165));

        int headerFont = Math.Max(11, (int)(sh * 0.016f));
        float colY = headerRect.y + (headerRect.height - headerFont) * 0.5f;
        DrawColumnHeader("RANK", headerRect.x, headerRect.width, 0.015f, colY, headerFont);
        DrawColumnHeader("PLAYER (STEAM ID)", headerRect.x, headerRect.width, 0.095f, colY, headerFont);
        DrawColumnHeader("FIGHTER", headerRect.x, headerRect.width, 0.390f, colY, headerFont);
        DrawColumnHeader("RATING", headerRect.x, headerRect.width, 0.545f, colY, headerFont);
        DrawColumnHeader("W / L", headerRect.x, headerRect.width, 0.655f, colY, headerFont);
        DrawColumnHeader("MAX COMBO", headerRect.x, headerRect.width, 0.760f, colY, headerFont);
        DrawColumnHeader("DIVERSITY", headerRect.x, headerRect.width, 0.880f, colY, headerFont);

        Rectangle listRect = GetListRect(panel);
        Raylib.DrawRectangleRec(listRect, new Color(11, 15, 25, 225));
        Raylib.DrawRectangleLinesEx(listRect, 1f, new Color(65, 88, 130, 160));

        float rowHeight = GetRowHeight();
        int rowFont = Math.Max(12, (int)(sh * 0.018f));
        float maxScroll = MathF.Max(0f, _rows.Count * rowHeight - listRect.height);
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx, 0f, maxScroll);

        Raylib.BeginScissorMode((int)listRect.x + 1, (int)listRect.y + 1, Math.Max(1, (int)listRect.width - 2), Math.Max(1, (int)listRect.height - 2));

        if (_rows.Count == 0)
        {
            string emptyMsg = "No recorded matches for this filter yet. Finish a match or AI eval to populate rankings!";
            int ew = UIRenderer.MeasureText(emptyMsg, rowFont);
            UIRenderer.DrawText(emptyMsg, listRect.x + (listRect.width - ew) * 0.5f, listRect.y + listRect.height * 0.44f, rowFont, Color.LIGHTGRAY);
        }
        else
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                float rowY = listRect.y + i * rowHeight - _scrollOffsetPx;
                if (rowY + rowHeight < listRect.y) continue;
                if (rowY > listRect.y + listRect.height) break;

                CachedRow row = _rows[i];
                Rectangle rowRect = new Rectangle(listRect.x + 2f, rowY + 1f, listRect.width - 4f, rowHeight - 2f);
                Raylib.DrawRectangleRec(rowRect, i % 2 == 0 ? new Color(20, 27, 43, 210) : new Color(15, 21, 35, 210));

                float textY = rowY + (rowHeight - rowFont) * 0.5f;
                UIRenderer.DrawText(row.Rank, rowRect.x + rowRect.width * 0.015f, textY, rowFont, row.RankColor);
                UIRenderer.DrawText(row.PlayerLabel, rowRect.x + rowRect.width * 0.095f, textY, rowFont, row.RowColor);
                UIRenderer.DrawText(row.Fighter, rowRect.x + rowRect.width * 0.390f, textY, rowFont, Color.SKYBLUE);
                UIRenderer.DrawText(row.Rating, rowRect.x + rowRect.width * 0.545f, textY, rowFont, Color.GOLD);
                UIRenderer.DrawText(row.Record, rowRect.x + rowRect.width * 0.655f, textY, rowFont, Color.RAYWHITE);
                UIRenderer.DrawText(row.BestCombo, rowRect.x + rowRect.width * 0.760f, textY, rowFont, new Color(255, 165, 95, 255));
                UIRenderer.DrawText(row.Diversity, rowRect.x + rowRect.width * 0.880f, textY, rowFont, new Color(120, 240, 175, 255));
            }
        }

        Raylib.EndScissorMode();

        if (maxScroll > 1f)
        {
            float barTrackH = listRect.height - 8f;
            float thumbH = MathF.Max(24f, barTrackH * (listRect.height / (_rows.Count * rowHeight)));
            float thumbY = listRect.y + 4f + (_scrollOffsetPx / maxScroll) * (barTrackH - thumbH);
            Raylib.DrawRectangle((int)(listRect.x + listRect.width - 7f), (int)thumbY, 4, (int)thumbH, new Color(255, 204, 64, 185));
        }

        int footerFont = Math.Max(11, (int)(sh * 0.016f));
        UIRenderer.DrawText(_footerSummary, panel.x + panel.width * 0.032f, panel.y + panel.height * 0.925f, footerFont, Color.LIGHTGRAY);

        _closeButton.Draw();
    }

    private void RefreshCacheIfNeeded(bool force)
    {
        int dbVersion = LeaderboardDatabase.Version;
        if (!force && dbVersion == _cachedVersion && _selectedFilter == _cachedFilter)
            return;

        _cachedVersion = dbVersion;
        _cachedFilter = _selectedFilter;
        _rows.Clear();

        List<LeaderboardEntry> entries = LeaderboardDatabase.GetTopEntries(_selectedFilter, MaxDisplayedRows);
        for (int i = 0; i < entries.Count; i++)
        {
            LeaderboardEntry e = entries[i];
            int rank = i + 1;
            Color rankColor = rank switch
            {
                1 => Color.GOLD,
                2 => new Color(215, 224, 240, 255),
                3 => new Color(215, 145, 95, 255),
                _ => Color.LIGHTGRAY
            };

            string persona = string.IsNullOrWhiteSpace(e.PersonaName) ? "Player" : e.PersonaName.Trim();
            string playerLabel = e.SteamId != 0
                ? $"{Truncate(persona, 16)} ({e.SteamId})"
                : $"{Truncate(persona, 18)} (LOCAL)";

            string fighter = string.IsNullOrWhiteSpace(e.CharacterId)
                ? "UNKNOWN"
                : Truncate(e.CharacterId.ToUpperInvariant(), 14);

            int diversityPct = (int)MathF.Round(Math.Clamp(e.SkillDiversityScore, 0f, 1f) * 100f);

            _rows.Add(new CachedRow(
                rank: $"#{rank}",
                playerLabel: playerLabel,
                fighter: fighter,
                rating: $"{e.Rating}",
                record: $"{e.Wins}W - {e.Losses}L",
                bestCombo: $"{e.BestCombo} HITS",
                diversity: $"{diversityPct}%",
                rankColor: rankColor,
                rowColor: e.SteamId != 0 ? Color.RAYWHITE : new Color(220, 232, 248, 255)));
        }

        int recentCount = LeaderboardDatabase.GetRecentMatches(2000).Count;
        _footerSummary = $"{_rows.Count} RANKED ENTRIES ({GetFilterLabel(_selectedFilter)})  |  {recentCount} MATCH TELEMETRY LOGS";
    }

    private static Rectangle GetListRect(Rectangle panel)
    {
        float headerBottom = panel.y + panel.height * 0.21f + MathF.Max(26f, Raylib.GetScreenHeight() * 0.042f);
        float bottomMargin = panel.height * 0.115f;
        return new Rectangle(
            panel.x + panel.width * 0.032f,
            headerBottom + 6f,
            panel.width * 0.936f,
            MathF.Max(40f, panel.y + panel.height - bottomMargin - (headerBottom + 6f)));
    }

    private static float GetRowHeight() => MathF.Max(28f, Raylib.GetScreenHeight() * 0.044f);

    private static void DrawColumnHeader(string text, float baseX, float width, float xPct, float y, int fontSize)
    {
        UIRenderer.DrawText(text, baseX + width * xPct, y, fontSize, new Color(255, 214, 110, 240));
    }

    private static string GetFilterLabel(ModFilterMode mode) => mode switch
    {
        ModFilterMode.VanillaOnly    => "VANILLA ONLY",
        ModFilterMode.VanillaAndMods => "VANILLA + MODS",
        ModFilterMode.ModsOnly       => "MODS ONLY",
        _                            => "VANILLA + MODS"
    };

    private static string Truncate(string value, int maxLen)
    {
        if (value.Length <= maxLen) return value;
        return maxLen > 3 ? value[..(maxLen - 3)] + "..." : value[..maxLen];
    }

    public void Dispose()
    {
        _vanillaTabButton.Dispose();
        _vanillaAndModsTabButton.Dispose();
        _modsOnlyTabButton.Dispose();
        _closeButton.Dispose();
        _rows.Clear();
    }
}
