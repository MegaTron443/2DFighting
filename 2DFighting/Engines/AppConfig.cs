using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Raylib_cs;

public class AppConfig
{
    public DisplaySettings Display { get; set; } = new();
    public ControlSettings Controls { get; set; } = new();
    public SoundSettings Sound { get; set; } = new();
    public AIDifficulty AIDifficulty { get; set; } = AIDifficulty.Medium;

    [JsonPropertyName("steam_id")]
    public ulong SteamId { get; set; } = 0;

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
            var loaded = JsonSerializer.Deserialize<AppConfig>(json, options) ?? new AppConfig();
            if (loaded.Controls.P1.TagSwitch == KeyboardKey.KEY_NULL)
                loaded.Controls.P1.TagSwitch = KeyboardKey.KEY_R;
            if (loaded.Controls.P1.Assist == KeyboardKey.KEY_NULL)
                loaded.Controls.P1.Assist = KeyboardKey.KEY_Y;
            if (loaded.Controls.P2.TagSwitch == KeyboardKey.KEY_NULL)
                loaded.Controls.P2.TagSwitch = KeyboardKey.KEY_U;
            if (loaded.Controls.P2.Assist == KeyboardKey.KEY_NULL)
                loaded.Controls.P2.Assist = KeyboardKey.KEY_O;
            return loaded;
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

public class SoundSettings
{
    public int Volume { get; set; } = 100;
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
    public PlayerControls P1 { get; set; } = new PlayerControls
    {
        Left = KeyboardKey.KEY_A,
        Right = KeyboardKey.KEY_D,
        Jump = KeyboardKey.KEY_W,
        Crouch = KeyboardKey.KEY_S,
        KeyTop =  KeyboardKey.KEY_T,
        KeyRight = KeyboardKey.KEY_H,
        KeyLeft = KeyboardKey.KEY_F,
        KeyBottom = KeyboardKey.KEY_G,
        TagSwitch = KeyboardKey.KEY_R,
        Assist = KeyboardKey.KEY_Y
    };

    public PlayerControls P2 { get; set; } = new PlayerControls
    {
        Left = KeyboardKey.KEY_LEFT,
        Right = KeyboardKey.KEY_RIGHT,
        Jump = KeyboardKey.KEY_UP,
        Crouch = KeyboardKey.KEY_DOWN,
        KeyTop =  KeyboardKey.KEY_I,
        KeyRight = KeyboardKey.KEY_L,
        KeyLeft = KeyboardKey.KEY_J,
        KeyBottom = KeyboardKey.KEY_K,
        TagSwitch = KeyboardKey.KEY_U,
        Assist = KeyboardKey.KEY_O
    };
}

public class PlayerControls
{
    public KeyboardKey Left { get; set; }
    public KeyboardKey Right { get; set; }
    public KeyboardKey Jump { get; set; }
    public KeyboardKey Crouch { get; set; }

    public KeyboardKey KeyTop { get; set; }
    public KeyboardKey KeyRight { get; set; }
    public KeyboardKey KeyLeft { get; set; }
    public KeyboardKey KeyBottom { get; set; }

    public KeyboardKey TagSwitch { get; set; }
    public KeyboardKey Assist { get; set; }
}