using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;

public class ScrollableTextPanel : IDisposable
{
    public float XPct { get; set; }
    public float YPct { get; set; }
    public float WidthPct { get; set; }
    public float HeightPct { get; set; }

    public string Title { get; set; } = "";
    public string Text
    {
        get => _text;
        set
        {
            string normalized = value ?? "";
            if (!string.Equals(_text, normalized, StringComparison.Ordinal))
            {
                _text = normalized;
                _scrollOffsetPx = 0f;
                _layoutDirty = true;
            }
        }
    }

    public Texture2D? BackgroundTexture { get; set; }
    public bool OwnsTexture { get; private set; }
    public RenderMaterial Material { get; set; } = new() { ShaderId = BuiltInShaders.UiButtonId };
    public Vector3 AccentColor { get; set; } = new Vector3(0.45f, 0.78f, 1.0f);

    public float TitleFontSizePct { get; set; } = 0.022f;
    public float FontSizePct { get; set; } = 0.017f;
    public bool IsHovered { get; private set; }
    public bool CapturesPointer => IsHovered || _isDraggingScrollbar;

    private string _text = "";
    private readonly List<string> _wrappedLines = new();
    private bool _layoutDirty = true;
    private int _cachedFontSize = -1;
    private float _cachedWrapWidth = -1f;
    private float _scrollOffsetPx = 0f;
    private bool _isDraggingScrollbar = false;

    public ScrollableTextPanel(float xPct, float yPct, float widthPct, float heightPct)
    {
        XPct = xPct;
        YPct = yPct;
        WidthPct = widthPct;
        HeightPct = heightPct;
    }

    public bool LoadTexture(string relativeOrFullPath)
    {
        if (OwnsTexture && BackgroundTexture is { id: > 0 } oldTex)
        {
            Raylib.UnloadTexture(oldTex);
        }
        BackgroundTexture = null;
        OwnsTexture = false;

        string? resolved = ModManager.ResolveFile(relativeOrFullPath)
                        ?? ModManager.ResolveFile("Assets", "UI", relativeOrFullPath)
                        ?? (File.Exists(relativeOrFullPath) ? relativeOrFullPath : null);

        if (resolved == null || !File.Exists(resolved))
            return false;

        Texture2D loaded = Raylib.LoadTexture(resolved);
        if (loaded.id == 0)
            return false;

        BackgroundTexture = loaded;
        OwnsTexture = true;
        return true;
    }

    public Rectangle GetPixelBounds() => UIRenderer.GetPctRect(XPct, YPct, WidthPct, HeightPct);

    public void Update(float dt)
    {
        Rectangle bounds = GetPixelBounds();
        Vector2 mousePos = Raylib.GetMousePosition();
        IsHovered = Raylib.CheckCollisionPointRec(mousePos, bounds);

        float maxScroll = ComputeMaxScroll(bounds, out Rectangle trackRect);

        if (maxScroll <= 0f)
        {
            _scrollOffsetPx = 0f;
            _isDraggingScrollbar = false;
            return;
        }

        if (IsHovered)
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (MathF.Abs(wheel) > 0.001f)
            {
                float step = Math.Max(22f, Raylib.GetScreenHeight() * FontSizePct * 2.2f);
                _scrollOffsetPx = Math.Clamp(_scrollOffsetPx - (wheel * step), 0f, maxScroll);
            }
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_LEFT) && Raylib.CheckCollisionPointRec(mousePos, trackRect))
        {
            _isDraggingScrollbar = true;
        }

        if (!Raylib.IsMouseButtonDown(MouseButton.MOUSE_BUTTON_LEFT))
        {
            _isDraggingScrollbar = false;
        }

        if (_isDraggingScrollbar && trackRect.height > 1f)
        {
            float t = Math.Clamp((mousePos.Y - trackRect.y) / trackRect.height, 0f, 1f);
            _scrollOffsetPx = t * maxScroll;
        }

        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx, 0f, maxScroll);
    }

    public void Draw()
    {
        Rectangle bounds = GetPixelBounds();
        Color accentCol = new Color(
            (byte)Math.Clamp(AccentColor.X * 255f, 0f, 255f),
            (byte)Math.Clamp(AccentColor.Y * 255f, 0f, 255f),
            (byte)Math.Clamp(AccentColor.Z * 255f, 0f, 255f),
            (byte)255
        );

        UIRenderer.DrawStyledPanel(
            bounds,
            Material,
            BackgroundTexture,
            IsHovered ? 0.55f : 0f,
            isSelected: false,
            AccentColor,
            new Color(20, 22, 32, 232),
            IsHovered ? accentCol : new Color(90, 95, 115, 220)
        );

        float pad = 10f;
        float sh = Raylib.GetScreenHeight();
        int titleFont = UIRenderer.FitFontSize(Title, Math.Max(14, (int)(sh * TitleFontSizePct)), 11, MathF.Max(24f, bounds.width - pad * 2f));
        float headerH = string.IsNullOrWhiteSpace(Title) ? 6f : (titleFont + 10f);

        if (!string.IsNullOrWhiteSpace(Title))
        {
            Raylib.DrawText(Title, (int)(bounds.x + pad + 1), (int)(bounds.y + 7), titleFont, Color.BLACK);
            Raylib.DrawText(Title, (int)(bounds.x + pad), (int)(bounds.y + 6), titleFont, Color.GOLD);
            Raylib.DrawLine(
                (int)(bounds.x + pad),
                (int)(bounds.y + headerH),
                (int)(bounds.x + bounds.width - pad),
                (int)(bounds.y + headerH),
                new Color(140, 150, 175, 130)
            );
        }

        float maxScroll = ComputeMaxScroll(bounds, out Rectangle trackRect, out Rectangle clipRect, out int bodyFont, out int lineHeight);
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx, 0f, maxScroll);

        Raylib.BeginScissorMode(
            (int)MathF.Floor(clipRect.x),
            (int)MathF.Floor(clipRect.y),
            Math.Max(1, (int)MathF.Ceiling(clipRect.width)),
            Math.Max(1, (int)MathF.Ceiling(clipRect.height))
        );

        float drawY = clipRect.y - _scrollOffsetPx;
        for (int i = 0; i < _wrappedLines.Count; i++)
        {
            float lineBottom = drawY + lineHeight;
            if (lineBottom >= clipRect.y && drawY <= clipRect.y + clipRect.height)
            {
                string line = _wrappedLines[i];
                if (line.Length > 0)
                {
                    Raylib.DrawText(line, (int)clipRect.x + 1, (int)drawY + 1, bodyFont, Color.BLACK);
                    Raylib.DrawText(line, (int)clipRect.x, (int)drawY, bodyFont, Color.RAYWHITE);
                }
            }
            drawY += lineHeight;
        }

        Raylib.EndScissorMode();

        if (maxScroll > 0f && trackRect.height > 8f)
        {
            Raylib.DrawRectangleRec(trackRect, new Color(15, 16, 22, 180));
            float totalContentH = _wrappedLines.Count * lineHeight;
            float thumbRatio = Math.Clamp(clipRect.height / MathF.Max(1f, totalContentH), 0.12f, 1f);
            float thumbH = MathF.Max(14f, trackRect.height * thumbRatio);
            float thumbY = trackRect.y + ((_scrollOffsetPx / maxScroll) * (trackRect.height - thumbH));

            Rectangle thumbRect = new Rectangle(trackRect.x + 1f, thumbY, MathF.Max(4f, trackRect.width - 2f), thumbH);
            Color thumbCol = _isDraggingScrollbar ? Color.GOLD : accentCol;
            Raylib.DrawRectangleRec(thumbRect, thumbCol);
        }
    }

    private float ComputeMaxScroll(Rectangle bounds, out Rectangle trackRect) =>
        ComputeMaxScroll(bounds, out trackRect, out _, out _, out _);

    private float ComputeMaxScroll(Rectangle bounds, out Rectangle trackRect, out Rectangle clipRect, out int bodyFont, out int lineHeight)
    {
        float pad = 10f;
        float scrollbarW = 10f;
        float sh = Raylib.GetScreenHeight();
        int titleFont = UIRenderer.FitFontSize(Title, Math.Max(14, (int)(sh * TitleFontSizePct)), 11, MathF.Max(24f, bounds.width - pad * 2f));
        float headerH = string.IsNullOrWhiteSpace(Title) ? 6f : (titleFont + 14f);

        bodyFont = Math.Max(12, (int)(sh * FontSizePct));
        lineHeight = (int)MathF.Ceiling(bodyFont * 1.32f);

        clipRect = new Rectangle(
            bounds.x + pad,
            bounds.y + headerH,
            MathF.Max(20f, bounds.width - (pad * 2f) - scrollbarW - 4f),
            MathF.Max(16f, bounds.height - headerH - 8f)
        );

        trackRect = new Rectangle(
            bounds.x + bounds.width - scrollbarW - 4f,
            clipRect.y,
            scrollbarW,
            clipRect.height
        );

        EnsureWrapped(clipRect.width, bodyFont);

        float contentH = _wrappedLines.Count * lineHeight;
        return MathF.Max(0f, contentH - clipRect.height);
    }

    private void EnsureWrapped(float maxWidth, int fontSize)
    {
        if (!_layoutDirty &&
            _cachedFontSize == fontSize &&
            MathF.Abs(_cachedWrapWidth - maxWidth) < 1f)
        {
            return;
        }

        _layoutDirty = false;
        _cachedFontSize = fontSize;
        _cachedWrapWidth = maxWidth;
        _wrappedLines.Clear();

        if (string.IsNullOrEmpty(_text))
            return;

        string[] paragraphs = _text.Replace("\r\n", "\n").Split('\n');
        foreach (string rawPara in paragraphs)
        {
            if (rawPara.Length == 0)
            {
                _wrappedLines.Add("");
                continue;
            }

            string[] words = rawPara.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                _wrappedLines.Add("");
                continue;
            }

            string current = "";
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
                if (Raylib.MeasureText(candidate, fontSize) <= maxWidth)
                {
                    current = candidate;
                }
                else
                {
                    if (!string.IsNullOrEmpty(current))
                    {
                        _wrappedLines.Add(current);
                        current = "";
                    }

                    if (Raylib.MeasureText(word, fontSize) <= maxWidth)
                    {
                        current = word;
                    }
                    else
                    {
                        SplitLongWord(word, fontSize, maxWidth, ref current);
                    }
                }
            }

            if (!string.IsNullOrEmpty(current))
                _wrappedLines.Add(current);
        }
    }

    private void SplitLongWord(string word, int fontSize, float maxWidth, ref string remainder)
    {
        string chunk = "";
        for (int i = 0; i < word.Length; i++)
        {
            string next = chunk + word[i];
            if (!string.IsNullOrEmpty(chunk) && Raylib.MeasureText(next, fontSize) > maxWidth)
            {
                _wrappedLines.Add(chunk);
                chunk = word[i].ToString();
            }
            else
            {
                chunk = next;
            }
        }
        remainder = chunk;
    }

    public void Dispose()
    {
        if (OwnsTexture && BackgroundTexture is { id: > 0 } tex)
        {
            Raylib.UnloadTexture(tex);
        }
        BackgroundTexture = null;
        OwnsTexture = false;
    }
}
