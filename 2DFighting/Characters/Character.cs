using MoonSharp.Interpreter;
using Raylib_cs;

[MoonSharpUserData]
public class Character
{
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

    // --- Rendering Offsets ---
    public float DrawOffsetX => Width * DrawOffsetXPct;
    public float DrawOffsetY => Height * DrawOffsetYPct;
    public float DrawOffsetXPct { get; private set; } = 0f;
    public float DrawOffsetYPct { get; private set; } = 0f;

    // --- Actions & Skills ---
    public SkillData? ActiveSkill { get; private set; }
    public int CurrentActionFrame { get; private set; } = 0;
    public Dictionary<string, SkillData> Skills { get; } = new();
    public Dictionary<string, float> SkillCooldowns { get; } = new();
    public bool HasHit { get; set; } = false;

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

    private void LoadFromLua(string path, Script script)
    {
        script.DoFile(path);
        Table t = script.Globals.Get("Character").Table;
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

        var rend = t.Get("rendering").Table;
        if (rend != null)
        {
            DrawOffsetXPct = (float)(rend.Get("draw_offset_x_pct").CastToNumber() ?? 0.0);
            DrawOffsetYPct = (float)(rend.Get("draw_offset_y_pct").CastToNumber() ?? 0.0);
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

                Skills[pair.Key.String] = new SkillData
                {
                    Name = pair.Key.String,
                    InputKey = sTable.Get("input_trigger").String ?? "",
                    Damage = (int)(sTable.Get("damage").CastToNumber() ?? 10),
                    StartupFrames = (int)(sTable.Get("startup_frames").CastToNumber() ?? 0),
                    ActiveFrames = (int)(sTable.Get("active_frames").CastToNumber() ?? 0),
                    RecoveryFrames = (int)(sTable.Get("recovery_frames").CastToNumber() ?? 0),
                    HitboxOffsetPct = hbPctRect,
                    HitStun = (float)(sTable.Get("hitStun").CastToNumber() ?? 0.25),
                    CooldownTime = (float)(sTable.Get("cooldown_time").CastToNumber() ?? 0.0)
                };
            }
        }
    }

    public void Move(Player p, float direction)
    {
        if (ActiveSkill != null) return;

        p.Velocity.X = direction * WalkSpeed;
        if (direction > 0) p.FacingRight = true;
        if (direction < 0) p.FacingRight = false;
    }

    public void Jump(Player p)
    {
        if (p.IsGrounded && ActiveSkill == null)
        {
            p.Velocity.Y = JumpForce;
            p.IsGrounded = false;
        }
    }

    public void ExecuteSkill(Player p, string skillName)
    {
        if (SkillCooldowns.ContainsKey(skillName) && SkillCooldowns[skillName] > 0) return;
        if (ActiveSkill != null) return;
        if (Skills.TryGetValue(skillName, out var skill))
        {
            ActiveSkill = skill;
            CurrentActionFrame = 0;
            HasHit = false;
            p.Velocity.X = 0;
            p.Velocity.Y = 0;
        }
    }

    public void Update(Player p, float dt)
    {
        if (!p.IsGrounded)
        {
            if (IsHitboxActive)
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
            p.Velocity.X *= Friction;
        }

        p.Position += p.Velocity * dt;

        if (ActiveSkill != null)
        {
            CurrentActionFrame++;
            int total = ActiveSkill.StartupFrames + ActiveSkill.ActiveFrames + ActiveSkill.RecoveryFrames;
            if (CurrentActionFrame >= total)
            {
                ActiveSkill = null;
                CurrentActionFrame = 0;
                HasHit = false;
            }
        }

        foreach (var skill in SkillCooldowns.Keys.ToList())
        {
            if (SkillCooldowns[skill] > 0)
            {
                SkillCooldowns[skill] = MathF.Max(0f, SkillCooldowns[skill] - dt);
            }
        }
    }

    public void Draw(Player p)
    {
        Rectangle hurtbox = p.GetHurtbox();

        Raylib.DrawRectangleRec(hurtbox, p.FacingRight ? Color.DARKBLUE : Color.MAROON);
        Raylib.DrawRectangleLinesEx(hurtbox, 2, Color.WHITE);

        if (p.TryGetHitbox(out Rectangle hitbox))
        {
            Raylib.DrawRectangleRec(hitbox, new Color(255, 0, 0, 160));
            Raylib.DrawRectangleLinesEx(hitbox, 2, Color.YELLOW);
        }
    }
}