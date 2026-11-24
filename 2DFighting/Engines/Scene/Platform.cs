using System;
using System.IO;
using System.Numerics;
using MoonSharp.Interpreter;
using Raylib_cs;

[MoonSharpUserData]
public class Platform : IDisposable
{
    public string Name { get; }
    public float XPct { get; set; }
    public float YPct { get; set; }
    public float WidthPct { get; set; }
    public float ThicknessPct { get; set; } = 0.016f;
    public bool IsOneWay { get; set; } = true;

    public Texture2D? Texture { get; set; }
    public bool OwnsTexture { get; private set; }
    public RenderMaterial Material { get; set; } = new() { ShaderId = BuiltInShaders.UiButtonId };
    public Vector3 AccentColor { get; set; } = new Vector3(0.45f, 0.78f, 1.0f);

    public Platform(string name, float xPct, float yPct, float widthPct, float thicknessPct = 0.016f, bool isOneWay = true)
    {
        Name = name;
        XPct = xPct;
        YPct = yPct;
        WidthPct = widthPct;
        ThicknessPct = thicknessPct;
        IsOneWay = isOneWay;
    }

    public bool LoadTexture(string relativeOrFullPath)
    {
        if (OwnsTexture && Texture is { id: > 0 } oldTex)
        {
            Raylib.UnloadTexture(oldTex);
        }
        Texture = null;
        OwnsTexture = false;

        string? resolved = ModManager.ResolveFile(relativeOrFullPath)
                        ?? ModManager.ResolveFile("Assets", "Scenes", relativeOrFullPath)
                        ?? ModManager.ResolveFile("Assets", "UI", relativeOrFullPath)
                        ?? (File.Exists(relativeOrFullPath) ? relativeOrFullPath : null);

        if (resolved == null || !File.Exists(resolved))
            return false;

        Texture2D loaded = Raylib.LoadTexture(resolved);
        if (loaded.id == 0)
            return false;

        Raylib.SetTextureFilter(loaded, TextureFilter.TEXTURE_FILTER_BILINEAR);
        Raylib.SetTextureWrap(loaded, TextureWrap.TEXTURE_WRAP_CLAMP);
        Texture = loaded;
        OwnsTexture = true;
        return true;
    }

    public Rectangle GetPixelBounds()
    {
        float sw = Raylib.GetScreenWidth();
        float sh = Raylib.GetScreenHeight();
        return new Rectangle(
            XPct * sw,
            YPct * sh,
            WidthPct * sw,
            MathF.Max(8f, ThicknessPct * sh)
        );
    }

    public void Draw()
    {
        DrawPanel(keepShaderBound: false);
        DrawLandingEdge();
    }

    public void DrawPanel(bool keepShaderBound = false)
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
            Texture,
            hover: 0.35f,
            isSelected: false,
            AccentColor,
            new Color(32, 36, 52, 235),
            accentCol,
            keepShaderBound
        );
    }

    public void DrawLandingEdge()
    {
        Rectangle bounds = GetPixelBounds();
        Color accentCol = new Color(
            (byte)Math.Clamp(AccentColor.X * 255f, 0f, 255f),
            (byte)Math.Clamp(AccentColor.Y * 255f, 0f, 255f),
            (byte)Math.Clamp(AccentColor.Z * 255f, 0f, 255f),
            (byte)255
        );

        // Bright top-surface landing edge
        Raylib.DrawRectangle(
            (int)bounds.x + 2,
            (int)bounds.y,
            Math.Max(2, (int)bounds.width - 4),
            2,
            accentCol
        );
    }

    public void Dispose()
    {
        if (OwnsTexture && Texture is { id: > 0 } tex)
        {
            Raylib.UnloadTexture(tex);
        }
        Texture = null;
        OwnsTexture = false;
    }
}
