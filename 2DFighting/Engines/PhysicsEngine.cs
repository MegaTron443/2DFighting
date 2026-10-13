using Raylib_cs;
using MoonSharp.Interpreter;

[MoonSharpUserData]
public class PhysicsEngine
{
    public static float GroundY = 0.85f * Raylib.GetScreenHeight();
    public static float LeftBoundary = 0.01f * Raylib.GetScreenWidth();
    public static float RightBoundary = 0.99f * Raylib.GetScreenWidth();

    public void Initialize() { }

    public void Update(GameData data)
    {
        if (GameState.CurrentState != State.Playing) return;
        if (data.Player1.CurrentCharacter == null || data.Player2.CurrentCharacter == null) return;

        float dt = Timer.FixedDeltaTime;
        data.Player1.Update(dt);
        data.Player2.Update(dt);

        ClampPlayer(data.Player1);
        ClampPlayer(data.Player2);
        ResolvePushback(data.Player1, data.Player2);

        CheckHits(data.Player1, data.Player2);
        CheckHits(data.Player2, data.Player1);
    }

    /// <summary>
    /// Centralized force & hitstun helper (Exposed to C# and Lua).
    /// Logic 1 (Grounded): Applies horizontal knockback + stun.
    /// Logic 2 (Airborne): No knockback (halted in air), applies stun only.
    /// </summary>
    public static void ApplyHitForce(Player source, Player target, int damage, float stun, float groundKnockbackSpeed)
    {
        target.Health -= damage;
        target.HitstunTimer = stun;

        if (target.IsGrounded)
        {
            float dir = source.FacingRight ? 1f : -1f;
            target.SetVelocity(dir * groundKnockbackSpeed, 0f);
        }
        else
        {
            target.SetVelocity(0f, 0f);
        }
    }

    private void ClampPlayer(Player p)
    {
        Character ch = p.CurrentCharacter!;
        float halfW = ch.Width / 2f;
        float halfH = ch.Height / 2f;

        if (p.Position.Y + halfH >= GroundY)
        {
            p.Position.Y = GroundY - halfH;
            p.Velocity.Y = 0;
            p.IsGrounded = true;
        }
        else
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

            float half = overlap * 0.5f;
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

        if (TryResolveCounter(attacker, defender, attackSkill))
            return;

        if (defender.IsNeutralBlocking && !attackSkill.IsBlockBreak)
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
            ApplyHitForce(defender, attacker, counterSkill.Damage, counterSkill.HitStun, 400f);
        }

        return true;
    }

    private void ApplyBlockedHit(Player attacker, Player defender, SkillData skill)
    {
        int chipDamage = (int)(skill.Damage * 0.20f);
        float blockStun = skill.HitStun * 0.4f;

        ApplyHitForce(attacker, defender, chipDamage, blockStun, 450f);
    }

    private void ApplyCleanHit(Player attacker, Player defender, SkillData skill)
    {
        if (skill.OnHit?.Function != null)
        {
            skill.OnHit.Function.Call(attacker, defender, skill);
            return;
        }

        ApplyHitForce(attacker, defender, skill.Damage, skill.HitStun, 350f);
    }
}