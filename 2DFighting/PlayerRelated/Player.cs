using System.Numerics;
using Raylib_cs;
using MoonSharp.Interpreter;

[MoonSharpUserData]
public class Player
{
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
    public int Health { get; set; } = 100;
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
        Health = 100;
        Velocity = Vector2.Zero;
        return true;
    }

    public Rectangle GetHurtbox()
    {
        if (CurrentCharacter == null) return default;
        return new Rectangle(Position.X, Position.Y, CurrentCharacter.Width, CurrentCharacter.Height);
    }

    public bool TryGetHitbox(out Rectangle hitbox)
    {
        hitbox = default;
        if (CurrentCharacter == null || !CurrentCharacter.IsHitboxActive) return false;

        SkillData skill = CurrentCharacter.ActiveSkill!;
        Rectangle offset = skill.HitboxOffset;
        float hitX = FacingRight
            ? Position.X + offset.x
            : Position.X + CurrentCharacter.Width - offset.x - offset.width;

        hitbox = new Rectangle(hitX, Position.Y + offset.y, offset.width, offset.height);
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

        _keyHandler.HandleInput(CurrentCharacter, this, _playerIndex);
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