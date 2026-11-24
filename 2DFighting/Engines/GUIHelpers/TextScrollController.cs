using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

public class TextScrollController
{
    private readonly List<string> _wrappedLines = new();
    private bool _layoutDirty = true;
    private string _cachedText = "";
    private int _cachedFontSize = -1;
    private float _cachedWrapWidth = -1f;
    private float _scrollOffsetPx = 0f;

    public bool IsDraggingScrollbar { get; private set; }

    public void NotifyTextChanged()
    {
        _scrollOffsetPx = 0f;
        _layoutDirty = true;
    }

    public void Update(Rectangle bounds, string title, string text, float titleFontSizePct, float bodyFontSizePct, bool isHovered)
    {
        Vector2 mousePos = Raylib.GetMousePosition();
        float maxScroll = ComputeScrollGeometry(bounds, title, text, titleFontSizePct, bodyFontSizePct, out Rectangle trackRect, out _, out _, out _, out _);

        if (isHovered)
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (MathF.Abs(wheel) > 0.001f)
            {
                float step = Math.Max(22f, Raylib.GetScreenHeight() * bodyFontSizePct * 2.2f);
                _scrollOffsetPx = Math.Clamp(_scrollOffsetPx - (wheel * step), 0f, maxScroll);
            }
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_LEFT) && Raylib.CheckCollisionPointRec(mousePos, trackRect))
        {
            IsDraggingScrollbar = true;
        }

        if (!Raylib.IsMouseButtonDown(MouseButton.MOUSE_BUTTON_LEFT))
        {
            IsDraggingScrollbar = false;
        }

        if (IsDraggingScrollbar && trackRect.height > 1f)
        {
            float t = Math.Clamp((mousePos.Y - trackRect.y) / trackRect.height, 0f, 1f);
            _scrollOffsetPx = t * maxScroll;
        }

        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx, 0f, maxScroll);
    }

    public void Draw(Rectangle bounds, string title, string text, float titleFontSizePct, float bodyFontSizePct, Color textColor, Vector3 accentColor)
    {
        Color accentCol = new Color(
            (byte)Math.Clamp(accentColor.X * 255f, 0f, 255f),
            (byte)Math.Clamp(accentColor.Y * 255f, 0f, 255f),
            (byte)Math.Clamp(accentColor.Z * 255f, 0f, 255f),
            (byte)255
        );

        float pad = 10f;
        float sh = Raylib.GetScreenHeight();
        int titleFont = UIRenderer.FitFontSize(title, Math.Max(14, (int)(sh * titleFontSizePct)), 11, MathF.Max(24f, bounds.width - pad * 2f));
        float headerH = string.IsNullOrWhiteSpace(title) ? 6f : (titleFont + 10f);

        if (!string.IsNullOrWhiteSpace(title))
        {
            UIRenderer.DrawTextShadowed(title, bounds.x + pad, bounds.y + 6f, titleFont, Color.GOLD, shadowOffset: 1f);
            Raylib.DrawLine(
                (int)(bounds.x + pad),
                (int)(bounds.y + headerH),
                (int)(bounds.x + bounds.width - pad),
                (int)(bounds.y + headerH),
                new Color(140, 150, 175, 130)
            );
        }

        float maxScroll = ComputeScrollGeometry(bounds, title, text, titleFontSizePct, bodyFontSizePct, out Rectangle trackRect, out Rectangle clipRect, out int bodyFont, out int lineHeight, out float contentH);
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
                    UIRenderer.DrawTextShadowed(line, clipRect.x, drawY, bodyFont, textColor, shadowOffset: 1f);
                }
            }
            drawY += lineHeight;
        }

        Raylib.EndScissorMode();

        if (trackRect.height > 8f)
        {
            Raylib.DrawRectangleRec(trackRect, new Color(15, 16, 22, 180));
            float thumbRatio = Math.Clamp(clipRect.height / MathF.Max(1f, contentH), 0.12f, 1f);
            float thumbH = MathF.Max(14f, trackRect.height * thumbRatio);
            float scrollNorm = maxScroll > 0f ? (_scrollOffsetPx / maxScroll) : 0f;
            float thumbY = trackRect.y + (scrollNorm * (trackRect.height - thumbH));

            Rectangle thumbRect = new Rectangle(trackRect.x + 1f, thumbY, MathF.Max(4f, trackRect.width - 2f), thumbH);
            Color thumbCol = IsDraggingScrollbar ? Color.GOLD : accentCol;
            Raylib.DrawRectangleRec(thumbRect, thumbCol);
        }
    }

    private float ComputeScrollGeometry(
        Rectangle bounds,
        string title,
        string text,
        float titleFontSizePct,
        float bodyFontSizePct,
        out Rectangle trackRect,
        out Rectangle clipRect,
        out int bodyFont,
        out int lineHeight,
        out float contentH)
    {
        float pad = 10f;
        float scrollbarW = 10f;
        float sh = Raylib.GetScreenHeight();
        int titleFont = UIRenderer.FitFontSize(title, Math.Max(14, (int)(sh * titleFontSizePct)), 11, MathF.Max(24f, bounds.width - pad * 2f));
        float headerH = string.IsNullOrWhiteSpace(title) ? 6f : (titleFont + 14f);

        bodyFont = Math.Max(12, (int)(sh * bodyFontSizePct));
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

        EnsureWrapped(text, clipRect.width, bodyFont);

        contentH = _wrappedLines.Count * lineHeight;
        return MathF.Max(0f, contentH - clipRect.height);
    }

    private void EnsureWrapped(string text, float maxWidth, int fontSize)
    {
        string normalized = text ?? "";
        if (!_layoutDirty &&
            string.Equals(_cachedText, normalized, StringComparison.Ordinal) &&
            _cachedFontSize == fontSize &&
            MathF.Abs(_cachedWrapWidth - maxWidth) < 1f)
        {
            return;
        }

        _layoutDirty = false;
        _cachedText = normalized;
        _cachedFontSize = fontSize;
        _cachedWrapWidth = maxWidth;
        _wrappedLines.Clear();

        if (string.IsNullOrEmpty(normalized))
            return;

        string[] paragraphs = normalized.Replace("\r\n", "\n").Split('\n');
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
                if (UIRenderer.MeasureText(candidate, fontSize) <= maxWidth)
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

                    if (UIRenderer.MeasureText(word, fontSize) <= maxWidth)
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
            if (!string.IsNullOrEmpty(chunk) && UIRenderer.MeasureText(next, fontSize) > maxWidth)
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
}
