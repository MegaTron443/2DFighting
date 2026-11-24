using System;
using System.IO;
using System.Numerics;
using Raylib_cs;

public class WorldButton : IDisposable
{
    private const float HitCooldownDuration = 0.35f;
    private const float HitFlashDuration = 0.16f;
    private const float MicroHitstopSeconds = 0.03f;
    private const float RecoilDecaySpeed = 18f;

    private string _label = "";
    private string _subLabel = "";

    public string Id { get; set; } = "";
    public string Label
    {
        get => _label;
        set
        {
            string normalized = value ?? "";
            if (!string.Equals(_label, normalized, StringComparison.Ordinal))
            {
                _label = normalized;
                _textLayoutDirty = true;
            }
        }
    }

    public string SubLabel
    {
        get => _subLabel;
        set
        {
            string normalized = value ?? "";
            if (!string.Equals(_subLabel, normalized, StringComparison.Ordinal))
            {
                _subLabel = normalized;
                _textLayoutDirty = true;
            }
        }
    }
    public Vector2 WorldPositionPct { get; set; }
    public Vector2 SizePct { get; set; }
    public Texture2D? Texture { get; set; }
    public bool OwnsTexture { get; private set; }
    public RenderMaterial Material { get; set; } = new() { ShaderId = BuiltInShaders.UiButtonId };
    public Vector3 AccentColor { get; set; } = new Vector3(0.45f, 0.78f, 1.0f);

    public bool IsToggle { get; set; } = false;
    public bool IsSelected { get; set; } = false;
    public float HitCooldownTimer { get; set; } = 0f;
    public Action<ButtonContext>? OnTrigger { get; set; }
    public bool IsHovered { get; private set; }

    private float _hoverLerp;
    private float _hitFlashTimer;
    private Vector2 _recoilOffsetPx;
    private int _cachedMainFont;
    private int _cachedMainWidth;
    private int _cachedSubFont;
    private int _cachedSubWidth;
    private int _cachedScreenW = -1;
    private int _cachedScreenH = -1;
    private Vector2 _cachedSizePct = new(-1f, -1f);
    private bool _textLayoutDirty = true;

    public bool LoadTexture(string relativeOrFullPath)
    {
        if (OwnsTexture && Texture is { id: > 0 } oldTex)
        {
            Raylib.UnloadTexture(oldTex);
        }
        Texture = null;
        OwnsTexture = false;

        string? resolved = ModManager.ResolveFile(relativeOrFullPath)
                        ?? ModManager.ResolveFile("Assets", "UI", relativeOrFullPath)
                        ?? (File.Exists(relativeOrFullPath) ? relativeOrFullPath : null);

        if (resolved == null || !File.Exists(resolved))
            return false;

        Texture2D loaded = Raylib.LoadTexture(resolved);
        if (loaded.id == 0)
            return false;

        Raylib.SetTextureFilter(loaded, TextureFilter.TEXTURE_FILTER_BILINEAR);
        Raylib.SetTextureWrap(loaded, TextureWrap.TEXTURE_WRAP_CLAMP);
        Texture = loaded;
        OwnsTexture = true;
        return true;
    }

    public Rectangle GetWorldBounds()
    {
        float sw = Raylib.GetScreenWidth();
        float sh = Raylib.GetScreenHeight();
        float w = SizePct.X * sw;
        float h = SizePct.Y * sh;
        float x = (WorldPositionPct.X * sw) - (w * 0.5f);
        float y = (WorldPositionPct.Y * sh) - (h * 0.5f);
        return new Rectangle(x, y, w, h);
    }

    public void Update(ButtonContext context, float dt, bool blockMouseInput = false)
    {
        if (HitCooldownTimer > 0f) HitCooldownTimer = MathF.Max(0f, HitCooldownTimer - dt);
        if (_hitFlashTimer > 0f) _hitFlashTimer = MathF.Max(0f, _hitFlashTimer - dt);

        float recoilDecay = MathF.Exp(-RecoilDecaySpeed * dt);
        _recoilOffsetPx *= recoilDecay;

        Rectangle bounds = GetWorldBounds();
        Vector2 worldMouse = Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), CameraManager.RawCamera);
        IsHovered = !blockMouseInput && Raylib.CheckCollisionPointRec(worldMouse, bounds);

        float targetHover = IsHovered ? 1f : 0f;
        _hoverLerp = Math.Clamp(_hoverLerp + ((targetHover - _hoverLerp) * Math.Min(1f, dt * 14f)), 0f, 1f);

        if (IsHovered && HitCooldownTimer <= 0f && Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_LEFT))
        {
            Vector2 center = new Vector2(bounds.x + bounds.width * 0.5f, bounds.y + bounds.height * 0.5f);
            Trigger(context, isCombatHit: false, hitDir: 0f, impactPos: center);
            return;
        }

        if (HitCooldownTimer > 0f) return;

        Player? p1 = context.Data.Player1;
        if (p1 == null) return;

        if (p1.TryGetHitbox(out Rectangle meleeHb) && Raylib.CheckCollisionRecs(meleeHb, bounds))
        {
            Vector2 impactPos = new Vector2(
                (MathF.Max(meleeHb.x, bounds.x) + MathF.Min(meleeHb.x + meleeHb.width, bounds.x + bounds.width)) * 0.5f,
                (MathF.Max(meleeHb.y, bounds.y) + MathF.Min(meleeHb.y + meleeHb.height, bounds.y + bounds.height)) * 0.5f
            );
            Trigger(context, isCombatHit: true, hitDir: p1.FacingRight ? 1f : -1f, impactPos);
            return;
        }

        var projectiles = context.Data.Services?.Projectiles.ActiveProjectiles ?? PhysicsEngine.ActiveProjectiles;
        for (int i = 0; i < projectiles.Count; i++)
        {
            Projectile proj = projectiles[i];
            if (!proj.IsActive || proj.Owner != p1) continue;

            if (Raylib.CheckCollisionRecs(proj.GetHitbox(), bounds))
            {
                float dir = MathF.Abs(proj.Velocity.X) > 1f ? MathF.Sign(proj.Velocity.X) : (proj.FacingRight ? 1f : -1f);
                Vector2 impactPos = proj.Position;
                if (proj.DestroyOnHit)
                {
                    proj.Destroy();
                }
                Trigger(context, isCombatHit: true, hitDir: dir, impactPos);
                return;
            }
        }
    }

    private void Trigger(ButtonContext context, bool isCombatHit, float hitDir, Vector2 impactPos)
    {
        HitCooldownTimer = HitCooldownDuration;
        _hitFlashTimer = HitFlashDuration;

        if (isCombatHit)
        {
            float pushSign = hitDir != 0f ? hitDir : 1f;
            _recoilOffsetPx = new Vector2(pushSign * 10f, -8f);
            VFXManager.SpawnPreset(VFXPreset.HitSpark, impactPos, pushSign >= 0f);

            if (context.Data.Services != null)
                context.Data.Services.Match.AddHitstop(MicroHitstopSeconds);
            else
                PhysicsEngine.HitstopTimer = MathF.Max(PhysicsEngine.HitstopTimer, MicroHitstopSeconds);

            context.Data.SoundHandler?.PlayEx(null, "prism_chime", "sfx_world", pitch: 1.15f, volumeMult: 0.70f);
        }
        else
        {
            _recoilOffsetPx = new Vector2(0f, 4f);
            context.Data.SoundHandler?.PlayEx(null, "prism_chime", "sfx_world", pitch: 1.35f, volumeMult: 0.45f);
        }

        OnTrigger?.Invoke(context);
    }

    public void Draw()
    {
        DrawPanel(keepShaderBound: false);
        DrawOverlay();
    }

    public void DrawPanel(bool keepShaderBound = false)
    {
        Rectangle baseBounds = GetWorldBounds();
        Rectangle bounds = new Rectangle(
            baseBounds.x + _recoilOffsetPx.X,
            baseBounds.y + _recoilOffsetPx.Y,
            baseBounds.width,
            baseBounds.height
        );

        float effectiveHover = MathF.Max(_hoverLerp, _hitFlashTimer > 0f ? 1f : 0f);
        Color accentFallback = new Color(
            (byte)Math.Clamp(AccentColor.X * 255f, 0f, 255f),
            (byte)Math.Clamp(AccentColor.Y * 255f, 0f, 255f),
            (byte)Math.Clamp(AccentColor.Z * 255f, 0f, 255f),
            (byte)255
        );
        Color fallbackBg = IsHovered ? new Color(50, 52, 68, 235) : new Color(26, 28, 38, 220);
        Color fallbackBorder = IsSelected ? Color.GOLD : (IsHovered ? accentFallback : Color.GRAY);

        UIRenderer.DrawStyledPanel(
            bounds,
            Material,
            Texture,
            effectiveHover,
            IsSelected,
            AccentColor,
            fallbackBg,
            fallbackBorder,
            keepShaderBound
        );
    }

    public void DrawOverlay()
    {
        Rectangle baseBounds = GetWorldBounds();
        Rectangle bounds = new Rectangle(
            baseBounds.x + _recoilOffsetPx.X,
            baseBounds.y + _recoilOffsetPx.Y,
            baseBounds.width,
            baseBounds.height
        );
        Color accentFallback = new Color(
            (byte)Math.Clamp(AccentColor.X * 255f, 0f, 255f),
            (byte)Math.Clamp(AccentColor.Y * 255f, 0f, 255f),
            (byte)Math.Clamp(AccentColor.Z * 255f, 0f, 255f),
            (byte)255
        );

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        bool hasSub = !string.IsNullOrWhiteSpace(_subLabel);
        if (_textLayoutDirty || sw != _cachedScreenW || sh != _cachedScreenH || SizePct != _cachedSizePct)
        {
            int mainPref = Math.Max(14, (int)(sh * (hasSub ? 0.023f : 0.026f)));
            _cachedMainFont = UIRenderer.FitFontSize(_label, mainPref, 11, bounds.width - 14f, out _cachedMainWidth);
            if (hasSub)
            {
                int subPref = Math.Max(11, (int)(sh * 0.017f));
                _cachedSubFont = UIRenderer.FitFontSize(_subLabel, subPref, 10, bounds.width - 14f, out _cachedSubWidth);
            }
            else
            {
                _cachedSubFont = 0;
                _cachedSubWidth = 0;
            }
            _cachedScreenW = sw;
            _cachedScreenH = sh;
            _cachedSizePct = SizePct;
            _textLayoutDirty = false;
        }

        int mainFont = _cachedMainFont;
        int mainW = _cachedMainWidth;
        int mainX = (int)(bounds.x + (bounds.width - mainW) * 0.5f);

        int mainY = hasSub
            ? (int)(bounds.y + bounds.height * 0.28f - mainFont * 0.45f)
            : (int)(bounds.y + (bounds.height - mainFont) * 0.5f);

        Color mainCol = _hitFlashTimer > 0f
            ? Color.WHITE
            : (IsHovered ? Color.GOLD : (IsSelected ? new Color(255, 240, 150, 255) : Color.RAYWHITE));

        Raylib.DrawText(_label, mainX + 1, mainY + 1, mainFont, Color.BLACK);
        Raylib.DrawText(_label, mainX, mainY, mainFont, mainCol);

        if (hasSub)
        {
            int subFont = _cachedSubFont;
            int subW = _cachedSubWidth;
            int subX = (int)(bounds.x + (bounds.width - subW) * 0.5f);
            int subY = (int)(bounds.y + bounds.height * 0.66f - subFont * 0.4f);

            Color subCol = IsSelected ? accentFallback : new Color(185, 200, 220, 240);
            Raylib.DrawText(_subLabel, subX + 1, subY + 1, subFont, Color.BLACK);
            Raylib.DrawText(_subLabel, subX, subY, subFont, subCol);
        }

        if (IsToggle)
        {
            float dotR = MathF.Max(3.5f, sh * 0.006f);
            Vector2 dotPos = new Vector2(bounds.x + bounds.width - dotR * 2.2f, bounds.y + dotR * 2.2f);
            Raylib.DrawCircleV(dotPos, dotR, IsSelected ? accentFallback : new Color(90, 95, 110, 200));
        }
    }

    public void Dispose()
    {
        if (OwnsTexture && Texture is { id: > 0 } tex)
        {
            Raylib.UnloadTexture(tex);
        }
        Texture = null;
        OwnsTexture = false;
    }
}
