using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;

public static class ShaderManager
{
    private static readonly Dictionary<string, Shader> _shaders = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<(int shaderId, string uniform), int> _uniformLocations = new();
    private static readonly Dictionary<uint, uint> _shaderGlobalUploadFrame = new();

    private static string? _currentShaderId = null;
    private static uint _currentShaderGpuId = 0;
    private static uint _frameIndex = 0;
    private static float _frameTime = 0f;
    private static Vector2 _frameResolution;
    private static Vector2 _frameCameraPos;
    private static int _frameUniformUploadCount = 0;

    public static string? CurrentShaderId => _currentShaderId;
    public static int FrameUniformUploadCount => _frameUniformUploadCount;

    public static void BeginFrame()
    {
        _frameIndex++;
        _frameTime = (float)Raylib.GetTime();
        _frameResolution = new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        _frameCameraPos = CameraManager.Target;
        _frameUniformUploadCount = 0;
    }

    public static bool BeginShader(string shaderId, out Shader shader)
    {
        if (!TryGetShader(shaderId, out shader))
        {
            EndShader();
            return false;
        }

        if (_currentShaderGpuId == shader.id)
        {
            Rlgl.rlDrawRenderBatchActive();
            UploadGlobalUniformsOncePerFrame(shader);
            return true;
        }

        if (_currentShaderGpuId != 0)
        {
            Raylib.EndShaderMode();
        }

        Raylib.BeginShaderMode(shader);
        _currentShaderId = shaderId;
        _currentShaderGpuId = shader.id;
        UploadGlobalUniformsOncePerFrame(shader);
        return true;
    }

    public static void EndShader()
    {
        if (_currentShaderGpuId != 0)
        {
            Raylib.EndShaderMode();
            _currentShaderId = null;
            _currentShaderGpuId = 0;
        }
    }

    public static void UploadGlobalUniformsOncePerFrame(Shader shader)
    {
        if (_shaderGlobalUploadFrame.TryGetValue(shader.id, out uint lastFrame) && lastFrame == _frameIndex)
            return;

        _shaderGlobalUploadFrame[shader.id] = _frameIndex;
        SetFloat(shader, "u_Time", _frameTime);
        SetVec2(shader, "u_Resolution", _frameResolution);
        SetVec2(shader, "u_CameraPos", _frameCameraPos);
    }

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
        if (loc >= 0)
        {
            Raylib.SetShaderValue(shader, loc, &value, ShaderUniformDataType.SHADER_UNIFORM_FLOAT);
            _frameUniformUploadCount++;
        }
    }

    public static unsafe void SetVec2(Shader shader, string name, Vector2 value)
    {
        int loc = GetLocation(shader, name);
        if (loc >= 0)
        {
            Raylib.SetShaderValue(shader, loc, &value, ShaderUniformDataType.SHADER_UNIFORM_VEC2);
            _frameUniformUploadCount++;
        }
    }

    public static unsafe void SetVec3(Shader shader, string name, Vector3 value)
    {
        int loc = GetLocation(shader, name);
        if (loc >= 0)
        {
            Raylib.SetShaderValue(shader, loc, &value, ShaderUniformDataType.SHADER_UNIFORM_VEC3);
            _frameUniformUploadCount++;
        }
    }

    public static unsafe void SetVec4(Shader shader, string name, Vector4 value)
    {
        int loc = GetLocation(shader, name);
        if (loc >= 0)
        {
            Raylib.SetShaderValue(shader, loc, &value, ShaderUniformDataType.SHADER_UNIFORM_VEC4);
            _frameUniformUploadCount++;
        }
    }

    private static void RegisterBuiltInShaders()
    {
        foreach (var (id, fallbackFragSource) in BuiltInShaders.FragmentSources)
        {
            string? overridePath = ModManager.ResolveFile("Assets", "Shaders", $"{id}.fs")
                                ?? ModManager.ResolveFile("Shaders", $"{id}.fs");

            if (overridePath != null && File.Exists(overridePath))
            {
                Shader custom = Raylib.LoadShader(null, overridePath);
                if (custom.id > 0)
                {
                    _shaders[id] = custom;
                    continue;
                }
            }

            _shaders[id] = Raylib.LoadShaderFromMemory(null, fallbackFragSource);
        }
    }

    public static void Dispose()
    {
        EndShader();
        foreach (var kv in _shaders)
        {
            if (kv.Value.id > 0)
            {
                Raylib.UnloadShader(kv.Value);
            }
        }
        _shaders.Clear();
        _uniformLocations.Clear();
        _shaderGlobalUploadFrame.Clear();
    }
}
