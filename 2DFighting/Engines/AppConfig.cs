using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Raylib_cs;

public class AppConfig
{
    public DisplaySettings Display { get; set; } = new();
    public ControlSettings Controls { get; set; } = new();

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            var defaultConfig = new AppConfig();
            Save(path, defaultConfig);
            return defaultConfig;
        }

        try
        {
            string json = File.ReadAllText(path);
            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() } 
            };
            return JsonSerializer.Deserialize<AppConfig>(json, options) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public static void Save(string path, AppConfig config)
    {
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        string json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(path, json);
    }
}

public class DisplaySettings
{
    public int ScreenWidth { get; set; } = 800;
    public int ScreenHeight { get; set; } = 600;
    public int TargetFps { get; set; } = 60;
    public string Title { get; set; } = "2D Fighting Game";
}

public class ControlSettings
{
    public KeyboardKey Pause { get; set; } = KeyboardKey.KEY_EQUAL;
    public KeyboardKey Restart { get; set; } = KeyboardKey.KEY_R;
    public PlayerControls P1 { get; set; } = new PlayerControls
    {
        Left = KeyboardKey.KEY_A,
        Right = KeyboardKey.KEY_D,
        Jump = KeyboardKey.KEY_W,
        Crouch = KeyboardKey.KEY_S,
        LightPunch = KeyboardKey.KEY_J,
        HeavyKick = KeyboardKey.KEY_K
    };

    public PlayerControls P2 { get; set; } = new PlayerControls
    {
        Left = KeyboardKey.KEY_LEFT,
        Right = KeyboardKey.KEY_RIGHT,
        Jump = KeyboardKey.KEY_UP,
        Crouch = KeyboardKey.KEY_DOWN,
        LightPunch = KeyboardKey.KEY_KP_1,
        HeavyKick = KeyboardKey.KEY_KP_2
    };
}

public class PlayerControls
{
    public KeyboardKey Left { get; set; }
    public KeyboardKey Right { get; set; }
    public KeyboardKey Jump { get; set; }
    public KeyboardKey Crouch { get; set; }
    public KeyboardKey LightPunch { get; set; }
    public KeyboardKey HeavyKick { get; set; }
}