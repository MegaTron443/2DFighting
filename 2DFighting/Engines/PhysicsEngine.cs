using Raylib_cs;

public class PhysicsEngine
{
    public const float GroundY = 500f;
    public const float LeftBoundary = 40f;
    public const float RightBoundary = 760f;

    public void Initialize() { }

    public void Update(GameData data)
    {
        if (GameState.CurrentState != State.Playing) return;
        if (data.Player1.CurrentCharacter == null || data.Player2.CurrentCharacter == null) return;

        float dt = data.Timer.DeltaTime;

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
        if (p.Position.Y + ch.Height >= GroundY)
        {
            p.Position.Y = GroundY - ch.Height;
            p.Velocity.Y = 0;
            p.IsGrounded = true;
        }
        else
        {
            p.IsGrounded = false;
        }

        if (p.Position.X < LeftBoundary) p.Position.X = LeftBoundary;
        if (p.Position.X + ch.Width > RightBoundary) p.Position.X = RightBoundary - ch.Width;
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

        if (attacker.TryGetHitbox(out Rectangle hitbox))
        {
            Rectangle defHurtbox = defender.GetHurtbox();
            if (Raylib.CheckCollisionRecs(hitbox, defHurtbox))
            {
                attacker.CurrentCharacter.HasHit = true;

                defender.HitstunTimer = attacker.CurrentCharacter!.ActiveSkill!.HitStun;
                defender.Health -= attacker.CurrentCharacter!.ActiveSkill!.Damage;
                defender.Velocity.X = attacker.FacingRight ? 350f : -350f;
            }
        }
    }


}