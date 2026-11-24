using System;
using System.Collections.Generic;
using System.Numerics;
using MoonSharp.Interpreter;
using Raylib_cs;

public class ActivePostFXPass
{
    public string ShaderId { get; set; } = "postfx_cosmic";
    public int Priority { get; set; } = 0;
    public RenderMaterial Material { get; } = new() { ShaderId = "postfx_cosmic" };
}

[MoonSharpUserData]
public static class PostFXManager
{
    private static RenderTexture2D _sceneTarget;
    private static RenderTexture2D _pingPongTarget;
    private static RenderTexture2D _finalSourceTarget;
    private static ActivePostFXPass? _finalPass;
    private static int _targetWidth = 0;
    private static int _targetHeight = 0;
    private static bool _isCapturing = false;
    private static bool _defaultPassExplicitlyEnabled = false;
    private static int _customPassCount = 0;
    private static bool _hasCapturedFrame = false;

    public static bool HasActiveEffects =>
        !PhysicsEngine.IsTrainingMode &&
        (GameState.IsLiveArena ||
         _impactFramesRemaining > 0 ||
         _shakeFramesRemaining > 0 ||
         _paletteMix > 0.001f ||
         MathF.Abs(_pinchAmount) > 0.001f ||
         _defaultPassExplicitlyEnabled ||
         _customPassCount > 0);

    private static readonly Dictionary<string, ActivePostFXPass> _activePasses = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<ActivePostFXPass> _sortedPasses = new();

    private static int _impactFramesRemaining = 0;
    private static float _impactMode = 0f;

    private static int _shakeFramesRemaining = 0;
    private static int _shakeTotalFrames = 1;
    private static Vector2 _shakeAmplitudeUv = Vector2.Zero;

    private static int _paletteHoldFrames = 0;
    private static float _paletteMix = 0f;
    private static float _paletteFadeSpeed = 2.0f;
    private static Vector3 _paletteTint = new(0.55f, 0.15f, 0.95f);

    private static int _pinchHoldFrames = 0;
    private static float _pinchAmount = 0f;
    private static float _pinchFadeSpeed = 2.5f;
    private static Vector2 _pinchCenter = new(0.5f, 0.5f);

    public static void Initialize()
    {
        ResetAll();
    }

    private static void EnsureRenderTargetSize()
    {
        int sw = Math.Max(1, Raylib.GetScreenWidth());
        int sh = Math.Max(1, Raylib.GetScreenHeight());

        if (_sceneTarget.id == 0 || _targetWidth != sw || _targetHeight != sh)
        {
            if (_sceneTarget.id > 0)
            {
                Raylib.UnloadRenderTexture(_sceneTarget);
            }
            if (_pingPongTarget.id > 0)
            {
                Raylib.UnloadRenderTexture(_pingPongTarget);
                _pingPongTarget = default;
            }
            _finalSourceTarget = default;
            _sceneTarget = Raylib.LoadRenderTexture(sw, sh);
            if (_sceneTarget.id > 0)
            {
                Raylib.SetTextureFilter(_sceneTarget.texture, TextureFilter.TEXTURE_FILTER_BILINEAR);
                Raylib.SetTextureWrap(_sceneTarget.texture, TextureWrap.TEXTURE_WRAP_CLAMP);
            }
            _targetWidth = sw;
            _targetHeight = sh;
        }
    }

    private static void EnsurePingPongTargetSize()
    {
        if (_pingPongTarget.id == 0 || _pingPongTarget.texture.width != _targetWidth || _pingPongTarget.texture.height != _targetHeight)
        {
            if (_pingPongTarget.id > 0)
            {
                Raylib.UnloadRenderTexture(_pingPongTarget);
            }
            _pingPongTarget = Raylib.LoadRenderTexture(_targetWidth, _targetHeight);
            if (_pingPongTarget.id > 0)
            {
                Raylib.SetTextureFilter(_pingPongTarget.texture, TextureFilter.TEXTURE_FILTER_BILINEAR);
                Raylib.SetTextureWrap(_pingPongTarget.texture, TextureWrap.TEXTURE_WRAP_CLAMP);
            }
        }
    }

    public static void ResetAll()
    {
        _activePasses.Clear();
        _sortedPasses.Clear();
        _finalPass = null;
        _finalSourceTarget = default;
        _defaultPassExplicitlyEnabled = false;
        _customPassCount = 0;
        _hasCapturedFrame = false;
        _impactFramesRemaining = 0;
        _impactMode = 0f;
        _shakeFramesRemaining = 0;
        _paletteHoldFrames = 0;
        _paletteMix = 0f;
        _pinchHoldFrames = 0;
        _pinchAmount = 0f;
        GetOrCreateDefaultPass();
    }

    public static void UpdateTick(float dt)
    {
        if (PhysicsEngine.IsTrainingMode) return;
        if (!HasActiveEffects && !_activePasses.ContainsKey(BuiltInShaders.PostFxCosmicId)) return;

        var pass = GetOrCreateDefaultPass();
        var mat = pass.Material;

        if (_impactFramesRemaining > 0)
        {
            _impactFramesRemaining--;
            mat.SetFloat("u_ImpactMode", _impactMode);
        }
        else
        {
            mat.SetFloat("u_ImpactMode", 0f);
        }

        if (_shakeFramesRemaining > 0)
        {
            float decay = _shakeFramesRemaining / (float)Math.Max(1, _shakeTotalFrames);
            float sign = (_shakeFramesRemaining % 2 == 0) ? 1f : -1f;
            Vector2 currentOffset = _shakeAmplitudeUv * decay * sign;
            mat.SetVec2("u_ShakeOffset", currentOffset.X, currentOffset.Y);
            mat.SetFloat("u_ChromaticSplit", MathF.Min(0.018f, currentOffset.Length() * 0.45f));
            _shakeFramesRemaining--;
        }
        else
        {
            mat.SetVec2("u_ShakeOffset", 0f, 0f);
            mat.SetFloat("u_ChromaticSplit", 0f);
        }

        if (_paletteHoldFrames > 0)
        {
            _paletteHoldFrames--;
        }
        else if (_paletteMix > 0f)
        {
            _paletteMix = MathF.Max(0f, _paletteMix - (_paletteFadeSpeed * dt));
        }
        mat.SetFloat("u_PaletteMix", _paletteMix);
        mat.SetColor("u_TintColor", _paletteTint.X, _paletteTint.Y, _paletteTint.Z);

        if (_pinchHoldFrames > 0)
        {
            _pinchHoldFrames--;
        }
        else if (MathF.Abs(_pinchAmount) > 0.001f)
        {
            float step = _pinchFadeSpeed * dt;
            _pinchAmount = _pinchAmount > 0f
                ? MathF.Max(0f, _pinchAmount - step)
                : MathF.Min(0f, _pinchAmount + step);
        }
        mat.SetFloat("u_PinchAmount", _pinchAmount);
        mat.SetVec2("u_PinchCenter", _pinchCenter.X, _pinchCenter.Y);
    }

    private static ActivePostFXPass GetOrCreateDefaultPass()
    {
        if (!_activePasses.TryGetValue("postfx_cosmic", out var pass))
        {
            pass = new ActivePostFXPass { ShaderId = "postfx_cosmic", Priority = 0 };
            pass.Material.SetFloat("u_ImpactMode", 0f);
            pass.Material.SetVec2("u_ShakeOffset", 0f, 0f);
            pass.Material.SetFloat("u_ChromaticSplit", 0f);
            pass.Material.SetFloat("u_PaletteMix", 0f);
            pass.Material.SetFloat("u_PinchAmount", 0f);
            pass.Material.SetVec2("u_PinchCenter", 0.5f, 0.5f);
            pass.Material.SetColor("u_TintColor", _paletteTint.X, _paletteTint.Y, _paletteTint.Z);
            _activePasses["postfx_cosmic"] = pass;
        }
        return pass;
    }

    public static void BeginSceneCapture()
    {
        if (!HasActiveEffects)
        {
            _hasCapturedFrame = false;
            return;
        }
        EnsureRenderTargetSize();
        Raylib.BeginTextureMode(_sceneTarget);
        Raylib.ClearBackground(Color.RAYWHITE);
        _isCapturing = true;
        _hasCapturedFrame = true;
    }

    public static void EndSceneCapture()
    {
        if (!_isCapturing) return;
        ShaderManager.EndShader();
        Raylib.EndTextureMode();
        _isCapturing = false;

        _sortedPasses.Clear();
        foreach (var pass in _activePasses.Values)
        {
            _sortedPasses.Add(pass);
        }
        _sortedPasses.Sort((a, b) =>
        {
            int cmp = a.Priority.CompareTo(b.Priority);
            return cmp != 0 ? cmp : string.Compare(a.ShaderId, b.ShaderId, StringComparison.OrdinalIgnoreCase);
        });

        if (_sortedPasses.Count == 0)
        {
            _finalSourceTarget = _sceneTarget;
            _finalPass = null;
            return;
        }

        if (_sortedPasses.Count == 1)
        {
            _finalSourceTarget = _sceneTarget;
            _finalPass = _sortedPasses[0];
            return;
        }

        EnsurePingPongTargetSize();
        RenderTexture2D srcTarget = _sceneTarget;
        RenderTexture2D dstTarget = _pingPongTarget;

        for (int i = 0; i < _sortedPasses.Count - 1; i++)
        {
            Raylib.BeginTextureMode(dstTarget);
            Raylib.ClearBackground(Color.BLANK);

            Rectangle src = new Rectangle(0, 0, srcTarget.texture.width, -srcTarget.texture.height);
            Rectangle dst = new Rectangle(0, 0, dstTarget.texture.width, dstTarget.texture.height);

            bool started = _sortedPasses[i].Material.Begin();
            Raylib.DrawTexturePro(srcTarget.texture, src, dst, Vector2.Zero, 0f, Color.WHITE);
            _sortedPasses[i].Material.End(started);

            Raylib.EndTextureMode();
            (srcTarget, dstTarget) = (dstTarget, srcTarget);
        }

        _finalSourceTarget = srcTarget;
        _finalPass = _sortedPasses[^1];
    }

    public static void PresentScene()
    {
        if (!_hasCapturedFrame) return;
        RenderTexture2D source = _finalSourceTarget.id > 0 ? _finalSourceTarget : _sceneTarget;
        if (source.id == 0) return;

        Rectangle src = new Rectangle(0, 0, source.texture.width, -source.texture.height);
        Rectangle dst = new Rectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

        bool shaderStarted = _finalPass != null && _finalPass.Material.Begin();
        Raylib.DrawTexturePro(source.texture, src, dst, Vector2.Zero, 0f, Color.WHITE);
        _finalPass?.Material.End(shaderStarted);
    }

    public static void EnablePassFromLua(string shaderId, int priority = 0)
    {
        ShaderManager.LoadOrGet(shaderId);
        if (shaderId.Equals(BuiltInShaders.PostFxCosmicId, StringComparison.OrdinalIgnoreCase))
        {
            _defaultPassExplicitlyEnabled = true;
        }
        else if (!_activePasses.ContainsKey(shaderId))
        {
            _customPassCount++;
        }
        var pass = new ActivePostFXPass { ShaderId = shaderId, Priority = priority };
        pass.Material.ShaderId = shaderId;
        _activePasses[shaderId] = pass;
    }

    public static void DisablePassFromLua(string shaderId)
    {
        if (_activePasses.Remove(shaderId))
        {
            if (shaderId.Equals(BuiltInShaders.PostFxCosmicId, StringComparison.OrdinalIgnoreCase))
                _defaultPassExplicitlyEnabled = false;
            else
                _customPassCount = Math.Max(0, _customPassCount - 1);
        }
    }

    public static void SetUniformFloatFromLua(string shaderId, string uniformName, float value)
    {
        if (!_activePasses.TryGetValue(shaderId, out var pass))
        {
            EnablePassFromLua(shaderId);
            pass = _activePasses[shaderId];
        }
        pass.Material.SetFloat(uniformName, value);
    }

    public static void SetUniformVec2FromLua(string shaderId, string uniformName, float x, float y)
    {
        if (!_activePasses.TryGetValue(shaderId, out var pass))
        {
            EnablePassFromLua(shaderId);
            pass = _activePasses[shaderId];
        }
        pass.Material.SetVec2(uniformName, x, y);
    }

    public static void SetUniformColorFromLua(string shaderId, string uniformName, float r, float g, float b)
    {
        if (!_activePasses.TryGetValue(shaderId, out var pass))
        {
            EnablePassFromLua(shaderId);
            pass = _activePasses[shaderId];
        }
        pass.Material.SetColor(uniformName, r, g, b);
    }

    public static void TriggerScreenShakeFromLua(float xPixels, float yPixels, int durationFrames = 10)
    {
        if (PhysicsEngine.IsTrainingMode) return;
        GetOrCreateDefaultPass();
        float sw = Math.Max(1f, Raylib.GetScreenWidth());
        float sh = Math.Max(1f, Raylib.GetScreenHeight());

        _shakeAmplitudeUv = new Vector2(xPixels / sw, yPixels / sh);
        _shakeTotalFrames = Math.Max(1, durationFrames);
        _shakeFramesRemaining = _shakeTotalFrames;
    }

    public static void TriggerImpactFrameFromLua(int frameCount = 5, int mode = 1)
    {
        if (PhysicsEngine.IsTrainingMode) return;
        GetOrCreateDefaultPass();
        _impactFramesRemaining = Math.Max(_impactFramesRemaining, frameCount);
        _impactMode = mode; // 1 = Monochrome Black/White, 2 = Inverted Void
    }

    public static void TriggerPaletteShiftFromLua(float r, float g, float b, float intensity = 0.85f, int holdFrames = 10, float fadeOutSeconds = 0.45f)
    {
        if (PhysicsEngine.IsTrainingMode) return;
        GetOrCreateDefaultPass();
        if (r > 1f || g > 1f || b > 1f)
            _paletteTint = new Vector3(r / 255f, g / 255f, b / 255f);
        else
            _paletteTint = new Vector3(r, g, b);

        _paletteMix = Math.Clamp(intensity, 0f, 1f);
        _paletteHoldFrames = Math.Max(_paletteHoldFrames, holdFrames);
        _paletteFadeSpeed = fadeOutSeconds > 0.01f ? _paletteMix / fadeOutSeconds : 10f;
    }

    public static void TriggerGravityPinchFromLua(float pinchAmount = 0.45f, int holdFrames = 8, float fadeOutSeconds = 0.35f, float centerX = 0.5f, float centerY = 0.5f)
    {
        if (PhysicsEngine.IsTrainingMode) return;
        GetOrCreateDefaultPass();
        _pinchAmount = Math.Clamp(pinchAmount, -0.8f, 0.85f);
        _pinchHoldFrames = Math.Max(_pinchHoldFrames, holdFrames);
        _pinchFadeSpeed = fadeOutSeconds > 0.01f ? MathF.Abs(_pinchAmount) / fadeOutSeconds : 10f;
        _pinchCenter = new Vector2(centerX, centerY);
    }

    public static void Dispose()
    {
        if (_sceneTarget.id > 0)
        {
            Raylib.UnloadRenderTexture(_sceneTarget);
            _sceneTarget = default;
        }
        if (_pingPongTarget.id > 0)
        {
            Raylib.UnloadRenderTexture(_pingPongTarget);
            _pingPongTarget = default;
        }
        _finalSourceTarget = default;
        _finalPass = null;
        _defaultPassExplicitlyEnabled = false;
        _customPassCount = 0;
        _hasCapturedFrame = false;
        _sortedPasses.Clear();
        _activePasses.Clear();
    }
}