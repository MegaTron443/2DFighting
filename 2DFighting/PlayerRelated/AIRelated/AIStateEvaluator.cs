using System;
using Raylib_cs;

public class AIStateEvaluator
{
    private const float CornerMarginPct = 0.13f;
    private const float ProjectileMovingVelocityThreshold = 15f;
    private const float TrapProximityThresholdPct = 0.16f;
    private const float EmergencyProjectileDistPct = 0.18f;
    private const float EmergencyMeleeGapPct = 0.20f;
    private const float BodyMarginMultiplier = 0.55f;

    private readonly Player _self;
    private Player? _opponent;

    public AIStateEvaluator(Player self, Player? opponent = null)
    {
        _self = self;
        _opponent = opponent;
    }

    public void SetOpponent(Player? opponent) => _opponent = opponent;

    public float GetDistancePct()
    {
        if (_opponent == null) return 1f;
        float sw = PhysicsEngine.ViewportWidth;
        return Math.Abs(_self.Position.X - _opponent.Position.X) / sw;
    }

    public float GetEffectiveGapPct()
    {
        if (_self.CurrentCharacter == null) return 1f;
        float distPct = GetDistancePct();
        float bodyMargin = (_self.CurrentCharacter.WidthPct + (_opponent?.CurrentCharacter?.WidthPct ?? 0.05f)) * BodyMarginMultiplier;
        return Math.Max(0f, distPct - bodyMargin);
    }

    public int GetOpponentRemainingRecoveryFrames()
    {
        if (_opponent?.CurrentCharacter?.ActiveSkill == null) return 0;
        SkillData s = _opponent.CurrentCharacter.ActiveSkill;
        int totalFrames = s.StartupFrames + s.ActiveFrames + s.RecoveryFrames;
        return Math.Max(0, totalFrames - _opponent.CurrentCharacter.CurrentActionFrame);
    }

    public bool IsSelfNearCorner() => IsNearCorner(_self);

    public bool IsOpponentNearCorner() => IsNearCorner(_opponent);

    public bool IsNearCorner(Player? target)
    {
        if (target == null) return false;
        float sw = PhysicsEngine.ViewportWidth;
        float xPct = target.Position.X / sw;
        return xPct < CornerMarginPct || xPct > (1f - CornerMarginPct);
    }

    /// <summary>
    /// Only returns projectiles moving toward self within maxDistPct.
    /// Stationary mines/pillars are handled as terrain obstacles to jump over.
    /// </summary>
    public Projectile? GetApproachingProjectile(float maxDistPct)
    {
        float sw = PhysicsEngine.ViewportWidth;
        Projectile? closest = null;
        float closestDist = float.MaxValue;

        foreach (var proj in PhysicsEngine.ActiveProjectiles)
        {
            if (!proj.IsActive || proj.Owner == _self) continue;

            float dx = _self.Position.X - proj.Position.X;
            bool movingTowardSelf = (dx > 0f && proj.Velocity.X > ProjectileMovingVelocityThreshold)
                                 || (dx < 0f && proj.Velocity.X < -ProjectileMovingVelocityThreshold);
            if (!movingTowardSelf) continue;

            float distPct = MathF.Abs(dx) / sw;
            if (distPct <= maxDistPct && distPct < closestDist)
            {
                closestDist = distPct;
                closest = proj;
            }
        }
        return closest;
    }

    public bool HasTrapBetweenSelfAndOpponent()
    {
        if (_opponent == null) return false;
        float minX = MathF.Min(_self.Position.X, _opponent.Position.X);
        float maxX = MathF.Max(_self.Position.X, _opponent.Position.X);
        float sw = PhysicsEngine.ViewportWidth;

        foreach (var proj in PhysicsEngine.ActiveProjectiles)
        {
            if (!proj.IsActive || proj.Owner == _self) continue;
            if (MathF.Abs(proj.Velocity.X) <= ProjectileMovingVelocityThreshold && proj.Position.X >= minX && proj.Position.X <= maxX)
            {
                if (MathF.Abs(proj.Position.X - _self.Position.X) / sw < TrapProximityThresholdPct)
                    return true;
            }
        }
        return false;
    }

    public bool ShouldTriggerEmergencyDefense(out Projectile? imminentProj)
    {
        imminentProj = GetApproachingProjectile(EmergencyProjectileDistPct);
        if (imminentProj != null) return true;

        return _opponent != null &&
               _opponent.IsAttacking &&
               !_opponent.InRecoveryFrames &&
               GetEffectiveGapPct() < EmergencyMeleeGapPct;
    }

    public bool IsSkillReady(SkillData skill)
    {
        if (_self.CurrentCharacter == null) return false;
        return !_self.CurrentCharacter.SkillCooldowns.TryGetValue(skill.Name, out float cd) || cd <= 0f;
    }

    public GameDirection GetTowardDirection() =>
        (_opponent != null && _opponent.Position.X > _self.Position.X) ? GameDirection.Right : GameDirection.Left;

    public GameDirection GetAwayDirection() =>
        (_opponent != null && _opponent.Position.X > _self.Position.X) ? GameDirection.Left : GameDirection.Right;

    public GameDirection GetJumpTowardDirection() =>
        (_opponent != null && _opponent.Position.X > _self.Position.X) ? GameDirection.UpRight : GameDirection.UpLeft;

    public GameDirection GetJumpAwayDirection() =>
        (_opponent != null && _opponent.Position.X > _self.Position.X) ? GameDirection.UpLeft : GameDirection.UpRight;

    public MacroStateVector BuildMacroStateVector(in CharacterTacticalSummary summary)
    {
        float distPct = GetEffectiveGapPct();
        int oppRemRecov = GetOpponentRemainingRecoveryFrames();

        return new MacroStateVector
        {
            DistancePct = Math.Clamp(distPct, 0f, 1f),
            SelfHealthRatio = Math.Clamp(_self.Health / (float)Player.MAX_HEALTH, 0f, 1f),
            OpponentHealthRatio = Math.Clamp((_opponent?.Health ?? Player.MAX_HEALTH) / (float)Player.MAX_HEALTH, 0f, 1f),
            OpponentAirborne = (_opponent != null && !_opponent.IsGrounded) ? 1f : 0f,
            OpponentBlocking = (_opponent != null && _opponent.IsNeutralBlocking) ? 1f : 0f,
            OpponentCrouching = (_opponent != null && _opponent.IsCrouching) ? 1f : 0f,
            OpponentInRecovery = Math.Clamp(oppRemRecov / 25f, 0f, 1f),
            IncomingProjectileThreat = GetApproachingProjectile(0.45f) != null ? 1f : 0f,
            SelfCornered = IsSelfNearCorner() ? 1f : 0f,
            OpponentCornered = IsOpponentNearCorner() ? 1f : 0f,
            ZonerAffinity = summary.ZonerAffinity,
            RushdownAffinity = summary.RushdownAffinity,
            GrapplerAffinity = summary.GrapplerArmorAffinity,
            TricksterTrapAffinity = summary.TricksterTrapAffinity
        };
    }
}
