using System.Numerics;
using Raylib_cs;
using MoonSharp.Interpreter;

[MoonSharpUserData]
public class Player
{
    public const int MAX_HEALTH = 100;
    public const float CROUCH_HEIGHT_RATIO = 0.65f;
    private readonly string _name;
    private readonly bool _isAI;
    private readonly int _playerIndex;
    private readonly KeyHandler _keyHandler;
    private readonly PlayerAI? _aiController;
    public float HitstunTimer { get; set; } = 0f;
    public bool IsInHitstun => HitstunTimer > 0f;
    public Character? CurrentCharacter { get; private set; }

    public Vector2 Position;
    public Vector2 Velocity;
    public bool IsGrounded = false;
    public bool FacingRight = true;
    public bool IsInputIdle { get; private set; } = true;
    public bool IsCrouching { get; set; } = false;
    public bool IsAirStalled { get; set; } = false;

    public bool IsNeutralBlocking => 
        IsGrounded && 
        IsInputIdle && 
        CurrentCharacter?.ActiveSkill == null && 
        !IsInHitstun;
    
    public int Health { get; set; } = MAX_HEALTH;
    public bool IsAlive => Health > 0;

    public Player(string name, bool isAI, int playerIndex, Vector2 spawnPos, AppConfig appConfig)
    {
        _name = name;
        _isAI = isAI;
        _playerIndex = playerIndex;
        _keyHandler = new KeyHandler(appConfig);
        Position = spawnPos;
        FacingRight = playerIndex == 0;

        if (_isAI)
        {
            _aiController = new PlayerAI();
        }
    }

    public bool ChangeCharacter(Character newCharacter)
    {
        CurrentCharacter = newCharacter;
        Health = MAX_HEALTH;
        Velocity = Vector2.Zero;
        return true;
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

    public void HandleInput()
    {
        if (GameState.CurrentState != State.Playing) return;
        if (CurrentCharacter == null) return;
        if (IsInHitstun) return;

        if (_isAI && _aiController != null)
        {
            _aiController.HandleAIInput();
            return;
        }

        IsInputIdle = _keyHandler.HandleInput(CurrentCharacter, this, _playerIndex);
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
        if (HitstunTimer > 0f)
        {
            HitstunTimer -= dt;
        }

        if (CurrentCharacter == null) return;
        CurrentCharacter.Update(this, dt);

        if (!IsAlive)
        {
            GameState.SetState(State.GameOver);
        }
    }

    public void Draw()
    {
        if (CurrentCharacter == null) return;
        CurrentCharacter.Draw(this);
    }
}