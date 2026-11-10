using System;
using System.Numerics;
using MoonSharp.Interpreter;
using Raylib_cs;

[MoonSharpUserData]
public class Projectile
{
    public Player Owner { get; }
    public Player Target { get; }

    public Vector2 Position;
    public Vector2 PreviousPosition;
    public Vector2 Velocity;

    public float PosX => Position.X;
    public float PosY => Position.Y;
    public float VelX => Velocity.X;
    public float VelY => Velocity.Y;
    public float GroundY => PhysicsEngine.GroundY;

    public float WidthPct { get; set; } = 0.035f;
    public float HeightPct { get; set; } = 0.055f;

    public int Damage { get; set; } = 12;
    public float HitStun { get; set; } = 0.30f;
    public float KnockbackPct { get; set; } = 0.15f;
    public AttackHeight attackHeight { get; set; } = AttackHeight.Mid;

    public float ElapsedTime { get; private set; } = 0f;
    public float Lifetime { get; set; } = 3.0f;
    public bool DestroyOnHit { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public bool HasHit { get; set; } = false;
    public bool FacingRight { get; set; } = true;
    public bool IsBlockBreak { get; set; } = false;

    public Color FillColor { get; set; } = new Color(255, 140, 0, 220);
    public Color BorderColor { get; set; } = Color.YELLOW;

    public DynValue? Trajectory { get; set; }
    public DynValue? OnHit { get; set; }

    public float Width => WidthPct * Raylib.GetScreenWidth();
    public float Height => HeightPct * Raylib.GetScreenHeight();

    public Projectile(Player owner, Player target, Vector2 spawnPos, Vector2 initialVelocity)
    {
        Owner = owner;
        Target = target;
        Position = spawnPos;
        PreviousPosition = spawnPos;
        Velocity = initialVelocity;
        FacingRight = owner.FacingRight;
        owner.JustExecutedAction = GameAction.ProjectileAttack;
    }

    public void SavePreviousState()
    {
        PreviousPosition = Position;
    }

    public void Translate(float dx, float dy)
    {
        Position.X += dx;
        Position.Y += dy;
    }

    public void SetVelocity(float vx, float vy)
    {
        Velocity.X = vx;
        Velocity.Y = vy;
    }

    public void ResetHit()
    {
        HasHit = false;
    }

    public void Destroy()
    {
        IsActive = false;
    }

    public void Update(float dt)
    {
        if (!IsActive) return;

        ElapsedTime += dt;
        if (ElapsedTime >= Lifetime)
        {
            IsActive = false;
            return;
        }

        // Custom Lua trajectory callback: function(proj, dt)
        if (Trajectory != null && Trajectory.Type == DataType.Function)
        {
            Trajectory.Function.Call(this, dt);
        }

        Position += Velocity * dt;

        float sw = Raylib.GetScreenWidth();
        if (Position.X < -sw * 0.25f || Position.X > sw * 1.25f)
        {
            IsActive = false;
        }
    }

    public Rectangle GetHitbox()
    {
        float w = Width;
        float h = Height;
        return new Rectangle(Position.X - (w / 2f), Position.Y - (h / 2f), w, h);
    }

    public Rectangle GetRenderHitbox(float alpha)
    {
        Vector2 renderPos = Vector2.Lerp(PreviousPosition, Position, alpha);
        float w = Width;
        float h = Height;
        return new Rectangle(renderPos.X - (w / 2f), renderPos.Y - (h / 2f), w, h);
    }

    public void Draw(float alpha)
    {
        if (!IsActive) return;
        Rectangle rect = GetRenderHitbox(alpha);
        Raylib.DrawRectangleRec(rect, FillColor);
        Raylib.DrawRectangleLinesEx(rect, 2f, BorderColor);
    }
}

public static class ProjectileFactory
{
    /// <summary>
    /// Shorthand Lua factory: create_projectile(owner, offsetVec, velocityVec, onHitFunc, trajectoryFunc)
    /// </summary>
    public static Projectile CreateShorthand(
        Player owner,
        Vector2 offsetPct,
        Vector2 velocityPct,
        DynValue? onHit = null,
        DynValue? trajectory = null)
    {
        Player target = owner.TargetOpponent!;
        float dir = owner.FacingRight ? 1f : -1f;
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        Vector2 spawnPos = new Vector2(
            owner.Position.X + (dir * offsetPct.X * sw),
            owner.Position.Y + (offsetPct.Y * sh)
        );

        Vector2 vel = new Vector2(
            dir * velocityPct.X * sw,
            velocityPct.Y * sh
        );

        var proj = new Projectile(owner, target, spawnPos, vel)
        {
            OnHit = onHit,
            Trajectory = trajectory
        };

        PhysicsEngine.SpawnProjectile(proj);
        return proj;
    }

    /// <summary>
    /// Full Configurable Lua Factory: CreateProjectile(owner, configTable)
    /// </summary>
    public static Projectile CreateFromTable(Player owner, Table config)
    {
        Player target = owner.TargetOpponent!;
        float dir = owner.FacingRight ? 1f : -1f;
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        float offXPct = (float)(config.Get("x_offset_pct").CastToNumber() ?? 0.06);
        float offYPct = (float)(config.Get("y_offset_pct").CastToNumber() ?? 0.0);
        float velXPct = (float)(config.Get("speed_x_pct").CastToNumber() ?? 0.45);
        float velYPct = (float)(config.Get("speed_y_pct").CastToNumber() ?? 0.0);

        Vector2 spawnPos = new Vector2(
            owner.Position.X + (dir * offXPct * sw),
            owner.Position.Y + (offYPct * sh)
        );

        Vector2 vel = new Vector2(
            dir * velXPct * sw,
            velYPct * sh
        );

        string heightStr = (config.Get("attack_height").String ?? "mid").ToLowerInvariant();
        AttackHeight atkHeight = heightStr switch
        {
            "low"      => AttackHeight.Low,
            "overhead" => AttackHeight.Overhead,
            _          => AttackHeight.Mid
        };

        Color fill = new Color(255, 140, 0, 220);
        var colorTbl = config.Get("color").Table;
        if (colorTbl != null)
        {
            fill = new Color(
                (int)(colorTbl.Get(1).CastToNumber() ?? 255),
                (int)(colorTbl.Get(2).CastToNumber() ?? 140),
                (int)(colorTbl.Get(3).CastToNumber() ?? 0),
                230
            );
        }

        var proj = new Projectile(owner, target, spawnPos, vel)
        {
            WidthPct = (float)(config.Get("width_pct").CastToNumber() ?? 0.035),
            HeightPct = (float)(config.Get("height_pct").CastToNumber() ?? 0.055),
            Damage = (int)(config.Get("damage").CastToNumber() ?? 12),
            HitStun = (float)(config.Get("hitStun").CastToNumber() ?? 0.30),
            KnockbackPct = (float)(config.Get("knockback_pct").CastToNumber() ?? 0.14),
            Lifetime = (float)(config.Get("lifetime").CastToNumber() ?? 3.0),
            DestroyOnHit = config.Get("destroy_on_hit").IsNil() || config.Get("destroy_on_hit").CastToBool(),
            IsBlockBreak = config.Get("is_blockbreak").CastToBool(),
            attackHeight = atkHeight,
            FillColor = fill,
            Trajectory = config.Get("trajectory"),
            OnHit = config.Get("on_hit")
        };

        PhysicsEngine.SpawnProjectile(proj);
        return proj;
    }
}