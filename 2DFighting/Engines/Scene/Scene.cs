using System.Numerics;
using MoonSharp.Interpreter;
using Raylib_cs;

[MoonSharpUserData]
public class Scene : IDisposable
{
    public string Id { get; }
    public Rectangle Bounds
    {
        get
        {
            float sw = Raylib.GetScreenWidth();
            float sh = Raylib.GetScreenHeight();
            float minZoom = MathF.Max(0.1f, CameraManager.Config.MinZoom);

            float extraW = MathF.Max(0f, (sw / minZoom) - sw);
            float extraH = MathF.Max(0f, (sh / minZoom) - sh);

            float padX = extraW * 0.6f;
            float padY = extraH * 0.6f;

            return new Rectangle(-padX, -padY, sw + padX * 2f, sh + padY * 2f);
        }
    }

    public Color ClearColor { get; set; } = Color.RAYWHITE;

    private readonly List<SceneLayer> _layers = new();

    public Scene(string id)
    {
        Id = id;
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

    public void DrawBackground(Vector2 cameraTarget)
    {
        Rectangle b = Bounds;
        float groundUvY = (PhysicsEngine.GroundY - b.y) / b.height;

        foreach (var layer in _layers)
        {
            if (!layer.IsForeground)
            {
                if (layer.Material.ShaderId == BuiltInShaders.SceneDefaultId)
                    layer.Material.SetFloat("u_GroundYPct", groundUvY);

                layer.Draw(b, cameraTarget);
            }
        }
    }

    public void DrawForeground(Vector2 cameraTarget)
    {
        foreach (var layer in _layers)
            if (layer.IsForeground) layer.Draw(Bounds, cameraTarget);
    }

    public void Dispose()
    {
        foreach (var layer in _layers)
            layer.Dispose();
        _layers.Clear();
    }
}