using System.Numerics;
using Raylib_cs;

public class Button
{
    private static double _lastGlobalClickTime = -1.0;
    private const double ClickCooldown = 0.05;

    public float XPct { get; set; }
    public float YPct { get; set; }
    public float WidthPct { get; set; }
    public float HeightPct { get; set; }
    public string Text { get; set; }
    public float FontSizePct { get; set; } = 0.025f;
    public Action<ButtonContext>? OnClick { get; set; }
    public Texture2D? Icon { get; set; } = null;
    public bool IsSelected { get; set; } = false;

    public Color NormalColor { get; set; } = new Color(40, 40, 40, 200);
    public Color HoverColor { get; set; } = new Color(70, 70, 70, 240);
    public Color SelectedColor { get; set; } = Color.GOLD;
    public Color TextColor { get; set; } = Color.WHITE;
    public Vector3 AccentColor { get; set; } = new Vector3(0.45f, 0.78f, 1.0f);
    public TextAlignment Alignment { get; set; } = TextAlignment.Center;

    public bool IsHovered { get; private set; }
    private float _hoverLerp = 0f;

    public Button(float xPct, float yPct, float widthPct, float heightPct, string text, Action<ButtonContext>? onClick = null, Texture2D? icon = null, bool isCentered = false)
    {
        WidthPct = widthPct;
        HeightPct = heightPct;
        XPct = isCentered ? xPct - (widthPct / 2f) : xPct;
        YPct = isCentered ? yPct - (heightPct / 2f) : yPct;
        Text = text;
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

    public Button WithAlignment(TextAlignment alignment)
    {
        Alignment = alignment;
        return this;
    }

    public void Update(ButtonContext context)
    {
        Rectangle bounds = GetPixelBounds();
        Vector2 mousePos = Raylib.GetMousePosition();

        IsHovered = Raylib.CheckCollisionPointRec(mousePos, bounds);

        float dt = Raylib.GetFrameTime();
        float targetHover = IsHovered ? 1f : 0f;
        _hoverLerp = Math.Clamp(_hoverLerp + ((targetHover - _hoverLerp) * Math.Min(1f, dt * 14f)), 0f, 1f);

        if (IsHovered && Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_LEFT))
        {
            double now = Raylib.GetTime();
            if (_lastGlobalClickTime < 0.0 || (now - _lastGlobalClickTime) >= ClickCooldown)
            {
                _lastGlobalClickTime = now;
                OnClick?.Invoke(context);
            }
        }
    }

    public void Draw()
    {
        Rectangle bounds = GetPixelBounds();
        int fontSize = Math.Max(12, (int)(Raylib.GetScreenHeight() * FontSizePct));

        if (ShaderManager.TryGetShader("ui_button", out Shader btnShader))
        {
            Raylib.BeginShaderMode(btnShader);
            ShaderManager.SetVec2(btnShader, "u_Size", new Vector2(bounds.width, bounds.height));
            ShaderManager.SetFloat(btnShader, "u_Time", (float)Raylib.GetTime());
            ShaderManager.SetFloat(btnShader, "u_Hover", _hoverLerp);
            ShaderManager.SetFloat(btnShader, "u_Selected", IsSelected ? 1.0f : 0.0f);
            ShaderManager.SetVec3(btnShader, "u_AccentColor", AccentColor);

            Raylib.DrawTexturePro(SharedTextures.WhitePixel, new Rectangle(0, 0, 1, 1), bounds, Vector2.Zero, 0f, Color.WHITE);

            Raylib.EndShaderMode();
        }
        else
        {
            Color bg = IsHovered ? HoverColor : NormalColor;
            Raylib.DrawRectangleRec(bounds, bg);
            Raylib.DrawRectangleLinesEx(bounds, IsSelected ? 3 : (IsHovered ? 2 : 1), IsSelected ? SelectedColor : (IsHovered ? Color.SKYBLUE : Color.GRAY));
        }

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

        int textWidth = Raylib.MeasureText(Text, fontSize);
        int textX = (int)(bounds.x + (bounds.width - textWidth) / 2f);

        int textY = Alignment switch
        {
            TextAlignment.Top    => (int)(bounds.y + 4),
            TextAlignment.Bottom => (int)(bounds.y + bounds.height - fontSize - 4),
            TextAlignment.Center => (int)(bounds.y + (bounds.height - fontSize) / 2f),
            _                    => (int)(bounds.y + (bounds.height - fontSize) / 2f)
        };

        Raylib.DrawText(Text, textX + 1, textY + 1, fontSize, Color.BLACK);
        Color finalTextCol = IsHovered ? Color.GOLD : TextColor;
        Raylib.DrawText(Text, textX, textY, fontSize, finalTextCol);
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