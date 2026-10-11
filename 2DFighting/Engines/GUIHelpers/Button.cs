using System.Numerics;
using Raylib_cs;

public class Button
{
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
    public TextAlignment Alignment { get; set; } = TextAlignment.Center;

    public bool IsHovered { get; private set; }

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

        if (IsHovered && Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_LEFT))
        {
            OnClick?.Invoke(context);
        }
    }

    public void Draw()
    {
        Rectangle bounds = GetPixelBounds();
        int fontSize = Math.Max(12, (int)(Raylib.GetScreenHeight() * FontSizePct));

        Color bg = IsHovered ? HoverColor : NormalColor;
        Raylib.DrawRectangleRec(bounds, bg);

        if (Icon.HasValue && Icon.Value.id > 0)
        {
            Texture2D tex = Icon.Value;
            Rectangle srcRect = new Rectangle(0, 0, tex.width, tex.height);
            
            float bottomPadding = (Alignment == TextAlignment.Bottom) ? (fontSize + 8f) : 4f;
            Rectangle destRect = new Rectangle(
                bounds.x + 2, 
                bounds.y + 2, 
                bounds.width - 4, 
                Math.Max(4, bounds.height - bottomPadding - 2)
            );
            Raylib.DrawTexturePro(tex, srcRect, destRect, Vector2.Zero, 0f, Color.WHITE);
        }

        if (IsSelected)
        {
            Raylib.DrawRectangleLinesEx(bounds, 3, SelectedColor);
        }
        else if (IsHovered)
        {
            Raylib.DrawRectangleLinesEx(bounds, 2, Color.SKYBLUE);
        }
        else
        {
            Raylib.DrawRectangleLinesEx(bounds, 1, Color.GRAY);
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

        Raylib.DrawText(Text, textX, textY, fontSize, TextColor);
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