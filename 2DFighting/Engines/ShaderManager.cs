using System.Numerics;
using Raylib_cs;

public static class ShaderManager
{
    private static readonly Dictionary<string, Shader> _shaders = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<(int shaderId, string uniform), int> _uniformLocations = new();

    public static void Initialize()
    {
        Dispose();
        RegisterBuiltInShaders();
    }

    public static Shader LoadOrGet(string id, string? fragRelativePath = null, string? vertRelativePath = null)
    {
        if (_shaders.TryGetValue(id, out Shader existing))
            return existing;

        string? fragPath = fragRelativePath != null
            ? (ModManager.ResolveFile(fragRelativePath) ?? (File.Exists(fragRelativePath) ? fragRelativePath : null))
            : (ModManager.ResolveFile("Assets", "Shaders", $"{id}.fs") ?? ModManager.ResolveFile("Shaders", $"{id}.fs"));

        string? vertPath = vertRelativePath != null
            ? (ModManager.ResolveFile(vertRelativePath) ?? (File.Exists(vertRelativePath) ? vertRelativePath : null))
            : null;

        if (fragPath != null && File.Exists(fragPath))
        {
            Shader loaded = Raylib.LoadShader(vertPath, fragPath);
            _shaders[id] = loaded;
            return loaded;
        }

        DiagnosticsLogger.LogSilentIssueOnce(
            $"missing_shader_{id}",
            $"Shader '{id}' not found on disk; returning default shader.",
            LogSeverity.Warning
        );

        return default;
    }

    public static bool TryGetShader(string id, out Shader shader)
    {
        if (string.IsNullOrEmpty(id) || id.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            shader = default;
            return false;
        }

        if (_shaders.TryGetValue(id, out shader) && shader.id > 0)
            return true;

        shader = LoadOrGet(id);
        return shader.id > 0;
    }

    public static int GetLocation(Shader shader, string uniformName)
    {
        var key = ((int)shader.id, uniformName);
        if (_uniformLocations.TryGetValue(key, out int loc))
            return loc;

        loc = Raylib.GetShaderLocation(shader, uniformName);
        _uniformLocations[key] = loc;
        return loc;
    }

    public static unsafe void SetFloat(Shader shader, string name, float value)
    {
        int loc = GetLocation(shader, name);
        if (loc >= 0) Raylib.SetShaderValue(shader, loc, &value, ShaderUniformDataType.SHADER_UNIFORM_FLOAT);
    }

    public static unsafe void SetVec2(Shader shader, string name, Vector2 value)
    {
        int loc = GetLocation(shader, name);
        if (loc >= 0) Raylib.SetShaderValue(shader, loc, &value, ShaderUniformDataType.SHADER_UNIFORM_VEC2);
    }

    public static unsafe void SetVec3(Shader shader, string name, Vector3 value)
    {
        int loc = GetLocation(shader, name);
        if (loc >= 0) Raylib.SetShaderValue(shader, loc, &value, ShaderUniformDataType.SHADER_UNIFORM_VEC3);
    }

    public static unsafe void SetVec4(Shader shader, string name, Vector4 value)
    {
        int loc = GetLocation(shader, name);
        if (loc >= 0) Raylib.SetShaderValue(shader, loc, &value, ShaderUniformDataType.SHADER_UNIFORM_VEC4);
    }

    private static void RegisterBuiltInShaders()
    {
        string postFxFrag = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;
uniform vec2 u_ShakeOffset;      // UV offset for directional screen shake
uniform float u_ChromaticSplit;  // RGB split intensity (0.0 = off, 0.015 = heavy)
uniform float u_PinchAmount;     // Black hole radial pinch (-0.5 to 0.8)
uniform vec2 u_PinchCenter;      // Normalized screen center (default 0.5, 0.5)
uniform float u_ImpactMode;      // 0 = Off, 1 = 2-Tone Monochrome, 2 = Inverted Void
uniform vec3 u_TintColor;        // Palette tint (default 1, 1, 1)
uniform float u_PaletteMix;      // 0.0 = Normal, 1.0 = Full Cosmic Palette Shift

void main()
{
    vec2 center = (u_PinchCenter.x == 0.0 && u_PinchCenter.y == 0.0) ? vec2(0.5, 0.5) : u_PinchCenter;
    
    // Slight zoom when shaking so edges never clamp/smear
    float zoom = (length(u_ShakeOffset) > 0.0001) ? 0.965 : 1.0;
    vec2 uv = (fragTexCoord - center) * zoom + center + u_ShakeOffset;

    // Radial Gravity Pinch / Shockwave Warp
    if (abs(u_PinchAmount) > 0.001)
    {
        vec2 delta = uv - center;
        float dist = length(delta);
        float factor = 1.0 - (u_PinchAmount * exp(-dist * 4.5));
        uv = center + delta * factor;
    }

    uv = clamp(uv, vec2(0.001), vec2(0.999));

    // Chromatic Aberration RGB Split
    vec2 splitOffset = vec2(u_ChromaticSplit, u_ChromaticSplit * 0.4);
    float r = texture(texture0, clamp(uv + splitOffset, 0.001, 0.999)).r;
    float g = texture(texture0, uv).g;
    float b = texture(texture0, clamp(uv - splitOffset, 0.001, 0.999)).b;
    float a = texture(texture0, uv).a;
    vec3 col = vec3(r, g, b);

    float luma = dot(col, vec3(0.299, 0.587, 0.114));

    // Impact Frames (1 = Stark Black/White, 2 = Inverted Void)
    if (u_ImpactMode >= 1.5)
    {
        col = vec3(1.0) - col;
        col = mix(col, vec3(1.0 - luma) * vec3(0.65, 0.15, 1.0), 0.55);
    }
    else if (u_ImpactMode >= 0.5)
    {
        float bw = step(0.48, luma);
        col = mix(vec3(0.05, 0.0, 0.12), vec3(1.0, 0.95, 1.0), bw);
    }

    // Cosmic / Chromatic Palette Shift
    if (u_PaletteMix > 0.001)
    {
        vec3 tint = (length(u_TintColor) < 0.01) ? vec3(0.55, 0.15, 0.95) : u_TintColor;
        vec3 voidMapped = pow(luma, 1.25) * tint * 1.4;
        col = mix(col, voidMapped, clamp(u_PaletteMix, 0.0, 1.0));
    }

    finalColor = vec4(col, a) * fragColor;
}";

        string spriteFrag = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;
uniform vec3 u_PrimaryColor;   // Main tint for light-gray bands
uniform vec3 u_SecondaryColor; // Shadow/mid-tone tint
uniform vec3 u_GlowColor;      // Pure-white energy band tint
uniform float u_PortalMode;    // > 0.5 renders screen-space cosmic starfield inside sprite
uniform float u_Time;
uniform vec2 u_Resolution;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

void main()
{
    vec4 tex = texture(texture0, fragTexCoord);
    if (tex.a < 0.01) discard;

    float luma = dot(tex.rgb, vec3(0.299, 0.587, 0.114));

    // Screen-Space Cosmic Portal Mode (for Gravity Samurai head/void parts)
    if (u_PortalMode > 0.5 && luma > 0.25)
    {
        vec2 res = (u_Resolution.x > 0.0) ? u_Resolution : vec2(800.0, 600.0);
        vec2 screenUV = gl_FragCoord.xy / res;
        vec2 cosmicUV = screenUV * 6.0 + vec2(u_Time * 0.15, u_Time * 0.08);
        float star = step(0.985, hash(floor(cosmicUV * 18.0)));
        float swirl = 0.5 + 0.5 * sin(cosmicUV.x * 3.0 + sin(cosmicUV.y * 2.5 + u_Time * 2.0));
        vec3 nebula = mix(vec3(0.05, 0.01, 0.12), vec3(0.55, 0.12, 0.90), swirl) + vec3(star);
        finalColor = vec4(nebula, tex.a) * fragColor;
        return;
    }

    vec3 cDark = (length(u_SecondaryColor) > 0.01) ? u_SecondaryColor : vec3(0.12, 0.10, 0.18);
    vec3 cMid  = (length(u_PrimaryColor) > 0.01)   ? u_PrimaryColor   : vec3(0.75, 0.75, 0.82);
    vec3 cGlow = (length(u_GlowColor) > 0.01)      ? u_GlowColor      : vec3(0.85, 0.40, 1.00);

    vec3 mapped;
    if (luma < 0.5)
        mapped = mix(cDark, cMid, luma * 2.0);
    else if (luma < 0.88)
        mapped = mix(cMid, cGlow, (luma - 0.5) / 0.38);
    else
        mapped = cGlow * 1.25;

    finalColor = vec4(mapped, tex.a) * fragColor;
}";

        _shaders["postfx_cosmic"] = Raylib.LoadShaderFromMemory(null, postFxFrag);
        _shaders["sprite_lut"] = Raylib.LoadShaderFromMemory(null, spriteFrag);
    }

    public static void Dispose()
    {
        foreach (var kv in _shaders)
        {
            if (kv.Value.id > 0)
            {
                Raylib.UnloadShader(kv.Value);
            }
        }
        _shaders.Clear();
        _uniformLocations.Clear();
    }
}