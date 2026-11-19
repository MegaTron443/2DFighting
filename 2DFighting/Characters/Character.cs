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
    public RenderMaterial Material { get; set; } = new();
    public float OffsetXPct { get; set; } = 0f;
    public float OffsetYPct { get; set; } = 0f;
    public float WidthPct { get; set; } = 1.0f;
    public float HeightPct { get; set; } = 1.0f;
    public float RotationDeg { get; set; } = 0f;
    public bool IsSocketProp { get; set; } = false;
    public Color FallbackColor { get; set; } = Color.WHITE;
}

public struct AfterimageSnapshot
{
    public Vector2 Position;
    public bool FacingRight;
    public int AtlasFrame;
    public float Alpha;
    public Vector3 TintColor;
    public float WeaponAngleDeg;
}

[MoonSharpUserData]
public class Character
{
    public string RawLuaCode { get; private set; } = "";
    public static bool ShowDebugBoxes { get; set; } = false;

    public string Name { get; private set; } = "";

    // --- Viewport Percentage Ratios (Mutable from Lua for Stance Switching!) ---
    public float WidthPct { get; set; } = 0.05f;
    public float HeightPct { get; set; } = 0.20f;
    public float WalkSpeedPct { get; set; } = 0.25f;
    public float JumpForcePct { get; set; } = -0.65f;
    public float GravityPct { get; set; } = 1.40f;
    public float Friction { get; set; } = 0.85f;
    public float HitStun { get; set; } = 0.25f;

    // --- Physics Parameters (From Lua) ---
    public float Width => Raylib.GetScreenWidth() * WidthPct;
    public float Height => Raylib.GetScreenHeight() * HeightPct;
    public float WalkSpeed => Raylib.GetScreenWidth() * WalkSpeedPct;
    public float JumpForce => Raylib.GetScreenHeight() * JumpForcePct;
    public float Gravity => Raylib.GetScreenHeight() * GravityPct;

    // --- Rendering, Shaders & Compound Parts ---
    public float DrawOffsetX => Width * DrawOffsetXPct;
    public float DrawOffsetY => Height * DrawOffsetYPct;
    public float DrawOffsetXPct { get; private set; } = 0f;
    public float DrawOffsetYPct { get; private set; } = 0f;
    public int FrameWidth { get; private set; } = 128;
    public int FrameHeight { get; private set; } = 128;
    private Texture2D? _spriteSheet = null;

    public RenderMaterial BaseMaterial { get; } = new();
    public Dictionary<string, SpritePart> Parts { get; } = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SpritePart> _sortedParts = new();

    public Dictionary<string, AnimationClip> Animations { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Animation Animator { get; } = new();

    // --- Afterimage Ghost Trail Ring Buffer ---
    private readonly List<AfterimageSnapshot> _afterimages = new(8);
    public bool EmitAfterimages { get; set; } = false;
    public Vector3 AfterimageTint { get; set; } = new Vector3(0.65f, 0.20f, 1.0f);

    // --- Actions & Skills ---
    public SkillData? ActiveSkill { get; set; }
    public int CurrentActionFrame { get; private set; } = 0;
    public Dictionary<string, SkillData> Skills { get; } = new();
    public Dictionary<string, float> SkillCooldowns { get; } = new();
    public bool HasHit { get; set; } = false;

    public Color PrimaryColor { get; set; } = Color.DARKBLUE;
    public Color SecondaryColor { get; set; } = Color.MAROON;
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


    public void SetPartVisible(string partName, bool visible)
    {
        if (Parts.TryGetValue(partName, out var part))
            part.Visible = visible;
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

    public void SpawnAfterimage(float r = 165f, float g = 50f, float b = 255f)
    {
        Vector3 col = (r > 1f || g > 1f || b > 1f)
            ? new Vector3(r / 255f, g / 255f, b / 255f)
            : new Vector3(r, g, b);
        AfterimageTint = col;
        EmitAfterimages = true;
    }

    private void LoadFromLua(string path, Script script)
    {
        Table env = new Table(script);
        Table meta = new Table(script);
        meta["__index"] = script.Globals;
        env.MetaTable = meta;

        string[] forwardedGlobals =
        {
            "Keys", "Trajectories", "ApplyHitForce", "ApplyLaunchForce",
            "create_projectile", "CreateProjectile", "PhysicsEngine", "NewVec2",
            "math", "PlaySound", "SpawnVFX", "TriggerAudioDuck", "SetAudioBusVolume",
            "SetAudioBusLowPass", "EnablePostFX", "DisablePostFX", "SetPostFXFloat",
            "SetPostFXVec2", "SetPostFXColor", "TriggerScreenShake", "TriggerImpactFrame",
            "TriggerPaletteShift", "TriggerGravityPinch", "SetHitstop"
        };
        foreach (string g in forwardedGlobals)
        {
            env[g] = script.Globals.Get(g);
        }

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

            string? baseShader = rend.Get("shader").String;
            if (!string.IsNullOrEmpty(baseShader))
            {
                BaseMaterial.ShaderId = baseShader;
                BaseMaterial.SetColor("u_PrimaryColor", PrimaryColor.r, PrimaryColor.g, PrimaryColor.b);
            }

            var partsTbl = rend.Get("parts").Table;
            if (partsTbl != null)
            {
                foreach (var pair in partsTbl.Pairs)
                {
                    string partName = pair.Key.String;
                    Table? pTbl = pair.Value.Table;
                    if (pTbl == null) continue;

                    var part = new SpritePart
                    {
                        Name = partName,
                        ZOrder = (int)(pTbl.Get("z_order").CastToNumber() ?? 0),
                        Visible = pTbl.Get("visible").IsNil() || pTbl.Get("visible").CastToBool(),
                        OffsetXPct = (float)(pTbl.Get("x_offset_pct").CastToNumber() ?? 0.0),
                        OffsetYPct = (float)(pTbl.Get("y_offset_pct").CastToNumber() ?? 0.0),
                        WidthPct = (float)(pTbl.Get("width_pct").CastToNumber() ?? 1.0),
                        HeightPct = (float)(pTbl.Get("height_pct").CastToNumber() ?? 1.0),
                        RotationDeg = (float)(pTbl.Get("rotation").CastToNumber() ?? 0.0),
                        IsSocketProp = pTbl.Get("is_socket").CastToBool()
                    };

                    string? partSheet = pTbl.Get("sprite_sheet").String;
                    if (!string.IsNullOrEmpty(partSheet))
                    {
                        string? resolvedPartTex = ModManager.ResolveFile(partSheet);
                        if (resolvedPartTex != null && File.Exists(resolvedPartTex))
                        {
                            part.Texture = GetOrLoadSpriteSheet(resolvedPartTex);
                        }
                    }

                    string? partShader = pTbl.Get("shader").String ?? BaseMaterial.ShaderId;
                    part.Material.ShaderId = partShader;

                    if (pTbl.Get("additive").CastToBool())
                        part.Material.BlendMode = BlendMode.BLEND_ADDITIVE;

                    if (pTbl.Get("portal_mode").CastToBool())
                        part.Material.SetFloat("u_PortalMode", 1.0f);

                    var pCol = pTbl.Get("color").Table;
                    if (pCol != null)
                    {
                        int r = (int)(pCol.Get(1).CastToNumber() ?? 255);
                        int g = (int)(pCol.Get(2).CastToNumber() ?? 255);
                        int b = (int)(pCol.Get(3).CastToNumber() ?? 255);
                        part.FallbackColor = new Color(r, g, b, 255);
                        part.Material.SetColor("u_PrimaryColor", r, g, b);
                        part.Material.SetColor("u_GlowColor", r, g, b);
                    }

                    Parts[partName] = part;
                    _sortedParts.Add(part);
                }

                _sortedParts.Sort((a, b) => a.ZOrder.CompareTo(b.ZOrder));
            }
        }

        var animTable = t.Get("animations").Table;
        if (animTable != null)
        {
            foreach (var pair in animTable.Pairs)
            {
                var clipTable = pair.Value.Table;
                if (clipTable == null) continue;

                var clip = new AnimationClip
                {
                    StartFrame = (int)(clipTable.Get("start_frame").CastToNumber() ?? 0),
                    FrameCount = (int)(clipTable.Get("frame_count").CastToNumber() ?? 1),
                    Speed = (float)(clipTable.Get("speed").CastToNumber() ?? 8.0)
                };

                var keyframesTbl = clipTable.Get("keyframes").Table;
                if (keyframesTbl != null)
                {
                    clip.Keyframes = new List<KeyframeStep>();
                    foreach (var kfPair in keyframesTbl.Pairs)
                    {
                        Table? kf = kfPair.Value.Table;
                        if (kf == null) continue;
                        clip.Keyframes.Add(new KeyframeStep
                        {
                            AtlasFrame = (int)(kf.Get("frame").CastToNumber() ?? 0),
                            HoldTicks = (int)(kf.Get("duration").CastToNumber() ?? 4),
                            SpawnAfterimage = kf.Get("afterimage").CastToBool(),
                            WeaponOffsetPct = new Vector2(
                                (float)(kf.Get("weapon_x").CastToNumber() ?? 0.0),
                                (float)(kf.Get("weapon_y").CastToNumber() ?? 0.0)
                            ),
                            WeaponAngleDeg = (float)(kf.Get("weapon_angle").CastToNumber() ?? 0.0)
                        });
                    }
                }

                Animations[pair.Key.String] = clip;
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
                    AnimationClipName = sTable.Get("animation").String ?? pair.Key.String,
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
            EmitAfterimages = false;
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
                EmitAfterimages = false;
            }
        }

        if (!p.IsAlive)
            Animator.SetState(AnimationState.Dying);
        else if (p.IsInHitstun)
            Animator.SetState(AnimationState.TakingDamage);
        else if (ActiveSkill != null)
            Animator.SetState(AnimationState.Attacking, ActiveSkill.AnimationClipName);
        else if (!p.IsGrounded)
            Animator.SetState(AnimationState.Jumping);
        else if (MathF.Abs(p.Velocity.X) > 5f)
            Animator.SetState(AnimationState.Walking);
        else
            Animator.SetState(AnimationState.Idle);

        Animator.Update(dt, Animations);

        var currentKf = Animator.GetCurrentKeyframe(Animations);
        bool shouldSnapshot = EmitAfterimages || (currentKf.HasValue && currentKf.Value.SpawnAfterimage);

        for (int i = _afterimages.Count - 1; i >= 0; i--)
        {
            var snap = _afterimages[i];
            snap.Alpha -= dt * 3.8f;
            if (snap.Alpha <= 0.02f)
                _afterimages.RemoveAt(i);
            else
                _afterimages[i] = snap;
        }

        if (shouldSnapshot && _afterimages.Count < 6)
        {
            float wepAngle = currentKf?.WeaponAngleDeg ?? 0f;
            _afterimages.Add(new AfterimageSnapshot
            {
                Position = p.Position,
                FacingRight = p.FacingRight,
                AtlasFrame = Animator.GetAtlasFrameIndex(Animations),
                Alpha = 0.65f,
                TintColor = AfterimageTint,
                WeaponAngleDeg = wepAngle
            });
            EmitAfterimages = false;
        }

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
        bool hasCompoundParts = _sortedParts.Count > 0;

        for (int i = 0; i < _afterimages.Count; i++)
        {
            var snap = _afterimages[i];
            Color ghostCol = new Color(
                (int)(snap.TintColor.X * 255),
                (int)(snap.TintColor.Y * 255),
                (int)(snap.TintColor.Z * 255),
                (byte)Math.Clamp(snap.Alpha * 255f, 0f, 255f)
            );
            Rectangle ghostBox = new Rectangle(
                snap.Position.X - (Width / 2f),
                snap.Position.Y - (Height / 2f),
                Width,
                Height
            );

            if (hasSprite)
            {
                DrawAtlasSprite(_spriteSheet!.Value, snap.AtlasFrame, snap.FacingRight, ghostBox, ghostCol);
            }
            else
            {
                Raylib.DrawRectangleRec(ghostBox, ghostCol);
            }
        }

        if (hasCompoundParts)
        {
            var kf = Animator.GetCurrentKeyframe(Animations);
            int atlasFrame = Animator.GetAtlasFrameIndex(Animations);
            float dir = p.FacingRight ? 1f : -1f;

            for (int i = 0; i < _sortedParts.Count; i++)
            {
                SpritePart part = _sortedParts[i];
                if (!part.Visible) continue;

                float offX = part.OffsetXPct * Width * dir;
                float offY = part.OffsetYPct * Height;
                float rot = part.RotationDeg * dir;

                if (part.IsSocketProp && kf.HasValue)
                {
                    offX += kf.Value.WeaponOffsetPct.X * Width * dir;
                    offY += kf.Value.WeaponOffsetPct.Y * Height;
                    rot += kf.Value.WeaponAngleDeg * dir;
                }

                float partW = Width * part.WidthPct;
                float partH = Height * part.HeightPct;

                Rectangle partDest = new Rectangle(
                    hurtbox.CenterX() + offX - (partW * 0.5f),
                    hurtbox.CenterY() + offY - (partH * 0.5f),
                    partW,
                    partH
                );

                bool shaderStarted = part.Material.Begin();

                Texture2D? texToUse = part.Texture ?? (_spriteSheet.HasValue && !part.IsSocketProp ? _spriteSheet : null);
                if (texToUse is { id: > 0 } validTex)
                {
                    DrawAtlasSprite(validTex, part.IsSocketProp ? 0 : atlasFrame, p.FacingRight, partDest, Color.WHITE, rot);
                }
                else
                {
                    Texture2D defaultTex = new Texture2D { id = 1, width = 1, height = 1, mipmaps = 1, format = PixelFormat.PIXELFORMAT_UNCOMPRESSED_R8G8B8A8 };
                    Vector2 origin = new Vector2(partW * 0.5f, partH * 0.5f);
                    Rectangle centeredDest = new Rectangle(hurtbox.CenterX() + offX, hurtbox.CenterY() + offY, partW, partH);
                    Raylib.DrawTexturePro(defaultTex, new Rectangle(0, 0, 1, 1), centeredDest, origin, rot, part.FallbackColor);
                }

                part.Material.End(shaderStarted);
            }
        }
        else if (hasSprite)
        {
            bool shaderStarted = BaseMaterial.Begin();
            Color tint = p.IsInvulnerable ? Color.SKYBLUE : (p.IsInHitstun ? Color.ORANGE : Color.WHITE);
            DrawAtlasSprite(_spriteSheet!.Value, Animator.GetAtlasFrameIndex(Animations), p.FacingRight, hurtbox, tint);
            BaseMaterial.End(shaderStarted);
        }

        if (ShowDebugBoxes || (!hasSprite && !hasCompoundParts))
        {
            Color tintBase = PrimaryColor;
            if (!p.FacingRight)
            {
                tintBase = new Color((int)(PrimaryColor.r * 0.8f), (int)(PrimaryColor.g * 0.8f), (int)(PrimaryColor.b * 0.8f), 255);
            }

            Color bodyColor = p.IsInvulnerable
                ? Color.SKYBLUE
                : (p.IsInHitstun ? Color.ORANGE : tintBase);

            if (hasSprite || hasCompoundParts)
            {
                bodyColor.a = 90;
            }

            bool shaderStarted = !hasCompoundParts && BaseMaterial.Begin();
            Raylib.DrawRectangleRec(hurtbox, bodyColor);
            BaseMaterial.End(shaderStarted);

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

    private void DrawAtlasSprite(Texture2D tex, int frameIdx, bool facingRight, Rectangle hurtbox, Color tint, float rotationDeg = 0f)
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
            hurtbox.x + DrawOffsetX,
            hurtbox.y + DrawOffsetY,
            hurtbox.width * 2f,
            hurtbox.height * 1.15f
        );

        Raylib.DrawTexturePro(tex, srcRect, destRect, Vector2.Zero, rotationDeg, tint);
    }
}

public static class RectangleExtensions
{
    public static float CenterX(this Rectangle r) => r.x + (r.width * 0.5f);
    public static float CenterY(this Rectangle r) => r.y + (r.height * 0.5f);
}