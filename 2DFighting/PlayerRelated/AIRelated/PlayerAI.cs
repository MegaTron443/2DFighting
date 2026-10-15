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
    Counter
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

public class LearnedSkillProfile
{
    public SkillData Skill { get; }

    public float BaseReachPct { get; }
    public float LearnedMaxReachPct { get; set; }
    public bool CanAntiAir { get; set; }

    public int Uses { get; set; } = 0;
    public int Hits { get; set; } = 0;
    public int Whiffs { get; set; } = 0;
    public int InterruptedCount { get; set; } = 0;

    public float ConfidenceMultiplier { get; set; } = 1.0f;

    public LearnedSkillProfile(Character character, SkillData skill)
    {
        Skill = skill;

        float halfBodyW = character.WidthPct * 0.5f;
        float hitboxReach = character.WidthPct * (skill.HitboxOffsetPct.x + skill.HitboxOffsetPct.width);
        BaseReachPct = Math.Max(halfBodyW, hitboxReach - halfBodyW);

        LearnedMaxReachPct = skill.CustomLogic
            ? BaseReachPct + (character.WalkSpeedPct * 0.28f)
            : BaseReachPct;

        CanAntiAir = !skill.IsCounter && (skill.HitboxOffsetPct.y < 0f || skill.HitboxOffsetPct.height >= 0.75f);
    }

    public void RecordOutcome(bool hit, bool wasInterrupted, float distanceUsedPct)
    {
        Uses++;

        if (hit)
        {
            Hits++;
            ConfidenceMultiplier = Math.Clamp(ConfidenceMultiplier + 0.15f, 0.3f, 2.2f);

            if (distanceUsedPct > LearnedMaxReachPct)
            {
                LearnedMaxReachPct = distanceUsedPct * 1.05f;
            }
        }
        else if (wasInterrupted)
        {
            InterruptedCount++;
            ConfidenceMultiplier = Math.Clamp(ConfidenceMultiplier - 0.18f, 0.3f, 2.2f);
        }
        else
        {
            Whiffs++;
            ConfidenceMultiplier = Math.Clamp(ConfidenceMultiplier - 0.08f, 0.3f, 2.2f);

            if (distanceUsedPct > BaseReachPct)
            {
                LearnedMaxReachPct = Math.Max(BaseReachPct, (LearnedMaxReachPct * 0.75f) + (distanceUsedPct * 0.20f));
            }
        }
    }
}

public class PlayerAI
{
    private readonly Player _self;
    private Player? _opponent;
    private readonly KeyHandler _keyHandler;
    private readonly int _playerIndex;
    private readonly OpponentPatternTracker _patternTracker;

    public AIDifficulty Difficulty { get; set; } = AIDifficulty.Medium;

    private Character? _analyzedCharacter = null;
    private readonly Dictionary<string, LearnedSkillProfile> _skillKnowledge = new();

    private LearnedSkillProfile? _activeTrackedSkill = null;
    private float _activeSkillStartDistPct = 0f;
    private bool _activeSkillHitRecorded = false;

    private float _closeRangeThresholdPct = 0.09f;
    private float _maxOffensiveReachPct = 0.18f;

    private float _matchTimer = 0f;
    private float _reactionCooldown = 0f;
    private GameDirection _currentMovement = GameDirection.None;
    private readonly Random _rng = new Random();

    public PlayerAI(Player self, KeyHandler keyHandler, int playerIndex = 1, Player? opponent = null)
    {
        _self = self;
        _keyHandler = keyHandler;
        _playerIndex = playerIndex;
        _opponent = opponent;
        _patternTracker = new OpponentPatternTracker();
    }

    public void SetOpponent(Player opponent)
    {
        _opponent = opponent;
    }

    public void HandleAIInput(float deltaTime)
    {
        if (_opponent == null || _self.CurrentCharacter == null) return;

        if (_analyzedCharacter != _self.CurrentCharacter)
        {
            LearnCharacterMoveSet(_self.CurrentCharacter);
        }

        _matchTimer += deltaTime;
        ObserveOpponentBehavior();
        EvaluateActiveSkillOutcome();

        if (_self.CanTech)
        {
            if (CanAdaptByDifficulty())
            {
                _keyHandler.SimulateDirection(GetAwayDirection());
                _keyHandler.SimulateMaskPress(1 << 0, _playerIndex);
            }
            return;
        }

        if (_self.IsStunned || _self.IsHit)
        {
            _currentMovement = GameDirection.None;
            return;
        }

        if (ShouldDefend())
        {
            HandleDefense();
            return;
        }

        if (IsOnReactionCooldown(deltaTime))
        {
            _keyHandler.SimulateDirection(_currentMovement);
            return;
        }

        _currentMovement = GameDirection.None;

        OpponentPattern detectedSpam = _patternTracker.DetectSpamPattern();
        if (detectedSpam != OpponentPattern.None && CanAdaptByDifficulty())
        {
            ExecuteDynamicSpamCounter(detectedSpam);
        }
        else
        {
            ExecuteUtilityDecision();
        }

        SetReactionCooldown();
    }

    private void LearnCharacterMoveSet(Character character)
    {
        _analyzedCharacter = character;
        _skillKnowledge.Clear();

        float minReach = 1.0f;
        float maxReach = 0.05f;

        foreach (var skill in character.Skills.Values)
        {
            if (skill.TriggerMask == 0) continue;

            var profile = new LearnedSkillProfile(character, skill);
            _skillKnowledge[skill.Name] = profile;

            if (!skill.IsCounter)
            {
                if (profile.BaseReachPct < minReach) minReach = profile.BaseReachPct;
                if (profile.LearnedMaxReachPct > maxReach) maxReach = profile.LearnedMaxReachPct;
            }
        }

        float bodyTouchDist = character.WidthPct;
        _closeRangeThresholdPct = Math.Max(bodyTouchDist * 1.6f, minReach + bodyTouchDist * 0.5f);
        _maxOffensiveReachPct = Math.Max(_closeRangeThresholdPct * 1.4f, maxReach + bodyTouchDist * 0.5f);
    }

    private void EvaluateActiveSkillOutcome()
    {
        Character ch = _self.CurrentCharacter!;

        if (_activeTrackedSkill != null && ch.ActiveSkill == _activeTrackedSkill.Skill && ch.HasHit)
        {
            _activeSkillHitRecorded = true;
        }

        if (_activeTrackedSkill != null && ch.ActiveSkill != _activeTrackedSkill.Skill)
        {
            bool wasInterrupted = _self.IsInHitstun && !_activeSkillHitRecorded;
            _activeTrackedSkill.RecordOutcome(_activeSkillHitRecorded, wasInterrupted, _activeSkillStartDistPct);

            if (_activeTrackedSkill.LearnedMaxReachPct > _maxOffensiveReachPct)
            {
                _maxOffensiveReachPct = _activeTrackedSkill.LearnedMaxReachPct;
            }

            _activeTrackedSkill = null;
            _activeSkillHitRecorded = false;
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

    private bool IsSkillReady(SkillData skill)
    {
        if (_self.CurrentCharacter == null) return false;
        return !_self.CurrentCharacter.SkillCooldowns.TryGetValue(skill.Name, out float cd) || cd <= 0f;
    }

    private void ExecuteLearnedSkill(LearnedSkillProfile profile, float currentDistPct)
    {
        _activeTrackedSkill = profile;
        _activeSkillStartDistPct = currentDistPct;
        _activeSkillHitRecorded = false;

        _keyHandler.SimulateMaskPress(profile.Skill.TriggerMask, _playerIndex);
    }

    private LearnedSkillProfile? SelectBestSkill(Func<LearnedSkillProfile, float> scoreEvaluator)
    {
        LearnedSkillProfile? bestProfile = null;
        float bestScore = 0f;

        foreach (var profile in _skillKnowledge.Values)
        {
            if (!IsSkillReady(profile.Skill)) continue;

            float baseScore = scoreEvaluator(profile);
            if (baseScore <= 0f) continue;

            float noise = 0.90f + ((float)_rng.NextDouble() * 0.20f);
            float finalScore = baseScore * profile.ConfidenceMultiplier * noise;

            if (finalScore > bestScore)
            {
                bestScore = finalScore;
                bestProfile = profile;
            }
        }

        return bestProfile;
    }

    private void ExecuteUtilityDecision()
    {
        if (_opponent == null || _self.CurrentCharacter == null) return;

        float distPct = GetDistancePct();
        float bodyMargin = (_self.CurrentCharacter.WidthPct + (_opponent.CurrentCharacter?.WidthPct ?? 0.05f)) * 0.55f;
        float effectiveGap = Math.Max(0f, distPct - bodyMargin);

        if (!_opponent.IsGrounded && distPct < _maxOffensiveReachPct * 0.9f)
        {
            var antiAirSkill = SelectBestSkill(p =>
            {
                if (effectiveGap > p.LearnedMaxReachPct * 1.15f) return -1f;
                float score = 0f;
                if (p.CanAntiAir) score += 80f + p.Skill.Damage;
                if (p.Skill.IsCounter) score += 50f;
                if (p.Skill.StartupFrames <= 6) score += 25f;
                return score;
            });

            if (antiAirSkill != null)
            {
                ExecuteLearnedSkill(antiAirSkill, effectiveGap);
                return;
            }
        }

        if (_opponent.InRecoveryFrames)
        {
            int oppRemainingRecovery = GetOpponentRemainingRecoveryFrames();
            var punishSkill = SelectBestSkill(p =>
            {
                if (p.Skill.IsCounter) return -1f;
                if (effectiveGap > p.LearnedMaxReachPct) return -1f;
                if (p.Skill.StartupFrames > oppRemainingRecovery + 4) return -1f;

                return (p.Skill.Damage * 2.0f) + (p.Skill.HitStun * 30f);
            });

            if (punishSkill != null)
            {
                ExecuteLearnedSkill(punishSkill, effectiveGap);
                return;
            }
        }

        if (effectiveGap > _maxOffensiveReachPct)
        {
            int approachRoll = _rng.Next(0, 100);
            if (approachRoll < 75)
            {
                _currentMovement = GetTowardDirection();
                _keyHandler.SimulateDirection(_currentMovement);
            }
            else
            {
                GameDirection jumpForward = (_opponent.Position.X > _self.Position.X)
                    ? GameDirection.UpRight
                    : GameDirection.UpLeft;
                _keyHandler.SimulateDirection(jumpForward);
            }
            return;
        }

        var chosenSkill = SelectBestSkill(p =>
        {
            SkillData s = p.Skill;

            if (s.IsCounter)
            {
                return (effectiveGap < _closeRangeThresholdPct && !_opponent.IsNeutralBlocking) ? 20f : -1f;
            }

            if (effectiveGap > p.LearnedMaxReachPct) return -1f;

            float score = s.Damage + (s.HitStun * 20f);

            float reachFit = 1.0f - Math.Abs(effectiveGap - p.LearnedMaxReachPct * 0.7f);
            score *= Math.Clamp(reachFit, 0.5f, 1.4f);

            if (_opponent.IsNeutralBlocking)
            {
                if (s.IsBlockBreak)
                    score *= 2.5f;
                else if (_opponent.IsCrouching && s.Height == AttackHeight.Overhead)
                    score *= 2.2f;
                else if (!_opponent.IsCrouching && s.Height == AttackHeight.Low)
                    score *= 2.2f;
                else
                    score *= 0.25f;
            }
            else
            {
                if (effectiveGap < _closeRangeThresholdPct)
                {
                    score += Math.Max(0, (12 - s.StartupFrames) * 5f);
                }
            }

            if (s.IsHyperFrames) score += 15f;

            return score;
        });

        if (chosenSkill != null && _rng.Next(0, 100) < 78)
        {
            ExecuteLearnedSkill(chosenSkill, effectiveGap);
        }
        else
        {
            _currentMovement = GetTowardDirection();
            _keyHandler.SimulateDirection(_currentMovement);
        }
    }

    private void ExecuteDynamicSpamCounter(OpponentPattern pattern)
    {
        float distPct = GetDistancePct();
        float bodyMargin = (_self.CurrentCharacter!.WidthPct + (_opponent?.CurrentCharacter?.WidthPct ?? 0.05f)) * 0.55f;
        float effectiveGap = Math.Max(0f, distPct - bodyMargin);

        LearnedSkillProfile? counterMove = pattern switch
        {
            OpponentPattern.JumpInSpam => SelectBestSkill(p =>
                effectiveGap <= p.LearnedMaxReachPct * 1.2f && (p.CanAntiAir || p.Skill.IsCounter)
                    ? 100f + p.Skill.Damage : -1f),

            OpponentPattern.LowAttackSpam => SelectBestSkill(p =>
                effectiveGap <= p.LearnedMaxReachPct && (p.Skill.Height == AttackHeight.Overhead || p.Skill.CustomLogic)
                    ? 100f + p.Skill.Damage : -1f),

            OpponentPattern.FastPokeSpam or OpponentPattern.HighOverheadSpam => SelectBestSkill(p =>
                (p.Skill.IsCounter && effectiveGap < _closeRangeThresholdPct) ? 140f :
                (p.Skill.IsHyperFrames && effectiveGap <= p.LearnedMaxReachPct) ? 110f : -1f),

            OpponentPattern.GuardBreakSpam => SelectBestSkill(p =>
                !p.Skill.IsCounter && effectiveGap <= p.LearnedMaxReachPct
                    ? Math.Max(10f, 100f - (p.Skill.StartupFrames * 8f)) : -1f),

            _ => null
        };

        if (counterMove != null)
        {
            ExecuteLearnedSkill(counterMove, effectiveGap);
        }
        else
        {
            ExecuteUtilityDecision();
        }
    }

    private void HandleDefense()
    {
        if (_opponent == null || _self.CurrentCharacter == null) return;

        SkillData? oppSkill = _opponent.CurrentCharacter?.ActiveSkill;
        float distPct = GetDistancePct();
        float bodyMargin = (_self.CurrentCharacter.WidthPct + (_opponent.CurrentCharacter?.WidthPct ?? 0.05f)) * 0.55f;
        float effectiveGap = Math.Max(0f, distPct - bodyMargin);

        if (oppSkill != null && oppSkill.IsBlockBreak)
        {
            int remainingStartup = oppSkill.StartupFrames - _opponent.CurrentCharacter!.CurrentActionFrame;

            var defensiveAnswer = SelectBestSkill(p =>
            {
                if (p.Skill.IsCounter && p.Skill.StartupFrames <= remainingStartup)
                    return 150f;
                if (!p.Skill.IsCounter && effectiveGap <= p.LearnedMaxReachPct && p.Skill.StartupFrames < remainingStartup - 1)
                    return 120f - (p.Skill.StartupFrames * 5f);
                return -1f;
            });

            if (defensiveAnswer != null)
            {
                ExecuteLearnedSkill(defensiveAnswer, effectiveGap);
            }
            else
            {
                GameDirection jumpBack = (_opponent.Position.X > _self.Position.X) ? GameDirection.UpLeft : GameDirection.UpRight;
                _keyHandler.SimulateDirection(jumpBack);
            }
            return;
        }

        if (Difficulty == AIDifficulty.Hard && _rng.Next(0, 100) < 20)
        {
            var counterSkill = SelectBestSkill(p => p.Skill.IsCounter ? 100f : -1f);
            if (counterSkill != null)
            {
                ExecuteLearnedSkill(counterSkill, effectiveGap);
                return;
            }
        }

        _currentMovement = _opponent.IsAttackingLow ? GetLowBlockDirection() : GetAwayDirection();
        _keyHandler.SimulateDirection(_currentMovement);
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

    private bool ShouldDefend() =>
        _opponent != null &&
        _opponent.IsAttacking &&
        !_opponent.InRecoveryFrames &&
        GetDistancePct() < _maxOffensiveReachPct * 1.15f;

    private bool CanAdaptByDifficulty()
    {
        int adaptationChance = Difficulty switch
        {
            AIDifficulty.Easy => 25,
            AIDifficulty.Medium => 65,
            AIDifficulty.Hard => 95,
            _ => 50
        };
        return _rng.Next(0, 100) < adaptationChance;
    }

    private GameDirection GetAwayDirection() => (_opponent != null && _opponent.Position.X > _self.Position.X) ? GameDirection.Left : GameDirection.Right;
    private GameDirection GetTowardDirection() => (_opponent != null && _opponent.Position.X > _self.Position.X) ? GameDirection.Right : GameDirection.Left;
    private GameDirection GetLowBlockDirection() => (_opponent != null && _opponent.Position.X > _self.Position.X) ? GameDirection.DownLeft : GameDirection.DownRight;

    private bool IsOnReactionCooldown(float dt)
    {
        _reactionCooldown -= dt;
        return _reactionCooldown > 0f;
    }

    private void SetReactionCooldown()
    {
        _reactionCooldown = Difficulty switch
        {
            AIDifficulty.Easy => 0.35f,
            AIDifficulty.Medium => 0.18f,
            AIDifficulty.Hard => 0.06f,
            _ => 0.18f
        };
    }
}