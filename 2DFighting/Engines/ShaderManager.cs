using System;
using System.Collections.Generic;
using System.IO;
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
