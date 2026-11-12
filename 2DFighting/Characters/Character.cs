using MoonSharp.Interpreter;
using Raylib_cs;
using System.Numerics;

[MoonSharpUserData]
public class Character
{
    public string RawLuaCode { get; private set; } = "";
    public static bool ShowDebugBoxes { get; set; } = false;

    public string Name { get; private set; } = "";

    // --- Viewport Percentage Ratios ---
    public float WidthPct { get; private set; } = 0.05f;
    public float HeightPct { get; private set; } = 0.20f;
    public float WalkSpeedPct { get; private set; } = 0.25f;
    public float JumpForcePct { get; private set; } = -0.65f;
    public float GravityPct { get; private set; } = 1.40f;
    public float Friction { get; private set; } = 0.85f;
    public float HitStun { get; private set; } = 0.25f;

    // --- Physics Parameters (From Lua) ---
    public float Width => Raylib.GetScreenWidth() * WidthPct;
    public float Height => Raylib.GetScreenHeight() * HeightPct;
    public float WalkSpeed => Raylib.GetScreenWidth() * WalkSpeedPct;
    public float JumpForce => Raylib.GetScreenHeight() * JumpForcePct;
    public float Gravity => Raylib.GetScreenHeight() * GravityPct;

    // --- Rendering & Animation ---
    public float DrawOffsetX => Width * DrawOffsetXPct;
    public float DrawOffsetY => Height * DrawOffsetYPct;
    public float DrawOffsetXPct { get; private set; } = 0f;
    public float DrawOffsetYPct { get; private set; } = 0f;
    public int FrameWidth { get; private set; } = 128;
    public int FrameHeight { get; private set; } = 128;
    private Texture2D? _spriteSheet = null;
    public Dictionary<string, AnimationClip> Animations { get; } = new();
    public Animation Animator { get; } = new();

    // --- Actions & Skills ---
    public SkillData? ActiveSkill { get; set; }
    public int CurrentActionFrame { get; private set; } = 0;
    public Dictionary<string, SkillData> Skills { get; } = new();
    public Dictionary<string, float> SkillCooldowns { get; } = new();
    public bool HasHit { get; set; } = false;

    public Color PrimaryColor { get; private set; } = Color.DARKBLUE;
    public Color SecondaryColor { get; private set; } = Color.MAROON;
    public DynValue? OnPassive { get; private set; }
    public Dictionary<string, float> CustomData { get; } = new();
    public string ScriptHash { get; private set; } = "";

    private static readonly Dictionary<string, Texture2D> _textureCache = new(StringComparer.OrdinalIgnoreCase);

    private static Texture2D GetOrLoadSpriteSheet(string fullPath)
    {
        if (_textureCache.TryGetValue(fullPath, out Texture2D cached))
            return cached;

        Texture2D loaded = Raylib.LoadTexture(fullPath);
        _textureCache[fullPath] = loaded;
        return loaded;
    }

    public static void UnloadAllSpriteSheets()
    {
        foreach (var tex in _textureCache.Values)
        {
            Raylib.UnloadTexture(tex);
        }
        _textureCache.Clear();
    }

    public Character(string luaFilePath, Script script)
    {
        LoadFromLua(luaFilePath, script);
    }

    public bool IsHitboxActive
    {
        get
        {
            if (ActiveSkill == null) return false;
            int start = ActiveSkill.StartupFrames;
            int end = start + ActiveSkill.ActiveFrames;
            return CurrentActionFrame >= start && CurrentActionFrame < end;
        }
    }

    public bool IsHyperArmorActive
    {
        get
        {
            if (ActiveSkill == null || !ActiveSkill.IsHyperFrames) return false;
            int start = ActiveSkill.HyperStartFrame;
            int end = start + ActiveSkill.HyperFrames;
            return CurrentActionFrame >= start && CurrentActionFrame < end;
        }
    }

    private void LoadFromLua(string path, Script script)
    {
        // Isolated environment per character script so globals don't collide
        Table env = new Table(script);
        env["Keys"] = script.Globals.Get("Keys");
        env["Trajectories"] = script.Globals.Get("Trajectories");
        env["ApplyHitForce"] = script.Globals.Get("ApplyHitForce");
        env["ApplyLaunchForce"] = script.Globals.Get("ApplyLaunchForce");
        env["create_projectile"] = script.Globals.Get("create_projectile");
        env["CreateProjectile"] = script.Globals.Get("CreateProjectile");
        env["PhysicsEngine"] = script.Globals.Get("PhysicsEngine");
        env["NewVec2"] = script.Globals.Get("NewVec2");
        env["math"] = script.Globals.Get("math");

        string code = File.ReadAllText(path);
        RawLuaCode = code;
        ScriptHash = ModManager.ComputeStringHash(RawLuaCode);
        DynValue result = script.DoString(code, env, path);

        Table? t = result.Type == DataType.Table ? result.Table : env.Get("Character").Table;
        if (t == null)
            throw new Exception($"Table 'Character' missing in: {path}");

        Name = t.Get("name").String ?? "Fighter";

        var phys = t.Get("physics").Table;
        if (phys != null)
        {
            WidthPct = (float)(phys.Get("width_pct").CastToNumber() ?? 0.05);
            HeightPct = (float)(phys.Get("height_pct").CastToNumber() ?? 0.20);
            WalkSpeedPct = (float)(phys.Get("walk_speed_pct").CastToNumber() ?? 0.25);
            JumpForcePct = (float)(phys.Get("jump_force_pct").CastToNumber() ?? -0.65);
            GravityPct = (float)(phys.Get("gravity_pct").CastToNumber() ?? 1.40);
            Friction = (float)(phys.Get("friction").CastToNumber() ?? 0.85);
        }
        OnPassive = t.Get("on_passive");

        var rend = t.Get("rendering").Table;
        if (rend != null)
        {
            DrawOffsetXPct = (float)(rend.Get("draw_offset_x_pct").CastToNumber() ?? 0.0);
            DrawOffsetYPct = (float)(rend.Get("draw_offset_y_pct").CastToNumber() ?? 0.0);
            FrameWidth = (int)(rend.Get("frame_width").CastToNumber() ?? 128);
            FrameHeight = (int)(rend.Get("frame_height").CastToNumber() ?? 128);

            string? sheetRelPath = rend.Get("sprite_sheet").String;
            if (!string.IsNullOrEmpty(sheetRelPath))
            {
                string? fullSheetPath = ModManager.ResolveFile(sheetRelPath);
                if (fullSheetPath != null && File.Exists(fullSheetPath))
                {
                    _spriteSheet = GetOrLoadSpriteSheet(fullSheetPath);
                }
            }

            var colTbl = rend.Get("body_color").Table;
            if (colTbl != null)
            {
                PrimaryColor = new Color(
                    (int)(colTbl.Get(1).CastToNumber() ?? 30),
                    (int)(colTbl.Get(2).CastToNumber() ?? 80),
                    (int)(colTbl.Get(3).CastToNumber() ?? 180),
                    255
                );
            }
        }

        var animTable = t.Get("animations").Table;
        if (animTable != null)
        {
            foreach (var pair in animTable.Pairs)
            {
                var clipTable = pair.Value.Table;
                if (clipTable != null)
                {
                    Animations[pair.Key.String] = new AnimationClip
                    {
                        StartFrame = (int)(clipTable.Get("start_frame").CastToNumber() ?? 0),
                        FrameCount = (int)(clipTable.Get("frame_count").CastToNumber() ?? 1),
                        Speed = (float)(clipTable.Get("speed").CastToNumber() ?? 8.0)
                    };
                }
            }
        }

        var skillsTable = t.Get("skills").Table;
        if (skillsTable != null)
        {
            foreach (var pair in skillsTable.Pairs)
            {
                var sTable = pair.Value.Table;
                var hb = sTable.Get("hitbox").Table;

                Rectangle hbPctRect = new Rectangle(0, 0, 0, 0);
                if (hb != null)
                {
                    hbPctRect = new Rectangle(
                        (float)(hb.Get("x_offset_pct").CastToNumber() ?? hb.Get("x_offset").CastToNumber() ?? 0.0),
                        (float)(hb.Get("y_offset_pct").CastToNumber() ?? hb.Get("y_offset").CastToNumber() ?? 0.0),
                        (float)(hb.Get("width_pct").CastToNumber() ?? hb.Get("width").CastToNumber() ?? 0.2),
                        (float)(hb.Get("height_pct").CastToNumber() ?? hb.Get("height").CastToNumber() ?? 0.2)
                    );
                }

                var triggerObj = sTable.Get("input_trigger");
                int triggerMask = 0;
                if (triggerObj.Type == DataType.Table)
                {
                    triggerMask = (int)(triggerObj.Table.Get("value").CastToNumber() ?? 0);
                }
                else if (triggerObj.Type == DataType.Number)
                {
                    triggerMask = (int)triggerObj.Number;
                }

                string heightStr = (sTable.Get("attack_height").String ?? "mid").ToLowerInvariant();
                AttackHeight atkHeight = heightStr switch
                {
                    "low"      => AttackHeight.Low,
                    "overhead" => AttackHeight.Overhead,
                    _          => AttackHeight.Mid
                };

                Skills[pair.Key.String] = new SkillData
                {
                    Name = pair.Key.String,
                    TriggerMask = triggerMask,
                    Damage = (int)(sTable.Get("damage").CastToNumber() ?? 10),
                    Height = atkHeight,
                    StartupFrames = (int)(sTable.Get("startup_frames").CastToNumber() ?? 0),
                    ActiveFrames = (int)(sTable.Get("active_frames").CastToNumber() ?? 0),
                    RecoveryFrames = (int)(sTable.Get("recovery_frames").CastToNumber() ?? 0),
                    IsHyperFrames = sTable.Get("is_hyperframes").CastToBool(),
                    HyperStartFrame = (int)(sTable.Get("hyper_start_frame").CastToNumber() ?? 0),
                    HyperFrames = (int)(sTable.Get("hyper_frames").CastToNumber() ?? 0),
                    HitboxOffsetPct = hbPctRect,
                    HitStun = (float)(sTable.Get("hitStun").CastToNumber() ?? 0.25),
                    CooldownTime = (float)(sTable.Get("cooldown_time").CastToNumber() ?? 0.0),
                    IsBlockBreak = sTable.Get("is_blockbreak").CastToBool(),
                    OnBlockBreak = sTable.Get("on_blockbreak"),
                    IsCounter = sTable.Get("is_counter").CastToBool(),
                    OnCounter = sTable.Get("on_counter"),
                    CustomLogic = sTable.Get("is_customlogic").CastToBool(),
                    OnCustomLogic = sTable.Get("on_customlogic"),
                    OnHit = sTable.Get("on_hit"),
                    ShortLabel = pair.Key.String.Length > 5 ? pair.Key.String[..5].ToUpperInvariant() : pair.Key.String.ToUpperInvariant()
                };
            }
        }
    }

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

    public void ExecuteSkill(Player p, string skillName)
    {
        if (SkillCooldowns.ContainsKey(skillName) && SkillCooldowns[skillName] > 0) return;
        if (ActiveSkill != null) return;
        if (p.IsCrouching) return;

        if (Skills.TryGetValue(skillName, out var skill))
        {
            ActiveSkill = skill;
            CurrentActionFrame = 0;
            HasHit = false;
            SkillCooldowns[skillName] = skill.CooldownTime;
            p.Velocity.X = 0;
            p.Velocity.Y = 0;

            if (skill.IsCounter)
                p.JustExecutedAction = GameAction.Counter;
            else if (skill.IsBlockBreak)
                p.JustExecutedAction = GameAction.GuardBreak;
            else if (skill.Height == AttackHeight.Low)
                p.JustExecutedAction = GameAction.LowAttack;
            else if (skill.Height == AttackHeight.Overhead)
                p.JustExecutedAction = GameAction.OverheadAttack;
            else if (skill.StartupFrames <= 5)
                p.JustExecutedAction = GameAction.FastPoke;
            else
                p.JustExecutedAction = GameAction.HeavyAttack;
        }
    }

    public void Update(Player p, float dt)
    {
        if (p.IsInHitstun && ActiveSkill != null)
        {
            ActiveSkill = null;
            CurrentActionFrame = 0;
            HasHit = false;
        }

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

        if (ActiveSkill != null)
        {
            if (ActiveSkill.CustomLogic && ActiveSkill.OnCustomLogic?.Function != null)
            {
                ActiveSkill.OnCustomLogic.Function.Call(p, this, dt);
            }

            CurrentActionFrame++;
            int total = ActiveSkill.StartupFrames + ActiveSkill.ActiveFrames + ActiveSkill.RecoveryFrames;
            if (CurrentActionFrame >= total)
            {
                ActiveSkill = null;
                CurrentActionFrame = 0;
                HasHit = false;
            }
        }

        // Update Animation State
        if (!p.IsAlive)
            Animator.SetState(AnimationState.Dying);
        else if (p.IsInHitstun)
            Animator.SetState(AnimationState.TakingDamage);
        else if (ActiveSkill != null)
            Animator.SetState(AnimationState.Attacking);
        else if (!p.IsGrounded)
            Animator.SetState(AnimationState.Jumping);
        else if (MathF.Abs(p.Velocity.X) > 5f)
            Animator.SetState(AnimationState.Walking);
        else
            Animator.SetState(AnimationState.Idle);

        Animator.Update(dt, Animations);

        if (OnPassive != null && OnPassive.Type == DataType.Function)
        {
            OnPassive.Function.Call(p, this, dt);
        }

        foreach (string skill in Skills.Keys)
        {
            if (SkillCooldowns.TryGetValue(skill, out float cd) && cd > 0f)
            {
                SkillCooldowns[skill] = MathF.Max(0f, cd - dt);
            }
        }
    }

    public void Draw(Player p, float alpha)
    {
        Rectangle hurtbox = p.GetRenderHurtbox(alpha);

        bool hasSprite = _spriteSheet is { id: > 0 };
        if (hasSprite)
        {
            Texture2D tex = _spriteSheet!.Value;
            int cols = Math.Max(1, tex.width / FrameWidth);
            int frameIdx = Animator.GetAtlasFrameIndex(Animations);
            int srcX = (frameIdx % cols) * FrameWidth;
            int srcY = (frameIdx / cols) * FrameHeight;

            Rectangle srcRect = new Rectangle(
                srcX,
                srcY,
                p.FacingRight ? FrameWidth : -FrameWidth,
                FrameHeight
            );

            Rectangle destRect = new Rectangle(
                hurtbox.x + DrawOffsetX,
                hurtbox.y + DrawOffsetY,
                hurtbox.width * 2f,
                hurtbox.height * 1.15f
            );

            Color tint = p.IsInvulnerable ? Color.SKYBLUE : (p.IsInHitstun ? Color.ORANGE : Color.WHITE);
            Raylib.DrawTexturePro(tex, srcRect, destRect, Vector2.Zero, 0f, tint);
        }

        if (ShowDebugBoxes || !hasSprite)
        {
            Color tintBase = PrimaryColor;
            if (!p.FacingRight)
            {
                tintBase = new Color((int)(PrimaryColor.r * 0.8f), (int)(PrimaryColor.g * 0.8f), (int)(PrimaryColor.b * 0.8f), 255);
            }

            Color bodyColor = p.IsInvulnerable
                ? Color.SKYBLUE
                : (p.IsInHitstun ? Color.ORANGE : tintBase);

            if (hasSprite)
            {
                bodyColor.a = 110;
            }

            Raylib.DrawRectangleRec(hurtbox, bodyColor);

            if (IsHyperArmorActive)
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
    }
}