using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;

public class PaginatedRosterWidget : IDisposable
{
    private readonly List<Button> _pageButtons = new();
    private readonly Dictionary<string, Texture2D> _thumbnails = new(StringComparer.OrdinalIgnoreCase);

    private string[] _fighters = Array.Empty<string>();
    private Action<string, ButtonContext>? _onSelect;
    private float _gridXPct;
    private float _gridYPct;
    private float _gridWPct;
    private float _gridHPct;

    public float XPct { get; }
    public float YPct { get; }
    public float WidthPct { get; }
    public float HeightPct { get; }
    public int Columns { get; }
    public int Rows { get; }
    public int ItemsPerPage => Math.Max(1, Columns * Rows);

    public int CurrentPage { get; private set; } = 0;
    public int TotalPages => Math.Max(1, (int)MathF.Ceiling(_fighters.Length / (float)ItemsPerPage));
    public string? SelectedFighterId { get; set; }

    public RenderMaterial PanelMaterial { get; set; } = new() { ShaderId = BuiltInShaders.UiButtonId };
    public Texture2D? PanelTexture { get; set; }
    public bool OwnsPanelTexture { get; private set; }
    public Vector3 AccentColor { get; set; } = new Vector3(0.45f, 0.78f, 1.0f);

    public Button PrevButton { get; }
    public Button NextButton { get; }
    public bool CapturesPointer { get; private set; }

    public PaginatedRosterWidget(
        float xPct,
        float yPct,
        float widthPct,
        float heightPct,
        int columns,
        int rows)
    {
        XPct = xPct;
        YPct = yPct;
        WidthPct = widthPct;
        HeightPct = heightPct;
        Columns = Math.Max(1, columns);
        Rows = Math.Max(1, rows);

        float navH = MathF.Min(0.042f, heightPct * 0.25f);
        float navW = MathF.Min(0.11f, widthPct * 0.28f);
        float navY = yPct + heightPct - navH - 0.008f;

        _gridXPct = xPct + 0.008f;
        _gridYPct = yPct + 0.008f;
        _gridWPct = widthPct - 0.016f;
        _gridHPct = MathF.Max(0.04f, heightPct - navH - 0.022f);

        PrevButton = new Button(xPct + 0.008f, navY, navW, navH, "< PREV", _ => ChangePage(-1))
        {
            FontSizePct = 0.017f,
            AccentColor = AccentColor
        };

        NextButton = new Button(xPct + widthPct - navW - 0.008f, navY, navW, navH, "NEXT >", _ => ChangePage(1))
        {
            FontSizePct = 0.017f,
            AccentColor = AccentColor
        };
    }

    public bool LoadPanelTexture(string relativeOrFullPath)
    {
        if (OwnsPanelTexture && PanelTexture is { id: > 0 } oldTex)
        {
            Raylib.UnloadTexture(oldTex);
        }
        PanelTexture = null;
        OwnsPanelTexture = false;

        string? resolved = ModManager.ResolveFile(relativeOrFullPath)
                        ?? ModManager.ResolveFile("Assets", "UI", relativeOrFullPath)
                        ?? (File.Exists(relativeOrFullPath) ? relativeOrFullPath : null);

        if (resolved == null || !File.Exists(resolved))
            return false;

        Texture2D loaded = Raylib.LoadTexture(resolved);
        if (loaded.id == 0)
            return false;

        PanelTexture = loaded;
        OwnsPanelTexture = true;
        return true;
    }

    public void SetFighters(string[] fighters, Action<string, ButtonContext> onSelect, string? selectedId = null)
    {
        _fighters = fighters ?? Array.Empty<string>();
        _onSelect = onSelect;
        if (selectedId != null)
            SelectedFighterId = selectedId;

        LoadMissingThumbnails(_fighters);
        CurrentPage = Math.Clamp(CurrentPage, 0, TotalPages - 1);
        RebuildPageButtons();
    }

    public void ReloadThumbnails()
    {
        UnloadThumbnails();
        LoadMissingThumbnails(_fighters);
        RebuildPageButtons();
    }

    public void ChangePage(int delta)
    {
        int next = Math.Clamp(CurrentPage + delta, 0, TotalPages - 1);
        if (next != CurrentPage)
        {
            CurrentPage = next;
            RebuildPageButtons();
        }
    }

    private void RebuildPageButtons()
    {
        _pageButtons.Clear();
        if (_fighters.Length == 0) return;

        int start = CurrentPage * ItemsPerPage;
        int end = Math.Min(_fighters.Length, start + ItemsPerPage);

        var grid = new UIGrid(_gridXPct, _gridYPct, _gridWPct, _gridHPct, Columns, Rows, spacingPct: 0.008f)
            .WithAlignment(TextAlignment.Bottom);

        for (int i = start; i < end; i++)
        {
            string id = _fighters[i];
            string label = char.ToUpper(id[0]) + id[1..];
            Texture2D? thumb = _thumbnails.TryGetValue(id, out var tex) ? tex : null;

            grid.Add(label, ctx =>
            {
                SelectedFighterId = id;
                SyncSelectionStates();
                _onSelect?.Invoke(id, ctx);
            }, thumb);
        }

        _pageButtons.AddRange(grid.Build());
        for (int i = 0; i < _pageButtons.Count; i++)
        {
            _pageButtons[i].FontSizePct = 0.017f;
            _pageButtons[i].AccentColor = AccentColor;
        }
        SyncSelectionStates();
    }

    public void SetSelectedFighter(string? fighterId)
    {
        if (string.Equals(SelectedFighterId, fighterId, StringComparison.OrdinalIgnoreCase))
            return;

        SelectedFighterId = fighterId;
        SyncSelectionStates();
    }

    private void SyncSelectionStates()
    {
        int start = CurrentPage * ItemsPerPage;
        for (int i = 0; i < _pageButtons.Count; i++)
        {
            int fighterIdx = start + i;
            if (fighterIdx < _fighters.Length)
            {
                _pageButtons[i].IsSelected = string.Equals(_fighters[fighterIdx], SelectedFighterId, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private void LoadMissingThumbnails(string[] fighters)
    {
        foreach (string fighter in fighters)
        {
            if (_thumbnails.ContainsKey(fighter)) continue;

            string? path = ModManager.ResolveFile("Assets", "Characters", fighter, "Roaster.png")
                        ?? ModManager.ResolveFile("Characters", "assets", fighter, "Roaster.png");

            if (path != null && File.Exists(path))
            {
                Texture2D loaded = Raylib.LoadTexture(path);
                if (loaded.id > 0)
                    _thumbnails[fighter] = loaded;
            }
        }
    }

    public void Update(ButtonContext context)
    {
        Rectangle bounds = UIRenderer.GetPctRect(XPct, YPct, WidthPct, HeightPct);
        CapturesPointer = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), bounds);

        for (int i = 0; i < _pageButtons.Count; i++)
        {
            _pageButtons[i].Update(context);
        }

        if (CurrentPage > 0)
            PrevButton.Update(context);

        if (CurrentPage < TotalPages - 1)
            NextButton.Update(context);

        SyncSelectionStates();
    }

    public void Draw()
    {
        Rectangle bounds = UIRenderer.GetPctRect(XPct, YPct, WidthPct, HeightPct);
        UIRenderer.DrawStyledPanel(
            bounds,
            PanelMaterial,
            PanelTexture,
            hover: 0f,
            isSelected: false,
            AccentColor,
            new Color(18, 20, 30, 215),
            new Color(80, 90, 115, 200)
        );

        if (_fighters.Length == 0)
        {
            int emptyFont = Math.Max(14, (int)(Raylib.GetScreenHeight() * 0.022f));
            string emptyMsg = "NO FIGHTERS MATCH FILTER";
            int ew = Raylib.MeasureText(emptyMsg, emptyFont);
            Raylib.DrawText(emptyMsg, (int)(bounds.x + (bounds.width - ew) * 0.5f), (int)(bounds.y + bounds.height * 0.38f), emptyFont, Color.LIGHTGRAY);
        }
        else
        {
            for (int i = 0; i < _pageButtons.Count; i++)
            {
                _pageButtons[i].Draw();
            }
        }

        if (CurrentPage > 0)
            PrevButton.Draw();

        if (CurrentPage < TotalPages - 1)
            NextButton.Draw();

        string pageLabel = $"PAGE {CurrentPage + 1} / {TotalPages}";
        int pageFont = Math.Max(12, (int)(Raylib.GetScreenHeight() * 0.019f));
        int pw = Raylib.MeasureText(pageLabel, pageFont);
        Rectangle prevRect = PrevButton.GetPixelBounds();
        int px = (int)(bounds.x + (bounds.width - pw) * 0.5f);
        int py = (int)(prevRect.y + (prevRect.height - pageFont) * 0.5f);

        Raylib.DrawText(pageLabel, px + 1, py + 1, pageFont, Color.BLACK);
        Raylib.DrawText(pageLabel, px, py, pageFont, Color.RAYWHITE);
    }

    private void UnloadThumbnails()
    {
        foreach (var tex in _thumbnails.Values)
        {
            if (tex.id > 0) Raylib.UnloadTexture(tex);
        }
        _thumbnails.Clear();
    }

    public void Dispose()
    {
        UnloadThumbnails();
        if (OwnsPanelTexture && PanelTexture is { id: > 0 } tex)
        {
            Raylib.UnloadTexture(tex);
        }
        PanelTexture = null;
        OwnsPanelTexture = false;
    }
}
