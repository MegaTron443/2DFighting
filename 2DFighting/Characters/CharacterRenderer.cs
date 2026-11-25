using System;
using System.Collections.Generic;
using System.Numerics;
using MoonSharp.Interpreter;
using Raylib_cs;

[MoonSharpUserData]
public class SpritePart
{
    public string Name { get; set; } = "";
    public int ZOrder { get; set; } = 0;
    public bool Visible { get; set; } = true;
    public Texture2D? Texture { get; set; } = null;
    public string? TexturePath { get; set; } = null;
    public RenderMaterial Material { get; set; } = new();
    public float OffsetXPct { get; set; } = 0f;
    public float OffsetYPct { get; set; } = 0f;
    public float WidthPct { get; set; } = 1.0f;
    public float HeightPct { get; set; } = 1.0f;
    public float RotationDeg { get; set; } = 0f;
    public bool IsSocketProp { get; set; } = false;
    public bool IsAtlas { get; set; } = false;
    public Color FallbackColor { get; set; } = Color.WHITE;
}

public static class RectangleExtensions
{
    public static float CenterX(this Rectangle r) => r.x + (r.width * 0.5f);
    public static float CenterY(this Rectangle r) => r.y + (r.height * 0.5f);
}

public class CharacterRenderer
{
    private const float AtlasDestWidthScale = 2f;
    private const float AtlasDestHeightScale = 1.15f;
    private const float FacingLeftTintFactor = 0.8f;
    private const byte DebugOverlayAlpha = 90;

    private readonly List<SpritePart> _sortedParts = new();

    public Texture2D? SpriteSheet { get; set; }
    public string? SpriteSheetPath { get; set; }
    public bool HasSpriteSheet => SpriteSheet is { id: > 0 };
    public bool HasCompoundParts => _sortedParts.Count > 0;

    public float DrawOffsetXPct { get; set; } = 0f;
    public float DrawOffsetYPct { get; set; } = 0f;
    public int FrameWidth { get; set; } = 128;
    public int FrameHeight { get; set; } = 128;

    public RenderMaterial BaseMaterial { get; } = new();
    public Dictionary<string, SpritePart> Parts { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void AddPart(string name, SpritePart part)
    {
        Parts[name] = part;
        _sortedParts.Add(part);
    }

    public void SortParts() => _sortedParts.Sort((a, b) => a.ZOrder.CompareTo(b.ZOrder));

    public void SetPartVisible(string partName, bool visible)
    {
        if (Parts.TryGetValue(partName, out var part))
            part.Visible = visible;
        else
            DiagnosticsLogger.LogSilentIssueOnce(
                $"missing_part_vis_{partName}",
                $"SetPartVisible('{partName}') failed: part does not exist in rendering.parts.",
                LogSeverity.Warning
            );
    }

    public void SetPartTexture(string partName, string relativePath)
    {
        if (!Parts.TryGetValue(partName, out var part))
        {
            DiagnosticsLogger.LogSilentIssueOnce(
                $"missing_part_tex_{partName}",
                $"SetPartTexture('{partName}') failed: part does not exist.",
                LogSeverity.Warning
            );
            return;
        }

        string? resolved = ModManager.ResolveFile(relativePath);
        if (resolved != null && System.IO.File.Exists(resolved))
        {
            part.TexturePath = resolved;
            if (!PhysicsEngine.IsHeadlessThread)
            {
                part.Texture = CharacterLoader.GetOrLoadSpriteSheet(resolved);
            }
        }
    }

    public void CollectTexturePaths(HashSet<string> activePaths)
    {
        if (!string.IsNullOrEmpty(SpriteSheetPath))
            activePaths.Add(SpriteSheetPath);

        for (int i = 0; i < _sortedParts.Count; i++)
        {
            string? path = _sortedParts[i].TexturePath;
            if (!string.IsNullOrEmpty(path))
                activePaths.Add(path);
        }
    }

    public void EnsureTexturesLoaded()
    {
        if (!string.IsNullOrEmpty(SpriteSheetPath))
            SpriteSheet = CharacterLoader.GetOrLoadSpriteSheet(SpriteSheetPath);

        for (int i = 0; i < _sortedParts.Count; i++)
        {
            SpritePart part = _sortedParts[i];
            if (!string.IsNullOrEmpty(part.TexturePath))
                part.Texture = CharacterLoader.GetOrLoadSpriteSheet(part.TexturePath);
        }
    }

    public void SetPartScale(string partName, float widthPct, float heightPct)
    {
        if (Parts.TryGetValue(partName, out var part))
        {
            part.WidthPct = widthPct;
            part.HeightPct = heightPct;
        }
    }

    public bool IsPartVisible(string partName)
    {
        return Parts.TryGetValue(partName, out var part) && part.Visible;
    }

    public void SetPartOffset(string partName, float offsetXPct, float offsetYPct, float rotationDeg = 0f)
    {
        if (Parts.TryGetValue(partName, out var part))
        {
            part.OffsetXPct = offsetXPct;
            part.OffsetYPct = offsetYPct;
            part.RotationDeg = rotationDeg;
        }
    }

    public void SetPartShaderFloat(string partName, string uniform, float val)
    {
        if (Parts.TryGetValue(partName, out var part))
            part.Material.SetFloat(uniform, val);
    }

    public void SetPartShaderColor(string partName, string uniform, float r, float g, float b)
    {
        if (Parts.TryGetValue(partName, out var part))
            part.Material.SetColor(uniform, r, g, b);
    }

    public void SetShaderColor(string uniform, float r, float g, float b)
    {
        BaseMaterial.SetColor(uniform, r, g, b);
        foreach (var part in Parts.Values)
        {
            part.Material.SetColor(uniform, r, g, b);
        }
    }

    public void SetShaderFloat(string uniform, float val)
    {
        BaseMaterial.SetFloat(uniform, val);
        foreach (var part in Parts.Values)
        {
            part.Material.SetFloat(uniform, val);
        }
    }
    public void SetPartShaderVec2(string partName, string uniform, float x, float y)
    {
        if (Parts.TryGetValue(partName, out var part))
            part.Material.SetVec2(uniform, x, y);
    }

    public void SetShaderVec2(string uniform, float x, float y)
    {
        BaseMaterial.SetVec2(uniform, x, y);
        foreach (var part in Parts.Values)
        {
            part.Material.SetVec2(uniform, x, y);
        }
    }

    public void Draw(Character character, Player p, AfterimageSystem afterimages, float alpha)
    {
        Rectangle hurtbox = p.GetRenderHurtbox(alpha);
        DrawGroundShadow(character, hurtbox);
        afterimages.Draw(this, character);

        if (HasCompoundParts)
        {
            DrawCompoundParts(character, p, hurtbox);
        }
        else if (HasSpriteSheet)
        {
            DrawBaseSprite(character, p, hurtbox);
        }

        if (Character.ShowDebugBoxes || (!HasSpriteSheet && !HasCompoundParts))
        {
            DrawDebugOrFallback(character, p, hurtbox, alpha);
        }
    }

    private static void DrawGroundShadow(Character character, Rectangle hurtbox)
    {
        ShaderManager.EndShader();
        float groundY = PhysicsEngine.GetEffectiveGroundY(new Vector2(hurtbox.CenterX(), hurtbox.CenterY()));
        float feetY = hurtbox.y + hurtbox.height;
        float heightAboveGround = MathF.Max(0f, feetY < groundY ? groundY - feetY : 0f);
        float maxJumpRef = MathF.Max(1f, PhysicsEngine.LogicalHeight * 0.45f);
        float scale = 1f - Math.Clamp(heightAboveGround / maxJumpRef, 0f, 0.78f);

        int cx = (int)hurtbox.CenterX();
        int cy = (int)groundY;

        Raylib.DrawEllipse(
            cx,
            cy,
            character.Width * 0.72f * scale,
            MathF.Max(3f, character.Height * 0.052f * scale),
            new Color((byte)8, (byte)6, (byte)16, (byte)(68 * scale))
        );
        Raylib.DrawEllipse(
            cx,
            cy,
            character.Width * 0.44f * scale,
            MathF.Max(2f, character.Height * 0.030f * scale),
            new Color((byte)5, (byte)4, (byte)12, (byte)(118 * scale))
        );
    }

    private void DrawCompoundParts(Character character, Player p, Rectangle hurtbox)
    {
        var kf = character.Animator.GetCurrentKeyframe(character.Animations);
        int atlasFrame = character.Animator.GetAtlasFrameIndex(character.Animations);
        float dir = p.FacingRight ? 1f : -1f;

        float refHeight = hurtbox.height;

        for (int i = 0; i < _sortedParts.Count; i++)
        {
            SpritePart part = _sortedParts[i];
            if (!part.Visible) continue;

            float offX = part.OffsetXPct * character.Width * dir;
            float offY = part.OffsetYPct * refHeight;
            float rot = part.RotationDeg * dir;

            if (part.IsSocketProp && kf.HasValue)
            {
                offX += kf.Value.WeaponOffsetPct.X * character.Width * dir;
                offY += kf.Value.WeaponOffsetPct.Y * refHeight;
                rot += kf.Value.WeaponAngleDeg * dir;
            }

            float partW = character.Width * part.WidthPct;
            float partH = (part.IsSocketProp ? character.Height : refHeight) * part.HeightPct;

            Rectangle partDest = new Rectangle(
                hurtbox.CenterX() + offX - (partW * 0.5f),
                hurtbox.CenterY() + offY - (partH * 0.5f),
                partW,
                partH
            );

            bool shaderStarted = part.Material.Begin();

            Texture2D? texToUse = part.Texture ?? (SpriteSheet.HasValue && !part.IsSocketProp ? SpriteSheet : null);
            if (texToUse is { id: > 0 } validTex)
            {
                if (part.Texture.HasValue && !part.IsAtlas)
                {
                    DrawSinglePartTexture(character, validTex, p.FacingRight, partDest, Color.WHITE, rot, part.IsSocketProp);
                }
                else
                {
                    DrawAtlasSprite(character, validTex, part.IsSocketProp ? 0 : atlasFrame, p.FacingRight, partDest, Color.WHITE, rot);
                }
            }

            part.Material.End(shaderStarted, keepShaderBound: true);
        }

        ShaderManager.EndShader();
    }

    private static void DrawSinglePartTexture(Character character, Texture2D tex, bool facingRight, Rectangle partDest, Color tint, float rotationDeg, bool isSocket)
    {
        Rectangle srcRect = new Rectangle(
            0,
            0,
            facingRight ? tex.width : -tex.width,
            tex.height
        );

        Rectangle destRect = new Rectangle(
            partDest.x + character.DrawOffsetX,
            partDest.y + character.DrawOffsetY,
            partDest.width,
            partDest.height
        );

        Vector2 origin;
        if (isSocket)
        {
            // Hilt is at top-left when facing right, and top-right when horizontally flipped!
            origin = facingRight
                ? new Vector2(destRect.width * 0.12f, destRect.height * 0.12f)
                : new Vector2(destRect.width * 0.88f, destRect.height * 0.12f);

            destRect.x += origin.X;
            destRect.y += origin.Y;
        }
        else
        {
            origin = new Vector2(destRect.width * 0.5f, destRect.height * 0.5f);
            destRect.x += origin.X;
            destRect.y += origin.Y;
        }

        Raylib.DrawTexturePro(tex, srcRect, destRect, origin, rotationDeg, tint);
    }

    private void DrawBaseSprite(Character character, Player p, Rectangle hurtbox)
    {
        bool shaderStarted = BaseMaterial.Begin();
        Color tint = p.IsInvulnerable ? Color.SKYBLUE : (p.IsInHitstun ? Color.ORANGE : Color.WHITE);
        DrawAtlasSprite(character, SpriteSheet!.Value, character.Animator.GetAtlasFrameIndex(character.Animations), p.FacingRight, hurtbox, tint);
        BaseMaterial.End(shaderStarted);
    }

    private void DrawDebugOrFallback(Character character, Player p, Rectangle hurtbox, float alpha)
    {
        Color primary = character.PrimaryColor;
        Color tintBase = p.FacingRight
            ? primary
            : new Color((int)(primary.r * FacingLeftTintFactor), (int)(primary.g * FacingLeftTintFactor), (int)(primary.b * FacingLeftTintFactor), 255);

        Color bodyColor = p.IsInvulnerable
            ? Color.SKYBLUE
            : (p.IsInHitstun ? Color.ORANGE : tintBase);

        if (HasSpriteSheet || HasCompoundParts)
        {
            bodyColor.a = DebugOverlayAlpha;
        }

        bool shaderStarted = !HasCompoundParts && BaseMaterial.Begin();
        Raylib.DrawRectangleRec(hurtbox, bodyColor);
        BaseMaterial.End(shaderStarted);

        if (character.IsHyperArmorActive)
        {
            Raylib.DrawRectangleLinesEx(hurtbox, 5, Color.GOLD);
        }
        else if (p.CanTech)
        {
            Raylib.DrawRectangleLinesEx(hurtbox, 4, Color.LIME);
        }
        else
        {
            Raylib.DrawRectangleLinesEx(hurtbox, 2, Color.WHITE);
        }

        if (p.TryGetRenderHitbox(alpha, out Rectangle hitbox))
        {
            Raylib.DrawRectangleRec(hitbox, new Color(255, 0, 0, 160));
            Raylib.DrawRectangleLinesEx(hitbox, 2, Color.YELLOW);
        }
    }

    public void DrawAtlasSprite(Character character, Texture2D tex, int frameIdx, bool facingRight, Rectangle hurtbox, Color tint, float rotationDeg = 0f)
    {
        int cols = Math.Max(1, tex.width / FrameWidth);
        int srcX = (frameIdx % cols) * FrameWidth;
        int srcY = (frameIdx / cols) * FrameHeight;

        Rectangle srcRect = new Rectangle(
            srcX,
            srcY,
            facingRight ? FrameWidth : -FrameWidth,
            FrameHeight
        );

        Rectangle destRect = new Rectangle(
            hurtbox.x + character.DrawOffsetX,
            hurtbox.y + character.DrawOffsetY,
            hurtbox.width * AtlasDestWidthScale,
            hurtbox.height * AtlasDestHeightScale
        );

        Raylib.DrawTexturePro(tex, srcRect, destRect, Vector2.Zero, rotationDeg, tint);
    }
}
