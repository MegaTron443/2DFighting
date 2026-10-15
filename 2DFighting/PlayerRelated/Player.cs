using System.Numerics;
using Raylib_cs;
using MoonSharp.Interpreter;

[MoonSharpUserData]
public class Player
{
    public Vector2 SpawnPosition { get; }
    public int ComboCount { get; set; } = 0;
    public int RoundsWon { get; set; } = 0;

    public const int MAX_HEALTH = 350;
    public const float CROUCH_HEIGHT_RATIO = 0.65f;
    private const float TECH_INVULNERABILITY_DURATION = 0.25f;
    private const float TECH_POPUP_DURATION = 0.45f;

    private readonly string _name;
    private readonly bool _isAI;
    private readonly int _playerIndex;
    private readonly KeyHandler _keyHandler;
    private readonly PlayerAI? _aiController;

    public string Name => _name;
    public float HitstunTimer { get; set; } = 0f;
    public float InvulnerableTimer { get; set; } = 0f;
    public float TechPopupTimer { get; set; } = 0f;

    public bool IsInHitstun => HitstunTimer > 0f;
    public bool IsInvulnerable => InvulnerableTimer > 0f;

    public Character? CurrentCharacter { get; private set; }
    public Player? TargetOpponent { get; private set; }

    public Vector2 Position;
    public Vector2 PreviousPosition;
    public Vector2 Velocity;

    public bool IsGrounded = false;
    public bool FacingRight = true;
    public bool IsInputIdle { get; private set; } = true;
    public bool IsCrouching { get; set; } = false;
    public bool IsAirStalled { get; set; } = false;
    public bool IsHoldingBack { get; set; } = false;

    public bool IsNeutralBlocking =>
        IsGrounded &&
        (IsInputIdle || IsHoldingBack || IsCrouching) &&
        CurrentCharacter?.ActiveSkill == null &&
        !IsInHitstun;

    public int Health { get; set; } = MAX_HEALTH;
    public bool IsAlive => Health > 0;
    public bool IsStunned => IsInHitstun;
    public bool IsHit { get; set; } = false;

    public GameAction JustExecutedAction { get; set; } = GameAction.None;
    public SpecialMotion JustExecutedMotion { get; set; } = SpecialMotion.None;
    public string? JustCompletedComboSignature { get; set; } = null;

    public bool IsAttacking => CurrentCharacter?.ActiveSkill != null;
    public bool IsAttackingLow => CurrentCharacter?.ActiveSkill?.Height == AttackHeight.Low;

    public float PosX => Position.X;
    public float PosY => Position.Y;

    public bool InRecoveryFrames
    {
        get
        {
            if (CurrentCharacter?.ActiveSkill == null) return false;
            SkillData skill = CurrentCharacter.ActiveSkill;
            int activeEndFrame = skill.StartupFrames + skill.ActiveFrames;
            return CurrentCharacter.CurrentActionFrame >= activeEndFrame;
        }
    }

    public bool CanTech => !IsGrounded && IsInHitstun && HitstunTimer < 0.15f;

    public Player(string name, bool isAI, int playerIndex, Vector2 spawnPos, AppConfig appConfig, Player? opponent = null)
    {
        _name = name;
        _isAI = isAI;
        _playerIndex = playerIndex;
        _keyHandler = new KeyHandler(appConfig);
        SpawnPosition = spawnPos;
        Position = spawnPos;
        PreviousPosition = spawnPos;
        FacingRight = playerIndex == 0;
        TargetOpponent = opponent;

        if (_isAI)
        {
            _aiController = new PlayerAI(this, _keyHandler, playerIndex, TargetOpponent);
        }
    }

    public static Player Create(string name, int playerIndex, Vector2 spawnPos, AppConfig appConfig, bool isAI = false)
    {
        return new Player(name, isAI, playerIndex, spawnPos, appConfig);
    }

    public Player WithOpponent(Player opponent)
    {
        SetOpponent(opponent);
        return this;
    }

    public Player WithCharacter(Character character)
    {
        ChangeCharacter(character);
        return this;
    }

    public void SetOpponent(Player opponent)
    {
        TargetOpponent = opponent;
        _aiController?.SetOpponent(opponent);
    }

    public bool ChangeCharacter(Character newCharacter)
    {
        CurrentCharacter = newCharacter;
        Health = MAX_HEALTH;
        Velocity = Vector2.Zero;
        return true;
    }

    public void ResetState(bool faceRight)
    {
        Position = SpawnPosition;
        PreviousPosition = SpawnPosition;
        Velocity = Vector2.Zero;
        Health = MAX_HEALTH;
        HitstunTimer = 0f;
        InvulnerableTimer = 0f;
        TechPopupTimer = 0f;
        IsGrounded = false;
        IsCrouching = false;
        IsAirStalled = false;
        FacingRight = faceRight;
        ComboCount = 0;
        _keyHandler.ClearBuffer(_playerIndex);

        if (CurrentCharacter != null)
        {
            CurrentCharacter.ActiveSkill = null;
            CurrentCharacter.HasHit = false;
            CurrentCharacter.SkillCooldowns.Clear();
        }
    }

    public void SavePreviousState()
    {
        PreviousPosition = Position;
    }

    public Vector2 GetInterpolatedPosition(float alpha)
    {
        return Vector2.Lerp(PreviousPosition, Position, alpha);
    }

    public Rectangle GetHurtbox()
    {
        if (CurrentCharacter == null) return default;
        float fullW = CurrentCharacter.Width;
        float fullH = CurrentCharacter.Height;

        if (IsCrouching && IsGrounded)
        {
            float crouchH = fullH * CROUCH_HEIGHT_RATIO;
            float topY = (Position.Y + (fullH / 2f)) - crouchH;
            return new Rectangle(Position.X - (fullW / 2f), topY, fullW, crouchH);
        }

        return new Rectangle(
            Position.X - (fullW / 2f),
            Position.Y - (fullH / 2f),
            fullW,
            fullH
        );
    }

    public Rectangle GetRenderHurtbox(float alpha)
    {
        if (CurrentCharacter == null) return default;
        Vector2 renderPos = GetInterpolatedPosition(alpha);
        float fullW = CurrentCharacter.Width;
        float fullH = CurrentCharacter.Height;

        if (IsCrouching && IsGrounded)
        {
            float crouchH = fullH * CROUCH_HEIGHT_RATIO;
            float topY = (renderPos.Y + (fullH / 2f)) - crouchH;
            return new Rectangle(renderPos.X - (fullW / 2f), topY, fullW, crouchH);
        }

        return new Rectangle(
            renderPos.X - (fullW / 2f),
            renderPos.Y - (fullH / 2f),
            fullW,
            fullH
        );
    }

    public bool TryGetRenderHitbox(float alpha, out Rectangle hitbox)
    {
        hitbox = default;
        if (CurrentCharacter == null || !CurrentCharacter.IsHitboxActive) return false;

        Vector2 renderPos = GetInterpolatedPosition(alpha);
        SkillData skill = CurrentCharacter.ActiveSkill!;
        Rectangle pct = skill.HitboxOffsetPct;

        float hbWidth = CurrentCharacter.Width * pct.width;
        float hbHeight = CurrentCharacter.Height * pct.height;
        float offsetX = CurrentCharacter.Width * pct.x;
        float offsetY = CurrentCharacter.Height * pct.y;

        float rootX = renderPos.X - (CurrentCharacter.Width / 2f);
        float rootY = renderPos.Y - (CurrentCharacter.Height / 2f);

        float hitX = FacingRight
            ? rootX + offsetX
            : rootX + CurrentCharacter.Width - offsetX - hbWidth;
        float hitY = rootY + offsetY;

        hitbox = new Rectangle(hitX, hitY, hbWidth, hbHeight);
        return true;
    }

    public bool TryGetHitbox(out Rectangle hitbox)
    {
        hitbox = default;
        if (CurrentCharacter == null || !CurrentCharacter.IsHitboxActive) return false;

        SkillData skill = CurrentCharacter.ActiveSkill!;
        Rectangle pct = skill.HitboxOffsetPct;

        float hbWidth = CurrentCharacter.Width * pct.width;
        float hbHeight = CurrentCharacter.Height * pct.height;
        float offsetX = CurrentCharacter.Width * pct.x;
        float offsetY = CurrentCharacter.Height * pct.y;

        float rootX = Position.X - (CurrentCharacter.Width / 2f);
        float rootY = Position.Y - (CurrentCharacter.Height / 2f);

        float hitX = FacingRight
            ? rootX + offsetX
            : rootX + CurrentCharacter.Width - offsetX - hbWidth;
        float hitY = rootY + offsetY;

        hitbox = new Rectangle(hitX, hitY, hbWidth, hbHeight);
        return true;
    }

    public void PollInput()
    {
        if (GameState.CurrentState != State.Playing || CurrentCharacter == null) return;
        if (!_isAI) _keyHandler.PollHardwareInput(_playerIndex);
    }

    private void ExecuteAirTech(float techDir)
    {
        if (CurrentCharacter == null) return;

        HitstunTimer = 0f;
        IsAirStalled = false;
        InvulnerableTimer = TECH_INVULNERABILITY_DURATION;
        TechPopupTimer = TECH_POPUP_DURATION;

        if (TargetOpponent != null)
        {
            TargetOpponent.ComboCount = 0;
        }

        float escapeDir = MathF.Abs(techDir) > 0.1f
            ? MathF.Sign(techDir)
            : (FacingRight ? -1f : 1f);

        Velocity.X = escapeDir * CurrentCharacter.WalkSpeed * 0.85f;
        Velocity.Y = CurrentCharacter.JumpForce * 0.55f;
    }

    public void ConsumeInput()
    {
        JustExecutedAction = GameAction.None;
        JustExecutedMotion = SpecialMotion.None;
        JustCompletedComboSignature = null;

        if (GameState.CurrentState != State.Playing || CurrentCharacter == null) return;

        if (CanTech)
        {
            if (_isAI && _aiController != null)
            {
                _aiController.HandleAIInput(Timer.FixedDeltaTime);
            }

            if (_keyHandler.TryConsumeTechInput(_playerIndex, out float techDir))
            {
                ExecuteAirTech(techDir);
                return;
            }
        }

        if (IsInHitstun) return;

        if (_isAI && _aiController != null)
        {
            _aiController.HandleAIInput(Timer.FixedDeltaTime);
            IsInputIdle = _keyHandler.ConsumeBufferedInput(CurrentCharacter, this, _playerIndex);
            return;
        }

        IsInputIdle = _keyHandler.ConsumeBufferedInput(CurrentCharacter, this, _playerIndex);
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

    public void Update(float dt)
    {
        if (GameState.CurrentState != State.Playing) return;

        if (HitstunTimer > 0f) HitstunTimer = MathF.Max(0f, HitstunTimer - dt);
        if (InvulnerableTimer > 0f) InvulnerableTimer = MathF.Max(0f, InvulnerableTimer - dt);
        if (TechPopupTimer > 0f) TechPopupTimer = MathF.Max(0f, TechPopupTimer - dt);

        if (CurrentCharacter == null) return;
        CurrentCharacter.Update(this, dt);
    }

    public void Draw(float alpha)
    {
        if (CurrentCharacter == null) return;
        CurrentCharacter.Draw(this, alpha);

        if (TechPopupTimer > 0f)
        {
            Vector2 renderPos = GetInterpolatedPosition(alpha);
            int fontSize = Math.Max(14, (int)(Raylib.GetScreenHeight() * 0.025f));
            string techText = "TECH!";
            int tw = Raylib.MeasureText(techText, fontSize);
            int tx = (int)renderPos.X - (tw / 2);
            int ty = (int)(renderPos.Y - (CurrentCharacter.Height * 0.65f));
            Raylib.DrawText(techText, tx + 2, ty + 2, fontSize, Color.BLACK);
            Raylib.DrawText(techText, tx, ty, fontSize, Color.SKYBLUE);
        }
    }
}