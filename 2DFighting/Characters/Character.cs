using MoonSharp.Interpreter;
using Raylib_cs;

[MoonSharpUserData]
public class Character
{
    public string Name { get; private set; } = "";

    // --- Physics Parameters (From Lua) ---
    public float Width { get; private set; } = 60f;
    public float Height { get; private set; } = 110f;
    public float WalkSpeed { get; private set; } = 320f;
    public float JumpForce { get; private set; } = -650f;
    public float Gravity { get; private set; } = 1400f;
    public float Friction { get; private set; } = 0.85f;
    public float HitStun { get; private set; } = 0.25f;

    // --- Rendering Offsets ---
    public float DrawOffsetX { get; private set; } = 0f;
    public float DrawOffsetY { get; private set; } = 0f;
    public int FrameWidth { get; private set; } = 128;
    public int FrameHeight { get; private set; } = 128;

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
            Width = (float)(phys.Get("width").CastToNumber() ?? 60.0);
            Height = (float)(phys.Get("height").CastToNumber() ?? 110.0);
            WalkSpeed = (float)(phys.Get("walk_speed").CastToNumber() ?? 320.0);
            JumpForce = (float)(phys.Get("jump_force").CastToNumber() ?? -650.0);
            Gravity = (float)(phys.Get("gravity").CastToNumber() ?? 1400.0);
            Friction = (float)(phys.Get("friction").CastToNumber() ?? 0.85);
        }

        var rend = t.Get("rendering").Table;
        if (rend != null)
        {
            FrameWidth = (int)(rend.Get("frame_width").CastToNumber() ?? 128);
            FrameHeight = (int)(rend.Get("frame_height").CastToNumber() ?? 128);
            DrawOffsetX = (float)(rend.Get("draw_offset_x").CastToNumber() ?? 0);
            DrawOffsetY = (float)(rend.Get("draw_offset_y").CastToNumber() ?? 0);
        }

        var skillsTable = t.Get("skills").Table;
        if (skillsTable != null)
        {
            foreach (var pair in skillsTable.Pairs)
            {
                var sTable = pair.Value.Table;
                var hb = sTable.Get("hitbox").Table;
                Rectangle hbRect = new Rectangle(0, 0, 0, 0);

                if (hb != null)
                {
                    hbRect = new Rectangle(
                        (float)(hb.Get("x_offset").CastToNumber() ?? 0),
                        (float)(hb.Get("y_offset").CastToNumber() ?? 0),
                        (float)(hb.Get("width").CastToNumber() ?? 20),
                        (float)(hb.Get("height").CastToNumber() ?? 20)
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
                    HitboxOffset = hbRect,
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