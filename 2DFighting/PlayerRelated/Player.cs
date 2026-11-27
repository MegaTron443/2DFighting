using System.Numerics;
using Raylib_cs;
using MoonSharp.Interpreter;

public enum DummyBlockMode
{
    Off,
    Stand,
    Crouch
}

public sealed class TagTeamMember
{
    public Character? Character { get; set; }
    public int Health { get; set; } = Player.MAX_HEALTH;
    public int MaxHealth { get; set; } = Player.MAX_HEALTH;
    public float RecoverableHealth { get; set; } = 0f;
    public bool IsAlive => Character != null && Health > 0;
}

[MoonSharpUserData]
public class Player : IDisposable
{
    public Vector2 SpawnPosition { get; }
    public int ComboCount { get; set; } = 0;
    public int RoundsWon { get; set; } = 0;

    public const int MAX_HEALTH = 350;
    public const float CROUCH_HEIGHT_RATIO = 0.65f;
    public const float TAG_SWITCH_COOLDOWN = 4.0f;
    public const float ASSIST_COOLDOWN = 5.0f;
    public const float FORCED_TAG_INVULN_DURATION = 0.75f;
    public const float TAG_IN_INVULN_DURATION = 0.25f;
    public const float RECOVERABLE_HEALTH_RATIO = 0.35f;
    public const float BENCH_REGEN_PER_SEC = 18.0f;
    public const float ASSIST_SPAWN_OFFSET_X_PCT = 0.08f;
    private const float TECH_INVULNERABILITY_DURATION = 0.25f;
    private const float TECH_POPUP_DURATION = 0.45f;
    private const float AIR_TECH_WINDOW_SECONDS = 0.15f;
    private const float AIR_TECH_VX_MULT = 0.85f;
    private const float AIR_TECH_VY_MULT = 0.55f;
    private const float PLATFORM_DROP_DURATION = 0.18f;

    private readonly string _name;
    private readonly bool _isAI;
    private readonly int _playerIndex;
    private readonly KeyHandler _keyHandler;
    private readonly PlayerAI? _aiController;
    private readonly TagTeamMember[] _team = { new(), new() };
    private float _benchRegenAccum = 0f;

    public int PlayerIndex => _playerIndex;
    public string Name => _name;
    public bool IsAI => _isAI;
    public string? LastExecutedSkillName { get; set; }
    public bool IsPassiveDummy { get; set; } = false;
    public DummyBlockMode DummyBlock { get; set; } = DummyBlockMode.Off;
    public bool AutoHealEnabled { get; set; } = true;
    public float TimeSinceHitstun { get; set; } = 0f;
    public float HitstunTimer { get; set; } = 0f;
    public float InvulnerableTimer { get; set; } = 0f;
    public float TechPopupTimer { get; set; } = 0f;

    public bool IsInHitstun => HitstunTimer > 0f;
    public bool IsInvulnerable => InvulnerableTimer > 0f;

    public int ActiveMemberIndex { get; private set; } = 0;
    public TagTeamMember ActiveMember => _team[ActiveMemberIndex];
    public TagTeamMember PartnerMember => _team[ActiveMemberIndex == 0 ? 1 : 0];
    public TagTeamMember GetMember(int index) => _team[index == 1 ? 1 : 0];
    public Character? PrimaryCharacter => _team[0].Character;
    public Character? PartnerCharacter => _team[1].Character;
    public Character? ActivePartnerCharacter => PartnerMember.Character;
    public bool HasPartner => _team[1].Character != null;
    public bool IsTagTeam => _team[0].Character != null && _team[1].Character != null;
    public Character? CurrentCharacter => _team[ActiveMemberIndex].Character;
    public Player? TargetOpponent { get; private set; }

    public float TagCooldownTimer { get; set; } = 0f;
    public float AssistCooldownTimer { get; set; } = 0f;
    public bool IsAssistActive { get; set; } = false;
    public Vector2 AssistPosition;
    public Vector2 AssistPreviousPosition;
    public bool AssistFacingRight { get; set; } = true;
    public int AssistActionFrame { get; set; } = 0;
    public bool AssistHasHit { get; set; } = false;
    public string? AssistSkillName { get; set; } = null;
    public SkillData? ActiveAssistSkill =>
        (!string.IsNullOrEmpty(AssistSkillName) && ActivePartnerCharacter != null && ActivePartnerCharacter.Skills.TryGetValue(AssistSkillName, out var skill))
            ? skill
            : null;
    public float BenchRegenAccum => _benchRegenAccum;

    public Vector2 Position;
    public Vector2 PreviousPosition;
    public Vector2 Velocity;

    public bool IsGrounded = false;
    public bool IsOnPlatform { get; set; } = false;
    public float PlatformDropTimer { get; set; } = 0f;
    public bool FacingRight = true;
    public bool IsInputIdle { get; private set; } = true;
    public bool IsCrouching { get; set; } = false;
    public bool IsAirStalled { get; set; } = false;
    public bool HasUsedAirStall { get; set; } = false;
    public bool IsHoldingBack { get; set; } = false;

    public bool IsNeutralBlocking =>
        (!IsPassiveDummy || DummyBlock != DummyBlockMode.Off) &&
        IsGrounded &&
        (IsInputIdle || IsHoldingBack || IsCrouching) &&
        CurrentCharacter?.ActiveSkill == null &&
        !IsInHitstun;

    public int Health
    {
        get => _team[ActiveMemberIndex].Health;
        set
        {
            TagTeamMember active = _team[ActiveMemberIndex];
            int prev = active.Health;
            int clamped = Math.Clamp(value, 0, active.MaxHealth);
            if (clamped < prev)
            {
                float maxRecoverable = Math.Max(0, active.MaxHealth - clamped);
                active.RecoverableHealth = MathF.Min(maxRecoverable, active.RecoverableHealth + ((prev - clamped) * RECOVERABLE_HEALTH_RATIO));
            }
            else if (clamped > prev)
            {
                active.RecoverableHealth = MathF.Min(active.RecoverableHealth, Math.Max(0, active.MaxHealth - clamped));
            }
            active.Health = clamped;
        }
    }

    public bool IsAlive => _team[ActiveMemberIndex].Health > 0;
    public bool IsTeamAlive => HasPartner ? (_team[0].Health > 0 || _team[1].Health > 0) : (_team[0].Health > 0);
    public int TotalTeamHealth => HasPartner ? (_team[0].Health + _team[1].Health) : _team[0].Health;
    public bool IsStunned => IsInHitstun;
    public bool IsHit { get; set; } = false;

    public GameAction JustExecutedAction { get; set; } = GameAction.None;
    public SpecialMotion JustExecutedMotion { get; set; } = SpecialMotion.None;
    public string? JustCompletedComboSignature { get; set; } = null;

    public bool IsAttacking => CurrentCharacter?.ActiveSkill != null;
    public bool IsAttackingLow => CurrentCharacter?.ActiveSkill?.Height == AttackHeight.Low;

    public float PosX => Position.X;
    public float PosY => Position.Y;
    public float ScreenWidth => PhysicsEngine.ViewportWidth;
    public float ScreenHeight => PhysicsEngine.ViewportHeight;

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

    public bool CanTech => !IsGrounded && IsInHitstun && HitstunTimer < AIR_TECH_WINDOW_SECONDS;

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
            _aiController = new PlayerAI(this, _keyHandler, playerIndex, TargetOpponent)
            {
                Difficulty = appConfig.AIDifficulty
            };
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

    public void SetAIDifficulty(AIDifficulty difficulty)
    {
        if (_aiController != null)
            _aiController.Difficulty = difficulty;
    }

    public bool ChangeCharacter(Character newCharacter)
    {
        ActiveMemberIndex = 0;
        _team[0].Character = newCharacter;
        _team[0].Health = MAX_HEALTH;
        _team[0].MaxHealth = MAX_HEALTH;
        _team[0].RecoverableHealth = 0f;
        Velocity = Vector2.Zero;
        return true;
    }

    public void SetPartnerCharacter(Character? partner)
    {
        _team[1].Character = partner;
        _team[1].MaxHealth = MAX_HEALTH;
        _team[1].Health = partner != null ? MAX_HEALTH : 0;
        _team[1].RecoverableHealth = 0f;
        EndAssist(spawnVfx: false);
    }

    public void SetTeam(Character primary, Character? partner)
    {
        ActiveMemberIndex = 0;
        _team[0].Character = primary;
        _team[0].MaxHealth = MAX_HEALTH;
        _team[0].Health = MAX_HEALTH;
        _team[0].RecoverableHealth = 0f;

        _team[1].Character = partner;
        _team[1].MaxHealth = MAX_HEALTH;
        _team[1].Health = partner != null ? MAX_HEALTH : 0;
        _team[1].RecoverableHealth = 0f;

        _benchRegenAccum = 0f;
        TagCooldownTimer = 0f;
        AssistCooldownTimer = 0f;
        Velocity = Vector2.Zero;
        EndAssist(spawnVfx: false);
    }

    public void RestoreTeamSnapshot(
        int activeMemberIndex,
        int member0Health,
        int member1Health,
        float member0Recoverable,
        float member1Recoverable,
        float benchRegenAccum,
        float tagCooldownTimer,
        float assistCooldownTimer,
        bool isAssistActive,
        Vector2 assistPos,
        Vector2 assistPrevPos,
        bool assistFacingRight,
        int assistActionFrame,
        bool assistHasHit,
        string? assistSkillName)
    {
        ActiveMemberIndex = (activeMemberIndex == 1 && _team[1].Character != null) ? 1 : 0;
        _team[0].Health = Math.Clamp(member0Health, 0, _team[0].MaxHealth);
        _team[1].Health = Math.Clamp(member1Health, 0, _team[1].MaxHealth);
        _team[0].RecoverableHealth = MathF.Max(0f, member0Recoverable);
        _team[1].RecoverableHealth = MathF.Max(0f, member1Recoverable);
        _benchRegenAccum = MathF.Max(0f, benchRegenAccum);
        TagCooldownTimer = MathF.Max(0f, tagCooldownTimer);
        AssistCooldownTimer = MathF.Max(0f, assistCooldownTimer);
        IsAssistActive = isAssistActive;
        AssistPosition = assistPos;
        AssistPreviousPosition = assistPrevPos;
        AssistFacingRight = assistFacingRight;
        AssistActionFrame = assistActionFrame;
        AssistHasHit = assistHasHit;
        AssistSkillName = assistSkillName;
    }

    public void ResetState(bool faceRight)
    {
        ActiveMemberIndex = 0;
        _team[0].MaxHealth = MAX_HEALTH;
        _team[0].Health = _team[0].Character != null ? MAX_HEALTH : 0;
        _team[0].RecoverableHealth = 0f;
        _team[1].MaxHealth = MAX_HEALTH;
        _team[1].Health = _team[1].Character != null ? MAX_HEALTH : 0;
        _team[1].RecoverableHealth = 0f;
        _benchRegenAccum = 0f;
        TagCooldownTimer = 0f;
        AssistCooldownTimer = 0f;
        EndAssist(spawnVfx: false);

        Position = SpawnPosition;
        PreviousPosition = SpawnPosition;
        Velocity = Vector2.Zero;
        HitstunTimer = 0f;
        TimeSinceHitstun = 0f;
        InvulnerableTimer = 0f;
        TechPopupTimer = 0f;
        IsGrounded = false;
        IsOnPlatform = false;
        PlatformDropTimer = 0f;
        IsCrouching = false;
        IsAirStalled = false;
        HasUsedAirStall = false;
        FacingRight = faceRight;
        ComboCount = 0;
        LastExecutedSkillName = null;
        _keyHandler.ClearBuffer(_playerIndex);

        _team[0].Character?.ResetCombatState();
        _team[1].Character?.ResetCombatState();
    }

    public bool TryTagSwitch(bool forced = false)
    {
        if (!IsTagTeam) return false;

        int nextIndex = ActiveMemberIndex == 0 ? 1 : 0;
        TagTeamMember incoming = _team[nextIndex];
        if (incoming.Character == null || incoming.Health <= 0)
            return false;

        if (!forced && (TagCooldownTimer > 0f || IsInHitstun || IsAssistActive))
            return false;

        CurrentCharacter?.SkillController.CancelActiveSkill();
        EndAssist(spawnVfx: false);

        ActiveMemberIndex = nextIndex;
        Character nextChar = _team[nextIndex].Character!;
        nextChar.SkillController.CancelActiveSkill();

        Velocity = Vector2.Zero;
        IsCrouching = false;
        IsAirStalled = false;
        HasUsedAirStall = false;
        _benchRegenAccum = 0f;

        if (forced)
        {
            HitstunTimer = 0f;
            TimeSinceHitstun = 0f;
            InvulnerableTimer = FORCED_TAG_INVULN_DURATION;
            if (TargetOpponent != null) TargetOpponent.ComboCount = 0;

            float groundY = PhysicsEngine.GetEffectiveGroundY(Position);
            Position.Y = groundY - (nextChar.Height * 0.5f);
            PreviousPosition = Position;
            IsGrounded = true;
            IsOnPlatform = false;
        }
        else
        {
            InvulnerableTimer = MathF.Max(InvulnerableTimer, TAG_IN_INVULN_DURATION);
            TagCooldownTimer = TAG_SWITCH_COOLDOWN;
        }

        VFXManager.SpawnPreset(VFXPreset.Shockwave, Position, FacingRight);
        if (!PhysicsEngine.IsHeadlessThread && !PhysicsEngine.SuppressPresentation)
        {
            SoundHandler.Current?.PlaySound(this, SoundType.RoundStart);
        }

        return true;
    }

    public bool TryHandleForcedTagOnKO(GameData data)
    {
        if (!IsAlive && IsTeamAlive)
        {
            if (!PhysicsEngine.IsHeadlessThread && !PhysicsEngine.SuppressPresentation)
            {
                data.SoundHandler?.PlaySound(this, SoundType.Death);
            }
            return TryTagSwitch(forced: true);
        }
        return false;
    }

    public bool TryCallAssist()
    {
        if (!IsTagTeam || IsAssistActive || AssistCooldownTimer > 0f)
            return false;

        TagTeamMember partner = PartnerMember;
        if (!partner.IsAlive || partner.Character == null)
            return false;

        SkillData? chosen = SelectAssistSkill(partner.Character);
        if (chosen == null)
            return false;

        float dir = FacingRight ? 1f : -1f;
        float halfW = partner.Character.Width * 0.5f;
        float spawnX = Math.Clamp(
            Position.X - (dir * PhysicsEngine.ViewportWidth * ASSIST_SPAWN_OFFSET_X_PCT),
            PhysicsEngine.LeftBoundary + halfW,
            PhysicsEngine.RightBoundary - halfW);
        float groundY = PhysicsEngine.GetEffectiveGroundY(Position);
        float spawnY = groundY - (partner.Character.Height * 0.5f);

        AssistPosition = new Vector2(spawnX, spawnY);
        AssistPreviousPosition = AssistPosition;
        AssistFacingRight = FacingRight;
        AssistActionFrame = 0;
        AssistHasHit = false;
        AssistSkillName = chosen.Name;
        IsAssistActive = true;
        AssistCooldownTimer = ASSIST_COOLDOWN;

        if (!PhysicsEngine.IsHeadlessThread)
        {
            partner.Character.Animator.SetState(AnimationState.Attacking, chosen.AnimationClipName);
        }

        VFXManager.SpawnPreset(VFXPreset.Dust, AssistPosition, AssistFacingRight);
        return true;
    }

    public void EndAssist(bool spawnVfx = true)
    {
        if (spawnVfx && IsAssistActive)
        {
            VFXManager.SpawnPreset(VFXPreset.Dust, AssistPosition, AssistFacingRight);
        }
        IsAssistActive = false;
        AssistActionFrame = 0;
        AssistHasHit = false;
        AssistSkillName = null;
    }

    public bool TryGetAssistHitbox(out Rectangle hitbox)
    {
        hitbox = default;
        Character? partner = ActivePartnerCharacter;
        SkillData? skill = ActiveAssistSkill;
        if (!IsAssistActive || partner == null || skill == null) return false;

        int start = skill.StartupFrames;
        int end = start + skill.ActiveFrames;
        if (AssistActionFrame < start || AssistActionFrame >= end)
            return false;

        Rectangle pct = skill.HitboxOffsetPct;
        float hbWidth = partner.Width * pct.width;
        float hbHeight = partner.Height * pct.height;
        float offsetX = partner.Width * pct.x;
        float offsetY = partner.Height * pct.y;

        float rootX = AssistPosition.X - (partner.Width * 0.5f);
        float rootY = AssistPosition.Y - (partner.Height * 0.5f);
        float hitX = AssistFacingRight
            ? rootX + offsetX
            : rootX + partner.Width - offsetX - hbWidth;
        float hitY = rootY + offsetY;

        hitbox = new Rectangle(hitX, hitY, hbWidth, hbHeight);
        return true;
    }

    private static SkillData? SelectAssistSkill(Character partner)
    {
        if (partner.Skills.TryGetValue("assist", out SkillData? explicitAssist))
            return explicitAssist;

        SkillData? best = null;
        int bestBits = int.MaxValue;
        var list = partner.SkillController.SkillList;
        for (int i = 0; i < list.Count; i++)
        {
            SkillData s = list[i];
            if (s.IsCounter) continue;
            if (s.TriggerMask != 0)
            {
                int bits = BitOperations.PopCount((uint)s.TriggerMask);
                if (bits < bestBits)
                {
                    bestBits = bits;
                    best = s;
                }
            }
            else if (best == null)
            {
                best = s;
            }
        }

        return best ?? (list.Count > 0 ? list[0] : null);
    }

    public void SavePreviousState()
    {
        PreviousPosition = Position;
        if (IsAssistActive)
            AssistPreviousPosition = AssistPosition;
    }

    public Vector2 GetInterpolatedPosition(float alpha) // Larp funtion, lets LARP together.
    {
        return Vector2.Lerp(PreviousPosition, Position, alpha);
    }

    public Rectangle GetHurtbox() => ComputeHurtboxAt(Position);

    public Rectangle GetRenderHurtbox(float alpha) => ComputeHurtboxAt(GetInterpolatedPosition(alpha));

    private Rectangle ComputeHurtboxAt(Vector2 center)
    {
        if (CurrentCharacter == null) return default;
        float fullW = CurrentCharacter.Width;
        float fullH = CurrentCharacter.Height;

        if (IsCrouching && IsGrounded)
        {
            float crouchH = fullH * CROUCH_HEIGHT_RATIO;
            float topY = (center.Y + (fullH / 2f)) - crouchH;
            return new Rectangle(center.X - (fullW / 2f), topY, fullW, crouchH);
        }

        return new Rectangle(
            center.X - (fullW / 2f),
            center.Y - (fullH / 2f),
            fullW,
            fullH
        );
    }

    public bool TryGetRenderHitbox(float alpha, out Rectangle hitbox) =>
        TryComputeHitboxAt(GetInterpolatedPosition(alpha), out hitbox);

    public bool TryGetHitbox(out Rectangle hitbox) =>
        TryComputeHitboxAt(Position, out hitbox);

    private bool TryComputeHitboxAt(Vector2 center, out Rectangle hitbox)
    {
        hitbox = default;
        if (CurrentCharacter == null || !CurrentCharacter.IsHitboxActive) return false;

        SkillData skill = CurrentCharacter.ActiveSkill!;
        Rectangle pct = skill.HitboxOffsetPct;

        float hbWidth = CurrentCharacter.Width * pct.width;
        float hbHeight = CurrentCharacter.Height * pct.height;
        float offsetX = CurrentCharacter.Width * pct.x;
        float offsetY = CurrentCharacter.Height * pct.y;

        float rootX = center.X - (CurrentCharacter.Width / 2f);
        float rootY = center.Y - (CurrentCharacter.Height / 2f);

        float hitX = FacingRight
            ? rootX + offsetX
            : rootX + CurrentCharacter.Width - offsetX - hbWidth;
        float hitY = rootY + offsetY;

        hitbox = new Rectangle(hitX, hitY, hbWidth, hbHeight);
        return true;
    }

    public void PollInput()
    {
        if ((!GameState.IsLiveArena && !PhysicsEngine.IsHeadlessThread) || CurrentCharacter == null) return;
        if (!_isAI) _keyHandler.PollHardwareInput(_playerIndex);
    }

    public void LatchOnlineHardwareEdges(int bindingsIndex = 0) => _keyHandler.LatchOnlineHardwareEdges(bindingsIndex);
    public ushort SampleHardwareInputBitmask(int bindingsIndex = 0) => _keyHandler.SampleHardwareInputBitmask(bindingsIndex);
    public void ApplyNetworkInputBitmask(ushort bitmask, ushort prevBitmask) => _keyHandler.ApplyNetworkInputBitmask(bitmask, prevBitmask, _playerIndex);
    public KeyHandler.Snapshot CaptureKeyHandlerSnapshot() => _keyHandler.CaptureSnapshot(_playerIndex);
    public void RestoreKeyHandlerSnapshot(in KeyHandler.Snapshot snapshot) => _keyHandler.RestoreSnapshot(_playerIndex, snapshot);

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

        Velocity.X = escapeDir * CurrentCharacter.WalkSpeed * AIR_TECH_VX_MULT;
        Velocity.Y = CurrentCharacter.JumpForce * AIR_TECH_VY_MULT;
    }

    public void ConsumeInput()
    {
        JustExecutedAction = GameAction.None;
        JustExecutedMotion = SpecialMotion.None;
        JustCompletedComboSignature = null;

        if ((!GameState.IsLiveArena && !PhysicsEngine.IsHeadlessThread) || CurrentCharacter == null) return;

        if (_isAI && IsPassiveDummy)
        {
            IsInputIdle = true;
            IsCrouching = DummyBlock == DummyBlockMode.Crouch;
            IsHoldingBack = DummyBlock != DummyBlockMode.Off;
            return;
        }

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

    public void TranslatePct(float dxPct, float dyPct)
    {
        Position.X += dxPct * PhysicsEngine.ViewportWidth;
        Position.Y += dyPct * PhysicsEngine.ViewportHeight;
    }

    public void NotifyRoundEnd(bool wonRound)
    {
        _aiController?.OnRoundEnd(wonRound);
    }

    public void SetVelocity(float vx, float vy)
    {
        Velocity.X = vx;
        Velocity.Y = vy;
    }

    public void SetVelocityPct(float vxPct, float vyPct)
    {
        Velocity.X = vxPct * PhysicsEngine.ViewportWidth;
        Velocity.Y = vyPct * PhysicsEngine.ViewportHeight;
    }

    public void RequestPlatformDrop()
    {
        if (IsGrounded && IsOnPlatform)
        {
            PlatformDropTimer = PLATFORM_DROP_DURATION;
            IsGrounded = false;
            IsOnPlatform = false;
            Position.Y += 4f;
        }
    }

    public void Dispose()
    {
        _aiController?.Dispose();
    }

    public void Update(float dt)
    {
        if (!GameState.IsLiveArena && !PhysicsEngine.IsHeadlessThread) return;

        if (HitstunTimer > 0f)
        {
            HitstunTimer = MathF.Max(0f, HitstunTimer - dt);
            TimeSinceHitstun = 0f;
        }
        else
        {
            TimeSinceHitstun += dt;
        }

        if (InvulnerableTimer > 0f) InvulnerableTimer = MathF.Max(0f, InvulnerableTimer - dt);
        if (TechPopupTimer > 0f) TechPopupTimer = MathF.Max(0f, TechPopupTimer - dt);
        if (PlatformDropTimer > 0f) PlatformDropTimer = MathF.Max(0f, PlatformDropTimer - dt);
        if (TagCooldownTimer > 0f) TagCooldownTimer = MathF.Max(0f, TagCooldownTimer - dt);
        if (AssistCooldownTimer > 0f) AssistCooldownTimer = MathF.Max(0f, AssistCooldownTimer - dt);

        if (IsTagTeam && !IsAssistActive)
        {
            TagTeamMember bench = PartnerMember;
            if (bench.IsAlive && bench.RecoverableHealth > 0f && bench.Health < bench.MaxHealth)
            {
                float healFloat = MathF.Min(bench.RecoverableHealth, BENCH_REGEN_PER_SEC * dt);
                _benchRegenAccum += healFloat;
                int wholeHeal = (int)_benchRegenAccum;
                if (wholeHeal > 0)
                {
                    int actualHeal = Math.Min(wholeHeal, bench.MaxHealth - bench.Health);
                    bench.Health += actualHeal;
                    bench.RecoverableHealth = MathF.Max(0f, bench.RecoverableHealth - actualHeal);
                    _benchRegenAccum -= wholeHeal;
                }
            }
        }

        if (CurrentCharacter == null) return;
        CurrentCharacter.Update(this, dt);

        if (IsAssistActive)
        {
            UpdateAssist(dt);
        }
    }

    private void UpdateAssist(float dt)
    {
        Character? partner = ActivePartnerCharacter;
        SkillData? skill = ActiveAssistSkill;
        if (partner == null || skill == null)
        {
            EndAssist(spawnVfx: false);
            return;
        }

        if (skill.CustomLogic && skill.OnCustomLogic?.Function != null)
        {
            Vector2 savedPos = Position;
            Vector2 savedPrev = PreviousPosition;
            Vector2 savedVel = Velocity;
            bool savedFacing = FacingRight;
            int savedMemberIdx = ActiveMemberIndex;
            try
            {
                ActiveMemberIndex = savedMemberIdx == 0 ? 1 : 0;
                Position = AssistPosition;
                PreviousPosition = AssistPreviousPosition;
                Velocity = Vector2.Zero;
                FacingRight = AssistFacingRight;
                LuaEngine.CallSandboxed(skill.OnCustomLogic, LuaEngine.MaxTickInstructions, this, partner, dt);
                AssistPosition = Position + (Velocity * dt);
            }
            catch (ScriptRuntimeException ex)
            {
                DiagnosticsLogger.LogSilentIssueOnce($"lua_assist_err_{partner.CharacterId}_{skill.Name}", $"Assist CustomLogic error: {ex.DecoratedMessage}", LogSeverity.Error);
            }
            finally
            {
                ActiveMemberIndex = savedMemberIdx;
                Position = savedPos;
                PreviousPosition = savedPrev;
                Velocity = savedVel;
                FacingRight = savedFacing;
            }
        }

        if (!PhysicsEngine.IsHeadlessThread)
        {
            partner.Animator.SetState(AnimationState.Attacking, skill.AnimationClipName);
            partner.Animator.Update(dt, partner.Animations);
        }

        AssistActionFrame++;
        int totalFrames = Math.Max(1, skill.StartupFrames + skill.ActiveFrames + skill.RecoveryFrames);
        if (AssistActionFrame >= totalFrames)
        {
            EndAssist(spawnVfx: true);
        }
    }

    public void Draw(float alpha)
    {
        if (IsAssistActive && ActivePartnerCharacter != null)
        {
            DrawAssistPartner(alpha);
        }

        if (CurrentCharacter == null) return;
        CurrentCharacter.Draw(this, alpha);

        if (TechPopupTimer > 0f)
        {
            Vector2 renderPos = GetInterpolatedPosition(alpha);
            int fontSize = Math.Max(14, (int)(PhysicsEngine.LogicalHeight * 0.025f));
            string techText = "TECH!";
            int tw = Raylib.MeasureText(techText, fontSize);
            int tx = (int)renderPos.X - (tw / 2);
            int ty = (int)(renderPos.Y - (CurrentCharacter.Height * 0.65f));
            Raylib.DrawText(techText, tx + 2, ty + 2, fontSize, Color.BLACK);
            Raylib.DrawText(techText, tx, ty, fontSize, Color.SKYBLUE);
        }

        if (_isAI && _aiController != null && Character.ShowDebugBoxes && CurrentCharacter != null)
        {
            Vector2 renderPos = GetInterpolatedPosition(alpha);
            string aiText = $"AI: {_aiController.CurrentMacroTactic}";
            int fontSize = Math.Max(12, (int)(PhysicsEngine.LogicalHeight * 0.018f));
            int tw = Raylib.MeasureText(aiText, fontSize);
            Raylib.DrawText(aiText, (int)renderPos.X - (tw / 2), (int)(renderPos.Y - (CurrentCharacter.Height * 0.85f)), fontSize, Color.DARKGREEN);
        }
    }

    private void DrawAssistPartner(float alpha)
    {
        Character? partner = ActivePartnerCharacter;
        if (partner == null) return;

        Vector2 savedPos = Position;
        Vector2 savedPrev = PreviousPosition;
        bool savedFacing = FacingRight;
        bool savedCrouching = IsCrouching;
        int savedMemberIdx = ActiveMemberIndex;
        try
        {
            ActiveMemberIndex = savedMemberIdx == 0 ? 1 : 0;
            Position = AssistPosition;
            PreviousPosition = AssistPreviousPosition;
            FacingRight = AssistFacingRight;
            IsCrouching = false;
            partner.Draw(this, alpha);
        }
        finally
        {
            ActiveMemberIndex = savedMemberIdx;
            Position = savedPos;
            PreviousPosition = savedPrev;
            FacingRight = savedFacing;
            IsCrouching = savedCrouching;
        }
    }
}