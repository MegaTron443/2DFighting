using Raylib_cs;
using MoonSharp.Interpreter;

[MoonSharpUserData]
public class PhysicsEngine
{
    public const int ROUNDS_TO_WIN = 2;
    public static int CurrentRound { get; private set; } = 1;
    public static float RoundTimer { get; set; } = 99f;
    public static float RoundTransitionTimer { get; private set; } = 0f;
    public static string RoundAnnouncement { get; private set; } = "";
    public static string MatchWinnerText { get; private set; } = "";

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
    private const float ROUND_TRANSITION_DURATION = 2.0f;

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

        // Inter-round pause/announcement handling
        if (RoundTransitionTimer > 0f)
        {
            data.Player1.SavePreviousState();
            data.Player2.SavePreviousState();
            RoundTransitionTimer -= dt;

            if (RoundTransitionTimer <= 0f)
            {
                if (data.Player1.RoundsWon >= ROUNDS_TO_WIN || data.Player2.RoundsWon >= ROUNDS_TO_WIN)
                {
                    MatchWinnerText = data.Player1.RoundsWon > data.Player2.RoundsWon
                        ? $"{data.Player1.Name} ({data.Player1.CurrentCharacter.Name}) WINS!"
                        : $"{data.Player2.Name} ({data.Player2.CurrentCharacter.Name}) WINS!";
                    GameState.SetState(State.GameOver);
                }
                else
                {
                    CurrentRound++;
                    StartNextRound(data.Player1, data.Player2);
                }
            }
            return;
        }

        if (HitstopTimer > 0f)
        {
            data.Player1.SavePreviousState();
            data.Player2.SavePreviousState();
            HitstopTimer -= dt;
            return;
        }

        data.Player1.SavePreviousState();
        data.Player2.SavePreviousState();

        data.Player1.Update(dt);
        data.Player2.Update(dt);

        ClampPlayer(data.Player1);
        ClampPlayer(data.Player2);

        ResolvePushback(data.Player1, data.Player2);

        UpdateAutoFacing(data.Player1, data.Player2);
        UpdateAutoFacing(data.Player2, data.Player1);

        // Simultaneous Hit Detection (Prevents P1 Priority Bias)
        ResolveSimultaneousHits(data.Player1, data.Player2);

        RoundTimer = MathF.Max(0f, RoundTimer - dt);

        if (!data.Player1.IsInHitstun) data.Player2.ComboCount = 0;
        if (!data.Player2.IsInHitstun) data.Player1.ComboCount = 0;

        CheckRoundEndCondition(data.Player1, data.Player2);
    }

    private void CheckRoundEndCondition(Player p1, Player p2)
    {
        bool p1Dead = !p1.IsAlive;
        bool p2Dead = !p2.IsAlive;
        bool timeOut = RoundTimer <= 0f;

        if (!p1Dead && !p2Dead && !timeOut)
            return;

        if (p1Dead && p2Dead)
        {
            if (p1.RoundsWon < ROUNDS_TO_WIN - 1) p1.RoundsWon++;
            if (p2.RoundsWon < ROUNDS_TO_WIN - 1) p2.RoundsWon++;
            RoundAnnouncement = "DOUBLE K.O.!";
        }
        else if (p1Dead)
        {
            p2.RoundsWon++;
            RoundAnnouncement = $"K.O.! {p2.Name} takes Round {CurrentRound}!";
        }
        else if (p2Dead)
        {
            p1.RoundsWon++;
            RoundAnnouncement = $"K.O.! {p1.Name} takes Round {CurrentRound}!";
        }
        else if (timeOut)
        {
            if (p1.Health > p2.Health)
            {
                p1.RoundsWon++;
                RoundAnnouncement = $"TIME OVER! {p1.Name} takes Round {CurrentRound}!";
            }
            else if (p2.Health > p1.Health)
            {
                p2.RoundsWon++;
                RoundAnnouncement = $"TIME OVER! {p2.Name} takes Round {CurrentRound}!";
            }
            else
            {
                RoundAnnouncement = "TIME OVER! DRAW ROUND!";
            }
        }

        RoundTransitionTimer = ROUND_TRANSITION_DURATION;
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
        target.Health = Math.Max(0, target.Health - damage);
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
        target.Health = Math.Max(0, target.Health - damage);
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
        CurrentRound = 1;
        p1.RoundsWon = 0;
        p2.RoundsWon = 0;
        MatchWinnerText = "";
        StartNextRound(p1, p2);
    }

    public static void StartNextRound(Player p1, Player p2)
    {
        HitstopTimer = 0f;
        RoundTimer = 99f;
        RoundTransitionTimer = 0f;
        RoundAnnouncement = "";
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

    private bool CheckHitCollision(Player attacker, Player defender)
    {
        if (attacker.CurrentCharacter == null || attacker.CurrentCharacter.HasHit) return false;
        if (defender.IsInvulnerable) return false;
        if (!attacker.TryGetHitbox(out Rectangle hitbox)) return false;

        Rectangle defHurtbox = defender.GetHurtbox();
        return Raylib.CheckCollisionRecs(hitbox, defHurtbox);
    }

    private void ResolveSimultaneousHits(Player p1, Player p2)
    {
        bool p1HitsP2 = CheckHitCollision(p1, p2);
        bool p2HitsP1 = CheckHitCollision(p2, p1);

        if (p1HitsP2 && p2HitsP1)
        {
            SkillData s1 = p1.CurrentCharacter!.ActiveSkill!;
            SkillData s2 = p2.CurrentCharacter!.ActiveSkill!;

            // Active Counter beats normal attack
            if (s1.IsCounter && !s2.IsCounter)
            {
                ResolveHitInteraction(p2, p1, s2);
                return;
            }
            if (s2.IsCounter && !s1.IsCounter)
            {
                ResolveHitInteraction(p1, p2, s1);
                return;
            }

            // Otherwise, simultaneous trade: cache skills before either resets ActiveSkill
            ResolveHitInteraction(p1, p2, s1);
            ResolveHitInteraction(p2, p1, s2);
        }
        else if (p1HitsP2)
        {
            ResolveHitInteraction(p1, p2, p1.CurrentCharacter!.ActiveSkill!);
        }
        else if (p2HitsP1)
        {
            ResolveHitInteraction(p2, p1, p2.CurrentCharacter!.ActiveSkill!);
        }
    }

    private void ResolveHitInteraction(Player attacker, Player defender, SkillData attackSkill)
    {
        if (attacker.CurrentCharacter != null)
        {
            attacker.CurrentCharacter.HasHit = true;
        }

        float hitstop = attackSkill.Damage >= HEAVY_HITSTOP_DAMAGE_THRESHOLD
            ? HEAVY_HITSTOP_DURATION
            : LIGHT_HITSTOP_DURATION;
        HitstopTimer = MathF.Max(HitstopTimer, hitstop);

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
        if (attacker.CurrentCharacter != null)
        {
            attacker.CurrentCharacter.ActiveSkill = null;
        }
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