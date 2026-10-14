public enum AIZone
{
    Close, // Distance < 120f
    Mid,   // Distance < 300f
    Far    // Distance >= 300f
}

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
    CrouchAttack,
    LightAttack,
    MediumAttack,
    HeavyAttack
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
    Down,
    DownLeft,
    UpRight
}

public class PlayerAI
{
    private readonly Player _self;
    private Player? _opponent;
    private readonly KeyHandler _keyHandler;
    private readonly OpponentPatternTracker _patternTracker;

    public AIDifficulty Difficulty { get; set; } = AIDifficulty.Medium;
    private float _matchTimer = 0f;
    private float _reactionCooldown = 0f;
    private readonly Random _rng = new Random();

    public PlayerAI(Player self, KeyHandler keyHandler, Player? opponent = null)
    {
        _self = self;
        _keyHandler = keyHandler;
        _opponent = opponent;
        _patternTracker = new OpponentPatternTracker();
    }

    public void SetOpponent(Player opponent)
    {
        _opponent = opponent;
    }

    public void HandleAIInput(float deltaTime)
    {
        if (_opponent == null) return;

        _matchTimer += deltaTime;
        ObserveOpponentBehavior();

        if (_self.IsStunned || _self.IsHit) return;
        if (IsOnReactionCooldown(deltaTime)) return;

        OpponentPattern detectedSpam = _patternTracker.DetectSpamPattern();
        if (detectedSpam != OpponentPattern.None && CanAdaptByDifficulty())
        {
            ExecuteSpamCounterStrategy(detectedSpam);
        }
        else
        {
            HandleStandardStrategy();
        }

        SetReactionCooldown();
    }

    private void ObserveOpponentBehavior()
    {
        if (_opponent == null) return;

        if (_opponent.JustExecutedAction != GameAction.None)
        {
            _patternTracker.RecordAction(_opponent.JustExecutedAction, _opponent.JustExecutedMotion, _matchTimer);
        }

        if (_opponent.JustCompletedComboSignature != null)
        {
            _patternTracker.RecordComboSequence(_opponent.JustCompletedComboSignature);
        }
    }

    private bool CanAdaptByDifficulty()
    {
        int adaptationChance = Difficulty switch
        {
            AIDifficulty.Easy => 20,
            AIDifficulty.Medium => 60,
            AIDifficulty.Hard => 95,
            _ => 50
        };
        return _rng.Next(0, 100) < adaptationChance;
    }

    private void ExecuteSpamCounterStrategy(OpponentPattern pattern)
    {
        switch (pattern)
        {
            case OpponentPattern.ProjectileSpam:
                CounterProjectileSpam();
                break;
            case OpponentPattern.JumpInSpam:
                CounterJumpInSpam();
                break;
            case OpponentPattern.LowAttackSpam:
                CounterLowAttackSpam();
                break;
            case OpponentPattern.RepeatedCombo:
                CounterRepeatedComboPattern();
                break;
        }
    }

    private void CounterProjectileSpam()
    {
        if (GetDistanceToOpponent() > 200f)
        {
            _keyHandler.SimulateDirection(GameDirection.UpRight);
        }
        else
        {
            _keyHandler.SimulateSpecialMotion(SpecialMotion.QuarterCircleForward);
            _keyHandler.SimulateButtonPress(GameAction.HeavyAttack);
        }
    }

    private void CounterJumpInSpam()
    {
        _keyHandler.SimulateDirection(GameDirection.Down);
        _keyHandler.SimulateButtonPress(GameAction.HeavyAttack);
    }

    private void CounterLowAttackSpam()
    {
        if (_opponent != null && _opponent.IsAttacking)
        {
            _keyHandler.SimulateDirection(GameDirection.DownLeft);
        }
        else
        {
            _keyHandler.SimulateDirection(GameDirection.UpRight);
            _keyHandler.SimulateButtonPress(GameAction.HeavyAttack);
        }
    }

    private void CounterRepeatedComboPattern()
    {
        if (_opponent == null) return;

        GameDirection blockDirection = (_opponent.Position.X > _self.Position.X) ? GameDirection.Left : GameDirection.Right;

        if (_opponent.IsAttackingLow)
        {
            _keyHandler.SimulateDirection(GameDirection.DownLeft);
        }
        else
        {
            _keyHandler.SimulateDirection(blockDirection);
        }
    }

    private void HandleStandardStrategy()
    {
        float distance = GetDistanceToOpponent();
        AIZone zone = CategorizeZone(distance);

        if (ShouldTechOrRecover()) ExecuteRecovery();
        else if (ShouldAntiAir()) ExecuteAntiAirRoute();
        else if (ShouldPunish()) ExecutePunishRoute();
        else if (ShouldDefend()) HandleDefense();
        else HandleNeutralAndOffense(zone);
    }

    private void HandleNeutralAndOffense(AIZone zone)
    {
        if (_opponent == null) return;

        GameDirection moveToward = (_opponent.Position.X > _self.Position.X) 
            ? GameDirection.Right 
            : GameDirection.Left;

        switch (zone)
        {
            case AIZone.Far:
                _keyHandler.SimulateDirection(moveToward);
                break;

            case AIZone.Mid:
                int midRoll = _rng.Next(0, 100);
                if (midRoll < 50)
                {
                    _keyHandler.SimulateDirection(moveToward);
                }
                else if (midRoll < 80)
                {
                    _keyHandler.SimulateButtonPress(GameAction.HeavyAttack);
                }
                else
                {
                    _keyHandler.SimulateButtonPress(GameAction.MediumAttack);
                }
                break;

            case AIZone.Close:
                int closeRoll = _rng.Next(0, 100);
                if (closeRoll < 30)
                {
                    _keyHandler.SimulateButtonPress(GameAction.LightAttack);
                }
                else if (closeRoll < 60)
                {
                    _keyHandler.SimulateButtonPress(GameAction.MediumAttack);
                }
                else if (closeRoll < 85)
                {
                    _keyHandler.SimulateButtonPress(GameAction.CrouchAttack);
                }
                else
                {
                    _keyHandler.SimulateButtonPress(GameAction.HeavyAttack);
                }
                break;
        }
    }

    private float GetDistanceToOpponent() => _opponent != null ? Math.Abs(_self.Position.X - _opponent.Position.X) : 0f;
    private AIZone CategorizeZone(float dist) => dist < 120f ? AIZone.Close : dist < 300f ? AIZone.Mid : AIZone.Far;
    private bool ShouldTechOrRecover() => !_self.IsGrounded && _self.CanTech;
    private void ExecuteRecovery() => _keyHandler.SimulateButtonPress(GameAction.LightAttack);
    private bool ShouldAntiAir() => _opponent != null && !_opponent.IsGrounded && GetDistanceToOpponent() < 180f;
    private void ExecuteAntiAirRoute() => _keyHandler.SimulateButtonPress(GameAction.HeavyAttack);
    private bool ShouldPunish() => _opponent != null && _opponent.InRecoveryFrames && GetDistanceToOpponent() < 200f;
    private void ExecutePunishRoute() => _keyHandler.SimulateButtonPress(GameAction.MediumAttack);
    private bool ShouldDefend() => _opponent != null && _opponent.IsAttacking;
    private void HandleDefense() => _keyHandler.SimulateDirection(GameDirection.Left);
    private bool IsOnReactionCooldown(float dt) { _reactionCooldown -= dt; return _reactionCooldown > 0; }
    private void SetReactionCooldown()
    {
        _reactionCooldown = Difficulty switch
        {
            AIDifficulty.Easy => 0.35f,
            AIDifficulty.Medium => 0.20f,
            AIDifficulty.Hard => 0.08f,
            _ => 0.20f
        };
    }
}