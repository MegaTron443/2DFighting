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
    public Character? CurrentCharacter { get; private set; }

    public Vector2 Position;
    public Vector2 Velocity;
    public bool IsGrounded = false;
    public bool FacingRight = true;
    public int Health { get; set; } = 100;
    public bool IsAlive => Health > 0;

    public Player(string name, bool isAI, int playerIndex, Vector2 spawnPos)
    {
        _name = name;
        _isAI = isAI;
        _playerIndex = playerIndex;
        _keyHandler = new KeyHandler(playerIndex);
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
        if (CurrentCharacter == null || CurrentCharacter.ActiveSkill == null) return false;

        SkillData skill = CurrentCharacter.ActiveSkill;
        int start = skill.StartupFrames;
        int end = start + skill.ActiveFrames;

        if (CurrentCharacter.CurrentActionFrame >= start && CurrentCharacter.CurrentActionFrame < end)
        {
            Rectangle offset = skill.HitboxOffset;
            float hitX = FacingRight
                ? Position.X + offset.x
                : Position.X + CurrentCharacter.Width - offset.x - offset.width;

            hitbox = new Rectangle(hitX, Position.Y + offset.y, offset.width, offset.height);
            return true;
        }

        return false;
    }

    // TODO: Rework with KeyHandler and PlayerAI for better input handling
    public void HandleInput()
    {
        if (CurrentCharacter == null) return;

        if (_isAI && _aiController != null)
        {
            _aiController.HandleAIInput();
            return;
        }

        if (_playerIndex == 0)
        {
            float dir = 0;
            if (Raylib.IsKeyDown(KeyboardKey.KEY_D)) dir += 1;
            if (Raylib.IsKeyDown(KeyboardKey.KEY_A)) dir -= 1;

            CurrentCharacter.Move(this, dir);
            if (Raylib.IsKeyPressed(KeyboardKey.KEY_W)) CurrentCharacter.Jump(this);
            if (Raylib.IsKeyPressed(KeyboardKey.KEY_J)) CurrentCharacter.ExecuteSkill(this, "light_punch");
            if (Raylib.IsKeyPressed(KeyboardKey.KEY_K)) CurrentCharacter.ExecuteSkill(this, "heavy_kick");
        }
        else if (_playerIndex == 1)
        {
            float dir = 0;
            if (Raylib.IsKeyDown(KeyboardKey.KEY_RIGHT)) dir += 1;
            if (Raylib.IsKeyDown(KeyboardKey.KEY_LEFT)) dir -= 1;

            CurrentCharacter.Move(this, dir);
            if (Raylib.IsKeyPressed(KeyboardKey.KEY_UP)) CurrentCharacter.Jump(this);
            if (Raylib.IsKeyPressed(KeyboardKey.KEY_KP_1)) CurrentCharacter.ExecuteSkill(this, "light_punch");
            if (Raylib.IsKeyPressed(KeyboardKey.KEY_KP_2)) CurrentCharacter.ExecuteSkill(this, "heavy_kick");
        }
    }

    public void Update(float dt)
    {
        if (CurrentCharacter == null) return;
        CurrentCharacter.Update(this, dt);
    }

    public void Draw()
    {
        if (CurrentCharacter == null) return;
        CurrentCharacter.Draw(this);
    }
}