using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using Raylib_cs;

[MoonSharpUserData]
public class PhysicsEngine
{
    public const int ROUNDS_TO_WIN = MatchManager.RoundsToWin;
    public const float GROUND_Y_PCT = 0.85f;
    public const float LEFT_BOUNDARY_PCT = 0.01f;
    public const float RIGHT_BOUNDARY_PCT = 0.99f;
    private const float AUTO_FACE_DEADZONE_PX = 2f;
    private const float PUSHBACK_SPLIT_FACTOR = 0.5f;

    private static PhysicsEngine _activeInstance = new();

    public MatchManager Match { get; }
    public ProjectileManager Projectiles { get; }
    public CombatResolver Combat { get; }

    public PhysicsEngine()
    {
        Match = new MatchManager();
        Projectiles = new ProjectileManager();
        Combat = new CombatResolver(Match);
        _activeInstance = this;
    }

    // --- Static Compatibility Facade (Lua / Legacy Callers) ---
    public static bool IsTrainingMode
    {
        get => _activeInstance.Match.IsTrainingMode;
        set => _activeInstance.Match.IsTrainingMode = value;
    }

    public static int CurrentRound => _activeInstance.Match.CurrentRound;

    public static float RoundTimer
    {
        get => _activeInstance.Match.RoundTimer;
        set => _activeInstance.Match.RoundTimer = value;
    }

    public static float RoundTransitionTimer => _activeInstance.Match.RoundTransitionTimer;
    public static string RoundAnnouncement => _activeInstance.Match.RoundAnnouncement;
    public static string MatchWinnerText => _activeInstance.Match.MatchWinnerText;

    public static float HitstopTimer
    {
        get => _activeInstance.Match.HitstopTimer;
        set => _activeInstance.Match.HitstopTimer = value;
    }

    public static List<Projectile> ActiveProjectiles => _activeInstance.Projectiles.ActiveProjectiles;

    public static float GroundY => GROUND_Y_PCT * Raylib.GetScreenHeight();
    public static float LeftBoundary => LEFT_BOUNDARY_PCT * Raylib.GetScreenWidth();
    public static float RightBoundary => RIGHT_BOUNDARY_PCT * Raylib.GetScreenWidth();

    public static void ApplyHitForce(Player source, Player target, int damage, float stun, float groundKnockbackPct) =>
        CombatResolver.ApplyHitForce(source, target, damage, stun, groundKnockbackPct);

    public static void ApplyLaunchForce(Player source, Player target, int damage, float stun, float launchXPct, float launchYPct) =>
        CombatResolver.ApplyLaunchForce(source, target, damage, stun, launchXPct, launchYPct);

    public static void ResetMatch(Player p1, Player p2) =>
        _activeInstance.Match.ResetMatch(p1, p2, _activeInstance.Projectiles);

    public static void StartNextRound(Player p1, Player p2) =>
        _activeInstance.Match.StartNextRound(p1, p2, _activeInstance.Projectiles);

    public static void SpawnProjectile(Projectile p) => _activeInstance.Projectiles.Spawn(p);

    public static void DrawProjectiles(float alpha) => _activeInstance.Projectiles.Draw(alpha);

    public void Initialize() { }

    public void Update(GameData data)
    {
        if (GameState.CurrentState != State.Playing) return;
        if (data.Player1?.CurrentCharacter == null || data.Player2?.CurrentCharacter == null) return;

        float dt = Timer.FixedDeltaTime;
        Player p1 = data.Player1;
        Player p2 = data.Player2;

        if (Match.TickRoundTransition(data, Projectiles, dt))
            return;

        data.SoundHandler?.UpdateTick(dt);
        PostFXManager.UpdateTick(dt);

        if (Match.TickHitstop(p1, p2, dt))
            return;

        p1.SavePreviousState();
        p2.SavePreviousState();

        p1.Update(dt);
        p2.Update(dt);

        TryPlaySkillStartSound(data, p1);
        TryPlaySkillStartSound(data, p2);

        ClampPlayer(p1);
        ClampPlayer(p2);

        ResolvePushback(p1, p2);

        UpdateAutoFacing(p1, p2);
        UpdateAutoFacing(p2, p1);

        Combat.ResolveSimultaneousHits(data, p1, p2);

        Projectiles.Update(dt, Match);
        VFXManager.Update(dt);

        Match.TickRoundTimer(dt);

        if (!p1.IsInHitstun) p2.ComboCount = 0;
        if (!p2.IsInHitstun) p1.ComboCount = 0;

        Match.CheckRoundEndCondition(data, p1, p2);
    }

    private static void TryPlaySkillStartSound(GameData data, Player player)
    {
        if (player.CurrentCharacter?.ActiveSkill != null && player.CurrentCharacter.CurrentActionFrame == 1)
        {
            data.SoundHandler?.PlaySkillSound(player, player.CurrentCharacter.ActiveSkill.Name);
        }
    }

    private static void UpdateAutoFacing(Player player, Player opponent)
    {
        if (player.IsGrounded && !player.IsInHitstun && player.CurrentCharacter?.ActiveSkill == null)
        {
            if (MathF.Abs(player.Position.X - opponent.Position.X) > AUTO_FACE_DEADZONE_PX)
            {
                player.FacingRight = player.Position.X < opponent.Position.X;
            }
        }
    }

    private static void ClampPlayer(Player p)
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

    private static void ResolvePushback(Player p1, Player p2)
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
}
