using System;
using System.Collections.Generic;
using Raylib_cs;

public enum AIDifficulty
{
    Easy,
    Medium,
    Hard
}

public enum GameAction
{
    None,
    Jump,
    FastPoke,
    HeavyAttack,
    LowAttack,
    OverheadAttack,
    GuardBreak,
    Counter,
    ProjectileAttack
}

public enum SpecialMotion
{
    None,
    QuarterCircleForward
}

public enum GameDirection
{
    None,
    Left,
    Right,
    Up,
    Down,
    DownLeft,
    DownRight,
    UpRight,
    UpLeft
}

public class PlayerAI
{
    private readonly Player _self;
    private Player? _opponent;
    private readonly KeyHandler _keyHandler;
    private readonly int _playerIndex;
    private readonly OpponentPatternTracker _patternTracker;
    private readonly MacroTacticalModel _macroBrain;

    public AIDifficulty Difficulty { get; set; } = AIDifficulty.Medium;
    public MacroTactic CurrentMacroTactic { get; private set; } = MacroTactic.NeutralFootsies;

    private Character? _analyzedCharacter = null;
    private Dictionary<string, TranslatedMoveSemantic> _moveSemantics = new();
    private CharacterTacticalSummary _characterSummary = new();

    private TranslatedMoveSemantic? _activeTrackedMove = null;
    private float _activeMoveStartDistPct = 0f;
    private bool _activeMoveHitRecorded = false;

    // Reinforcement Learning Episode Trajectory & Reward Tracking
    private readonly List<ExperienceStep> _episodeTrajectory = new();
    private int _lastSelfHealth = Player.MAX_HEALTH;
    private int _lastOppHealth = Player.MAX_HEALTH;
    private float _lastGapPct = 0.5f;

    private float _matchTimer = 0f;
    private float _macroDecisionTimer = 0f;
    private float _reactionCooldown = 0f;
    private GameDirection _currentMovement = GameDirection.None;
    private readonly Random _rng = new();

    public PlayerAI(Player self, KeyHandler keyHandler, int playerIndex = 1, Player? opponent = null)
    {
        _self = self;
        _keyHandler = keyHandler;
        _playerIndex = playerIndex;
        _opponent = opponent;
        _patternTracker = new OpponentPatternTracker();
        _macroBrain = MacroTacticalModel.SharedInstance;
    }

    public void SetOpponent(Player opponent)
    {
        _opponent = opponent;
    }

    public void OnRoundEnd(bool wonRound)
    {
        if (_episodeTrajectory.Count == 0) return;

        float terminalReward = wonRound ? 2.5f : -2.0f;
        _macroBrain.TrainOnEpisode(_episodeTrajectory, terminalReward);
        _episodeTrajectory.Clear();

        _lastSelfHealth = Player.MAX_HEALTH;
        _lastOppHealth = Player.MAX_HEALTH;
    }

    public void HandleAIInput(float deltaTime)
    {
        if (_opponent == null || _self.CurrentCharacter == null) return;

        if (_analyzedCharacter != _self.CurrentCharacter)
        {
            _analyzedCharacter = _self.CurrentCharacter;
            _moveSemantics = LuaMoveTranslator.AnalyzeCharacter(_analyzedCharacter, out _characterSummary);
            _lastSelfHealth = _self.Health;
            _lastOppHealth = _opponent.Health;
            _lastGapPct = GetEffectiveGapPct();
        }

        _matchTimer += deltaTime;
        ObserveOpponentBehavior();
        EvaluateActiveMoveOutcome();

        if (_self.CanTech)
        {
            if (CanAdaptByDifficulty())
            {
                // Tech TOWARD center stage if cornered, otherwise away
                GameDirection techDir = IsSelfNearCorner() ? GetTowardDirection() : GetAwayDirection();
                _keyHandler.SimulateDirection(techDir);
                _keyHandler.SimulateMaskPress(1 << 0, _playerIndex);
            }
            return;
        }

        if (_self.IsStunned || _self.IsHit)
        {
            _currentMovement = GameDirection.None;
            return;
        }

        // Immediate Micro Reflex against imminent melee or projectile impact
        if (ShouldTriggerEmergencyDefense(out Projectile? imminentProj))
        {
            ExecuteMicroDefense(imminentProj);
            return;
        }

        // Macro RL Layer: Record step reward for previous decision & sample next MacroTactic
        _macroDecisionTimer -= deltaTime;
        if (_macroDecisionTimer <= 0f || !_opponent.IsGrounded || _opponent.InRecoveryFrames)
        {
            RecordStepReward();

            MacroStateVector stateVec = BuildMacroStateVector();
            OpponentPattern spam = CanAdaptByDifficulty() ? _patternTracker.DetectSpamPattern() : OpponentPattern.None;
            CurrentMacroTactic = _macroBrain.SampleTacticWithTrace(stateVec, Difficulty, spam, out ExperienceStep trace);
            _episodeTrajectory.Add(trace);

            _macroDecisionTimer = Difficulty == AIDifficulty.Hard ? 0.15f : 0.25f;
        }

        if (IsOnReactionCooldown(deltaTime))
        {
            _keyHandler.SimulateDirection(_currentMovement);
            return;
        }

        _currentMovement = GameDirection.None;
        ExecuteMicroTactic(CurrentMacroTactic);
        SetReactionCooldown();
    }

    private void RecordStepReward()
    {
        if (_episodeTrajectory.Count == 0 || _opponent == null) return;

        int lastIdx = _episodeTrajectory.Count - 1;
        ExperienceStep step = _episodeTrajectory[lastIdx];

        float dmgDealt = Math.Max(0, _lastOppHealth - _opponent.Health) / (float)Player.MAX_HEALTH;
        float dmgTaken = Math.Max(0, _lastSelfHealth - _self.Health) / (float)Player.MAX_HEALTH;
        float currentGap = GetEffectiveGapPct();

        float reward = (dmgDealt * 4.5f) - (dmgTaken * 4.0f);

        // Heavy penalty for sitting in the corner; reward for escaping the corner!
        if (IsSelfNearCorner())
        {
            reward -= 0.18f;
        }

        // Reward for cornering the opponent
        if (IsOpponentNearCorner())
        {
            reward += 0.12f;
        }

        // If opponent is spamming projectiles, reward closing the gap!
        if (_patternTracker.DetectSpamPattern() == OpponentPattern.ProjectileSpam && currentGap < _lastGapPct)
        {
            reward += (_lastGapPct - currentGap) * 1.5f;
        }

        step.Reward += reward;
        _episodeTrajectory[lastIdx] = step;

        _lastSelfHealth = _self.Health;
        _lastOppHealth = _opponent.Health;
        _lastGapPct = currentGap;
    }

    private MacroStateVector BuildMacroStateVector()
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
            ZonerAffinity = _characterSummary.ZonerAffinity,
            RushdownAffinity = _characterSummary.RushdownAffinity,
            GrapplerAffinity = _characterSummary.GrapplerArmorAffinity,
            TricksterTrapAffinity = _characterSummary.TricksterTrapAffinity
        };
    }

    private void ExecuteMicroTactic(MacroTactic tactic)
    {
        if (_opponent == null || _self.CurrentCharacter == null) return;
        float gap = GetEffectiveGapPct();

        switch (tactic)
        {
            case MacroTactic.AntiZoningCounter:
            {
                // 1. Use Teleport, Aerial Dive, or Long-Range Hyper-Armor move to bypass projectiles and punish!
                var bypassMove = SelectBestMove(m =>
                {
                    if (m.HasTag(MoveTacticalTags.TeleportBlink) && gap <= m.MaxEffectiveRangePct)
                        return 160f + m.Skill.Damage;
                    if (m.HasTag(MoveTacticalTags.AerialDive) && gap <= m.MaxEffectiveRangePct * 1.15f)
                        return 140f + m.Skill.Damage;
                    if (m.HasTag(MoveTacticalTags.HyperArmor) && m.HasTag(MoveTacticalTags.AdvancingDash) && gap <= m.MaxEffectiveRangePct * 1.1f)
                        return 135f + m.Skill.Damage;
                    if (m.HasTag(MoveTacticalTags.ProjectileFast) && gap >= 0.25f)
                        return 95f; // Clash their fireball with our own!
                    return -1f;
                });

                if (bypassMove != null)
                {
                    ExecuteMove(bypassMove, gap);
                    return;
                }

                // 2. Otherwise, jump forward OVER projectiles/traps or dash forward between shots!
                Projectile? approaching = GetApproachingProjectile(0.38f);
                if (approaching != null || HasTrapBetweenSelfAndOpponent())
                {
                    GameDirection jumpForward = (_opponent.Position.X > _self.Position.X)
                        ? GameDirection.UpRight
                        : GameDirection.UpLeft;
                    _keyHandler.SimulateDirection(jumpForward);
                }
                else
                {
                    _currentMovement = GetTowardDirection();
                    _keyHandler.SimulateDirection(_currentMovement);
                }
                break;
            }

            case MacroTactic.AntiAirIntercept:
            {
                var aaMove = SelectBestMove(m =>
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
                    ExecuteMove(aaMove, gap);
                    return;
                }
                MoveToTargetDistance(0.16f, gap);
                break;
            }

            case MacroTactic.WhiffPunishCounter:
            {
                int oppRecovery = GetOpponentRemainingRecoveryFrames();
                var punishMove = SelectBestMove(m =>
                {
                    if (m.HasTag(MoveTacticalTags.CounterParry)) return -1f;
                    if (m.HasTag(MoveTacticalTags.CooldownReset) && _self.Health > 80)
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
                    ExecuteMove(punishMove, gap);
                    return;
                }
                MoveToTargetDistance(0.10f, gap);
                break;
            }

            case MacroTactic.ZoningKeepaway:
            {
                var zoneMove = SelectBestMove(m =>
                {
                    if (gap < 0.11f && m.HasTag(MoveTacticalTags.RetreatingEvade) && !IsSelfNearCorner())
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
                    ExecuteMove(zoneMove, gap);
                    return;
                }
                MoveToTargetDistance(IsSelfNearCorner() ? 0.12f : 0.42f, gap);
                break;
            }

            case MacroTactic.TrapAndSpaceControl:
            {
                var trapMove = SelectBestMove(m =>
                {
                    if (m.HasTag(MoveTacticalTags.CooldownReset) && _self.Health > 100)
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
                    ExecuteMove(trapMove, gap);
                    return;
                }
                MoveToTargetDistance(0.28f, gap);
                break;
            }

            case MacroTactic.MixupGuardBreak:
            {
                var mixupMove = SelectBestMove(m =>
                {
                    if (gap > m.MaxEffectiveRangePct) return -1f;
                    float s = m.Skill.Damage + (m.Skill.HitStun * 20f);

                    if (m.HasTag(MoveTacticalTags.BlockBreaker))
                        s *= 2.8f;
                    else if (_opponent.IsCrouching && m.HasTag(MoveTacticalTags.Overhead))
                        s *= 2.5f;
                    else if (!_opponent.IsCrouching && m.HasTag(MoveTacticalTags.LowPoke))
                        s *= 2.5f;
                    else if (m.HasTag(MoveTacticalTags.TeleportBlink))
                        s *= 2.2f;
                    else if (m.HasTag(MoveTacticalTags.VacuumPull))
                        s *= 2.0f;
                    else
                        s *= 0.3f;

                    return s;
                });

                if (mixupMove != null)
                {
                    ExecuteMove(mixupMove, gap);
                    return;
                }
                MoveToTargetDistance(0.06f, gap);
                break;
            }

            case MacroTactic.RushdownPressure:
            {
                var rushMove = SelectBestMove(m =>
                {
                    if (m.HasTag(MoveTacticalTags.CounterParry)) return -1f;
                    if (gap > m.MaxEffectiveRangePct) return -1f;

                    float s = m.Skill.Damage + (m.Skill.HitStun * 25f);
                    if (gap > 0.14f && (m.HasTag(MoveTacticalTags.AdvancingDash) || m.HasTag(MoveTacticalTags.TeleportBlink)))
                        s += 65f;
                    if (gap <= 0.10f && m.HasTag(MoveTacticalTags.FastPoke))
                        s += 55f;
                    if (m.HasTag(MoveTacticalTags.ComboLauncher) && _opponent.IsInHitstun)
                        s += 80f;
                    if (m.HasTag(MoveTacticalTags.HyperArmor))
                        s += 30f;

                    return s;
                });

                if (rushMove != null && _rng.Next(0, 100) < 85)
                {
                    ExecuteMove(rushMove, gap);
                    return;
                }
                MoveToTargetDistance(0.05f, gap);
                break;
            }

            case MacroTactic.DefensiveTurtleParry:
            {
                ExecuteMicroDefense(null);
                break;
            }

            case MacroTactic.NeutralFootsies:
            default:
            {
                var footsieMove = SelectBestMove(m =>
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
                    ExecuteMove(footsieMove, gap);
                    return;
                }
                MoveToTargetDistance(_characterSummary.PreferredNeutralDistancePct, gap);
                break;
            }
        }
    }

    private void MoveToTargetDistance(float targetGapPct, float currentGapPct)
    {
        if (_opponent == null) return;

        // If a stationary trap is blocking our path, jump forward over it!
        if (HasTrapBetweenSelfAndOpponent())
        {
            GameDirection jumpOver = (_opponent.Position.X > _self.Position.X)
                ? GameDirection.UpRight
                : GameDirection.UpLeft;
            _keyHandler.SimulateDirection(jumpOver);
            return;
        }

        float diff = currentGapPct - targetGapPct;
        if (diff > 0.03f)
        {
            if (diff > 0.22f && _rng.Next(0, 100) < 25)
            {
                GameDirection jumpIn = (_opponent.Position.X > _self.Position.X)
                    ? GameDirection.UpRight
                    : GameDirection.UpLeft;
                _keyHandler.SimulateDirection(jumpIn);
            }
            else
            {
                _currentMovement = GetTowardDirection();
                _keyHandler.SimulateDirection(_currentMovement);
            }
        }
        else if (diff < -0.04f && !IsSelfNearCorner())
        {
            // Never walk backward if already near the stage corner!
            _currentMovement = GetAwayDirection();
            _keyHandler.SimulateDirection(_currentMovement);
        }
        else
        {
            _currentMovement = GameDirection.None;
            _keyHandler.SimulateDirection(_currentMovement);
        }
    }

    private void ExecuteMicroDefense(Projectile? imminentProj)
    {
        if (_opponent == null || _self.CurrentCharacter == null) return;
        float gap = GetEffectiveGapPct();

        // CASE 1: Defending against an incoming Projectile!
        if (imminentProj != null)
        {
            float projDistPct = MathF.Abs(imminentProj.Position.X - _self.Position.X) / Math.Max(1f, Raylib.GetScreenWidth());

            // A. Try to Parry/Absorb the projectile (heals 10 HP and destroys projectile!) or Teleport behind!
            var projCounter = SelectBestMove(m =>
            {
                if (m.HasTag(MoveTacticalTags.TeleportBlink)) return 160f;
                if (m.HasTag(MoveTacticalTags.CounterParry) && projDistPct < 0.11f) return 150f;
                return -1f;
            });

            if (projCounter != null && CanAdaptByDifficulty())
            {
                ExecuteMove(projCounter, gap);
                return;
            }

            // B. Jump FORWARD over low/mid projectiles if we have space before impact!
            if (imminentProj.attackHeight != AttackHeight.Overhead && projDistPct > 0.09f)
            {
                GameDirection jumpForward = (_opponent.Position.X > _self.Position.X)
                    ? GameDirection.UpRight
                    : GameDirection.UpLeft;
                _keyHandler.SimulateDirection(jumpForward);
                return;
            }

            // C. Stand-ground block (GameDirection.None or Down)—DO NOT walk backward into the corner!
            _currentMovement = imminentProj.attackHeight == AttackHeight.Low
                ? GameDirection.Down
                : GameDirection.None;
            _keyHandler.SimulateDirection(_currentMovement);
            return;
        }

        // CASE 2: Defending against a melee attack
        SkillData? oppSkill = _opponent.CurrentCharacter?.ActiveSkill;
        if (oppSkill != null && oppSkill.IsBlockBreak)
        {
            int remainingStartup = oppSkill.StartupFrames - _opponent.CurrentCharacter!.CurrentActionFrame;
            var answer = SelectBestMove(m =>
            {
                if (m.HasTag(MoveTacticalTags.CounterParry) && m.Skill.StartupFrames <= remainingStartup)
                    return 160f;
                if (m.HasTag(MoveTacticalTags.RetreatingEvade) && m.Skill.StartupFrames <= remainingStartup && !IsSelfNearCorner())
                    return 140f;
                if (!m.HasTag(MoveTacticalTags.CounterParry) && gap <= m.MaxEffectiveRangePct && m.Skill.StartupFrames < remainingStartup - 1)
                    return 125f - (m.Skill.StartupFrames * 5f);
                return -1f;
            });

            if (answer != null)
            {
                ExecuteMove(answer, gap);
            }
            else
            {
                // If cornered, jump FORWARD out of the corner over the grab/blockbreak!
                GameDirection escapeJump = IsSelfNearCorner()
                    ? ((_opponent.Position.X > _self.Position.X) ? GameDirection.UpRight : GameDirection.UpLeft)
                    : ((_opponent.Position.X > _self.Position.X) ? GameDirection.UpLeft : GameDirection.UpRight);
                _keyHandler.SimulateDirection(escapeJump);
            }
            return;
        }

        int parryChance = Difficulty switch
        {
            AIDifficulty.Hard   => 32,
            AIDifficulty.Medium => 18,
            _                   => 8
        };

        if (_rng.Next(0, 100) < parryChance)
        {
            var counterMove = SelectBestMove(m => m.HasTag(MoveTacticalTags.CounterParry) ? 120f : -1f);
            if (counterMove != null)
            {
                ExecuteMove(counterMove, gap);
                return;
            }
        }

        // Stand-ground block (GameDirection.Down for low, GameDirection.None for mid/overhead) so AI doesn't walk into the corner!
        _currentMovement = _opponent.IsAttackingLow ? GameDirection.Down : GameDirection.None;
        _keyHandler.SimulateDirection(_currentMovement);
    }

    private void EvaluateActiveMoveOutcome()
    {
        Character ch = _self.CurrentCharacter!;
        if (_activeTrackedMove != null && ch.ActiveSkill == _activeTrackedMove.Skill && ch.HasHit)
        {
            _activeMoveHitRecorded = true;
        }

        if (_activeTrackedMove != null && ch.ActiveSkill != _activeTrackedMove.Skill)
        {
            bool wasInterrupted = _self.IsInHitstun && !_activeMoveHitRecorded;
            _activeTrackedMove.RecordOutcome(_activeMoveHitRecorded, wasInterrupted, _activeMoveStartDistPct);
            _activeTrackedMove = null;
            _activeMoveHitRecorded = false;
        }
    }

    private void ObserveOpponentBehavior()
    {
        if (_opponent == null) return;
        if (_opponent.JustExecutedAction != GameAction.None)
        {
            _patternTracker.RecordAction(_opponent.JustExecutedAction, _matchTimer);
        }
    }

    /// <summary>
    /// Only returns projectiles that are actually moving TOWARD _self within maxDistPct.
    /// Stationary mines/pillars are handled as terrain obstacles to jump over, not panic triggers!
    /// </summary>
    private Projectile? GetApproachingProjectile(float maxDistPct)
    {
        float sw = Math.Max(1f, Raylib.GetScreenWidth());
        Projectile? closest = null;
        float closestDist = float.MaxValue;

        foreach (var proj in PhysicsEngine.ActiveProjectiles)
        {
            if (!proj.IsActive || proj.Owner == _self) continue;

            float dx = _self.Position.X - proj.Position.X;
            bool movingTowardSelf = (dx > 0f && proj.Velocity.X > 15f) || (dx < 0f && proj.Velocity.X < -15f);
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

    private bool HasTrapBetweenSelfAndOpponent()
    {
        if (_opponent == null) return false;
        float minX = MathF.Min(_self.Position.X, _opponent.Position.X);
        float maxX = MathF.Max(_self.Position.X, _opponent.Position.X);
        float sw = Math.Max(1f, Raylib.GetScreenWidth());

        foreach (var proj in PhysicsEngine.ActiveProjectiles)
        {
            if (!proj.IsActive || proj.Owner == _self) continue;
            if (MathF.Abs(proj.Velocity.X) <= 15f && proj.Position.X >= minX && proj.Position.X <= maxX)
            {
                if (MathF.Abs(proj.Position.X - _self.Position.X) / sw < 0.16f)
                    return true;
            }
        }
        return false;
    }

    private bool IsSelfNearCorner()
    {
        float sw = Math.Max(1f, Raylib.GetScreenWidth());
        float xPct = _self.Position.X / sw;
        return xPct < 0.13f || xPct > 0.87f;
    }

    private bool IsOpponentNearCorner()
    {
        if (_opponent == null) return false;
        float sw = Math.Max(1f, Raylib.GetScreenWidth());
        float xPct = _opponent.Position.X / sw;
        return xPct < 0.13f || xPct > 0.87f;
    }

    private bool IsSkillReady(SkillData skill)
    {
        if (_self.CurrentCharacter == null) return false;
        return !_self.CurrentCharacter.SkillCooldowns.TryGetValue(skill.Name, out float cd) || cd <= 0f;
    }

    private void ExecuteMove(TranslatedMoveSemantic move, float currentGapPct)
    {
        _activeTrackedMove = move;
        _activeMoveStartDistPct = currentGapPct;
        _activeMoveHitRecorded = false;
        _keyHandler.SimulateMaskPress(move.Skill.TriggerMask, _playerIndex);
    }

    private TranslatedMoveSemantic? SelectBestMove(Func<TranslatedMoveSemantic, float> scoreEvaluator)
    {
        TranslatedMoveSemantic? best = null;
        float bestScore = 0f;

        foreach (var move in _moveSemantics.Values)
        {
            if (!IsSkillReady(move.Skill)) continue;

            float baseScore = scoreEvaluator(move);
            if (baseScore <= 0f) continue;

            float noise = 0.90f + ((float)_rng.NextDouble() * 0.20f);
            float finalScore = baseScore * move.ConfidenceMultiplier * noise;

            if (finalScore > bestScore)
            {
                bestScore = finalScore;
                best = move;
            }
        }
        return best;
    }

    private int GetOpponentRemainingRecoveryFrames()
    {
        if (_opponent?.CurrentCharacter?.ActiveSkill == null) return 0;
        SkillData s = _opponent.CurrentCharacter.ActiveSkill;
        int totalFrames = s.StartupFrames + s.ActiveFrames + s.RecoveryFrames;
        return Math.Max(0, totalFrames - _opponent.CurrentCharacter.CurrentActionFrame);
    }

    private float GetDistancePct()
    {
        if (_opponent == null) return 1f;
        float sw = Math.Max(1, Raylib.GetScreenWidth());
        return Math.Abs(_self.Position.X - _opponent.Position.X) / sw;
    }

    private float GetEffectiveGapPct()
    {
        if (_self.CurrentCharacter == null) return 1f;
        float distPct = GetDistancePct();
        float bodyMargin = (_self.CurrentCharacter.WidthPct + (_opponent?.CurrentCharacter?.WidthPct ?? 0.05f)) * 0.55f;
        return Math.Max(0f, distPct - bodyMargin);
    }

    private bool ShouldTriggerEmergencyDefense(out Projectile? imminentProj)
    {
        imminentProj = GetApproachingProjectile(0.18f);
        if (imminentProj != null) return true;

        return _opponent != null &&
               _opponent.IsAttacking &&
               !_opponent.InRecoveryFrames &&
               GetEffectiveGapPct() < 0.20f;
    }

    private bool CanAdaptByDifficulty()
    {
        int chance = Difficulty switch
        {
            AIDifficulty.Easy   => 25,
            AIDifficulty.Medium => 65,
            AIDifficulty.Hard   => 95,
            _                   => 50
        };
        return _rng.Next(0, 100) < chance;
    }

    private GameDirection GetAwayDirection() => (_opponent != null && _opponent.Position.X > _self.Position.X) ? GameDirection.Left : GameDirection.Right;
    private GameDirection GetTowardDirection() => (_opponent != null && _opponent.Position.X > _self.Position.X) ? GameDirection.Right : GameDirection.Left;

    private bool IsOnReactionCooldown(float dt)
    {
        _reactionCooldown -= dt;
        return _reactionCooldown > 0f;
    }

    private void SetReactionCooldown()
    {
        _reactionCooldown = Difficulty switch
        {
            AIDifficulty.Easy   => 0.30f,
            AIDifficulty.Medium => 0.14f,
            AIDifficulty.Hard   => 0.05f,
            _                   => 0.14f
        };
    }
}