using Raylib_cs;
using MoonSharp.Interpreter;

[MoonSharpUserData]
public class PhysicsEngine
{
    public static float RoundTimer { get; set; } = 99f;
    public const float GROUND_Y_PCT = 0.85f;
    public const float LEFT_BOUNDARY_PCT = 0.01f;
    public const float RIGHT_BOUNDARY_PCT = 0.99f;

    private const float AUTO_FACE_DEADZONE_PX = 2f;
    private const float PUSHBACK_SPLIT_FACTOR = 0.5f;

    private const int HEAVY_HITSTOP_DAMAGE_THRESHOLD = 35;
    private const float HEAVY_HITSTOP_DURATION = 0.09f;
    private const float LIGHT_HITSTOP_DURATION = 0.045f;

    private const float DEFAULT_COUNTER_KNOCKBACK_PCT = 0.21f;
    private const float DEFAULT_BLOCK_KNOCKBACK_PCT = 0.23f;
    private const float DEFAULT_CLEAN_KNOCKBACK_PCT = 0.18f;

    private const float BLOCK_CHIP_DAMAGE_MULTIPLIER = 0.20f;
    private const float BLOCK_STUN_MULTIPLIER = 0.40f;

    public static float GroundY => GROUND_Y_PCT * Raylib.GetScreenHeight();
    public static float LeftBoundary => LEFT_BOUNDARY_PCT * Raylib.GetScreenWidth();
    public static float RightBoundary => RIGHT_BOUNDARY_PCT * Raylib.GetScreenWidth();

    public static float HitstopTimer { get; set; } = 0f;

    public void Initialize() { }

    public void Update(GameData data)
    {
        if (GameState.CurrentState != State.Playing) return;
        if (data.Player1.CurrentCharacter == null || data.Player2.CurrentCharacter == null) return;

        float dt = Timer.FixedDeltaTime;

        if (HitstopTimer > 0f)
        {
            HitstopTimer -= dt;
            return;
        }

        data.Player1.Update(dt);
        data.Player2.Update(dt);

        ClampPlayer(data.Player1);
        ClampPlayer(data.Player2);
        ResolvePushback(data.Player1, data.Player2);

        UpdateAutoFacing(data.Player1, data.Player2);
        UpdateAutoFacing(data.Player2, data.Player1);

        CheckHits(data.Player1, data.Player2);
        CheckHits(data.Player2, data.Player1);

        RoundTimer = MathF.Max(0f, RoundTimer - dt);
        if (RoundTimer <= 0f)
        {
            GameState.SetState(State.GameOver);
            return;
        }

        if (!data.Player1.IsInHitstun) data.Player2.ComboCount = 0;
        if (!data.Player2.IsInHitstun) data.Player1.ComboCount = 0;
    }

    private void UpdateAutoFacing(Player player, Player opponent)
    {
        if (player.IsGrounded && !player.IsInHitstun && player.CurrentCharacter?.ActiveSkill == null)
        {
            if (MathF.Abs(player.Position.X - opponent.Position.X) > AUTO_FACE_DEADZONE_PX)
            {
                player.FacingRight = player.Position.X < opponent.Position.X;
            }
        }
    }

    private bool CanBlockAttack(Player defender, SkillData attackSkill)
    {
        if (!defender.IsNeutralBlocking || attackSkill.IsBlockBreak)
            return false;

        return attackSkill.Height switch
        {
            AttackHeight.Low      => defender.IsCrouching,
            AttackHeight.Overhead => !defender.IsCrouching,
            _                     => true
        };
    }

    public static void ApplyHitForce(Player source, Player target, int damage, float stun, float groundKnockbackPct)
    {
        target.Health -= damage;

        Character? targetChar = target.CurrentCharacter;
        if (targetChar != null && targetChar.IsHyperArmorActive)
            return;

        if (targetChar != null)
        {
            targetChar.ActiveSkill = null;
            targetChar.HasHit = false;
        }

        target.HitstunTimer = stun;

        if (target.IsGrounded)
        {
            target.IsAirStalled = false;
            float dir = source.FacingRight ? 1f : -1f;
            float speedPx = groundKnockbackPct * Raylib.GetScreenWidth();
            target.SetVelocity(dir * speedPx, 0f);
        }
        else
        {
            target.IsAirStalled = true;
            target.SetVelocity(0f, 0f);
        }

        if (damage > 0 && stun > 0f)
        {
            source.ComboCount++;
        }
    }

    public static void ApplyLaunchForce(Player source, Player target, int damage, float stun, float launchXPct, float launchYPct)
    {
        target.Health -= damage;

        Character? targetChar = target.CurrentCharacter;
        if (targetChar != null && targetChar.IsHyperArmorActive)
            return;

        if (targetChar != null)
        {
            targetChar.ActiveSkill = null;
            targetChar.HasHit = false;
        }

        target.HitstunTimer = stun;
        target.IsGrounded = false;
        target.IsAirStalled = false;

        float dir = source.FacingRight ? 1f : -1f;
        float vx = dir * launchXPct * Raylib.GetScreenWidth();
        float vy = launchYPct * Raylib.GetScreenHeight();
        target.SetVelocity(vx, vy);

        if (damage > 0 && stun > 0f)
        {
            source.ComboCount++;
        }
    }

    public static void ResetMatch(Player p1, Player p2)
    {
        HitstopTimer = 0f;
        RoundTimer = 99f;
        p1.ResetState(faceRight: true);
        p2.ResetState(faceRight: false);
    }

    private void ClampPlayer(Player p)
    {
        Character ch = p.CurrentCharacter!;
        float halfW = ch.Width / 2f;
        float halfH = ch.Height / 2f;

        if (p.Position.Y + halfH >= GroundY && p.Velocity.Y >= 0f)
        {
            p.Position.Y = GroundY - halfH;
            p.Velocity.Y = 0;
            p.IsGrounded = true;
            p.IsAirStalled = false;
        }
        else if (p.Position.Y + halfH < GroundY)
        {
            p.IsGrounded = false;
        }

        if (p.Position.X - halfW < LeftBoundary)
            p.Position.X = LeftBoundary + halfW;

        if (p.Position.X + halfW > RightBoundary)
            p.Position.X = RightBoundary - halfW;
    }

    private void ResolvePushback(Player p1, Player p2)
    {
        Rectangle r1 = p1.GetHurtbox();
        Rectangle r2 = p2.GetHurtbox();

        if (Raylib.CheckCollisionRecs(r1, r2))
        {
            float overlap = (p1.Position.X < p2.Position.X)
                ? (r1.x + r1.width) - r2.x
                : (r2.x + r2.width) - r1.x;

            float half = overlap * PUSHBACK_SPLIT_FACTOR;
            if (p1.Position.X < p2.Position.X)
            {
                p1.Position.X -= half;
                p2.Position.X += half;
            }
            else
            {
                p1.Position.X += half;
                p2.Position.X -= half;
            }
        }
    }

    private void CheckHits(Player attacker, Player defender)
    {
        if (attacker.CurrentCharacter == null || attacker.CurrentCharacter.HasHit) return;
        if (!attacker.TryGetHitbox(out Rectangle hitbox)) return;

        Rectangle defHurtbox = defender.GetHurtbox();
        if (!Raylib.CheckCollisionRecs(hitbox, defHurtbox)) return;

        ResolveHitInteraction(attacker, defender);
    }

    private void ResolveHitInteraction(Player attacker, Player defender)
    {
        attacker.CurrentCharacter!.HasHit = true;
        SkillData attackSkill = attacker.CurrentCharacter.ActiveSkill!;

        HitstopTimer = attackSkill.Damage >= HEAVY_HITSTOP_DAMAGE_THRESHOLD
            ? HEAVY_HITSTOP_DURATION
            : LIGHT_HITSTOP_DURATION;

        if (TryResolveCounter(attacker, defender, attackSkill))
            return;

        if (CanBlockAttack(defender, attackSkill))
        {
            ApplyBlockedHit(attacker, defender, attackSkill);
            return;
        }

        if (defender.IsNeutralBlocking && attackSkill.IsBlockBreak)
        {
            if (attackSkill.OnBlockBreak?.Function != null)
            {
                attackSkill.OnBlockBreak.Function.Call(attacker, defender);
            }
            else
            {
                ApplyCleanHit(attacker, defender, attackSkill);
            }
            return;
        }

        ApplyCleanHit(attacker, defender, attackSkill);
    }

    private bool TryResolveCounter(Player attacker, Player defender, SkillData attackSkill)
    {
        Character? defChar = defender.CurrentCharacter;
        if (defChar?.ActiveSkill == null || !defChar.ActiveSkill.IsCounter || !defChar.IsHitboxActive)
            return false;

        SkillData counterSkill = defChar.ActiveSkill;

        attacker.CurrentCharacter!.ActiveSkill = null;
        defender.SetVelocity(0f, 0f);

        if (counterSkill.OnCounter?.Function != null)
        {
            counterSkill.OnCounter.Function.Call(defender, attacker);
        }
        else
        {
            ApplyHitForce(defender, attacker, counterSkill.Damage, counterSkill.HitStun, DEFAULT_COUNTER_KNOCKBACK_PCT);
        }

        return true;
    }

    private void ApplyBlockedHit(Player attacker, Player defender, SkillData skill)
    {
        int chipDamage = (int)(skill.Damage * BLOCK_CHIP_DAMAGE_MULTIPLIER);
        float blockStun = skill.HitStun * BLOCK_STUN_MULTIPLIER;

        ApplyHitForce(attacker, defender, chipDamage, blockStun, DEFAULT_BLOCK_KNOCKBACK_PCT);
    }

    private void ApplyCleanHit(Player attacker, Player defender, SkillData skill)
    {
        if (skill.OnHit?.Function != null)
        {
            skill.OnHit.Function.Call(attacker, defender, skill);
            return;
        }

        ApplyHitForce(attacker, defender, skill.Damage, skill.HitStun, DEFAULT_CLEAN_KNOCKBACK_PCT);
    }
}