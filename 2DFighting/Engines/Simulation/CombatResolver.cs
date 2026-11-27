using System;
using MoonSharp.Interpreter;
using Raylib_cs;

public class CombatResolver
{
    public const int HeavyHitstopDamageThreshold = 35;
    public const float HeavyHitstopDuration = 0.09f;
    public const float LightHitstopDuration = 0.045f;
    public const float DefaultCounterKnockbackPct = 0.21f;
    public const float DefaultBlockKnockbackPct = 0.23f;
    public const float DefaultCleanKnockbackPct = 0.18f;
    public const float BlockChipDamageMultiplier = 0.10f;
    private const float ComboScalingPerHit = 0.12f;
    private const float MinComboScaling = 0.40f;
    public const float BlockStunMultiplier = 0.40f;

    private readonly MatchManager _matchManager;

    public CombatResolver(MatchManager matchManager)
    {
        _matchManager = matchManager;
    }

    public static int CalculateScaledDamage(Player source, int baseDamage)
    {
        if (baseDamage <= 0) return 0;
        float scale = MathF.Max(MinComboScaling, 1.0f - (source.ComboCount * ComboScalingPerHit));
        return Math.Max(1, (int)MathF.Round(baseDamage * scale));
    }

    public static bool CanBlockHeight(Player defender, AttackHeight height, bool isBlockBreak)
    {
        if (!defender.IsNeutralBlocking || isBlockBreak)
            return false;

        return height switch
        {
            AttackHeight.Low      => defender.IsCrouching,
            AttackHeight.Overhead => !defender.IsCrouching,
            _                     => true
        };
    }

    public static int CalculateNonLethalChip(int rawDamage, int defenderHealth)
    {
        int rawChip = (int)(rawDamage * BlockChipDamageMultiplier);
        // Chip damage cannot K.O. a player (leaves them at 1 HP minimum)
        return Math.Max(0, Math.Min(rawChip, defenderHealth - 1));
    }

    public static void ApplyHitForce(Player source, Player target, int damage, float stun, float groundKnockbackPct) =>
        ApplyHitForce(source, target, damage, stun, groundKnockbackPct, isBlocked: false);

    public static void ApplyHitForce(Player source, Player target, int damage, float stun, float groundKnockbackPct, bool isBlocked)
    {
        int scaledDamage = CalculateScaledDamage(source, damage);
        target.Health = Math.Max(0, target.Health - scaledDamage);
        if (scaledDamage > 0 && (GameState.CurrentState == State.Playing || PhysicsEngine.IsHeadlessThread))
        {
            PhysicsEngine.ActiveTelemetry?.RecordDamageDealt(source, scaledDamage);
        }

        Character? targetChar = target.CurrentCharacter;

        if (targetChar != null && targetChar.IsHyperArmorActive)
            return;

        if (targetChar != null)
        {
            targetChar.ActiveSkill = null;
            targetChar.HasHit = false;
        }

        target.HitstunTimer = stun;
        if (target.IsGrounded)
        {
            target.IsAirStalled = false;
            float dir = source.FacingRight ? 1f : -1f;
            float speedPx = groundKnockbackPct * PhysicsEngine.ViewportWidth;
            target.SetVelocity(dir * speedPx, 0f);
        }
        else
        {
            target.IsAirStalled = true;
            target.SetVelocity(0f, 0f);
        }

        if (!isBlocked && scaledDamage > 0 && stun > 0f)
        {
            source.ComboCount++;
            if (GameState.CurrentState == State.Playing || PhysicsEngine.IsHeadlessThread)
            {
                PhysicsEngine.ActiveTelemetry?.RecordCleanHit(source, source.LastExecutedSkillName, source.ComboCount);
            }
        }
    }

    public static void ApplyLaunchForce(Player source, Player target, int damage, float stun, float launchXPct, float launchYPct)
    {
        int scaledDamage = CalculateScaledDamage(source, damage);
        target.Health = Math.Max(0, target.Health - scaledDamage);
        if (scaledDamage > 0 && (GameState.CurrentState == State.Playing || PhysicsEngine.IsHeadlessThread))
        {
            PhysicsEngine.ActiveTelemetry?.RecordDamageDealt(source, scaledDamage);
        }

        Character? targetChar = target.CurrentCharacter;

        if (targetChar != null && targetChar.IsHyperArmorActive)
            return;

        if (targetChar != null)
        {
            targetChar.ActiveSkill = null;
            targetChar.HasHit = false;
        }

        target.HitstunTimer = stun;
        target.IsGrounded = false;
        target.IsAirStalled = false;

        float dir = source.FacingRight ? 1f : -1f;
        float vx = dir * launchXPct * PhysicsEngine.ViewportWidth;
        float vy = launchYPct * PhysicsEngine.ViewportHeight;
        target.SetVelocity(vx, vy);

        if (scaledDamage > 0 && stun > 0f)
        {
            source.ComboCount++;
            if (GameState.CurrentState == State.Playing || PhysicsEngine.IsHeadlessThread)
            {
                PhysicsEngine.ActiveTelemetry?.RecordCleanHit(source, source.LastExecutedSkillName, source.ComboCount);
            }
        }
    }

    public void ResolveSimultaneousHits(GameData gamedata, Player p1, Player p2)
    {
        bool p1HitsP2 = CheckHitCollision(p1, p2);
        bool p2HitsP1 = CheckHitCollision(p2, p1);

        if (p1HitsP2 && p2HitsP1)
        {
            SkillData s1 = p1.CurrentCharacter!.ActiveSkill!;
            SkillData s2 = p2.CurrentCharacter!.ActiveSkill!;

            if (s1.IsCounter && !s2.IsCounter)
            {
                ResolveHitInteraction(gamedata, p2, p1, s2);
                return;
            }
            if (s2.IsCounter && !s1.IsCounter)
            {
                ResolveHitInteraction(gamedata, p1, p2, s1);
                return;
            }

            ResolveHitInteraction(gamedata, p1, p2, s1);
            ResolveHitInteraction(gamedata, p2, p1, s2);
        }
        else if (p1HitsP2)
        {
            ResolveHitInteraction(gamedata, p1, p2, p1.CurrentCharacter!.ActiveSkill!);
        }
        else if (p2HitsP1)
        {
            ResolveHitInteraction(gamedata, p2, p1, p2.CurrentCharacter!.ActiveSkill!);
        }

        ResolveAssistHit(gamedata, p1, p2);
        ResolveAssistHit(gamedata, p2, p1);
    }

    private void ResolveAssistHit(GameData gamedata, Player attacker, Player defender)
    {
        if (!attacker.IsAssistActive || attacker.AssistHasHit || defender.IsInvulnerable)
            return;

        SkillData? assistSkill = attacker.ActiveAssistSkill;
        if (assistSkill == null || !attacker.TryGetAssistHitbox(out Rectangle assistHb))
            return;

        if (!Raylib.CheckCollisionRecs(assistHb, defender.GetHurtbox()))
            return;

        attacker.AssistHasHit = true;

        if (!PhysicsEngine.IsHeadlessThread)
        {
            _matchManager.AddHitstop(LightHitstopDuration);
        }

        bool savedFacing = attacker.FacingRight;
        try
        {
            attacker.FacingRight = attacker.AssistFacingRight;

            if (CanBlockHeight(defender, assistSkill.Height, assistSkill.IsBlockBreak))
            {
                ApplyBlockedHit(gamedata, attacker, defender, assistSkill);
                return;
            }

            if (assistSkill.OnHit?.Function != null)
            {
                try
                {
                    LuaEngine.CallSandboxed(assistSkill.OnHit, LuaEngine.MaxTickInstructions, attacker, defender, assistSkill);
                }
                catch (ScriptRuntimeException ex)
                {
                    DiagnosticsLogger.LogSilentIssueOnce($"lua_assist_onhit_{attacker.ActivePartnerCharacter?.CharacterId}_{assistSkill.Name}", $"Assist OnHit error: {ex.DecoratedMessage}", LogSeverity.Error);
                }
                gamedata.SoundHandler?.PlaySound(defender, SoundType.CustomOnHit);
                return;
            }

            ApplyCleanHit(gamedata, attacker, defender, assistSkill);
        }
        finally
        {
            attacker.FacingRight = savedFacing;
        }
    }

    private bool CheckHitCollision(Player attacker, Player defender)
    {
        if (attacker.CurrentCharacter == null || attacker.CurrentCharacter.HasHit) return false;
        if (defender.IsInvulnerable) return false;
        if (!attacker.TryGetHitbox(out Rectangle hitbox)) return false;

        Rectangle defHurtbox = defender.GetHurtbox();
        return Raylib.CheckCollisionRecs(hitbox, defHurtbox);
    }

    private void ResolveHitInteraction(GameData gamedata, Player attacker, Player defender, SkillData attackSkill)
    {
        if (attacker.CurrentCharacter != null)
        {
            attacker.CurrentCharacter.HasHit = true;
        }

        float hitstop = attackSkill.Damage >= HeavyHitstopDamageThreshold
            ? HeavyHitstopDuration
            : LightHitstopDuration;
        if (!PhysicsEngine.IsHeadlessThread)
        {
            _matchManager.AddHitstop(hitstop);
        }

        if (TryResolveCounter(gamedata, attacker, defender))
            return;

        if (CanBlockHeight(defender, attackSkill.Height, attackSkill.IsBlockBreak))
        {
            ApplyBlockedHit(gamedata, attacker, defender, attackSkill);
            return;
        }

        if (defender.IsNeutralBlocking && attackSkill.IsBlockBreak)
        {
            if (attackSkill.OnBlockBreak?.Function != null)
            {
                try
                {
                    LuaEngine.CallSandboxed(attackSkill.OnBlockBreak, LuaEngine.MaxTickInstructions, attacker, defender);
                }
                catch (ScriptRuntimeException ex)
                {
                    DiagnosticsLogger.LogSilentIssueOnce($"lua_blockbreak_{attacker.CurrentCharacter?.CharacterId}_{attackSkill.Name}", $"OnBlockBreak error: {ex.DecoratedMessage}", LogSeverity.Error);
                }
            }
            else
            {
                ApplyCleanHit(gamedata, attacker, defender, attackSkill);
            }
            VFXManager.SpawnPreset(VFXPreset.BlockBreak, defender.Position, defender.FacingRight);
            gamedata.SoundHandler?.PlaySound(defender, SoundType.BlockBreak);
            return;
        }

        ApplyCleanHit(gamedata, attacker, defender, attackSkill);
    }

    private bool TryResolveCounter(GameData gamedata, Player attacker, Player defender)
    {
        Character? defChar = defender.CurrentCharacter;
        if (defChar?.ActiveSkill == null || !defChar.ActiveSkill.IsCounter || !defChar.IsHitboxActive)
            return false;

        SkillData counterSkill = defChar.ActiveSkill;
        if (attacker.CurrentCharacter != null)
        {
            attacker.CurrentCharacter.ActiveSkill = null;
        }
        defender.SetVelocity(0f, 0f);

        if (counterSkill.OnCounter?.Function != null)
        {
            try
            {
                LuaEngine.CallSandboxed(counterSkill.OnCounter, LuaEngine.MaxTickInstructions, defender, attacker);
            }
            catch (ScriptRuntimeException ex)
            {
                DiagnosticsLogger.LogSilentIssueOnce($"lua_counter_{defChar.CharacterId}_{counterSkill.Name}", $"OnCounter error: {ex.DecoratedMessage}", LogSeverity.Error);
            }
        }
        else
        {
            ApplyHitForce(defender, attacker, counterSkill.Damage, counterSkill.HitStun, DefaultCounterKnockbackPct);
        }
        gamedata.SoundHandler?.PlaySound(defender, SoundType.OnCounter);
        VFXManager.SpawnPreset(VFXPreset.CounterFlash, defender.Position, defender.FacingRight);
        return true;
    }

    private void ApplyBlockedHit(GameData gamedata, Player attacker, Player defender, SkillData skill)
    {
        int nonLethalChip = CalculateNonLethalChip(skill.Damage, defender.Health);
        float blockStun = skill.HitStun * BlockStunMultiplier;

        int prevCombo = attacker.ComboCount;
        ApplyHitForce(attacker, defender, nonLethalChip, blockStun, DefaultBlockKnockbackPct, isBlocked: true);
        gamedata.SoundHandler?.PlaySound(defender, SoundType.Block);
        attacker.ComboCount = prevCombo; // Blocked hits shouldn't inflate combo counter
        VFXManager.SpawnPreset(VFXPreset.BlockSpark, defender.Position, defender.FacingRight);
    }

    private void ApplyCleanHit(GameData gamedata, Player attacker, Player defender, SkillData skill)
    {
        if (skill.OnHit?.Function != null)
        {
            try
            {
                LuaEngine.CallSandboxed(skill.OnHit, LuaEngine.MaxTickInstructions, attacker, defender, skill);
            }
            catch (ScriptRuntimeException ex)
            {
                DiagnosticsLogger.LogSilentIssueOnce($"lua_onhit_{attacker.CurrentCharacter?.CharacterId}_{skill.Name}", $"OnHit error: {ex.DecoratedMessage}", LogSeverity.Error);
            }
            gamedata.SoundHandler?.PlaySound(defender, SoundType.CustomOnHit);
            return;
        }
        VFXPreset spark = skill.Damage >= HeavyHitstopDamageThreshold ? VFXPreset.HeavyHitSpark : VFXPreset.HitSpark;
        VFXManager.SpawnPreset(spark, defender.Position, attacker.FacingRight);
        gamedata.SoundHandler?.PlaySound(defender, SoundType.Hurt);
        ApplyHitForce(attacker, defender, skill.Damage, skill.HitStun, DefaultCleanKnockbackPct);
    }
}
