using System.Numerics;
using Raylib_cs;

public static class SceneManager
{
    public static Scene? CurrentScene { get; private set; }

    public static void Initialize()
    {
        LoadMatchScene();
    }

    public static void LoadMatchScene()
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

        var midground = defaultScene.AddLayer(
            "midground_parallax",
            texturePath: null,
            shaderId: BuiltInShaders.SceneParallaxId,
            isForeground: false
        );
        midground.Parallax = new Vector2(0.35f, 0.95f);
        midground.Material.SetFloat("u_GroundYPct", PhysicsEngine.GROUND_Y_PCT);

        var foreground = defaultScene.AddLayer(
            "foreground_atmosphere",
            texturePath: null,
            shaderId: BuiltInShaders.SceneForegroundId,
            isForeground: true
        );
        foreground.Parallax = new Vector2(1.22f, 1.02f);
        foreground.Material.SetFloat("u_GroundYPct", PhysicsEngine.GROUND_Y_PCT);

        LoadScene(defaultScene);
    }

    public static void LoadLobbyScene()
    {
        var lobbyScene = new Scene("lobby")
        {
            ExtraLeftScreens = 1.0f,
            ExtraRightScreens = 1.0f,
            ExtraBottomScreens = 1.0f,
            PlayableLeftPct = -0.97f,
            PlayableRightPct = 1.97f,
            PlayableTopPct = -0.25f,
            PlayableBottomPct = 2.0f
        };

        var bg = lobbyScene.AddLayer(
            "background",
            texturePath: null,
            shaderId: BuiltInShaders.SceneDefaultId,
            isForeground: false
        );
        bg.Parallax = Vector2.One;
        bg.Material.SetFloat("u_GroundYPct", PhysicsEngine.GROUND_Y_PCT);

        string? lobbyTex = ModManager.ResolveFile("Assets", "Scenes", "lobby_bg.png");
        if (lobbyTex != null)
        {
            var artLayer = lobbyScene.AddLayer("lobby_backdrop", lobbyTex, "none", isForeground: false);
            artLayer.Parallax = new Vector2(0.85f, 1.0f);
        }

        string? floorTex = ModManager.ResolveFile("Assets", "Scenes", "lobby_floor.png");
        if (floorTex != null)
        {
            var floorLayer = lobbyScene.AddLayer("lobby_floor", floorTex, "none", isForeground: false);
            floorLayer.Parallax = Vector2.One;
        }

        string? platformTex = ModManager.ResolveFile("Assets", "Scenes", "lobby_platform.png");
        Vector3 offlineAccent = new Vector3(1.0f, 0.62f, 0.28f);
        Vector3 mainAccent = new Vector3(0.45f, 0.78f, 1.0f);
        Vector3 hatchAccent = new Vector3(0.35f, 1.0f, 0.65f);
        Vector3 devAccent = new Vector3(0.0f, 0.95f, 0.88f);

        // Left Offline Room 3-tier jumpable platforms
        lobbyScene.AddPlatform("offline_low_left",    -0.89f, 0.760f, 0.22f, texturePath: platformTex, accentColor: offlineAccent);
        lobbyScene.AddPlatform("offline_low_right",   -0.33f, 0.760f, 0.22f, texturePath: platformTex, accentColor: offlineAccent);
        lobbyScene.AddPlatform("offline_mid_left",    -0.78f, 0.670f, 0.22f, texturePath: platformTex, accentColor: offlineAccent);
        lobbyScene.AddPlatform("offline_mid_right",   -0.44f, 0.670f, 0.22f, texturePath: platformTex, accentColor: offlineAccent);
        lobbyScene.AddPlatform("offline_high_left",   -0.93f, 0.580f, 0.20f, texturePath: platformTex, accentColor: offlineAccent);
        lobbyScene.AddPlatform("offline_high_center", -0.61f, 0.580f, 0.22f, texturePath: platformTex, accentColor: offlineAccent);
        lobbyScene.AddPlatform("offline_high_right",  -0.27f, 0.580f, 0.20f, texturePath: platformTex, accentColor: offlineAccent);

        // Center Online Hub 3-tier jumpable platforms + drop-through Training Lab floor hatch
        lobbyScene.AddPlatform("lobby_low_left",    0.11f, 0.760f, 0.22f, texturePath: platformTex, accentColor: mainAccent);
        lobbyScene.AddPlatform("lobby_low_right",   0.67f, 0.760f, 0.22f, texturePath: platformTex, accentColor: mainAccent);
        lobbyScene.AddPlatform("lobby_mid_left",    0.22f, 0.670f, 0.22f, texturePath: platformTex, accentColor: mainAccent);
        lobbyScene.AddPlatform("lobby_mid_right",   0.56f, 0.670f, 0.22f, texturePath: platformTex, accentColor: mainAccent);
        lobbyScene.AddPlatform("lobby_high_left",   0.07f, 0.580f, 0.20f, texturePath: platformTex, accentColor: mainAccent);
        lobbyScene.AddPlatform("lobby_high_center", 0.39f, 0.580f, 0.22f, texturePath: platformTex, accentColor: mainAccent);
        lobbyScene.AddPlatform("lobby_high_right",  0.73f, 0.580f, 0.20f, texturePath: platformTex, accentColor: mainAccent);
        lobbyScene.AddPlatform("training_hatch",    0.415f, PhysicsEngine.GROUND_Y_PCT, 0.17f, thicknessPct: 0.018f, isOneWay: true, accentColor: hatchAccent, texturePath: platformTex);

        // Right Developer Alcove 3-tier jumpable platforms
        lobbyScene.AddPlatform("dev_low_left",      1.15f, 0.760f, 0.22f, texturePath: platformTex, accentColor: devAccent);
        lobbyScene.AddPlatform("dev_low_right",     1.63f, 0.760f, 0.22f, texturePath: platformTex, accentColor: devAccent);
        lobbyScene.AddPlatform("dev_mid_left",      1.24f, 0.670f, 0.23f, texturePath: platformTex, accentColor: devAccent);
        lobbyScene.AddPlatform("dev_mid_right",     1.53f, 0.670f, 0.23f, texturePath: platformTex, accentColor: devAccent);
        lobbyScene.AddPlatform("dev_high_left",     1.13f, 0.580f, 0.20f, texturePath: platformTex, accentColor: devAccent);
        lobbyScene.AddPlatform("dev_high_center",   1.40f, 0.580f, 0.20f, texturePath: platformTex, accentColor: devAccent);
        lobbyScene.AddPlatform("dev_high_right",    1.67f, 0.580f, 0.20f, texturePath: platformTex, accentColor: devAccent);

        LoadScene(lobbyScene);
    }

    public static void LoadScene(Scene scene)
    {
        CurrentScene?.Dispose();
        CurrentScene = scene;
    }

    public static void DrawBackground(Vector2 cameraTarget) => CurrentScene?.DrawBackground(cameraTarget);
    public static void DrawPlatforms() => CurrentScene?.DrawPlatforms();
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