using System;
using System.Collections.Generic;
using System.Numerics;
using MoonSharp.Interpreter;
using Raylib_cs;

[MoonSharpUserData]
public class PhysicsEngine
{
    public const int ROUNDS_TO_WIN = MatchManager.RoundsToWin;
    public const float GROUND_Y_PCT = 0.85f;
    public const float LEFT_BOUNDARY_PCT = 0.01f;
    public const float RIGHT_BOUNDARY_PCT = 0.99f;
    public const float LOBBY_HATCH_LEFT_PCT = 0.425f;
    public const float LOBBY_HATCH_RIGHT_PCT = 0.575f;
    private const float AUTO_FACE_DEADZONE_PX = 2f;
    private const float PUSHBACK_SPLIT_FACTOR = 0.5f;
    private const float LOBBY_DUMMY_REGEN_DELAY_SEC = 3.0f;
    private const float LOBBY_FACING_MIN_SPEED_PX = 20f;

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

    public static MatchTelemetryTracker? ActiveTelemetry => _activeInstance?.Match.Telemetry;
    public static List<Projectile> ActiveProjectiles => _activeInstance.Projectiles.ActiveProjectiles;

    public static float GroundY => GROUND_Y_PCT * Raylib.GetScreenHeight();
    public static float LeftBoundary => LEFT_BOUNDARY_PCT * Raylib.GetScreenWidth();
    public static float RightBoundary => RIGHT_BOUNDARY_PCT * Raylib.GetScreenWidth();

    public static float GetEffectiveGroundY(Vector2 position)
    {
        float sh = Raylib.GetScreenHeight();
        if (SceneManager.CurrentScene is { HasTrainingLab: true } && position.Y > sh)
            return (1.0f + GROUND_Y_PCT) * sh;

        return GROUND_Y_PCT * sh;
    }

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
        if (!GameState.IsLiveArena) return;
        if (data.Player1?.CurrentCharacter == null || data.Player2?.CurrentCharacter == null) return;

        bool isLobby = GameState.CurrentState == State.MainMenu;
        float dt = Timer.FixedDeltaTime;
        Player p1 = data.Player1;
        Player p2 = data.Player2;

        if (!isLobby && Match.TickRoundTransition(data, Projectiles, dt))
            return;

        if (!Match.IsTrainingMode)
        {
            data.SoundHandler?.UpdateTick(dt);
        }
        PostFXManager.UpdateTick(dt);

        if (Match.TickHitstop(p1, p2, dt))
            return;

        p1.SavePreviousState();
        p2.SavePreviousState();

        p1.Update(dt);
        p2.Update(dt);

        TryPlaySkillStartSound(data, p1);
        TryPlaySkillStartSound(data, p2);

        ClampPlayer(p1, allowExtendedBounds: isLobby);
        ClampPlayer(p2, allowExtendedBounds: false);

        ResolvePushback(p1, p2);

        if (isLobby)
        {
            UpdateLobbyPlayerFacing(p1);
        }
        else
        {
            UpdateAutoFacing(p1, p2);
        }
        UpdateAutoFacing(p2, p1);

        Combat.ResolveSimultaneousHits(data, p1, p2);

        Projectiles.Update(dt, Match);
        VFXManager.Update(dt);

        if (!p1.IsInHitstun) p2.ComboCount = 0;
        if (!p2.IsInHitstun) p1.ComboCount = 0;

        if (isLobby)
        {
            UpdateLobbyAutoRegen(data, p1, p2);
        }
        else
        {
            Match.TickRoundTimer(dt);
            Match.CheckRoundEndCondition(data, p1, p2);
        }
    }

    private static void TryPlaySkillStartSound(GameData data, Player player)
    {
        if (player.CurrentCharacter?.ActiveSkill != null && player.CurrentCharacter.CurrentActionFrame == 1)
        {
            data.SoundHandler?.PlaySkillSound(player, player.CurrentCharacter.ActiveSkill.Name);
        }
    }

    private static void UpdateLobbyPlayerFacing(Player player)
    {
        if (!player.IsInHitstun &&
            player.CurrentCharacter?.ActiveSkill == null &&
            MathF.Abs(player.Velocity.X) > LOBBY_FACING_MIN_SPEED_PX)
        {
            player.FacingRight = player.Velocity.X > 0f;
        }
    }

    private static void UpdateLobbyAutoRegen(GameData data, Player p1, Player p2)
    {
        if (p2.AutoHealEnabled)
        {
            bool dummyShouldHeal = p2.Health <= 0 || (p2.Health < Player.MAX_HEALTH && p2.TimeSinceHitstun >= LOBBY_DUMMY_REGEN_DELAY_SEC);
            if (dummyShouldHeal)
            {
                p2.Health = Player.MAX_HEALTH;
                p2.TimeSinceHitstun = 0f;
                VFXManager.SpawnPreset(VFXPreset.HealGlow, p2.Position, p2.FacingRight);
                data.SoundHandler?.PlaySound(SoundType.RoundStart);
            }
        }

        if (p1.Health <= 0)
        {
            p1.Health = Player.MAX_HEALTH;
            p1.HitstunTimer = 0f;
            VFXManager.SpawnPreset(VFXPreset.HealGlow, p1.Position, p1.FacingRight);
            data.SoundHandler?.PlaySound(SoundType.RoundStart);
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

    private static void ClampPlayer(Player p, bool allowExtendedBounds)
    {
        Character ch = p.CurrentCharacter!;
        float halfW = ch.Width / 2f;
        float halfH = ch.Height / 2f;

        float sw = Raylib.GetScreenWidth();
        float sh = Raylib.GetScreenHeight();
        Scene? scene = SceneManager.CurrentScene;
        bool inBottomRoom = scene is { HasTrainingLab: true } && p.Position.Y > sh;

        float prevFeetY = p.PreviousPosition.Y + halfH;
        float currFeetY = p.Position.Y + halfH;
        bool landedOnPlatform = false;

        if (p.Velocity.Y >= 0f &&
            p.PlatformDropTimer <= 0f &&
            scene is { Platforms.Count: > 0 })
        {
            float bestTopY = float.MaxValue;
            float feetHalfW = halfW * 0.65f;

            for (int i = 0; i < scene.Platforms.Count; i++)
            {
                Rectangle plat = scene.Platforms[i].GetPixelBounds();
                bool overlapsX = (p.Position.X + feetHalfW) > plat.x &&
                                 (p.Position.X - feetHalfW) < (plat.x + plat.width);
                if (!overlapsX) continue;

                if (prevFeetY <= plat.y + 6f && currFeetY >= plat.y - 1f && plat.y < bestTopY)
                {
                    bestTopY = plat.y;
                    landedOnPlatform = true;
                }
            }

            if (landedOnPlatform)
            {
                p.Position.Y = bestTopY - halfH;
                p.Velocity.Y = 0f;
                p.IsGrounded = true;
                p.IsOnPlatform = true;
                p.IsAirStalled = false;
                p.HasUsedAirStall = false;
            }
        }

        if (!landedOnPlatform)
        {
            float activeGroundY = inBottomRoom ? (1.0f + GROUND_Y_PCT) * sh : GroundY;
            bool overHatchGap = !inBottomRoom &&
                                allowExtendedBounds &&
                                scene is { HasTrainingLab: true } &&
                                p.Position.X >= sw * LOBBY_HATCH_LEFT_PCT &&
                                p.Position.X <= sw * LOBBY_HATCH_RIGHT_PCT;

            if (!overHatchGap && currFeetY >= activeGroundY && p.Velocity.Y >= 0f)
            {
                p.Position.Y = activeGroundY - halfH;
                p.Velocity.Y = 0f;
                p.IsGrounded = true;
                p.IsOnPlatform = false;
                p.IsAirStalled = false;
                p.HasUsedAirStall = false;
            }
            else if (overHatchGap || currFeetY < activeGroundY || p.PlatformDropTimer > 0f || p.Velocity.Y < 0f)
            {
                p.IsGrounded = false;
                p.IsOnPlatform = false;
            }
        }

        if (inBottomRoom)
        {
            float labCeiling = sh * 1.02f;
            if (p.Velocity.Y < 0f && p.Position.Y - halfH < labCeiling)
            {
                p.Position.Y = labCeiling + halfH;
                p.Velocity.Y = 0f;
            }

            float maxBottom = (allowExtendedBounds && scene != null ? scene.PlayableBottomPct : 2.0f) * sh;
            if (p.Position.Y + halfH > maxBottom)
                p.Position.Y = maxBottom - halfH;
        }
        else if (allowExtendedBounds && scene != null)
        {
            float topBound = scene.PlayableTopPct * sh;
            if (p.Position.Y - halfH < topBound)
            {
                p.Position.Y = topBound + halfH;
                if (p.Velocity.Y < 0f) p.Velocity.Y = 0f;
            }
        }

        float leftBound = (!inBottomRoom && allowExtendedBounds && scene != null)
            ? scene.PlayableLeftPct * sw
            : LeftBoundary;
        float rightBound = (!inBottomRoom && allowExtendedBounds && scene != null)
            ? scene.PlayableRightPct * sw
            : RightBoundary;

        if (p.Position.X - halfW < leftBound)
            p.Position.X = leftBound + halfW;
        if (p.Position.X + halfW > rightBound)
            p.Position.X = rightBound - halfW;
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
