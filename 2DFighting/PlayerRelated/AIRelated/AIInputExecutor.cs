using System;
using System.Collections.Generic;

public class AIInputExecutor
{
    private readonly Player _self;
    private readonly KeyHandler _keyHandler;
    private readonly int _playerIndex;
    private readonly AIStateEvaluator _evaluator;
    private readonly Random _rng;

    private TranslatedMoveSemantic? _activeTrackedMove = null;
    private float _activeMoveStartDistPct = 0f;
    private bool _activeMoveHitRecorded = false;

    public GameDirection CurrentMovement { get; set; } = GameDirection.None;

    public AIInputExecutor(Player self, KeyHandler keyHandler, int playerIndex, AIStateEvaluator evaluator, Random rng)
    {
        _self = self;
        _keyHandler = keyHandler;
        _playerIndex = playerIndex;
        _evaluator = evaluator;
        _rng = rng;
    }

    public void ExecuteMove(TranslatedMoveSemantic move, float currentGapPct)
    {
        _activeTrackedMove = move;
        _activeMoveStartDistPct = currentGapPct;
        _activeMoveHitRecorded = false;
        _keyHandler.SimulateMaskPress(move.Skill.TriggerMask, _playerIndex);
    }

    public void HoldCurrentMovement() => _keyHandler.SimulateDirection(CurrentMovement);

    public void SimulateDirection(GameDirection direction) => _keyHandler.SimulateDirection(direction);

    public void ResetMovement() => CurrentMovement = GameDirection.None;

    public void ExecuteAirTech()
    {
        GameDirection techDir = _evaluator.IsSelfNearCorner()
            ? _evaluator.GetTowardDirection()
            : _evaluator.GetAwayDirection();
        _keyHandler.SimulateDirection(techDir);
        _keyHandler.SimulateMaskPress(1 << 0, _playerIndex);
    }

    public void MoveToTargetDistance(float targetGapPct, float currentGapPct, Player? opponent)
    {
        if (opponent == null) return;

        if (_evaluator.HasTrapBetweenSelfAndOpponent())
        {
            _keyHandler.SimulateDirection(_evaluator.GetJumpTowardDirection());
            return;
        }

        float diff = currentGapPct - targetGapPct;
        if (diff > 0.03f)
        {
            if (diff > 0.22f && _rng.Next(0, 100) < 25)
            {
                _keyHandler.SimulateDirection(_evaluator.GetJumpTowardDirection());
            }
            else
            {
                CurrentMovement = _evaluator.GetTowardDirection();
                _keyHandler.SimulateDirection(CurrentMovement);
            }
        }
        else if (diff < -0.04f && !_evaluator.IsSelfNearCorner())
        {
            CurrentMovement = _evaluator.GetAwayDirection();
            _keyHandler.SimulateDirection(CurrentMovement);
        }
        else
        {
            CurrentMovement = GameDirection.None;
            _keyHandler.SimulateDirection(CurrentMovement);
        }
    }

    public void EvaluateActiveMoveOutcome()
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

    public TranslatedMoveSemantic? SelectBestMove(
        IReadOnlyDictionary<string, TranslatedMoveSemantic> moveSemantics,
        Func<TranslatedMoveSemantic, float> scoreEvaluator)
    {
        TranslatedMoveSemantic? best = null;
        float bestScore = 0f;

        foreach (var move in moveSemantics.Values)
        {
            if (!_evaluator.IsSkillReady(move.Skill)) continue;

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
}
