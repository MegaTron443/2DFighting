using System.Numerics;
using Raylib_cs;

public static class SceneManager
{
    public static Scene? CurrentScene { get; private set; }

    public static void Initialize()
    {
        var defaultScene = new Scene("default");

        var bg = defaultScene.AddLayer(
            "background",
            texturePath: null,
            shaderId: BuiltInShaders.SceneDefaultId,
            isForeground: false
        );
        bg.Parallax = Vector2.One;
        bg.Material.SetFloat("u_GroundYPct", PhysicsEngine.GROUND_Y_PCT);

        LoadScene(defaultScene);
    }

    public static void LoadScene(Scene scene)
    {
        CurrentScene?.Dispose();
        CurrentScene = scene;
    }

    public static void DrawBackground(Vector2 cameraTarget) => CurrentScene?.DrawBackground(cameraTarget);
    public static void DrawForeground(Vector2 cameraTarget) => CurrentScene?.DrawForeground(cameraTarget);

    public static void Dispose()
    {
        CurrentScene?.Dispose();
        CurrentScene = null;
        SceneLayer.DisposeShared();
    }


    public static SceneLayer? AddLayerFromLua(string name, string? texturePath = null, string? shaderId = null, bool isForeground = false)
    {
        return CurrentScene?.AddLayer(name, texturePath, shaderId ?? "none", isForeground);
    }

    public static SceneLayer? GetLayerFromLua(string name) => CurrentScene?.GetLayer(name);

    public static void SetLayerTextureFromLua(string layerName, string texturePath)
    {
        CurrentScene?.GetLayer(layerName)?.LoadTexture(texturePath);
    }

    public static void SetLayerShaderFromLua(string layerName, string shaderId)
    {
        var layer = CurrentScene?.GetLayer(layerName);
        if (layer != null)
            layer.Material.ShaderId = string.IsNullOrEmpty(shaderId) ? "none" : shaderId;
    }

    public static void SetLayerFloatFromLua(string layerName, string uniform, float value)
    {
        CurrentScene?.GetLayer(layerName)?.Material.SetFloat(uniform, value);
    }

    public static void SetLayerVec2FromLua(string layerName, string uniform, float x, float y)
    {
        CurrentScene?.GetLayer(layerName)?.Material.SetVec2(uniform, x, y);
    }

    public static void SetLayerColorFromLua(string layerName, string uniform, float r, float g, float b)
    {
        CurrentScene?.GetLayer(layerName)?.Material.SetColor(uniform, r, g, b);
    }

    public static void SetLayerTintFromLua(string layerName, int r, int g, int b, int a)
    {
        var layer = CurrentScene?.GetLayer(layerName);
        if (layer != null)
            layer.Tint = new Color((byte)r, (byte)g, (byte)b, (byte)a);
    }
}