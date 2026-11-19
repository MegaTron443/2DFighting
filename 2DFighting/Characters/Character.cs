using System;
using System.Collections.Generic;
using System.Numerics;
using MoonSharp.Interpreter;
using Raylib_cs;

[MoonSharpUserData]
public class Character
{
    private const float WalkAnimationMinSpeedPx = 5f;

    public SkillController SkillController { get; } = new();
    public CharacterRenderer Renderer { get; } = new();
    public AfterimageSystem Afterimages { get; } = new();
    public Animation Animator { get; } = new();

    public string RawLuaCode { get; internal set; } = "";
    public static bool ShowDebugBoxes { get; set; } = false;

    public string Name { get; internal set; } = "";

    // --- Viewport Percentage Ratios (Mutable from Lua for Stance Switching) ---
    public float WidthPct { get; set; } = 0.05f;
    public float HeightPct { get; set; } = 0.20f;
    public float WalkSpeedPct { get; set; } = 0.25f;
    public float JumpForcePct { get; set; } = -0.65f;
    public float GravityPct { get; set; } = 1.40f;
    public float Friction { get; set; } = 0.85f;
    public float HitStun { get; set; } = 0.25f;

    // --- Computed Pixel Physics Parameters ---
    public float Width => Raylib.GetScreenWidth() * WidthPct;
    public float Height => Raylib.GetScreenHeight() * HeightPct;
    public float WalkSpeed => Raylib.GetScreenWidth() * WalkSpeedPct;
    public float JumpForce => Raylib.GetScreenHeight() * JumpForcePct;
    public float Gravity => Raylib.GetScreenHeight() * GravityPct;

    // --- Rendering & Animation ---
    public float DrawOffsetX => Width * Renderer.DrawOffsetXPct;
    public float DrawOffsetY => Height * Renderer.DrawOffsetYPct;
    public float DrawOffsetXPct => Renderer.DrawOffsetXPct;
    public float DrawOffsetYPct => Renderer.DrawOffsetYPct;
    public int FrameWidth => Renderer.FrameWidth;
    public int FrameHeight => Renderer.FrameHeight;

    public RenderMaterial BaseMaterial => Renderer.BaseMaterial;
    public Dictionary<string, SpritePart> Parts => Renderer.Parts;
    public Dictionary<string, AnimationClip> Animations { get; } = new(StringComparer.OrdinalIgnoreCase);

    // --- Afterimage Forwarding ---
    public bool EmitAfterimages
    {
        get => Afterimages.EmitAfterimages;
        set => Afterimages.EmitAfterimages = value;
    }

    public Vector3 AfterimageTint
    {
        get => Afterimages.AfterimageTint;
        set => Afterimages.AfterimageTint = value;
    }

    // --- Skill Controller Forwarding ---
    public SkillData? ActiveSkill
    {
        get => SkillController.ActiveSkill;
        set => SkillController.ActiveSkill = value;
    }

    public int CurrentActionFrame => SkillController.CurrentActionFrame;
    public Dictionary<string, SkillData> Skills => SkillController.Skills;
    public Dictionary<string, float> SkillCooldowns => SkillController.SkillCooldowns;

    public bool HasHit
    {
        get => SkillController.HasHit;
        set => SkillController.HasHit = value;
    }

    public bool IsHitboxActive => SkillController.IsHitboxActive;
    public bool IsHyperArmorActive => SkillController.IsHyperArmorActive;

    public Color PrimaryColor { get; set; } = Color.DARKBLUE;
    public Color SecondaryColor { get; set; } = Color.MAROON;
    public DynValue? OnPassive { get; internal set; }
    public Dictionary<string, float> CustomData { get; } = new();
    public string ScriptHash { get; internal set; } = "";

    public static void UnloadAllSpriteSheets() => CharacterLoader.UnloadAllSpriteSheets();

    public Character(string luaFilePath, Script script)
    {
        CharacterLoader.LoadFromLua(this, luaFilePath, script);
    }

    public void SetPartVisible(string partName, bool visible) => Renderer.SetPartVisible(partName, visible);
    public bool IsPartVisible(string partName) => Renderer.IsPartVisible(partName);
    public void SetPartOffset(string partName, float offsetXPct, float offsetYPct, float rotationDeg = 0f) =>
        Renderer.SetPartOffset(partName, offsetXPct, offsetYPct, rotationDeg);
    public void SetPartShaderFloat(string partName, string uniform, float val) => Renderer.SetPartShaderFloat(partName, uniform, val);
    public void SetPartShaderColor(string partName, string uniform, float r, float g, float b) =>
        Renderer.SetPartShaderColor(partName, uniform, r, g, b);
    public void SetShaderColor(string uniform, float r, float g, float b) => Renderer.SetShaderColor(uniform, r, g, b);
    public void SetShaderFloat(string uniform, float val) => Renderer.SetShaderFloat(uniform, val);
    public void SpawnAfterimage(float r = 165f, float g = 50f, float b = 255f) => Afterimages.RequestAfterimage(r, g, b);

    public void Move(Player p, float direction)
    {
        p.IsHoldingBack = (p.FacingRight && direction < 0f) || (!p.FacingRight && direction > 0f);
        if (ActiveSkill != null) return;
        p.Velocity.X = direction * WalkSpeed;
    }

    public void Jump(Player p)
    {
        if (p.IsGrounded && ActiveSkill == null)
        {
            p.Velocity.Y = JumpForce;
            p.IsGrounded = false;
            p.JustExecutedAction = GameAction.Jump;
        }
    }

    public void ExecuteSkill(Player p, string skillName) => SkillController.ExecuteSkill(p, skillName);

    public void ResetCombatState()
    {
        SkillController.ResetAll();
        CustomData.Clear();
        Afterimages.Clear();
    }

    public void Update(Player p, float dt)
    {
        if (SkillController.HandleHitstunInterrupt(p))
        {
            Afterimages.StopEmission();
        }

        UpdatePhysics(p, dt);

        if (SkillController.UpdateActiveSkill(p, this, dt))
        {
            Afterimages.StopEmission();
        }

        UpdateAnimationState(p);
        Animator.Update(dt, Animations);

        Afterimages.Update(
            p,
            Animator.GetAtlasFrameIndex(Animations),
            Animator.GetCurrentKeyframe(Animations),
            dt
        );

        if (OnPassive != null && OnPassive.Type == DataType.Function)
        {
            OnPassive.Function.Call(p, this, dt);
        }

        SkillController.UpdateCooldowns(dt);
    }

    private void UpdatePhysics(Player p, float dt)
    {
        if (!p.IsGrounded)
        {
            if (p.IsInHitstun && p.IsAirStalled)
            {
                p.Velocity.Y = 0f;
            }
            else if (IsHitboxActive && (ActiveSkill == null || !ActiveSkill.CustomLogic))
            {
                p.Velocity.Y = 0f;
            }
            else
            {
                p.Velocity.Y += Gravity * dt;
            }
        }
        else if (ActiveSkill == null)
        {
            p.IsAirStalled = false;
            p.Velocity.X *= Friction;
        }

        p.Position += p.Velocity * dt;
    }

    private void UpdateAnimationState(Player p)
    {
        if (!p.IsAlive)
            Animator.SetState(AnimationState.Dying);
        else if (p.IsInHitstun)
            Animator.SetState(AnimationState.TakingDamage);
        else if (ActiveSkill != null)
            Animator.SetState(AnimationState.Attacking, ActiveSkill.AnimationClipName);
        else if (!p.IsGrounded)
            Animator.SetState(AnimationState.Jumping);
        else if (MathF.Abs(p.Velocity.X) > WalkAnimationMinSpeedPx)
            Animator.SetState(AnimationState.Walking);
        else
            Animator.SetState(AnimationState.Idle);
    }

    public void Draw(Player p, float alpha) => Renderer.Draw(this, p, Afterimages, alpha);
}
