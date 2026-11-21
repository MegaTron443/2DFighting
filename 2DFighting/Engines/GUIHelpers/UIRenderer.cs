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

    public static void DrawStyledPanel(
        Rectangle bounds,
        RenderMaterial material,
        Texture2D? texture,
        float hover,
        bool isSelected,
        Vector3 accentColor,
        Color fallbackBg,
        Color fallbackBorder,
        bool keepShaderBound = false)
    {
        material.SetVec2("u_Size", bounds.width, bounds.height);
        material.SetFloat("u_Hover", hover);
        material.SetFloat("u_Selected", isSelected ? 1.0f : 0.0f);
        material.SetColor("u_AccentColor", accentColor.X, accentColor.Y, accentColor.Z);

        bool isBuiltInUi = string.Equals(material.ShaderId, BuiltInShaders.UiButtonId, StringComparison.OrdinalIgnoreCase);
        bool shaderStarted = material.Begin();

        if (shaderStarted)
        {
            Texture2D quadTex = (!isBuiltInUi && texture is { id: > 0 } customTex) ? customTex : SharedTextures.WhitePixel;
            Raylib.DrawTexturePro(quadTex, new Rectangle(0, 0, quadTex.width, quadTex.height), bounds, Vector2.Zero, 0f, Color.WHITE);

            bool hasInset = isBuiltInUi && texture is { id: > 0 };
            material.End(true, keepShaderBound && !hasInset);

            if (hasInset && texture is { id: > 0 } insetTex)
            {
                Rectangle inset = new Rectangle(bounds.x + 3f, bounds.y + 3f, MathF.Max(2f, bounds.width - 6f), MathF.Max(2f, bounds.height - 6f));
                Raylib.DrawTexturePro(insetTex, new Rectangle(0, 0, insetTex.width, insetTex.height), inset, Vector2.Zero, 0f, new Color(255, 255, 255, 215));
            }
            return;
        }

        ShaderManager.EndShader();

        if (texture is { id: > 0 } fallbackTex)
        {
            Raylib.DrawTexturePro(fallbackTex, new Rectangle(0, 0, fallbackTex.width, fallbackTex.height), bounds, Vector2.Zero, 0f, Color.WHITE);
        }
        else
        {
            Raylib.DrawRectangleRec(bounds, fallbackBg);
        }

        material.End(false, keepShaderBound: false);
        float borderWidth = isSelected ? 3f : (hover > 0.25f ? 2f : 1f);
        Raylib.DrawRectangleLinesEx(bounds, borderWidth, fallbackBorder);
    }

    public static int FitFontSize(string text, int preferredSize, int minSize, float maxWidth) =>
        FitFontSize(text, preferredSize, minSize, maxWidth, out _);

    public static int FitFontSize(string text, int preferredSize, int minSize, float maxWidth, out int measuredWidth)
    {
        int size = Math.Max(minSize, preferredSize);
        measuredWidth = Raylib.MeasureText(text, size);
        while (size > minSize && measuredWidth > maxWidth)
        {
            size--;
            measuredWidth = Raylib.MeasureText(text, size);
        }
        return size;
    }
}