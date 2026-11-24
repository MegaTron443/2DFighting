using System;
using System.IO;
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
    private static Font _uiFont;
    private static bool _ownsCustomFont;

    public static Font ActiveFont => _uiFont.texture.id > 0 ? _uiFont : Raylib.GetFontDefault();

    public static void InitializeFont()
    {
        DisposeFont();

        string? fontPath = FindUiFontPath();
        if (fontPath != null && File.Exists(fontPath))
        {
            Font loaded = Raylib.LoadFontEx(fontPath, 64, null!, 0);
            if (loaded.texture.id > 0)
            {
                Raylib.SetTextureFilter(loaded.texture, TextureFilter.TEXTURE_FILTER_BILINEAR);
                Raylib.SetTextureWrap(loaded.texture, TextureWrap.TEXTURE_WRAP_CLAMP);
                _uiFont = loaded;
                _ownsCustomFont = true;
                return;
            }
        }

        _uiFont = Raylib.GetFontDefault();
        _ownsCustomFont = false;
    }

    private static string? FindUiFontPath()
    {
        string? resolved = ModManager.ResolveFile("Assets", "Fonts", "ui_font.ttf")
                        ?? ModManager.ResolveFile("Assets", "Fonts", "ui_font.otf")
                        ?? ModManager.ResolveFile("Assets", "Fonts", "font.ttf")
                        ?? ModManager.ResolveFile("Assets", "UI", "font.ttf");
        if (resolved != null) return resolved;

        var fontDirs = ModManager.ResolveDirectories("Assets", "Fonts");
        for (int i = 0; i < fontDirs.Count; i++)
        {
            foreach (string ext in new[] { "*.ttf", "*.otf" })
            {
                string[] files = Directory.GetFiles(fontDirs[i], ext, SearchOption.TopDirectoryOnly);
                if (files.Length > 0) return files[0];
            }
        }

        try
        {
            string sysFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            if (!string.IsNullOrEmpty(sysFonts) && Directory.Exists(sysFonts))
            {
                string[] candidates = { "bahnschrift.ttf", "segoeuib.ttf", "segoeui.ttf", "arialbd.ttf" };
                for (int i = 0; i < candidates.Length; i++)
                {
                    string candidate = Path.Combine(sysFonts, candidates[i]);
                    if (File.Exists(candidate)) return candidate;
                }
            }
        }
        catch
        {
            // Ignore inaccessible system font directories
        }

        return null;
    }

    public static void DisposeFont()
    {
        if (_ownsCustomFont && _uiFont.texture.id > 0)
        {
            Raylib.UnloadFont(_uiFont);
        }
        _uiFont = default;
        _ownsCustomFont = false;
    }

    public static float GetFontSpacing(float fontSize) =>
        _ownsCustomFont ? MathF.Max(0.5f, fontSize * 0.035f) : MathF.Max(1f, fontSize * 0.08f);

    public static int MeasureText(string text, int fontSize)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)MathF.Ceiling(Raylib.MeasureTextEx(ActiveFont, text, fontSize, GetFontSpacing(fontSize)).X);
    }

    public static void DrawText(string text, float x, float y, int fontSize, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        Raylib.DrawTextEx(ActiveFont, text, new Vector2(x, y), fontSize, GetFontSpacing(fontSize), color);
    }

    public static void DrawTextShadowed(string text, float x, float y, int fontSize, Color color, float shadowOffset = 1.5f)
    {
        if (string.IsNullOrEmpty(text)) return;
        float spacing = GetFontSpacing(fontSize);
        Font font = ActiveFont;
        Raylib.DrawTextEx(font, text, new Vector2(x + shadowOffset, y + shadowOffset), fontSize, spacing, new Color(0, 0, 0, 210));
        Raylib.DrawTextEx(font, text, new Vector2(x, y), fontSize, spacing, color);
    }

    public static Rectangle GetPctRect(float xPct, float yPct, float wPct, float hPct)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        return new Rectangle(sw * xPct, sh * yPct, sw * wPct, sh * hPct);
    }

    public static void DrawProgressBar(Rectangle bounds, float fillPct, UIBarStyle style) =>
        DrawProgressBar(bounds, fillPct, -1f, style);

    public static void DrawProgressBar(Rectangle bounds, float fillPct, float trailPct, UIBarStyle style)
    {
        fillPct = Math.Clamp(fillPct, 0f, 1f);

        bool hasCustomTex = style.BackgroundTexture is { id: > 0 } ||
                            style.FillTexture is { id: > 0 } ||
                            style.FrameTexture is { id: > 0 };

        if (!hasCustomTex && ShaderManager.BeginShader(BuiltInShaders.HudBarId, out Shader barShader))
        {
            ShaderManager.SetVec2(barShader, "u_Size", new Vector2(bounds.width, bounds.height));
            ShaderManager.SetFloat(barShader, "u_FillPct", fillPct);
            ShaderManager.SetFloat(barShader, "u_TrailPct", trailPct >= 0f ? Math.Clamp(trailPct, 0f, 1f) : -1f);
            ShaderManager.SetFloat(barShader, "u_RightToLeft", style.RightToLeft ? 1f : 0f);
            ShaderManager.SetFloat(barShader, "u_SkewDir", style.RightToLeft ? -1f : 1f);
            ShaderManager.SetVec4(barShader, "u_BgColor", ColorToVec4(style.BackgroundColor));
            ShaderManager.SetVec4(barShader, "u_FillColor", ColorToVec4(style.FillColor));
            ShaderManager.SetVec4(barShader, "u_BorderColor", ColorToVec4(style.BorderColor));
            ShaderManager.SetFloat(barShader, "u_BorderThickness", style.BorderThickness);

            Raylib.DrawTexturePro(
                SharedTextures.WhitePixel,
                new Rectangle(0, 0, 1, 1),
                bounds,
                Vector2.Zero,
                0f,
                Color.WHITE
            );
            ShaderManager.EndShader();
            return;
        }

        if (style.BackgroundTexture is { id: > 0 } bgTex)
        {
            Raylib.DrawTexturePro(bgTex, new Rectangle(0, 0, bgTex.width, bgTex.height), bounds, Vector2.Zero, 0f, Color.WHITE);
        }
        else
        {
            Raylib.DrawRectangleRec(bounds, style.BackgroundColor);
        }

        if (trailPct > fillPct)
        {
            float clampedTrail = Math.Clamp(trailPct, 0f, 1f);
            float trailW = bounds.width * clampedTrail;
            float trailX = style.RightToLeft ? (bounds.x + bounds.width - trailW) : bounds.x;
            Raylib.DrawRectangleRec(new Rectangle(trailX, bounds.y, trailW, bounds.height), new Color(255, 215, 90, 255));
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

    private static Vector4 ColorToVec4(Color c) => new(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);

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
        measuredWidth = MeasureText(text, size);
        while (size > minSize && measuredWidth > maxWidth)
        {
            size--;
            measuredWidth = MeasureText(text, size);
        }
        return size;
    }
}