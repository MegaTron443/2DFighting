using System;
using System.IO;
using System.Numerics;
using Raylib_cs;

public class Button : IDisposable
{
    private static double _lastGlobalClickTime = -1.0;
    private const double ClickCooldown = 0.05;

    private string _text = "";
    private TextScrollController? _scrollController;

    public float XPct { get; set; }
    public float YPct { get; set; }
    public float WidthPct { get; set; }
    public float HeightPct { get; set; }
    public string Title { get; set; } = "";
    public float TitleFontSizePct { get; set; } = 0.022f;

    public string Text
    {
        get => _text;
        set
        {
            string normalized = value ?? "";
            if (!string.Equals(_text, normalized, StringComparison.Ordinal))
            {
                _text = normalized;
                _scrollController?.NotifyTextChanged();
            }
        }
    }

    public float FontSizePct { get; set; } = 0.025f;
    public Action<ButtonContext>? OnClick { get; set; }
    public Texture2D? Icon { get; set; } = null;
    public Texture2D? BackgroundTexture { get; set; } = null;
    public bool OwnsBackgroundTexture { get; private set; }
    public RenderMaterial Material { get; set; } = new() { ShaderId = BuiltInShaders.UiButtonId };
    public bool IsSelected { get; set; } = false;
    public bool IsScrollable { get; set; } = false;

    public Color NormalColor { get; set; } = new Color(40, 40, 40, 200);
    public Color HoverColor { get; set; } = new Color(70, 70, 70, 240);
    public Color SelectedColor { get; set; } = Color.GOLD;
    public Color TextColor { get; set; } = Color.WHITE;
    public Vector3 AccentColor { get; set; } = new Vector3(0.45f, 0.78f, 1.0f);
    public TextAlignment Alignment { get; set; } = TextAlignment.Center;

    public bool IsHovered { get; private set; }
    public bool CapturesPointer => IsHovered || (_scrollController?.IsDraggingScrollbar ?? false);
    private float _hoverLerp = 0f;

    public Button(float xPct, float yPct, float widthPct, float heightPct, string text, Action<ButtonContext>? onClick = null, Texture2D? icon = null, bool isCentered = false)
    {
        WidthPct = widthPct;
        HeightPct = heightPct;
        XPct = isCentered ? xPct - (widthPct / 2f) : xPct;
        YPct = isCentered ? yPct - (heightPct / 2f) : yPct;
        _text = text ?? "";
        OnClick = onClick;
        Icon = icon;
    }

    public static Button Create(string text) => new Button(0, 0, 0, 0, text, isCentered: true);

    public Button At(float xPct, float yPct)
    {
        XPct = xPct - (WidthPct / 2f);
        YPct = yPct - (HeightPct / 2f);
        return this;
    }

    public Button WithSize(float widthPct, float heightPct)
    {
        WidthPct = widthPct;
        HeightPct = heightPct;
        return this;
    }

    public Button WithIcon(Texture2D icon)
    {
        Icon = icon;
        return this;
    }

    public Button WithAccent(float r, float g, float b)
    {
        AccentColor = (r > 1f || g > 1f || b > 1f)
            ? new Vector3(r / 255f, g / 255f, b / 255f)
            : new Vector3(r, g, b);
        return this;
    }

    public Button Action(Action<ButtonContext> action)
    {
        OnClick = action;
        return this;
    }

    public Button WithAlignment(TextAlignment alignment)
    {
        Alignment = alignment;
        return this;
    }

    public Button WithTitle(string title)
    {
        Title = title ?? "";
        return this;
    }

    public Button WithFontSize(float fontSizePct)
    {
        FontSizePct = fontSizePct;
        return this;
    }

    public Button WithScrolling(bool enabled = true)
    {
        IsScrollable = enabled;
        if (enabled && _scrollController == null)
        {
            _scrollController = new TextScrollController();
        }
        return this;
    }

    public bool LoadBackgroundTexture(string relativeOrFullPath)
    {
        if (OwnsBackgroundTexture && BackgroundTexture is { id: > 0 } oldTex)
        {
            Raylib.UnloadTexture(oldTex);
        }
        BackgroundTexture = null;
        OwnsBackgroundTexture = false;

        string? resolved = ModManager.ResolveFile(relativeOrFullPath)
                        ?? ModManager.ResolveFile("Assets", "UI", relativeOrFullPath)
                        ?? (File.Exists(relativeOrFullPath) ? relativeOrFullPath : null);

        if (resolved == null || !File.Exists(resolved))
            return false;

        Texture2D loaded = Raylib.LoadTexture(resolved);
        if (loaded.id == 0)
            return false;

        BackgroundTexture = loaded;
        OwnsBackgroundTexture = true;
        return true;
    }

    public Rectangle GetPixelBounds()
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        return new Rectangle(
            (int)(sw * XPct),
            (int)(sh * YPct),
            (int)(sw * WidthPct),
            (int)(sh * HeightPct)
        );
    }

    public void Update(ButtonContext context)
    {
        Rectangle bounds = GetPixelBounds();
        Vector2 mousePos = Raylib.GetMousePosition();

        IsHovered = Raylib.CheckCollisionPointRec(mousePos, bounds);

        float dt = Raylib.GetFrameTime();
        float targetHover = IsHovered ? 1f : 0f;
        _hoverLerp = Math.Clamp(_hoverLerp + ((targetHover - _hoverLerp) * Math.Min(1f, dt * 14f)), 0f, 1f);

        if (IsScrollable)
        {
            _scrollController ??= new TextScrollController();
            _scrollController.Update(bounds, Title, _text, TitleFontSizePct, FontSizePct, IsHovered);
        }

        if (OnClick != null &&
            IsHovered &&
            !(_scrollController?.IsDraggingScrollbar ?? false) &&
            Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_LEFT))
        {
            double now = Raylib.GetTime();
            if (_lastGlobalClickTime < 0.0 || (now - _lastGlobalClickTime) >= ClickCooldown)
            {
                _lastGlobalClickTime = now;
                OnClick.Invoke(context);
            }
        }
    }

    public void Draw()
    {
        Rectangle bounds = GetPixelBounds();
        Color bg = IsHovered ? HoverColor : NormalColor;
        Color border = IsSelected ? SelectedColor : (IsHovered ? Color.SKYBLUE : Color.GRAY);
        UIRenderer.DrawStyledPanel(bounds, Material, BackgroundTexture, _hoverLerp, IsSelected, AccentColor, bg, border);

        if (IsScrollable)
        {
            _scrollController ??= new TextScrollController();
            _scrollController.Draw(bounds, Title, _text, TitleFontSizePct, FontSizePct, Color.RAYWHITE, AccentColor);
            return;
        }

        int preferredFont = Math.Max(12, (int)(Raylib.GetScreenHeight() * FontSizePct));
        int fontSize = UIRenderer.FitFontSize(_text, preferredFont, 10, MathF.Max(20f, bounds.width - 8f));

        if (Icon.HasValue && Icon.Value.id > 0)
        {
            Texture2D tex = Icon.Value;
            Rectangle srcRect = new Rectangle(0, 0, tex.width, tex.height);

            float bottomPadding = (Alignment == TextAlignment.Bottom) ? (fontSize + 8f) : 4f;
            Rectangle destRect = new Rectangle(
                bounds.x + 4,
                bounds.y + 4,
                bounds.width - 8,
                Math.Max(4, bounds.height - bottomPadding - 4)
            );

            Raylib.DrawTexturePro(tex, srcRect, destRect, Vector2.Zero, 0f, Color.WHITE);
        }

        int textWidth = Raylib.MeasureText(_text, fontSize);
        int textX = (int)(bounds.x + (bounds.width - textWidth) / 2f);

        int textY = Alignment switch
        {
            TextAlignment.Top    => (int)(bounds.y + 4),
            TextAlignment.Bottom => (int)(bounds.y + bounds.height - fontSize - 4),
            TextAlignment.Center => (int)(bounds.y + (bounds.height - fontSize) / 2f),
            _                    => (int)(bounds.y + (bounds.height - fontSize) / 2f)
        };

        Raylib.DrawText(_text, textX + 1, textY + 1, fontSize, Color.BLACK);
        Color finalTextCol = IsHovered ? Color.GOLD : TextColor;
        Raylib.DrawText(_text, textX, textY, fontSize, finalTextCol);
    }

    public void Dispose()
    {
        if (OwnsBackgroundTexture && BackgroundTexture is { id: > 0 } tex)
        {
            Raylib.UnloadTexture(tex);
        }
        BackgroundTexture = null;
        OwnsBackgroundTexture = false;
    }
}

public class ButtonContext
{
    public GameData Data { get; }
    public Action<Player?> SetP1 { get; }
    public Action<Player?> SetP2 { get; }

    public ButtonContext(GameData data, Action<Player?> setP1, Action<Player?> setP2)
    {
        Data = data;
        SetP1 = setP1;
        SetP2 = setP2;
    }
}
