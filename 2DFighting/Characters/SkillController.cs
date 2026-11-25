using System;
using System.Collections.Generic;

public class SkillController
{
    private const int FastPokeMaxStartupFrames = 5;

    public Dictionary<string, SkillData> Skills { get; } = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SkillData> _skillList = new();
    public IReadOnlyList<SkillData> SkillList => _skillList;
    public LuaFloatStore SkillCooldowns { get; } = new();
    public SkillData? ActiveSkill { get; set; }
    public int CurrentActionFrame { get; private set; } = 0;
    public bool HasHit { get; set; } = false;

    public readonly struct ControllerSnapshot
    {
        public readonly string? ActiveSkillName;
        public readonly int CurrentActionFrame;
        public readonly bool HasHit;
        public readonly float[] SkillCooldowns;

        public ControllerSnapshot(string? activeSkillName, int actionFrame, bool hasHit, float[] cooldowns)
        {
            ActiveSkillName = activeSkillName;
            CurrentActionFrame = actionFrame;
            HasHit = hasHit;
            SkillCooldowns = cooldowns;
        }
    }

    public ControllerSnapshot CaptureSnapshot()
    {
        float[] cooldowns = new float[_skillList.Count];
        for (int i = 0; i < _skillList.Count; i++)
            cooldowns[i] = _skillList[i].CurrentCooldown;
        return new ControllerSnapshot(ActiveSkill?.Name, CurrentActionFrame, HasHit, cooldowns);
    }

    public void RestoreSnapshot(in ControllerSnapshot snapshot)
    {
        ActiveSkill = (!string.IsNullOrEmpty(snapshot.ActiveSkillName) && Skills.TryGetValue(snapshot.ActiveSkillName, out var skill))
            ? skill
            : null;
        CurrentActionFrame = snapshot.CurrentActionFrame;
        HasHit = snapshot.HasHit;

        int count = Math.Min(_skillList.Count, snapshot.SkillCooldowns.Length);
        for (int i = 0; i < count; i++)
            _skillList[i].CurrentCooldown = snapshot.SkillCooldowns[i];
    }

    public SkillController()
    {
        SkillCooldowns.BindSkillBacking(Skills);
    }

    public void AddSkill(string name, SkillData skill)
    {
        if (Skills.TryGetValue(name, out SkillData? existing))
        {
            int idx = _skillList.IndexOf(existing);
            if (idx >= 0)
                _skillList[idx] = skill;
            else
                _skillList.Add(skill);
        }
        else
        {
            _skillList.Add(skill);
        }
        Skills[name] = skill;
    }

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
        if (ActiveSkill != null) return;
        if (p.IsCrouching) return;

        if (!Skills.TryGetValue(skillName, out var skill)) return;
        if (skill.CurrentCooldown > 0f) return;

        ActiveSkill = skill;
        CurrentActionFrame = 0;
        HasHit = false;
        skill.CurrentCooldown = skill.CooldownTime;
        p.LastExecutedSkillName = skillName;
        if (GameState.CurrentState == State.Playing || PhysicsEngine.IsHeadlessThread)
            PhysicsEngine.ActiveTelemetry?.RecordSkillUse(p, skillName);
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
        for (int i = 0; i < _skillList.Count; i++)
        {
            SkillData skill = _skillList[i];
            if (skill.CurrentCooldown > 0f)
            {
                skill.CurrentCooldown = MathF.Max(0f, skill.CurrentCooldown - dt);
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
        for (int i = 0; i < _skillList.Count; i++)
        {
            _skillList[i].CurrentCooldown = 0f;
        }
        SkillCooldowns.Clear();
    }
}
