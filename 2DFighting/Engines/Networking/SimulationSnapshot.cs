using System;
using System.Collections.Generic;
using System.Numerics;

public sealed class SimulationSnapshot
{
    public sealed class PlayerSnapshot
    {
        public Vector2 Position;
        public Vector2 PreviousPosition;
        public Vector2 Velocity;
        public int Health;
        public int RoundsWon;
        public int ComboCount;
        public float HitstunTimer;
        public float TimeSinceHitstun;
        public float InvulnerableTimer;
        public float TechPopupTimer;
        public float PlatformDropTimer;
        public bool IsGrounded;
        public bool IsOnPlatform;
        public bool FacingRight;
        public bool IsInputIdle;
        public bool IsCrouching;
        public bool IsAirStalled;
        public bool HasUsedAirStall;
        public bool IsHoldingBack;
        public bool IsHit;
        public GameAction JustExecutedAction;
        public SpecialMotion JustExecutedMotion;
        public string? JustCompletedComboSignature;
        public string? LastExecutedSkillName;

        public KeyHandler.Snapshot KeyState;
        public SkillController.ControllerSnapshot SkillState;
        public readonly Dictionary<string, float> CustomDataValues = new(StringComparer.OrdinalIgnoreCase);

        public float WidthPct;
        public float HeightPct;
        public float WalkSpeedPct;
        public float JumpForcePct;
        public float GravityPct;
        public float Friction;
        public float HitStun;

        // 2v2 Tag Team & Assist snapshot
        public int ActiveMemberIndex;
        public int Member0Health;
        public int Member1Health;
        public float Member0RecoverableHealth;
        public float Member1RecoverableHealth;
        public float BenchRegenAccum;
        public float TagCooldownTimer;
        public float AssistCooldownTimer;
        public bool IsAssistActive;
        public Vector2 AssistPosition;
        public Vector2 AssistPreviousPosition;
        public bool AssistFacingRight;
        public int AssistActionFrame;
        public bool AssistHasHit;
        public string? AssistSkillName;

        public SkillController.ControllerSnapshot PartnerSkillState;
        public readonly Dictionary<string, float> PartnerCustomDataValues = new(StringComparer.OrdinalIgnoreCase);
        public float PartnerWidthPct;
        public float PartnerHeightPct;
        public float PartnerWalkSpeedPct;
        public float PartnerJumpForcePct;
        public float PartnerGravityPct;
        public float PartnerFriction;
        public float PartnerHitStun;
    }

    public int Tick;
    public int CurrentRound;
    public float RoundTimer;
    public float RoundTransitionTimer;
    public float HitstopTimer;
    public string RoundAnnouncement = "";
    public string MatchWinnerText = "";
    public uint RngState;

    public readonly PlayerSnapshot P1 = new();
    public readonly PlayerSnapshot P2 = new();
    public List<Projectile> Projectiles = new();
    public MatchTelemetryTracker.TrackerSnapshot Telemetry = new();
    public ushort PrevP1Bitmask;
    public ushort PrevP2Bitmask;

    public void Capture(
        int tick,
        Player p1,
        Player p2,
        MatchManager match,
        ProjectileManager projectiles,
        ushort prevP1Bitmask,
        ushort prevP2Bitmask)
    {
        Tick = tick;
        CurrentRound = match.CurrentRound;
        RoundTimer = match.RoundTimer;
        RoundTransitionTimer = match.RoundTransitionTimer;
        HitstopTimer = match.HitstopTimer;
        RoundAnnouncement = match.RoundAnnouncement;
        MatchWinnerText = match.MatchWinnerText;
        RngState = match.Rng.State;

        CapturePlayer(p1, P1);
        CapturePlayer(p2, P2);
        Projectiles = projectiles.CaptureSnapshot();
        Telemetry = match.Telemetry.CaptureSnapshot();
        PrevP1Bitmask = prevP1Bitmask;
        PrevP2Bitmask = prevP2Bitmask;
    }

    public void Restore(Player p1, Player p2, MatchManager match, ProjectileManager projectiles)
    {
        match.RestoreStateSnapshot(
            CurrentRound,
            RoundTimer,
            RoundTransitionTimer,
            HitstopTimer,
            RoundAnnouncement,
            MatchWinnerText,
            RngState);

        RestorePlayer(p1, P1);
        RestorePlayer(p2, P2);
        projectiles.RestoreSnapshot(Projectiles);
        match.Telemetry.RestoreSnapshot(Telemetry);
    }

    private static void CapturePlayer(Player player, PlayerSnapshot dst)
    {
        dst.Position = player.Position;
        dst.PreviousPosition = player.PreviousPosition;
        dst.Velocity = player.Velocity;
        dst.Health = player.Health;
        dst.RoundsWon = player.RoundsWon;
        dst.ComboCount = player.ComboCount;
        dst.HitstunTimer = player.HitstunTimer;
        dst.TimeSinceHitstun = player.TimeSinceHitstun;
        dst.InvulnerableTimer = player.InvulnerableTimer;
        dst.TechPopupTimer = player.TechPopupTimer;
        dst.PlatformDropTimer = player.PlatformDropTimer;
        dst.IsGrounded = player.IsGrounded;
        dst.IsOnPlatform = player.IsOnPlatform;
        dst.FacingRight = player.FacingRight;
        dst.IsInputIdle = player.IsInputIdle;
        dst.IsCrouching = player.IsCrouching;
        dst.IsAirStalled = player.IsAirStalled;
        dst.HasUsedAirStall = player.HasUsedAirStall;
        dst.IsHoldingBack = player.IsHoldingBack;
        dst.IsHit = player.IsHit;
        dst.JustExecutedAction = player.JustExecutedAction;
        dst.JustExecutedMotion = player.JustExecutedMotion;
        dst.JustCompletedComboSignature = player.JustCompletedComboSignature;
        dst.LastExecutedSkillName = player.LastExecutedSkillName;
        dst.KeyState = player.CaptureKeyHandlerSnapshot();

        TagTeamMember m0 = player.GetMember(0);
        TagTeamMember m1 = player.GetMember(1);
        dst.ActiveMemberIndex = player.ActiveMemberIndex;
        dst.Member0Health = m0.Health;
        dst.Member1Health = m1.Health;
        dst.Member0RecoverableHealth = m0.RecoverableHealth;
        dst.Member1RecoverableHealth = m1.RecoverableHealth;
        dst.BenchRegenAccum = player.BenchRegenAccum;
        dst.TagCooldownTimer = player.TagCooldownTimer;
        dst.AssistCooldownTimer = player.AssistCooldownTimer;
        dst.IsAssistActive = player.IsAssistActive;
        dst.AssistPosition = player.AssistPosition;
        dst.AssistPreviousPosition = player.AssistPreviousPosition;
        dst.AssistFacingRight = player.AssistFacingRight;
        dst.AssistActionFrame = player.AssistActionFrame;
        dst.AssistHasHit = player.AssistHasHit;
        dst.AssistSkillName = player.AssistSkillName;

        Character? ch = player.PrimaryCharacter;
        if (ch != null)
        {
            dst.SkillState = ch.SkillController.CaptureSnapshot();
            ch.CustomData.CopyCustomValuesTo(dst.CustomDataValues);
            dst.WidthPct = ch.WidthPct;
            dst.HeightPct = ch.HeightPct;
            dst.WalkSpeedPct = ch.WalkSpeedPct;
            dst.JumpForcePct = ch.JumpForcePct;
            dst.GravityPct = ch.GravityPct;
            dst.Friction = ch.Friction;
            dst.HitStun = ch.HitStun;
        }

        Character? partner = player.PartnerCharacter;
        if (partner != null)
        {
            dst.PartnerSkillState = partner.SkillController.CaptureSnapshot();
            partner.CustomData.CopyCustomValuesTo(dst.PartnerCustomDataValues);
            dst.PartnerWidthPct = partner.WidthPct;
            dst.PartnerHeightPct = partner.HeightPct;
            dst.PartnerWalkSpeedPct = partner.WalkSpeedPct;
            dst.PartnerJumpForcePct = partner.JumpForcePct;
            dst.PartnerGravityPct = partner.GravityPct;
            dst.PartnerFriction = partner.Friction;
            dst.PartnerHitStun = partner.HitStun;
        }
    }

    private static void RestorePlayer(Player player, PlayerSnapshot src)
    {
        player.RestoreTeamSnapshot(
            src.ActiveMemberIndex,
            src.Member0Health,
            src.Member1Health,
            src.Member0RecoverableHealth,
            src.Member1RecoverableHealth,
            src.BenchRegenAccum,
            src.TagCooldownTimer,
            src.AssistCooldownTimer,
            src.IsAssistActive,
            src.AssistPosition,
            src.AssistPreviousPosition,
            src.AssistFacingRight,
            src.AssistActionFrame,
            src.AssistHasHit,
            src.AssistSkillName);

        player.Position = src.Position;
        player.PreviousPosition = src.PreviousPosition;
        player.Velocity = src.Velocity;
        player.Health = src.Health;
        player.RoundsWon = src.RoundsWon;
        player.ComboCount = src.ComboCount;
        player.HitstunTimer = src.HitstunTimer;
        player.TimeSinceHitstun = src.TimeSinceHitstun;
        player.InvulnerableTimer = src.InvulnerableTimer;
        player.TechPopupTimer = src.TechPopupTimer;
        player.PlatformDropTimer = src.PlatformDropTimer;
        player.IsGrounded = src.IsGrounded;
        player.IsOnPlatform = src.IsOnPlatform;
        player.FacingRight = src.FacingRight;
        player.IsCrouching = src.IsCrouching;
        player.IsAirStalled = src.IsAirStalled;
        player.HasUsedAirStall = src.HasUsedAirStall;
        player.IsHoldingBack = src.IsHoldingBack;
        player.IsHit = src.IsHit;
        player.JustExecutedAction = src.JustExecutedAction;
        player.JustExecutedMotion = src.JustExecutedMotion;
        player.JustCompletedComboSignature = src.JustCompletedComboSignature;
        player.LastExecutedSkillName = src.LastExecutedSkillName;
        player.RestoreKeyHandlerSnapshot(src.KeyState);

        Character? ch = player.PrimaryCharacter;
        if (ch != null)
        {
            ch.SkillController.RestoreSnapshot(src.SkillState);
            ch.CustomData.RestoreCustomValues(src.CustomDataValues);
            ch.WidthPct = src.WidthPct;
            ch.HeightPct = src.HeightPct;
            ch.WalkSpeedPct = src.WalkSpeedPct;
            ch.JumpForcePct = src.JumpForcePct;
            ch.GravityPct = src.GravityPct;
            ch.Friction = src.Friction;
            ch.HitStun = src.HitStun;
        }

        Character? partner = player.PartnerCharacter;
        if (partner != null)
        {
            partner.SkillController.RestoreSnapshot(src.PartnerSkillState);
            partner.CustomData.RestoreCustomValues(src.PartnerCustomDataValues);
            partner.WidthPct = src.PartnerWidthPct;
            partner.HeightPct = src.PartnerHeightPct;
            partner.WalkSpeedPct = src.PartnerWalkSpeedPct;
            partner.JumpForcePct = src.PartnerJumpForcePct;
            partner.GravityPct = src.PartnerGravityPct;
            partner.Friction = src.PartnerFriction;
            partner.HitStun = src.PartnerHitStun;
        }
    }

    public static uint ComputeStateChecksum(Player p1, Player p2, MatchManager match, ProjectileManager projectiles)
    {
        const uint fnvOffset = 2166136261u;
        const uint fnvPrime = 16777619u;
        uint hash = fnvOffset;

        HashPlayer(p1, ref hash, fnvPrime);
        HashPlayer(p2, ref hash, fnvPrime);

        HashUInt((uint)match.CurrentRound, ref hash, fnvPrime);
        HashUInt((uint)match.Rng.State, ref hash, fnvPrime);
        HashUInt((uint)projectiles.ActiveProjectiles.Count, ref hash, fnvPrime);
        return hash;
    }

    private static void HashPlayer(Player p, ref uint hash, uint prime)
    {
        HashUInt(BitConverter.SingleToUInt32Bits(p.Position.X), ref hash, prime);
        HashUInt(BitConverter.SingleToUInt32Bits(p.Position.Y), ref hash, prime);
        HashUInt(BitConverter.SingleToUInt32Bits(p.Velocity.X), ref hash, prime);
        HashUInt(BitConverter.SingleToUInt32Bits(p.Velocity.Y), ref hash, prime);
        HashUInt((uint)p.ActiveMemberIndex, ref hash, prime);
        HashUInt((uint)p.GetMember(0).Health, ref hash, prime);
        HashUInt((uint)p.GetMember(1).Health, ref hash, prime);
        HashUInt((uint)p.RoundsWon, ref hash, prime);
        HashUInt((uint)(p.CurrentCharacter?.CurrentActionFrame ?? 0), ref hash, prime);
        HashUInt(p.FacingRight ? 1u : 0u, ref hash, prime);
        HashUInt(BitConverter.SingleToUInt32Bits(p.TagCooldownTimer), ref hash, prime);
        HashUInt(BitConverter.SingleToUInt32Bits(p.AssistCooldownTimer), ref hash, prime);
        HashUInt(p.IsAssistActive ? 1u : 0u, ref hash, prime);
        HashUInt((uint)p.AssistActionFrame, ref hash, prime);
    }

    private static void HashUInt(uint value, ref uint hash, uint prime)
    {
        hash ^= value;
        hash *= prime;
    }
}
