using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

public sealed class LeaderboardOverlay : IDisposable
{
    private const float PanelXPct = 0.11f;
    private const float PanelYPct = 0.11f;
    private const float PanelWPct = 0.78f;
    private const float PanelHPct = 0.78f;
    private const float CharacterStripXPct = 0.135f;
    private const float CharacterStripYPct = 0.248f;
    private const float CharacterStripWPct = 0.730f;
    private const float CharacterPillHPct = 0.038f;
    private const float CharacterPillGapPct = 0.005f;
    private const int MaxUnpaginatedFighters = 9;
    private const int FightersPerPillPage = 8;
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

    private sealed class CharacterPillItem
    {
        public string CharacterId { get; }
        public Button Button { get; }

        public CharacterPillItem(string characterId, Button button)
        {
            CharacterId = characterId;
            Button = button;
        }
    }

    private static readonly LeaderboardSortMode[] SortColumns =
    {
        LeaderboardSortMode.Rating,
        LeaderboardSortMode.WinRate,
        LeaderboardSortMode.MaxCombo,
        LeaderboardSortMode.Diversity
    };

    private readonly Button _vanillaTabButton;
    private readonly Button _vanillaAndModsTabButton;
    private readonly Button _modsOnlyTabButton;
    private readonly Button _closeButton;
    private readonly List<CachedRow> _rows = new();
    private readonly List<CharacterPillItem> _characterPills = new();
    private readonly List<string> _availableCharacters = new();

    private Button? _prevPillPageButton;
    private Button? _nextPillPageButton;

    private ModFilterMode _selectedFilter = ModFilterMode.VanillaAndMods;
    private string _selectedCharacterId = "ALL";
    private LeaderboardSortMode _sortMode = LeaderboardSortMode.Rating;
    private int _pillPage = 0;

    private int _cachedVersion = -1;
    private int _cachedCatalogVersion = -1;
    private ModFilterMode _cachedFilter = ModFilterMode.VanillaAndMods;
    private string _cachedCharacterFilter = "";
    private LeaderboardSortMode _cachedSortMode = (LeaderboardSortMode)(-1);
    private int _cachedPillPage = -1;

    private int _hoveredSortColumn = -1;
    private float _scrollOffsetPx = 0f;
    private string _footerSummary = "0 RANKED ENTRIES (VANILLA + MODS | ALL FIGHTERS)  |  0 MATCH TELEMETRY LOGS";
    private string _ratingHeader = "RATING v";
    private string _wlHeader = "W / L";
    private string _comboHeader = "MAX COMBO";
    private string _diversityHeader = "DIVERSITY";

    public bool IsOpen { get; private set; }
    public bool CapturesPointer => IsOpen;

    public LeaderboardOverlay()
    {
        _vanillaTabButton = new Button(0.135f, 0.195f, 0.17f, 0.044f, "VANILLA ONLY", _ => SelectFilter(ModFilterMode.VanillaOnly))
        {
            FontSizePct = 0.016f,
            AccentColor = new Vector3(0.45f, 0.80f, 1.0f)
        };

        _vanillaAndModsTabButton = new Button(0.315f, 0.195f, 0.19f, 0.044f, "VANILLA + MODS", _ => SelectFilter(ModFilterMode.VanillaAndMods))
        {
            FontSizePct = 0.016f,
            AccentColor = new Vector3(1.0f, 0.80f, 0.25f)
        };

        _modsOnlyTabButton = new Button(0.515f, 0.195f, 0.16f, 0.044f, "MODS ONLY", _ => SelectFilter(ModFilterMode.ModsOnly))
        {
            FontSizePct = 0.016f,
            AccentColor = new Vector3(0.95f, 0.48f, 0.85f)
        };

        _closeButton = new Button(0.755f, 0.822f, 0.115f, 0.046f, "CLOSE", _ => Close())
        {
            FontSizePct = 0.017f,
            AccentColor = new Vector3(1.0f, 0.80f, 0.25f)
        };

        UpdateHeaderLabels();
    }

    public void Open(ModFilterMode activeFilter)
    {
        IsOpen = true;
        _selectedFilter = activeFilter;
        _pillPage = 0;
        _scrollOffsetPx = 0f;
        RefreshCacheIfNeeded(forcePills: true, forceRows: true);
    }

    public void Close()
    {
        IsOpen = false;
        _hoveredSortColumn = -1;
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
        _pillPage = 0;
        _scrollOffsetPx = 0f;
        RefreshCacheIfNeeded(forcePills: true, forceRows: true);
    }

    public void SelectCharacterFilter(string characterId)
    {
        string normalized = string.IsNullOrWhiteSpace(characterId) ? "ALL" : characterId.Trim();
        if (string.Equals(_selectedCharacterId, normalized, StringComparison.OrdinalIgnoreCase))
            return;

        _selectedCharacterId = normalized;
        _scrollOffsetPx = 0f;
        RefreshCacheIfNeeded(forcePills: false, forceRows: true);
    }

    public void SelectSortMode(LeaderboardSortMode sortMode)
    {
        if (_sortMode == sortMode) return;
        _sortMode = sortMode;
        _scrollOffsetPx = 0f;
        UpdateHeaderLabels();
        RefreshCacheIfNeeded(forcePills: false, forceRows: true);
    }

    public void Update(ButtonContext context)
    {
        if (!IsOpen) return;

        if (Raylib.IsKeyPressed(KeyboardKey.KEY_ESCAPE))
        {
            Close();
            return;
        }

        RefreshCacheIfNeeded(forcePills: false, forceRows: false);

        _vanillaTabButton.IsSelected = _selectedFilter == ModFilterMode.VanillaOnly;
        _vanillaAndModsTabButton.IsSelected = _selectedFilter == ModFilterMode.VanillaAndMods;
        _modsOnlyTabButton.IsSelected = _selectedFilter == ModFilterMode.ModsOnly;

        _vanillaTabButton.Update(context);
        _vanillaAndModsTabButton.Update(context);
        _modsOnlyTabButton.Update(context);

        for (int i = 0; i < _characterPills.Count; i++)
        {
            CharacterPillItem pill = _characterPills[i];
            pill.Button.IsSelected = string.Equals(_selectedCharacterId, pill.CharacterId, StringComparison.OrdinalIgnoreCase);
            pill.Button.Update(context);
        }

        _prevPillPageButton?.Update(context);
        _nextPillPageButton?.Update(context);
        if (_pillPage != _cachedPillPage)
        {
            RefreshCacheIfNeeded(forcePills: true, forceRows: false);
        }

        _closeButton.Update(context);

        Vector2 mousePos = Raylib.GetMousePosition();
        Rectangle panelRect = UIRenderer.GetPctRect(PanelXPct, PanelYPct, PanelWPct, PanelHPct);
        Rectangle headerRect = GetHeaderRect(panelRect);

        _hoveredSortColumn = -1;
        for (int i = 0; i < SortColumns.Length; i++)
        {
            if (Raylib.CheckCollisionPointRec(mousePos, GetSortColumnRect(headerRect, i)))
            {
                _hoveredSortColumn = i;
                if (Raylib.IsMouseButtonPressed(MouseButton.MOUSE_LEFT_BUTTON))
                {
                    SelectSortMode(SortColumns[i]);
                }
                break;
            }
        }

        float wheel = Raylib.GetMouseWheelMove();
        if (MathF.Abs(wheel) > 0.001f && Raylib.CheckCollisionPointRec(mousePos, panelRect))
        {
            float rowHeight = GetRowHeight();
            float listHeight = GetListRect(panelRect).height;
            float contentHeight = _rows.Count * rowHeight;
            float maxScroll = MathF.Max(0f, contentHeight - listHeight);
            _scrollOffsetPx = Math.Clamp(_scrollOffsetPx - wheel * rowHeight * 2.2f, 0f, maxScroll);
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
        UIRenderer.DrawTextShadowed("SEASON 1 LEADERBOARDS", panel.x + panel.width * 0.032f, panel.y + panel.height * 0.028f, titleFont, new Color(255, 214, 92, 255));

        string subtitle = "CLICK HEADERS TO SORT  |  SCROLL WHEEL TO BROWSE  |  ESC TO CLOSE";
        int subW = UIRenderer.MeasureText(subtitle, subFont);
        UIRenderer.DrawText(subtitle, panel.x + panel.width * 0.968f - subW, panel.y + panel.height * 0.040f, subFont, new Color(175, 192, 220, 230));

        _vanillaTabButton.Draw();
        _vanillaAndModsTabButton.Draw();
        _modsOnlyTabButton.Draw();

        for (int i = 0; i < _characterPills.Count; i++)
        {
            _characterPills[i].Button.Draw();
        }
        _prevPillPageButton?.Draw();
        _nextPillPageButton?.Draw();

        Rectangle headerRect = GetHeaderRect(panel);
        Raylib.DrawRectangleRec(headerRect, new Color(26, 35, 54, 240));
        Raylib.DrawRectangleLinesEx(headerRect, 1f, new Color(95, 130, 185, 165));

        for (int i = 0; i < SortColumns.Length; i++)
        {
            bool isActiveSort = _sortMode == SortColumns[i];
            bool isHoveredSort = _hoveredSortColumn == i;
            if (!isActiveSort && !isHoveredSort) continue;

            Rectangle colRect = GetSortColumnRect(headerRect, i);
            Color highlightFill = isActiveSort ? new Color(42, 72, 112, 185) : new Color(38, 52, 82, 165);
            Raylib.DrawRectangleRec(colRect, highlightFill);
            if (isActiveSort)
            {
                Raylib.DrawRectangle((int)colRect.x, (int)(colRect.y + colRect.height - 2f), (int)colRect.width, 2, new Color(115, 225, 255, 235));
            }
        }

        int headerFont = Math.Max(11, (int)(sh * 0.016f));
        float colY = headerRect.y + (headerRect.height - headerFont) * 0.5f;
        Color staticHeaderColor = new Color(255, 214, 110, 240);
        Color activeHeaderColor = new Color(115, 225, 255, 255);
        Color hoverHeaderColor = Color.RAYWHITE;

        UIRenderer.DrawText("RANK", headerRect.x + headerRect.width * 0.015f, colY, headerFont, staticHeaderColor);
        UIRenderer.DrawText("PLAYER (STEAM ID)", headerRect.x + headerRect.width * 0.095f, colY, headerFont, staticHeaderColor);
        UIRenderer.DrawText("FIGHTER", headerRect.x + headerRect.width * 0.390f, colY, headerFont, staticHeaderColor);

        UIRenderer.DrawText(_ratingHeader, headerRect.x + headerRect.width * 0.545f, colY, headerFont,
            _sortMode == LeaderboardSortMode.Rating ? activeHeaderColor : (_hoveredSortColumn == 0 ? hoverHeaderColor : staticHeaderColor));
        UIRenderer.DrawText(_wlHeader, headerRect.x + headerRect.width * 0.655f, colY, headerFont,
            _sortMode == LeaderboardSortMode.WinRate ? activeHeaderColor : (_hoveredSortColumn == 1 ? hoverHeaderColor : staticHeaderColor));
        UIRenderer.DrawText(_comboHeader, headerRect.x + headerRect.width * 0.760f, colY, headerFont,
            _sortMode == LeaderboardSortMode.MaxCombo ? activeHeaderColor : (_hoveredSortColumn == 2 ? hoverHeaderColor : staticHeaderColor));
        UIRenderer.DrawText(_diversityHeader, headerRect.x + headerRect.width * 0.880f, colY, headerFont,
            _sortMode == LeaderboardSortMode.Diversity ? activeHeaderColor : (_hoveredSortColumn == 3 ? hoverHeaderColor : staticHeaderColor));

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

        int footerFont = Math.Max(11, (int)(sh * 0.0155f));
        UIRenderer.DrawText(_footerSummary, panel.x + panel.width * 0.032f, panel.y + panel.height * 0.930f, footerFont, Color.LIGHTGRAY);

        _closeButton.Draw();
    }

    private void RefreshCacheIfNeeded(bool forcePills, bool forceRows)
    {
        int dbVersion = LeaderboardDatabase.Version;
        int catalogVersion = ModManager.CatalogVersion;

        bool pillsNeedRebuild = forcePills ||
                                dbVersion != _cachedVersion ||
                                catalogVersion != _cachedCatalogVersion ||
                                _selectedFilter != _cachedFilter ||
                                _pillPage != _cachedPillPage;

        if (pillsNeedRebuild)
        {
            RebuildCharacterPills();
            _cachedPillPage = _pillPage;
        }

        bool rowsNeedRebuild = forceRows ||
                               pillsNeedRebuild ||
                               !string.Equals(_selectedCharacterId, _cachedCharacterFilter, StringComparison.OrdinalIgnoreCase) ||
                               _sortMode != _cachedSortMode;

        if (!rowsNeedRebuild)
            return;

        _cachedVersion = dbVersion;
        _cachedCatalogVersion = catalogVersion;
        _cachedFilter = _selectedFilter;
        _cachedCharacterFilter = _selectedCharacterId;
        _cachedSortMode = _sortMode;
        _rows.Clear();

        List<LeaderboardEntry> entries = LeaderboardDatabase.GetTopEntries(
            _selectedFilter,
            _selectedCharacterId,
            _sortMode,
            MaxDisplayedRows);

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
        string entryWord = _rows.Count == 1 ? "RANKED ENTRY" : "RANKED ENTRIES";
        string charLabel = string.Equals(_selectedCharacterId, "ALL", StringComparison.OrdinalIgnoreCase)
            ? "ALL FIGHTERS"
            : _selectedCharacterId.ToUpperInvariant();

        _footerSummary = $"{_rows.Count} {entryWord} ({GetFilterLabel(_selectedFilter)} | {charLabel})  |  {recentCount} MATCH TELEMETRY LOGS";
    }

    private void RebuildCharacterPills()
    {
        DisposeCharacterPills();

        _availableCharacters.Clear();
        string[] modFighters = ModManager.GetFilteredFighters(_selectedFilter);
        AddUniqueCharacters(_availableCharacters, modFighters);
        List<string> dbFighters = LeaderboardDatabase.GetDistinctCharacterIds(_selectedFilter);
        AddUniqueCharacters(_availableCharacters, dbFighters);

        if (!string.Equals(_selectedCharacterId, "ALL", StringComparison.OrdinalIgnoreCase) &&
            !ContainsIgnoreCase(_availableCharacters, _selectedCharacterId))
        {
            _selectedCharacterId = "ALL";
            _scrollOffsetPx = 0f;
        }

        bool needsPagination = _availableCharacters.Count > MaxUnpaginatedFighters;
        int startIndex = 0;
        int visibleFightersCount = _availableCharacters.Count;

        if (needsPagination)
        {
            int maxPage = Math.Max(0, (_availableCharacters.Count - 1) / FightersPerPillPage);
            _pillPage = Math.Clamp(_pillPage, 0, maxPage);
            startIndex = _pillPage * FightersPerPillPage;
            visibleFightersCount = Math.Min(FightersPerPillPage, _availableCharacters.Count - startIndex);
        }
        else
        {
            _pillPage = 0;
        }

        const float navButtonW = 0.024f;
        float reservedNavW = needsPagination ? (navButtonW * 2f + CharacterPillGapPct * 2f) : 0f;
        float stripWidthForPills = CharacterStripWPct - reservedNavW;
        float totalGaps = visibleFightersCount * CharacterPillGapPct;
        float usableWidth = MathF.Max(0.10f, stripWidthForPills - totalGaps);

        const float allWeight = 1.45f;
        float totalWeight = allWeight + visibleFightersCount;
        float allPillW = usableWidth * (allWeight / totalWeight);
        float fighterPillW = usableWidth * (1.0f / totalWeight);

        float cursorX = CharacterStripXPct;

        var allBtn = new Button(cursorX, CharacterStripYPct, allPillW, CharacterPillHPct, "ALL FIGHTERS", _ => SelectCharacterFilter("ALL"))
        {
            FontSizePct = 0.0125f,
            AccentColor = new Vector3(1.0f, 0.80f, 0.25f)
        };
        _characterPills.Add(new CharacterPillItem("ALL", allBtn));
        cursorX += allPillW + CharacterPillGapPct;

        for (int i = 0; i < visibleFightersCount; i++)
        {
            string charId = _availableCharacters[startIndex + i];
            var pillBtn = new Button(cursorX, CharacterStripYPct, fighterPillW, CharacterPillHPct, charId.ToUpperInvariant(), _ => SelectCharacterFilter(charId))
            {
                FontSizePct = 0.0125f,
                AccentColor = new Vector3(0.45f, 0.85f, 1.0f)
            };
            _characterPills.Add(new CharacterPillItem(charId, pillBtn));
            cursorX += fighterPillW + CharacterPillGapPct;
        }

        if (needsPagination)
        {
            int maxPage = Math.Max(0, (_availableCharacters.Count - 1) / FightersPerPillPage);
            _prevPillPageButton = new Button(cursorX, CharacterStripYPct, navButtonW, CharacterPillHPct, "<", _ =>
            {
                _pillPage = _pillPage > 0 ? _pillPage - 1 : maxPage;
            })
            {
                FontSizePct = 0.013f,
                AccentColor = new Vector3(0.75f, 0.82f, 0.95f)
            };

            _nextPillPageButton = new Button(cursorX + navButtonW + CharacterPillGapPct, CharacterStripYPct, navButtonW, CharacterPillHPct, ">", _ =>
            {
                _pillPage = _pillPage < maxPage ? _pillPage + 1 : 0;
            })
            {
                FontSizePct = 0.013f,
                AccentColor = new Vector3(0.75f, 0.82f, 0.95f)
            };
        }
    }

    private void UpdateHeaderLabels()
    {
        _ratingHeader = _sortMode == LeaderboardSortMode.Rating ? "RATING v" : "RATING";
        _wlHeader = _sortMode == LeaderboardSortMode.WinRate ? "W / L v" : "W / L";
        _comboHeader = _sortMode == LeaderboardSortMode.MaxCombo ? "MAX COMBO v" : "MAX COMBO";
        _diversityHeader = _sortMode == LeaderboardSortMode.Diversity ? "DIVERSITY v" : "DIVERSITY";
    }

    private static Rectangle GetHeaderRect(Rectangle panel)
    {
        return new Rectangle(
            panel.x + panel.width * 0.032f,
            panel.y + panel.height * 0.245f,
            panel.width * 0.936f,
            MathF.Max(26f, Raylib.GetScreenHeight() * 0.042f));
    }

    private static Rectangle GetSortColumnRect(Rectangle headerRect, int sortColumnIndex)
    {
        (float startPct, float endPct) = sortColumnIndex switch
        {
            0 => (0.535f, 0.642f),
            1 => (0.645f, 0.747f),
            2 => (0.750f, 0.867f),
            _ => (0.870f, 0.992f)
        };

        return new Rectangle(
            headerRect.x + headerRect.width * startPct,
            headerRect.y + 1f,
            headerRect.width * (endPct - startPct),
            headerRect.height - 2f);
    }

    private static Rectangle GetListRect(Rectangle panel)
    {
        Rectangle headerRect = GetHeaderRect(panel);
        float bottomMargin = panel.height * 0.115f;
        float topY = headerRect.y + headerRect.height + 6f;
        return new Rectangle(
            panel.x + panel.width * 0.032f,
            topY,
            panel.width * 0.936f,
            MathF.Max(40f, panel.y + panel.height - bottomMargin - topY));
    }

    private static float GetRowHeight() => MathF.Max(28f, Raylib.GetScreenHeight() * 0.044f);

    private static void AddUniqueCharacters(List<string> target, IEnumerable<string> source)
    {
        foreach (string item in source)
        {
            if (string.IsNullOrWhiteSpace(item)) continue;
            string clean = item.Trim();
            if (string.Equals(clean, "ALL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(clean, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (!ContainsIgnoreCase(target, clean))
            {
                target.Add(clean);
            }
        }
    }

    private static bool ContainsIgnoreCase(List<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
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

    private void DisposeCharacterPills()
    {
        for (int i = 0; i < _characterPills.Count; i++)
        {
            _characterPills[i].Button.Dispose();
        }
        _characterPills.Clear();

        _prevPillPageButton?.Dispose();
        _prevPillPageButton = null;

        _nextPillPageButton?.Dispose();
        _nextPillPageButton = null;
    }

    public void Dispose()
    {
        _vanillaTabButton.Dispose();
        _vanillaAndModsTabButton.Dispose();
        _modsOnlyTabButton.Dispose();
        _closeButton.Dispose();
        DisposeCharacterPills();
        _rows.Clear();
        _availableCharacters.Clear();
    }
}
