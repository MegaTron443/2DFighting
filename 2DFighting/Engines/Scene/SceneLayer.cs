using System.Numerics;
using MoonSharp.Interpreter;
using Raylib_cs;

[MoonSharpUserData]
public class SceneLayer : IDisposable
{
    public string Name { get; }
    public RenderMaterial Material { get; set; } = new();
    public Texture2D Texture { get; private set; }
    public bool OwnsTexture { get; private set; }

    public Vector2 Parallax { get; set; } = Vector2.One;
    public Vector2 OffsetPercent { get; set; } = Vector2.Zero;
    public Color Tint { get; set; } = Color.WHITE;
    public bool IsForeground { get; set; } = false;
    public bool Visible { get; set; } = true;

    private static Texture2D _fallbackWhite;

    public SceneLayer(string name)
    {
        Name = name;
    }

    public bool LoadTexture(string relativeOrFullPath)
    {
        UnloadTexture();

        string? resolved = ModManager.ResolveFile(relativeOrFullPath)
                        ?? ModManager.ResolveFile("Assets", "Scenes", relativeOrFullPath)
                        ?? (File.Exists(relativeOrFullPath) ? relativeOrFullPath : null);

        if (resolved == null || !File.Exists(resolved))
        {
            DiagnosticsLogger.LogSilentIssueOnce(
                $"missing_scene_tex_{relativeOrFullPath}",
                $"Scene texture '{relativeOrFullPath}' not found.",
                LogSeverity.Warning
            );
            return false;
        }

        Texture2D loaded = Raylib.LoadTexture(resolved);
        if (loaded.id == 0)
            return false;

        Raylib.SetTextureFilter(loaded, TextureFilter.TEXTURE_FILTER_BILINEAR);
        Raylib.SetTextureWrap(loaded, TextureWrap.TEXTURE_WRAP_CLAMP);
        Texture = loaded;
        OwnsTexture = true;
        return true;
    }

    public void SetTexture(Texture2D texture, bool takeOwnership = false)
    {
        UnloadTexture();
        Texture = texture;
        OwnsTexture = takeOwnership;
    }

    public void Draw(Rectangle arenaBounds, Vector2 cameraTarget)
    {
        if (!Visible) return;

        bool hasTexture = Texture.id > 0;
        if (!hasTexture && !Material.HasShader) return;

        Texture2D tex = hasTexture ? Texture : GetFallbackWhite();
        bool shaderStarted = Material.Begin();

        Vector2 center = new(
            arenaBounds.x + arenaBounds.width * 0.5f,
            arenaBounds.y + arenaBounds.height * 0.5f
        );
        Vector2 parallaxShift = (cameraTarget - center) * (Vector2.One - Parallax);

        Rectangle src = new(0, 0, tex.width, tex.height);
        Rectangle dst = new(
            arenaBounds.x + parallaxShift.X + (OffsetPercent.X * arenaBounds.width),
            arenaBounds.y + parallaxShift.Y + (OffsetPercent.Y * arenaBounds.height),
            arenaBounds.width,
            arenaBounds.height
        );

        Raylib.DrawTexturePro(tex, src, dst, Vector2.Zero, 0f, Tint);

        Material.End(shaderStarted);
    }

    private static Texture2D GetFallbackWhite()
    {
        if (_fallbackWhite.id == 0)
        {
            Image img = Raylib.GenImageColor(1, 1, Color.WHITE);
            _fallbackWhite = Raylib.LoadTextureFromImage(img);
            Raylib.UnloadImage(img);
        }
        return _fallbackWhite;
    }

    public static void DisposeShared()
    {
        if (_fallbackWhite.id > 0)
        {
            Raylib.UnloadTexture(_fallbackWhite);
            _fallbackWhite = default;
        }
    }

    private void UnloadTexture()
    {
        if (OwnsTexture && Texture.id > 0)
        {
            Raylib.UnloadTexture(Texture);
            Texture = default;
            OwnsTexture = false;
        }
    }

    public void Dispose() => UnloadTexture();
}