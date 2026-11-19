using System;
using System.Collections.Generic;
using Raylib_cs;

public class AITacticalExecutor
{
    private readonly AIStateEvaluator _evaluator;
    private readonly AIInputExecutor _executor;
    private readonly Random _rng;

    public AITacticalExecutor(AIStateEvaluator evaluator, AIInputExecutor executor, Random rng)
    {
        _evaluator = evaluator;
        _executor = executor;
        _rng = rng;
    }

    public void ExecuteMicroTactic(
        MacroTactic tactic,
        Player self,
        Player opponent,
        IReadOnlyDictionary<string, TranslatedMoveSemantic> moves,
        in CharacterTacticalSummary summary)
    {
        float gap = _evaluator.GetEffectiveGapPct();

        switch (tactic)
        {
            case MacroTactic.AntiZoningCounter:
                ExecuteAntiZoning(opponent, moves, gap);
                break;
            case MacroTactic.AntiAirIntercept:
                ExecuteAntiAir(opponent, moves, gap);
                break;
            case MacroTactic.WhiffPunishCounter:
                ExecuteWhiffPunish(self, opponent, moves, gap);
                break;
            case MacroTactic.ZoningKeepaway:
                ExecuteZoning(opponent, moves, gap);
                break;
            case MacroTactic.TrapAndSpaceControl:
                ExecuteTrapControl(self, opponent, moves, gap);
                break;
            case MacroTactic.MixupGuardBreak:
                ExecuteMixup(opponent, moves, gap);
                break;
            case MacroTactic.RushdownPressure:
                ExecuteRushdown(opponent, moves, gap);
                break;
            case MacroTactic.DefensiveTurtleParry:
                ExecuteMicroDefense(self, opponent, null, moves, AIDifficulty.Medium, () => true);
                break;
            case MacroTactic.NeutralFootsies:
            default:
                ExecuteFootsies(opponent, moves, gap, summary.PreferredNeutralDistancePct);
                break;
        }
    }

    public void ExecuteMicroDefense(
        Player self,
        Player opponent,
        Projectile? imminentProj,
        IReadOnlyDictionary<string, TranslatedMoveSemantic> moves,
        AIDifficulty difficulty,
        Func<bool> canAdapt)
    {
        float gap = _evaluator.GetEffectiveGapPct();

        if (imminentProj != null)
        {
            DefendAgainstProjectile(self, imminentProj, moves, gap, canAdapt);
            return;
        }

        DefendAgainstMelee(opponent, moves, gap, difficulty);
    }

    private void ExecuteAntiZoning(Player opponent, IReadOnlyDictionary<string, TranslatedMoveSemantic> moves, float gap)
    {
        var bypassMove = _executor.SelectBestMove(moves, m =>
        {
            if (m.HasTag(MoveTacticalTags.TeleportBlink) && gap <= m.MaxEffectiveRangePct)
                return 160f + m.Skill.Damage;
            if (m.HasTag(MoveTacticalTags.AerialDive) && gap <= m.MaxEffectiveRangePct * 1.15f)
                return 140f + m.Skill.Damage;
            if (m.HasTag(MoveTacticalTags.HyperArmor) && m.HasTag(MoveTacticalTags.AdvancingDash) && gap <= m.MaxEffectiveRangePct * 1.1f)
                return 135f + m.Skill.Damage;
            if (m.HasTag(MoveTacticalTags.ProjectileFast) && gap >= 0.25f)
                return 95f;
            return -1f;
        });

        if (bypassMove != null)
        {
            _executor.ExecuteMove(bypassMove, gap);
            return;
        }

        Projectile? approaching = _evaluator.GetApproachingProjectile(0.38f);
        if (approaching != null || _evaluator.HasTrapBetweenSelfAndOpponent())
        {
            _executor.SimulateDirection(_evaluator.GetJumpTowardDirection());
        }
        else
        {
            _executor.CurrentMovement = _evaluator.GetTowardDirection();
            _executor.HoldCurrentMovement();
        }
    }

    private void ExecuteAntiAir(Player opponent, IReadOnlyDictionary<string, TranslatedMoveSemantic> moves, float gap)
    {
        var aaMove = _executor.SelectBestMove(moves, m =>
        {
            if (gap > m.MaxEffectiveRangePct * 1.15f) return -1f;
            float s = 0f;
            if (m.HasTag(MoveTacticalTags.AntiAir)) s += 95f + m.Skill.Damage;
            if (m.HasTag(MoveTacticalTags.CounterParry)) s += 60f;
            if (m.HasTag(MoveTacticalTags.ProjectileHoming)) s += 70f;
            return s;
        });

        if (aaMove != null)
        {
            _executor.ExecuteMove(aaMove, gap);
            return;
        }
        _executor.MoveToTargetDistance(0.16f, gap, opponent);
    }

    private void ExecuteWhiffPunish(Player self, Player opponent, IReadOnlyDictionary<string, TranslatedMoveSemantic> moves, float gap)
    {
        int oppRecovery = _evaluator.GetOpponentRemainingRecoveryFrames();
        var punishMove = _executor.SelectBestMove(moves, m =>
        {
            if (m.HasTag(MoveTacticalTags.CounterParry)) return -1f;
            if (m.HasTag(MoveTacticalTags.CooldownReset) && self.Health > 80)
                return 85f;
            if (gap > m.MaxEffectiveRangePct || gap < m.MinEffectiveRangePct * 0.7f) return -1f;
            if (oppRecovery > 0 && m.Skill.StartupFrames > oppRecovery + 5) return -1f;

            float s = (m.Skill.Damage * 2.2f) + (m.Skill.HitStun * 35f);
            if (m.HasTag(MoveTacticalTags.ComboLauncher)) s += 35f;
            if (m.HasTag(MoveTacticalTags.AdvancingDash)) s += 25f;
            return s;
        });

        if (punishMove != null)
        {
            _executor.ExecuteMove(punishMove, gap);
            return;
        }
        _executor.MoveToTargetDistance(0.10f, gap, opponent);
    }

    private void ExecuteZoning(Player opponent, IReadOnlyDictionary<string, TranslatedMoveSemantic> moves, float gap)
    {
        var zoneMove = _executor.SelectBestMove(moves, m =>
        {
            if (gap < 0.11f && m.HasTag(MoveTacticalTags.RetreatingEvade) && !_evaluator.IsSelfNearCorner())
                return 120f + m.Skill.Damage;
            if (gap >= m.MinEffectiveRangePct && gap <= m.MaxEffectiveRangePct)
            {
                if (m.HasTag(MoveTacticalTags.ProjectileFast) || m.HasTag(MoveTacticalTags.ProjectileHoming))
                    return 95f + m.Skill.Damage;
                if (m.MaxEffectiveRangePct >= 0.14f && !m.HasTag(MoveTacticalTags.CounterParry))
                    return 65f + m.Skill.Damage;
            }
            return -1f;
        });

        if (zoneMove != null && _rng.Next(0, 100) < 84)
        {
            _executor.ExecuteMove(zoneMove, gap);
            return;
        }
        _executor.MoveToTargetDistance(_evaluator.IsSelfNearCorner() ? 0.12f : 0.42f, gap, opponent);
    }

    private void ExecuteTrapControl(Player self, Player opponent, IReadOnlyDictionary<string, TranslatedMoveSemantic> moves, float gap)
    {
        var trapMove = _executor.SelectBestMove(moves, m =>
        {
            if (m.HasTag(MoveTacticalTags.CooldownReset) && self.Health > 100)
                return 90f;
            if (m.HasTag(MoveTacticalTags.ProjectileTrap) && gap >= 0.10f && gap <= 0.55f)
                return 115f + (m.Skill.HitStun * 30f);
            if (m.HasTag(MoveTacticalTags.VacuumPull) && gap <= m.MaxEffectiveRangePct)
                return 100f;
            if (m.HasTag(MoveTacticalTags.ProjectileFast) && gap >= m.MinEffectiveRangePct)
                return 75f;
            return -1f;
        });

        if (trapMove != null)
        {
            _executor.ExecuteMove(trapMove, gap);
            return;
        }
        _executor.MoveToTargetDistance(0.28f, gap, opponent);
    }

    private void ExecuteMixup(Player opponent, IReadOnlyDictionary<string, TranslatedMoveSemantic> moves, float gap)
    {
        var mixupMove = _executor.SelectBestMove(moves, m =>
        {
            if (gap > m.MaxEffectiveRangePct) return -1f;
            float s = m.Skill.Damage + (m.Skill.HitStun * 20f);

            if (m.HasTag(MoveTacticalTags.BlockBreaker)) s *= 2.8f;
            else if (opponent.IsCrouching && m.HasTag(MoveTacticalTags.Overhead)) s *= 2.5f;
            else if (!opponent.IsCrouching && m.HasTag(MoveTacticalTags.LowPoke)) s *= 2.5f;
            else if (m.HasTag(MoveTacticalTags.TeleportBlink)) s *= 2.2f;
            else if (m.HasTag(MoveTacticalTags.VacuumPull)) s *= 2.0f;
            else s *= 0.3f;

            return s;
        });

        if (mixupMove != null)
        {
            _executor.ExecuteMove(mixupMove, gap);
            return;
        }
        _executor.MoveToTargetDistance(0.06f, gap, opponent);
    }

    private void ExecuteRushdown(Player opponent, IReadOnlyDictionary<string, TranslatedMoveSemantic> moves, float gap)
    {
        var rushMove = _executor.SelectBestMove(moves, m =>
        {
            if (m.HasTag(MoveTacticalTags.CounterParry)) return -1f;
            if (gap > m.MaxEffectiveRangePct) return -1f;

            float s = m.Skill.Damage + (m.Skill.HitStun * 25f);
            if (gap > 0.14f && (m.HasTag(MoveTacticalTags.AdvancingDash) || m.HasTag(MoveTacticalTags.TeleportBlink)))
                s += 65f;
            if (gap <= 0.10f && m.HasTag(MoveTacticalTags.FastPoke))
                s += 55f;
            if (m.HasTag(MoveTacticalTags.ComboLauncher) && opponent.IsInHitstun)
                s += 80f;
            if (m.HasTag(MoveTacticalTags.HyperArmor))
                s += 30f;

            return s;
        });

        if (rushMove != null && _rng.Next(0, 100) < 85)
        {
            _executor.ExecuteMove(rushMove, gap);
            return;
        }
        _executor.MoveToTargetDistance(0.05f, gap, opponent);
    }

    private void ExecuteFootsies(Player opponent, IReadOnlyDictionary<string, TranslatedMoveSemantic> moves, float gap, float preferredNeutralPct)
    {
        var footsieMove = _executor.SelectBestMove(moves, m =>
        {
            if (m.HasTag(MoveTacticalTags.CounterParry)) return -1f;
            if (gap < m.MinEffectiveRangePct || gap > m.MaxEffectiveRangePct) return -1f;

            float reachFit = 1.15f - MathF.Abs(gap - m.OptimalRangePct);
            float s = (m.Skill.Damage + (m.Skill.HitStun * 18f)) * Math.Clamp(reachFit, 0.5f, 1.4f);

            if (m.HasTag(MoveTacticalTags.FastPoke)) s += 25f;
            if (m.HasTag(MoveTacticalTags.VacuumPull)) s += 30f;
            return s;
        });

        if (footsieMove != null && _rng.Next(0, 100) < 75)
        {
            _executor.ExecuteMove(footsieMove, gap);
            return;
        }
        _executor.MoveToTargetDistance(preferredNeutralPct, gap, opponent);
    }

    private void DefendAgainstProjectile(
        Player self,
        Projectile imminentProj,
        IReadOnlyDictionary<string, TranslatedMoveSemantic> moves,
        float gap,
        Func<bool> canAdapt)
    {
        float projDistPct = MathF.Abs(imminentProj.Position.X - self.Position.X) / Math.Max(1f, Raylib.GetScreenWidth());

        var projCounter = _executor.SelectBestMove(moves, m =>
        {
            if (m.HasTag(MoveTacticalTags.TeleportBlink)) return 160f;
            if (m.HasTag(MoveTacticalTags.CounterParry) && projDistPct < 0.11f) return 150f;
            return -1f;
        });

        if (projCounter != null && canAdapt())
        {
            _executor.ExecuteMove(projCounter, gap);
            return;
        }

        if (imminentProj.attackHeight != AttackHeight.Overhead && projDistPct > 0.09f)
        {
            _executor.SimulateDirection(_evaluator.GetJumpTowardDirection());
            return;
        }

        _executor.CurrentMovement = imminentProj.attackHeight == AttackHeight.Low
            ? GameDirection.Down
            : GameDirection.None;
        _executor.HoldCurrentMovement();
    }

    private void DefendAgainstMelee(
        Player opponent,
        IReadOnlyDictionary<string, TranslatedMoveSemantic> moves,
        float gap,
        AIDifficulty difficulty)
    {
        SkillData? oppSkill = opponent.CurrentCharacter?.ActiveSkill;
        if (oppSkill != null && oppSkill.IsBlockBreak)
        {
            int remainingStartup = oppSkill.StartupFrames - opponent.CurrentCharacter!.CurrentActionFrame;
            var answer = _executor.SelectBestMove(moves, m =>
            {
                if (m.HasTag(MoveTacticalTags.CounterParry) && m.Skill.StartupFrames <= remainingStartup)
                    return 160f;
                if (m.HasTag(MoveTacticalTags.RetreatingEvade) && m.Skill.StartupFrames <= remainingStartup && !_evaluator.IsSelfNearCorner())
                    return 140f;
                if (!m.HasTag(MoveTacticalTags.CounterParry) && gap <= m.MaxEffectiveRangePct && m.Skill.StartupFrames < remainingStartup - 1)
                    return 125f - (m.Skill.StartupFrames * 5f);
                return -1f;
            });

            if (answer != null)
            {
                _executor.ExecuteMove(answer, gap);
            }
            else
            {
                GameDirection escapeJump = _evaluator.IsSelfNearCorner()
                    ? _evaluator.GetJumpTowardDirection()
                    : _evaluator.GetJumpAwayDirection();
                _executor.SimulateDirection(escapeJump);
            }
            return;
        }

        int parryChance = difficulty switch
        {
            AIDifficulty.Hard   => 32,
            AIDifficulty.Medium => 18,
            _                   => 8
        };

        if (_rng.Next(0, 100) < parryChance)
        {
            var counterMove = _executor.SelectBestMove(moves, m => m.HasTag(MoveTacticalTags.CounterParry) ? 120f : -1f);
            if (counterMove != null)
            {
                _executor.ExecuteMove(counterMove, gap);
                return;
            }
        }

        _executor.CurrentMovement = opponent.IsAttackingLow ? GameDirection.Down : GameDirection.None;
        _executor.HoldCurrentMovement();
    }
}
