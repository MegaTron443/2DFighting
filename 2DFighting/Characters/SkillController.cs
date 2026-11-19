using System;
using System.Collections.Generic;

public class SkillController
{
    private const int FastPokeMaxStartupFrames = 5;

    public Dictionary<string, SkillData> Skills { get; } = new();
    public LuaFloatStore SkillCooldowns { get; } = new();
    public SkillData? ActiveSkill { get; set; }
    public int CurrentActionFrame { get; private set; } = 0;
    public bool HasHit { get; set; } = false;

    public bool IsHitboxActive
    {
        get
        {
            if (ActiveSkill == null) return false;
            int start = ActiveSkill.StartupFrames;
            int end = start + ActiveSkill.ActiveFrames;
            return CurrentActionFrame >= start && CurrentActionFrame < end;
        }
    }

    public bool IsHyperArmorActive
    {
        get
        {
            if (ActiveSkill == null || !ActiveSkill.IsHyperFrames) return false;
            int start = ActiveSkill.HyperStartFrame;
            int end = start + ActiveSkill.HyperFrames;
            return CurrentActionFrame >= start && CurrentActionFrame < end;
        }
    }

    public void ExecuteSkill(Player p, string skillName)
    {
        if (SkillCooldowns.TryGetValue(skillName, out float cd) && cd > 0f) return;
        if (ActiveSkill != null) return;
        if (p.IsCrouching) return;

        if (Skills.TryGetValue(skillName, out var skill))
        {
            ActiveSkill = skill;
            CurrentActionFrame = 0;
            HasHit = false;
            SkillCooldowns[skillName] = skill.CooldownTime;
            p.Velocity.X = 0;
            p.Velocity.Y = 0;

            if (skill.IsCounter)
                p.JustExecutedAction = GameAction.Counter;
            else if (skill.IsBlockBreak)
                p.JustExecutedAction = GameAction.GuardBreak;
            else if (skill.Height == AttackHeight.Low)
                p.JustExecutedAction = GameAction.LowAttack;
            else if (skill.Height == AttackHeight.Overhead)
                p.JustExecutedAction = GameAction.OverheadAttack;
            else if (skill.StartupFrames <= FastPokeMaxStartupFrames)
                p.JustExecutedAction = GameAction.FastPoke;
            else
                p.JustExecutedAction = GameAction.HeavyAttack;
        }
    }

    public bool HandleHitstunInterrupt(Player p)
    {
        if (p.IsInHitstun && ActiveSkill != null)
        {
            CancelActiveSkill();
            return true;
        }
        return false;
    }

    public bool UpdateActiveSkill(Player p, Character owner, float dt)
    {
        if (ActiveSkill == null) return false;

        if (ActiveSkill.CustomLogic && ActiveSkill.OnCustomLogic?.Function != null)
        {
            ActiveSkill.OnCustomLogic.Function.Call(p, owner, dt);
        }

        CurrentActionFrame++;
        int total = ActiveSkill.StartupFrames + ActiveSkill.ActiveFrames + ActiveSkill.RecoveryFrames;
        if (CurrentActionFrame >= total)
        {
            CancelActiveSkill();
            return true;
        }

        return false;
    }

    public void UpdateCooldowns(float dt)
    {
        foreach (string skill in Skills.Keys)
        {
            if (SkillCooldowns.TryGetValue(skill, out float cd) && cd > 0f)
            {
                SkillCooldowns[skill] = MathF.Max(0f, cd - dt);
            }
        }
    }

    public void CancelActiveSkill()
    {
        ActiveSkill = null;
        CurrentActionFrame = 0;
        HasHit = false;
    }

    public void ResetAll()
    {
        CancelActiveSkill();
        SkillCooldowns.Clear();
    }
}
