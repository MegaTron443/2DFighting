using System.Numerics;
using MoonSharp.Interpreter;
using Raylib_cs;

[MoonSharpUserData]
public class Scene : IDisposable
{
    public string Id { get; }
    public float ExtraLeftScreens { get; set; } = 0f;
    public float ExtraRightScreens { get; set; } = 0f;
    public float ExtraTopScreens { get; set; } = 0f;
    public float ExtraBottomScreens { get; set; } = 0f;
    public float PlayableLeftPct { get; set; } = PhysicsEngine.LEFT_BOUNDARY_PCT;
    public float PlayableRightPct { get; set; } = PhysicsEngine.RIGHT_BOUNDARY_PCT;
    public float PlayableTopPct { get; set; } = -0.25f;
    public float PlayableBottomPct { get; set; } = 1.0f;
    public bool HasOfflineWing => ExtraLeftScreens > 0.1f;
    public bool HasDeveloperAlcove => ExtraRightScreens > 0.1f;
    public bool HasTrainingLab => ExtraBottomScreens > 0.1f;

    public Rectangle Bounds
    {
        get
        {
            float sw = PhysicsEngine.LogicalWidth;
            float sh = PhysicsEngine.LogicalHeight;
            float minZoom = MathF.Max(0.1f, CameraManager.Config.MinZoom);

            float extraW = MathF.Max(0f, (sw / minZoom) - sw);
            float extraH = MathF.Max(0f, (sh / minZoom) - sh);

            float padX = extraW * 0.6f;
            float padY = extraH * 0.6f;

            float leftExt = ExtraLeftScreens * sw;
            float rightExt = ExtraRightScreens * sw;
            float topExt = ExtraTopScreens * sh;
            float bottomExt = ExtraBottomScreens * sh;

            return new Rectangle(
                -padX - leftExt,
                -padY - topExt,
                sw + padX * 2f + leftExt + rightExt,
                sh + padY * 2f + topExt + bottomExt
            );
        }
    }

    public Color ClearColor { get; set; } = Color.RAYWHITE;

    private readonly List<SceneLayer> _layers = new();
    public List<Platform> Platforms { get; } = new();

    public Scene(string id)
    {
        Id = id;
    }

    public Platform AddPlatform(
        string name,
        float xPct,
        float yPct,
        float widthPct,
        float thicknessPct = 0.016f,
        bool isOneWay = true,
        Vector3? accentColor = null,
        string? texturePath = null)
    {
        var platform = new Platform(name, xPct, yPct, widthPct, thicknessPct, isOneWay);
        if (accentColor.HasValue)
            platform.AccentColor = accentColor.Value;
        if (!string.IsNullOrEmpty(texturePath))
            platform.LoadTexture(texturePath);

        Platforms.Add(platform);
        return platform;
    }

    public void DrawPlatforms()
    {
        for (int i = 0; i < Platforms.Count; i++)
        {
            Platforms[i].DrawPanel(keepShaderBound: true);
        }
        ShaderManager.EndShader();
        for (int i = 0; i < Platforms.Count; i++)
        {
            Platforms[i].DrawLandingEdge();
        }
    }

    public SceneLayer AddLayer(string name, string? texturePath = null, string shaderId = "none", bool isForeground = false)
    {
        var layer = new SceneLayer(name) { IsForeground = isForeground };
        layer.Material.ShaderId = shaderId;
        if (!string.IsNullOrEmpty(texturePath))
            layer.LoadTexture(texturePath);

        _layers.Add(layer);
        return layer;
    }

    public SceneLayer? GetLayer(string name) =>
        _layers.FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static bool IsHorizonSyncedShader(string shaderId) =>
        shaderId == BuiltInShaders.SceneDefaultId ||
        shaderId == BuiltInShaders.SceneParallaxId ||
        shaderId == BuiltInShaders.SceneForegroundId;

    public void DrawBackground(Vector2 cameraTarget)
    {
        Rectangle b = Bounds;
        float groundUvY = (PhysicsEngine.GroundY - b.y) / b.height;

        foreach (var layer in _layers)
        {
            if (!layer.IsForeground)
            {
                if (IsHorizonSyncedShader(layer.Material.ShaderId))
                    layer.Material.SetFloat("u_GroundYPct", groundUvY);

                layer.Draw(b, cameraTarget);
            }
        }
    }

    public void DrawForeground(Vector2 cameraTarget)
    {
        Rectangle b = Bounds;
        float groundUvY = (PhysicsEngine.GroundY - b.y) / b.height;

        foreach (var layer in _layers)
        {
            if (layer.IsForeground)
            {
                if (IsHorizonSyncedShader(layer.Material.ShaderId))
                    layer.Material.SetFloat("u_GroundYPct", groundUvY);

                layer.Draw(b, cameraTarget);
            }
        }
    }

    public void Dispose()
    {
        foreach (var layer in _layers)
            layer.Dispose();
        _layers.Clear();

        foreach (var platform in Platforms)
            platform.Dispose();
        Platforms.Clear();
    }
}