using System;
using System.Collections.Generic;

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

public class PlayerAI : IDisposable
{
    private readonly Player _self;
    private Player? _opponent;
    private readonly OpponentPatternTracker _patternTracker;
    private readonly MacroTacticalModel _macroBrain;
    private readonly AIStateEvaluator _evaluator;
    private readonly AIInputExecutor _executor;
    private readonly AITacticalExecutor _tactics;
    private readonly Random _rng = new();

    public AIDifficulty Difficulty { get; set; } = AIDifficulty.Medium;
    public MacroTactic CurrentMacroTactic { get; private set; } = MacroTactic.NeutralFootsies;

    private Character? _analyzedCharacter = null;
    private string _analyzedScriptHash = "";
    private Dictionary<string, TranslatedMoveSemantic> _moveSemantics = new();
    private CharacterTacticalSummary _characterSummary = new();

    // Reinforcement Learning Episode Trajectory & Reward Tracking
    private readonly List<ExperienceStep> _episodeTrajectory = new();
    private int _lastSelfHealth = Player.MAX_HEALTH;
    private int _lastOppHealth = Player.MAX_HEALTH;
    private float _lastGapPct = 0.5f;

    private float _matchTimer = 0f;
    private float _macroDecisionTimer = 0f;
    private float _reactionCooldown = 0f;

    public PlayerAI(Player self, KeyHandler keyHandler, int playerIndex = 1, Player? opponent = null)
    {
        _self = self;
        _opponent = opponent;
        _patternTracker = new OpponentPatternTracker();
        _macroBrain = MacroTacticalModel.SharedInstance;
        _evaluator = new AIStateEvaluator(self, opponent);
        _executor = new AIInputExecutor(self, keyHandler, playerIndex, _evaluator, _rng);
        _tactics = new AITacticalExecutor(_evaluator, _executor, _rng);
    }

    public void SetOpponent(Player opponent)
    {
        _opponent = opponent;
        _evaluator.SetOpponent(opponent);
    }

    public void OnRoundEnd(bool wonRound)
    {
        if (PhysicsEngine.IsHeadlessThread || _episodeTrajectory.Count == 0) return;

        float terminalReward = wonRound ? 2.5f : -2.0f;
        _macroBrain.TrainOnEpisode(_episodeTrajectory, terminalReward);
        _episodeTrajectory.Clear();

        _lastSelfHealth = Player.MAX_HEALTH;
        _lastOppHealth = Player.MAX_HEALTH;
    }

    public void HandleAIInput(float deltaTime)
    {
        if (_opponent == null || _self.CurrentCharacter == null) return;

        if (_analyzedCharacter != _self.CurrentCharacter ||
            !string.Equals(_analyzedScriptHash, _self.CurrentCharacter.ScriptHash, StringComparison.Ordinal))
        {
            _analyzedCharacter = _self.CurrentCharacter;
            _analyzedScriptHash = _analyzedCharacter.ScriptHash;
            _moveSemantics = LuaMoveTranslator.AnalyzeCharacter(_analyzedCharacter, out _characterSummary);
            _lastSelfHealth = _self.Health;
            _lastOppHealth = _opponent.Health;
            _lastGapPct = _evaluator.GetEffectiveGapPct();
        }

        _matchTimer += deltaTime;
        ObserveOpponentBehavior();
        _executor.EvaluateActiveMoveOutcome();

        if (_self.CanTech)
        {
            if (CanAdaptByDifficulty())
            {
                _executor.ExecuteAirTech();
            }
            return;
        }

        if (_self.IsStunned || _self.IsHit)
        {
            _executor.ResetMovement();
            return;
        }

        if (_evaluator.ShouldTriggerEmergencyDefense(out Projectile? imminentProj))
        {
            _tactics.ExecuteMicroDefense(_self, _opponent, imminentProj, _moveSemantics, Difficulty, CanAdaptByDifficulty);
            return;
        }

        _macroDecisionTimer -= deltaTime;
        if (_macroDecisionTimer <= 0f || !_opponent.IsGrounded || _opponent.InRecoveryFrames)
        {
            RecordStepReward();

            MacroStateVector stateVec = _evaluator.BuildMacroStateVector(_characterSummary);
            OpponentPattern spam = CanAdaptByDifficulty() ? _patternTracker.DetectSpamPattern() : OpponentPattern.None;
            if (PhysicsEngine.IsHeadlessThread)
            {
                CurrentMacroTactic = _macroBrain.SampleTactic(stateVec, Difficulty, spam);
            }
            else
            {
                CurrentMacroTactic = _macroBrain.SampleTacticWithTrace(stateVec, Difficulty, spam, out ExperienceStep trace);
                _episodeTrajectory.Add(trace);
            }

            _macroDecisionTimer = Difficulty == AIDifficulty.Hard ? 0.15f : 0.25f;
        }

        if (IsOnReactionCooldown(deltaTime))
        {
            _executor.HoldCurrentMovement();
            return;
        }

        _executor.ResetMovement();
        _tactics.ExecuteMicroTactic(CurrentMacroTactic, _self, _opponent, _moveSemantics, _characterSummary);
        SetReactionCooldown();
    }

    private void RecordStepReward()
    {
        if (_episodeTrajectory.Count == 0 || _opponent == null) return;

        int lastIdx = _episodeTrajectory.Count - 1;
        ExperienceStep step = _episodeTrajectory[lastIdx];

        float dmgDealt = Math.Max(0, _lastOppHealth - _opponent.Health) / (float)Player.MAX_HEALTH;
        float dmgTaken = Math.Max(0, _lastSelfHealth - _self.Health) / (float)Player.MAX_HEALTH;
        float currentGap = _evaluator.GetEffectiveGapPct();

        float reward = (dmgDealt * 4.5f) - (dmgTaken * 4.0f);

        if (_evaluator.IsSelfNearCorner())
        {
            reward -= 0.18f;
        }

        if (_evaluator.IsOpponentNearCorner())
        {
            reward += 0.12f;
        }

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

    private void ObserveOpponentBehavior()
    {
        if (_opponent == null) return;
        if (_opponent.JustExecutedAction != GameAction.None)
        {
            _patternTracker.RecordAction(_opponent.JustExecutedAction, _matchTimer);
        }
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

    public void Dispose()
    {
        MacroTacticalModel.DiscardTrajectory(_episodeTrajectory);
    }
}
