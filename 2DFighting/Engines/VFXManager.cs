using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using MoonSharp.Interpreter;
using Raylib_cs;

public enum VFXPreset
{
    HitSpark,
    HeavyHitSpark,
    BlockSpark,
    BlockBreak,
    CounterFlash,
    Shockwave,
    Explosion,
    Dust,
    HealGlow,
    Afterimage
}

public class VFXInstance
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Elapsed;
    public float Lifetime;
    public float StartRadiusPx;
    public float EndRadiusPx;
    public Color PrimaryColor;
    public Color SecondaryColor;
    public VFXPreset Preset;
    public bool FacingRight;

    public Texture2D? SpriteSheet;
    public int FrameCount;
    public int FrameWidth;
    public int FrameHeight;

    public bool IsActive => Elapsed < Lifetime;
    public float Progress => Lifetime > 0f ? Math.Clamp(Elapsed / Lifetime, 0f, 1f) : 1f;
}

public static class VFXManager
{
    private static readonly List<VFXInstance> _activeVfx = new(64);
    private static readonly Dictionary<string, Texture2D?> _vfxTextureCache = new(StringComparer.OrdinalIgnoreCase);

    public static void Clear() => _activeVfx.Clear();

    public static void Update(float dt)
    {
        if (PhysicsEngine.IsTrainingMode || _activeVfx.Count == 0)
        {
            if (PhysicsEngine.IsTrainingMode && _activeVfx.Count > 0)
                _activeVfx.Clear();
            return;
        }

        for (int i = _activeVfx.Count - 1; i >= 0; i--)
        {
            var vfx = _activeVfx[i];
            vfx.Elapsed += dt;
            vfx.Position += vfx.Velocity * dt;

            if (!vfx.IsActive)
            {
                _activeVfx.RemoveAt(i);
            }
        }
    }

    public static void SpawnPreset(
        VFXPreset preset,
        Vector2 position,
        bool facingRight = true,
        Color? customColor = null,
        float scaleMultiplier = 1.0f)
    {
        if (PhysicsEngine.IsTrainingMode) return;

        float sh = Raylib.GetScreenHeight();
        string spriteKey = preset.ToString();
        Texture2D? sheet = TryGetVFXTexture(spriteKey);

        var (lifetime, startRadPct, endRadPct, col1, col2) = preset switch
        {
            VFXPreset.HitSpark      => (0.18f, 0.015f, 0.065f, new Color(255, 230, 80, 255),  new Color(255, 100, 0, 255)),
            VFXPreset.HeavyHitSpark => (0.28f, 0.025f, 0.115f, new Color(255, 255, 160, 255), new Color(255, 45, 0, 255)),
            VFXPreset.BlockSpark    => (0.20f, 0.020f, 0.075f, new Color(100, 210, 255, 255), new Color(30, 120, 255, 255)),
            VFXPreset.BlockBreak    => (0.36f, 0.030f, 0.140f, new Color(255, 40, 40, 255),   new Color(255, 215, 0, 255)),
            VFXPreset.CounterFlash  => (0.32f, 0.025f, 0.130f, new Color(255, 255, 255, 255), new Color(0, 240, 255, 255)),
            VFXPreset.Shockwave     => (0.30f, 0.020f, 0.150f, new Color(220, 240, 255, 255), new Color(120, 180, 255, 255)),
            VFXPreset.Explosion     => (0.35f, 0.020f, 0.130f, new Color(255, 180, 40, 255),  new Color(220, 40, 0, 255)),
            VFXPreset.Dust          => (0.25f, 0.012f, 0.045f, new Color(200, 190, 175, 220), new Color(140, 130, 120, 180)),
            VFXPreset.HealGlow      => (0.40f, 0.030f, 0.090f, new Color(80, 255, 120, 240),  new Color(30, 200, 80, 200)),
            _                       => (0.22f, 0.020f, 0.070f, Color.WHITE, Color.SKYBLUE)
        };

        if (customColor.HasValue)
        {
            col1 = customColor.Value;
        }

        _activeVfx.Add(new VFXInstance
        {
            Preset = preset,
            Position = position,
            FacingRight = facingRight,
            Lifetime = lifetime,
            StartRadiusPx = startRadPct * sh * scaleMultiplier,
            EndRadiusPx = endRadPct * sh * scaleMultiplier,
            PrimaryColor = col1,
            SecondaryColor = col2,
            SpriteSheet = sheet,
            FrameWidth = 128,
            FrameHeight = 128,
            FrameCount = sheet.HasValue ? Math.Max(1, sheet.Value.width / 128) : 0
        });
    }

    /// <summary>
    /// Lua-accessible factory: SpawnVFX(player, { preset = "Explosion", x_offset_pct = 0.08, y_offset_pct = 0.0, scale = 1.2, color = {255, 0, 128} })
    /// </summary>
    public static void SpawnFromLua(Player owner, Table config)
    {
        if (PhysicsEngine.IsTrainingMode || owner == null) return;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        float dir = owner.FacingRight ? 1f : -1f;

        bool spawnAtTarget = config.Get("spawn_at_target").CastToBool();
        Player anchor = (spawnAtTarget && owner.TargetOpponent != null) ? owner.TargetOpponent : owner;

        float offXPct = (float)(config.Get("x_offset_pct").CastToNumber() ?? 0.0);
        float offYPct = (float)(config.Get("y_offset_pct").CastToNumber() ?? 0.0);

        Vector2 pos = new Vector2(
            anchor.Position.X + (dir * offXPct * sw),
            anchor.Position.Y + (offYPct * sh)
        );

        string presetStr = config.Get("preset").String ?? config.Get("type").String ?? "Shockwave";
        if (!Enum.TryParse<VFXPreset>(presetStr, ignoreCase: true, out var preset))
        {
            preset = VFXPreset.Shockwave;
        }

        float scale = (float)(config.Get("scale").CastToNumber() ?? 1.0);
        float lifetime = (float)(config.Get("lifetime").CastToNumber() ?? 0.0);

        Color? customCol = null;
        var colTbl = config.Get("color").Table;
        if (colTbl != null)
        {
            customCol = new Color(
                (int)(colTbl.Get(1).CastToNumber() ?? 255),
                (int)(colTbl.Get(2).CastToNumber() ?? 255),
                (int)(colTbl.Get(3).CastToNumber() ?? 255),
                255
            );
        }

        string? customSprite = config.Get("sprite").String;
        SpawnPreset(preset, pos, owner.FacingRight, customCol, scale);

        if (_activeVfx.Count > 0)
        {
            var last = _activeVfx[^1];
            if (lifetime > 0f) last.Lifetime = lifetime;
            if (!string.IsNullOrEmpty(customSprite))
            {
                Texture2D? tex = TryGetVFXTexture(customSprite);
                if (tex.HasValue)
                {
                    last.SpriteSheet = tex;
                    last.FrameWidth = (int)(config.Get("frame_width").CastToNumber() ?? 128);
                    last.FrameHeight = (int)(config.Get("frame_height").CastToNumber() ?? 128);
                    last.FrameCount = Math.Max(1, tex.Value.width / last.FrameWidth);
                }
            }
        }
    }

    public static void Draw()
    {
        if (PhysicsEngine.IsTrainingMode || _activeVfx.Count == 0) return;

        for (int i = 0; i < _activeVfx.Count; i++)
        {
            var vfx = _activeVfx[i];
            float t = vfx.Progress;
            float easeOut = 1f - MathF.Pow(1f - t, 3f);
            byte alpha = (byte)(255 * Math.Clamp(1f - t, 0f, 1f));

            Color c1 = new Color(vfx.PrimaryColor.r, vfx.PrimaryColor.g, vfx.PrimaryColor.b, alpha);
            Color c2 = new Color(vfx.SecondaryColor.r, vfx.SecondaryColor.g, vfx.SecondaryColor.b, alpha);

            if (vfx.SpriteSheet is { id: > 0 } tex && vfx.FrameCount > 0)
            {
                int frame = Math.Min(vfx.FrameCount - 1, (int)(t * vfx.FrameCount));
                int cols = Math.Max(1, tex.width / vfx.FrameWidth);
                int srcX = (frame % cols) * vfx.FrameWidth;
                int srcY = (frame / cols) * vfx.FrameHeight;

                Rectangle src = new Rectangle(srcX, srcY, vfx.FacingRight ? vfx.FrameWidth : -vfx.FrameWidth, vfx.FrameHeight);
                float size = vfx.EndRadiusPx * 2f;
                Rectangle dst = new Rectangle(vfx.Position.X - size * 0.5f, vfx.Position.Y - size * 0.5f, size, size);

                Raylib.DrawTexturePro(tex, src, dst, Vector2.Zero, 0f, c1);
                continue;
            }

            float radius = vfx.StartRadiusPx + ((vfx.EndRadiusPx - vfx.StartRadiusPx) * easeOut);

            switch (vfx.Preset)
            {
                case VFXPreset.HitSpark:
                case VFXPreset.HeavyHitSpark:
                case VFXPreset.BlockBreak:
                {
                    Raylib.DrawCircleV(vfx.Position, radius * (1f - t * 0.5f), c2);
                    Raylib.DrawCircleV(vfx.Position, radius * 0.45f * (1f - t), c1);
                    Raylib.DrawCircleLines((int)vfx.Position.X, (int)vfx.Position.Y, radius, c1);

                    int spikes = vfx.Preset == VFXPreset.HitSpark ? 6 : 10;
                    float baseAngle = vfx.Preset == VFXPreset.BlockBreak ? t * 2.5f : 0.4f;
                    for (int s = 0; s < spikes; s++)
                    {
                        float angle = baseAngle + (s * (MathF.Tau / spikes));
                        Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                        Vector2 pStart = vfx.Position + dir * (radius * 0.25f);
                        Vector2 pEnd = vfx.Position + dir * (radius * 1.35f);
                        Raylib.DrawLineEx(pStart, pEnd, 3f * (1f - t * 0.7f), c1);
                    }
                    break;
                }

                case VFXPreset.BlockSpark:
                {
                    Raylib.DrawCircleLines((int)vfx.Position.X, (int)vfx.Position.Y, radius, c1);
                    Raylib.DrawCircleLines((int)vfx.Position.X, (int)vfx.Position.Y, radius * 0.75f, c2);
                    Raylib.DrawPolyLinesEx(vfx.Position, 6, radius * 0.9f, t * 45f, 3f * (1f - t), c1);
                    break;
                }

                case VFXPreset.CounterFlash:
                {
                    Raylib.DrawCircleLines((int)vfx.Position.X, (int)vfx.Position.Y, radius * 1.15f, c2);
                    Raylib.DrawCircleV(vfx.Position, radius * 0.35f * (1f - t), c1);
                    for (int s = 0; s < 4; s++)
                    {
                        float angle = (s * MathF.PI * 0.5f) + (MathF.PI * 0.25f);
                        Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                        Raylib.DrawLineEx(vfx.Position - dir * radius * 1.4f, vfx.Position + dir * radius * 1.4f, 4f * (1f - t), c1);
                    }
                    break;
                }

                case VFXPreset.Explosion:
                {
                    Raylib.DrawCircleV(vfx.Position, radius * (1f - t * 0.3f), c2);
                    Raylib.DrawCircleV(vfx.Position, radius * 0.65f * (1f - t * 0.5f), c1);
                    Raylib.DrawCircleLines((int)vfx.Position.X, (int)vfx.Position.Y, radius * 1.1f, c1);
                    break;
                }

                case VFXPreset.Shockwave:
                case VFXPreset.HealGlow:
                default:
                {
                    Raylib.DrawCircleLines((int)vfx.Position.X, (int)vfx.Position.Y, radius, c1);
                    Raylib.DrawCircleLines((int)vfx.Position.X, (int)vfx.Position.Y, radius * 0.7f, c2);
                    break;
                }
            }
        }
    }

    private static Texture2D? TryGetVFXTexture(string name)
    {
        if (_vfxTextureCache.TryGetValue(name, out Texture2D? cached))
            return cached;

        string? resolved = ModManager.ResolveFile("Assets", "VFX", $"{name}.png")
                        ?? ModManager.ResolveFile("Assets", "VFX", $"{name.ToLowerInvariant()}.png");

        if (resolved != null && File.Exists(resolved))
        {
            Texture2D tex = Raylib.LoadTexture(resolved);
            _vfxTextureCache[name] = tex;
            return tex;
        }

        _vfxTextureCache[name] = null;
        return null;
    }

    public static void Dispose()
    {
        foreach (var tex in _vfxTextureCache.Values)
        {
            if (tex.HasValue && tex.Value.id > 0)
            {
                Raylib.UnloadTexture(tex.Value);
            }
        }
        _vfxTextureCache.Clear();
        _activeVfx.Clear();
    }
}