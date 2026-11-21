using System;
using System.Collections.Generic;
using System.Numerics;
using MoonSharp.Interpreter;
using Raylib_cs;

[MoonSharpUserData]
public class RenderMaterial
{
    public string ShaderId { get; set; } = "none";
    public BlendMode BlendMode { get; set; } = BlendMode.BLEND_ALPHA;

    public Dictionary<string, float> FloatUniforms { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Vector2> Vec2Uniforms { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Vector3> Vec3Uniforms { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasShader => !string.IsNullOrEmpty(ShaderId) && !ShaderId.Equals("none", StringComparison.OrdinalIgnoreCase);

    public RenderMaterial Clone()
    {
        var copy = new RenderMaterial
        {
            ShaderId = ShaderId,
            BlendMode = BlendMode
        };
        foreach (var kv in FloatUniforms) copy.FloatUniforms[kv.Key] = kv.Value;
        foreach (var kv in Vec2Uniforms) copy.Vec2Uniforms[kv.Key] = kv.Value;
        foreach (var kv in Vec3Uniforms) copy.Vec3Uniforms[kv.Key] = kv.Value;
        return copy;
    }

    public void SetFloat(string name, float val) => FloatUniforms[name] = val;
    public void SetVec2(string name, float x, float y) => Vec2Uniforms[name] = new Vector2(x, y);
    public void SetColor(string name, float r, float g, float b)
    {
        if (r > 1.0f || g > 1.0f || b > 1.0f)
            Vec3Uniforms[name] = new Vector3(r / 255f, g / 255f, b / 255f);
        else
            Vec3Uniforms[name] = new Vector3(r, g, b);
    }

    public bool Begin()
    {
        if (BlendMode != BlendMode.BLEND_ALPHA)
        {
            ShaderManager.EndShader();
            Raylib.BeginBlendMode(BlendMode);
        }

        if (!HasShader || !ShaderManager.BeginShader(ShaderId, out Shader shader))
        {
            ShaderManager.EndShader();
            return false;
        }

        ShaderManager.SetFloat(shader, "u_HasPotion", 0f);

        if (ShaderId.Equals(BuiltInShaders.SpriteLutId, StringComparison.OrdinalIgnoreCase))
        {
            ShaderManager.SetFloat(shader, "u_PortalMode", 0f);
            ShaderManager.SetVec3(shader, "u_PrimaryColor", Vector3.Zero);
            ShaderManager.SetVec3(shader, "u_SecondaryColor", Vector3.Zero);
            ShaderManager.SetVec3(shader, "u_GlowColor", Vector3.Zero);
        }

        foreach (var (k, v) in FloatUniforms) ShaderManager.SetFloat(shader, k, v);
        foreach (var (k, v) in Vec2Uniforms) ShaderManager.SetVec2(shader, k, v);
        foreach (var (k, v) in Vec3Uniforms) ShaderManager.SetVec3(shader, k, v);

        return true;
    }

    public void End(bool shaderWasStarted, bool keepShaderBound = false)
    {
        if (shaderWasStarted && (!keepShaderBound || BlendMode != BlendMode.BLEND_ALPHA))
            ShaderManager.EndShader();

        if (BlendMode != BlendMode.BLEND_ALPHA)
            Raylib.EndBlendMode();
    }
}