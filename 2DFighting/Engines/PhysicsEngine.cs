using Raylib_cs;

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
        attacker.HitstunTimer = counterSkill.HitStun;
        attacker.Health -= counterSkill.Damage;
        attacker.Velocity.X = defender.FacingRight ? 400f : -400f;

        defender.Velocity.X = 0f;

        if (counterSkill.OnCounter != null && counterSkill.OnCounter.Function != null)
        {
            counterSkill.OnCounter.Function.Call(defender, attacker);
        }

        return true;
    }

    private void ApplyBlockedHit(Player attacker, Player defender, SkillData skill)
    {
        defender.Health -= (int)(skill.Damage * 0.20f);
        defender.HitstunTimer = skill.HitStun * 0.4f;
        defender.Velocity.X = attacker.FacingRight ? 450f : -450f;
    }

    private void ApplyCleanHit(Player attacker, Player defender, SkillData skill)
    {
        defender.Health -= skill.Damage;
        defender.HitstunTimer = skill.HitStun;
        defender.Velocity.X = attacker.FacingRight ? 350f : -350f;
    }
}