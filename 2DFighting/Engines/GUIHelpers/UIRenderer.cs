using System.Numerics;
using Raylib_cs;

public struct UIBarStyle
{
    public Color BackgroundColor;
    public Color FillColor;
    public Color BorderColor;
    public float BorderThickness;
    public bool RightToLeft;

    public Texture2D? BackgroundTexture;
    public Texture2D? FillTexture;
    public Texture2D? FrameTexture;
}

public static class UIRenderer
{
    public static Rectangle GetPctRect(float xPct, float yPct, float wPct, float hPct)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        return new Rectangle(sw * xPct, sh * yPct, sw * wPct, sh * hPct);
    }

    public static void DrawProgressBar(Rectangle bounds, float fillPct, UIBarStyle style)
    {
        fillPct = Math.Clamp(fillPct, 0f, 1f);

        if (style.BackgroundTexture is { id: > 0 } bgTex)
        {
            Raylib.DrawTexturePro(bgTex, new Rectangle(0, 0, bgTex.width, bgTex.height), bounds, Vector2.Zero, 0f, Color.WHITE);
        }
        else
        {
            Raylib.DrawRectangleRec(bounds, style.BackgroundColor);
        }

        float fillWidth = bounds.width * fillPct;
        float fillX = style.RightToLeft ? (bounds.x + bounds.width - fillWidth) : bounds.x;
        Rectangle fillRect = new Rectangle(fillX, bounds.y, fillWidth, bounds.height);

        if (fillWidth > 0f)
        {
            if (style.FillTexture is { id: > 0 } fillTex)
            {
                float srcW = fillTex.width * fillPct;
                float srcX = style.RightToLeft ? (fillTex.width - srcW) : 0f;
                Rectangle srcRect = new Rectangle(srcX, 0, srcW, fillTex.height);
                Raylib.DrawTexturePro(fillTex, srcRect, fillRect, Vector2.Zero, 0f, style.FillColor);
            }
            else
            {
                Raylib.DrawRectangleRec(fillRect, style.FillColor);
            }
        }

        if (style.FrameTexture is { id: > 0 } frameTex)
        {
            Raylib.DrawTexturePro(frameTex, new Rectangle(0, 0, frameTex.width, frameTex.height), bounds, Vector2.Zero, 0f, Color.WHITE);
        }
        else if (style.BorderThickness > 0f)
        {
            Raylib.DrawRectangleLinesEx(bounds, style.BorderThickness, style.BorderColor);
        }
    }
}