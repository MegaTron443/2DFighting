using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MoonSharp.Interpreter;
using Raylib_cs;

[Flags]
public enum MoveTacticalTags
{
    None             = 0,
    FastPoke         = 1 << 0,
    LowPoke          = 1 << 1,
    Overhead         = 1 << 2,
    AntiAir          = 1 << 3,
    CounterParry     = 1 << 4,
    BlockBreaker     = 1 << 5,
    HyperArmor       = 1 << 6,
    ProjectileFast   = 1 << 7,
    ProjectileTrap   = 1 << 8,
    ProjectileHoming = 1 << 9,
    AdvancingDash    = 1 << 10,
    RetreatingEvade  = 1 << 11,
    TeleportBlink    = 1 << 12,
    VacuumPull       = 1 << 13,
    ComboLauncher    = 1 << 14,
    HardFreezeStun   = 1 << 15,
    SelfHeal         = 1 << 16,
    CooldownReset    = 1 << 17,
    AerialDive       = 1 << 18
}

public class TranslatedMoveSemantic
{
    public SkillData Skill { get; }
    public MoveTacticalTags Tags { get; set; } = MoveTacticalTags.None;

    // Automatically deduced spatial metrics (in screen width %)
    public float MinEffectiveRangePct { get; set; }
    public float MaxEffectiveRangePct { get; set; }
    public float OptimalRangePct { get; set; }
    public float SelfAdvanceDeltaPct { get; set; }
    public float SelfVerticalDeltaPct { get; set; }
    public float TargetKnockbackPct { get; set; }

    public int Uses { get; set; }
    public int Hits { get; set; }
    public int Whiffs { get; set; }
    public int InterruptedCount { get; set; }
    public float ConfidenceMultiplier { get; set; } = 1.0f;

    public bool HasTag(MoveTacticalTags flag) => (Tags & flag) == flag;

    public TranslatedMoveSemantic(SkillData skill)
    {
        Skill = skill;
    }

    public void RecordOutcome(bool hit, bool wasInterrupted, float distanceUsedPct)
    {
        Uses++;
        if (hit)
        {
            Hits++;
            ConfidenceMultiplier = Math.Clamp(ConfidenceMultiplier + 0.12f, 0.35f, 2.1f);
            if (distanceUsedPct > MaxEffectiveRangePct && !HasTag(MoveTacticalTags.ProjectileFast))
            {
                MaxEffectiveRangePct = Math.Min(0.85f, distanceUsedPct * 1.04f);
            }
        }
        else if (wasInterrupted)
        {
            InterruptedCount++;
            ConfidenceMultiplier = Math.Clamp(ConfidenceMultiplier - 0.15f, 0.35f, 2.1f);
        }
        else
        {
            Whiffs++;
            ConfidenceMultiplier = Math.Clamp(ConfidenceMultiplier - 0.06f, 0.35f, 2.1f);
        }
    }
}

public class CharacterTacticalSummary
{
    public float ZonerAffinity { get; set; }
    public float RushdownAffinity { get; set; }
    public float GrapplerArmorAffinity { get; set; }
    public float TricksterTrapAffinity { get; set; }
    public float PreferredNeutralDistancePct { get; set; } = 0.16f;
}

public static class LuaMoveTranslator
{
    public static Dictionary<string, TranslatedMoveSemantic> AnalyzeCharacter(
        Character character,
        out CharacterTacticalSummary summary)
    {
        var semantics = new Dictionary<string, TranslatedMoveSemantic>();
        summary = new CharacterTacticalSummary();

        // Extract per-skill raw Lua blocks for behavioral static & sandbox analysis
        Dictionary<string, string> skillLuaBlocks = ExtractSkillSourceBlocks(character.RawLuaCode);

        int zonerPoints = 0;
        int rushdownPoints = 0;
        int armorPoints = 0;
        int trapPoints = 0;

        foreach (var skill in character.Skills.Values)
        {
            if (skill.TriggerMask == 0) continue;

            var translated = new TranslatedMoveSemantic(skill);
            skillLuaBlocks.TryGetValue(skill.Name, out string? luaSnippet);
            luaSnippet ??= "";

            TranslateSkillMechanics(character, skill, luaSnippet, translated);
            semantics[skill.Name] = translated;

            // Tally archetype affinities
            if (translated.HasTag(MoveTacticalTags.ProjectileFast) || translated.HasTag(MoveTacticalTags.ProjectileHoming))
                zonerPoints += 2;
            if (translated.HasTag(MoveTacticalTags.RetreatingEvade))
                zonerPoints += 1;

            if (translated.HasTag(MoveTacticalTags.AdvancingDash) || translated.HasTag(MoveTacticalTags.FastPoke))
                rushdownPoints += 2;
            if (translated.HasTag(MoveTacticalTags.Overhead) || translated.HasTag(MoveTacticalTags.LowPoke))
                rushdownPoints += 1;

            if (translated.HasTag(MoveTacticalTags.HyperArmor) || translated.HasTag(MoveTacticalTags.VacuumPull))
                armorPoints += 2;
            if (translated.HasTag(MoveTacticalTags.BlockBreaker) && translated.MaxEffectiveRangePct < 0.16f)
                armorPoints += 2;

            if (translated.HasTag(MoveTacticalTags.ProjectileTrap) ||
                translated.HasTag(MoveTacticalTags.TeleportBlink) ||
                translated.HasTag(MoveTacticalTags.CooldownReset) ||
                translated.HasTag(MoveTacticalTags.HardFreezeStun))
            {
                trapPoints += 2;
            }
        }

        float total = Math.Max(1f, zonerPoints + rushdownPoints + armorPoints + trapPoints);
        summary.ZonerAffinity = zonerPoints / total;
        summary.RushdownAffinity = rushdownPoints / total;
        summary.GrapplerArmorAffinity = armorPoints / total;
        summary.TricksterTrapAffinity = trapPoints / total;

        // Calculate optimal neutral spacing from the character's translated toolkit
        if (summary.ZonerAffinity >= 0.35f)
            summary.PreferredNeutralDistancePct = 0.42f;
        else if (summary.TricksterTrapAffinity >= 0.35f)
            summary.PreferredNeutralDistancePct = 0.30f;
        else if (summary.GrapplerArmorAffinity >= 0.35f)
            summary.PreferredNeutralDistancePct = 0.13f;
        else
            summary.PreferredNeutralDistancePct = 0.11f;

        return semantics;
    }

    private static void TranslateSkillMechanics(
        Character character,
        SkillData skill,
        string luaBlock,
        TranslatedMoveSemantic dest)
    {
        float halfBodyW = character.WidthPct * 0.5f;
        float hitboxReach = character.WidthPct * (skill.HitboxOffsetPct.x + skill.HitboxOffsetPct.width);
        float baseReach = Math.Max(halfBodyW, hitboxReach - halfBodyW);

        dest.MinEffectiveRangePct = 0f;
        dest.MaxEffectiveRangePct = baseReach;
        dest.TargetKnockbackPct = 0.18f;

        // 1. Inspect core C# SkillData properties
        if (skill.StartupFrames > 0 && skill.StartupFrames <= 5 && !skill.IsCounter)
            dest.Tags |= MoveTacticalTags.FastPoke;

        if (skill.Height == AttackHeight.Low)
            dest.Tags |= MoveTacticalTags.LowPoke;
        else if (skill.Height == AttackHeight.Overhead)
            dest.Tags |= MoveTacticalTags.Overhead;

        if (!skill.IsCounter && (skill.HitboxOffsetPct.y < -0.15f || skill.HitboxOffsetPct.height >= 0.75f))
            dest.Tags |= MoveTacticalTags.AntiAir;

        if (skill.IsCounter)
            dest.Tags |= MoveTacticalTags.CounterParry;

        if (skill.IsBlockBreak)
            dest.Tags |= MoveTacticalTags.BlockBreaker;

        if (skill.IsHyperFrames)
            dest.Tags |= MoveTacticalTags.HyperArmor;

        if (skill.HitStun >= 1.15f)
            dest.Tags |= MoveTacticalTags.HardFreezeStun;

        // 2. Inspect Lua source & closures without requiring any modder annotations
        AnalyzeLuaBehaviorBlock(character, skill, luaBlock, dest);

        // 3. Finalize optimal range calculation
        dest.OptimalRangePct = (dest.MinEffectiveRangePct + dest.MaxEffectiveRangePct) * 0.65f;
    }

    private static void AnalyzeLuaBehaviorBlock(
        Character character,
        SkillData skill,
        string lua,
        TranslatedMoveSemantic dest)
    {
        if (string.IsNullOrWhiteSpace(lua)) return;

        // A. Detect Projectile Creation (CreateProjectile or create_projectile)
        bool spawnsProjectile = lua.Contains("CreateProjectile") || lua.Contains("create_projectile");
        if (spawnsProjectile)
        {
            float speedX = ExtractFloatProperty(lua, "speed_x_pct", 0.45f);
            float offsetX = ExtractFloatProperty(lua, "x_offset_pct", 0.06f);
            float lifetime = ExtractFloatProperty(lua, "lifetime", 3.0f);

            // Check if it's a stationary trap/pillar/mine (speed_x_pct == 0)
            if (MathF.Abs(speedX) < 0.02f)
            {
                dest.Tags |= MoveTacticalTags.ProjectileTrap;
                dest.MinEffectiveRangePct = Math.Max(0.04f, offsetX - 0.05f);
                dest.MaxEffectiveRangePct = Math.Max(dest.MaxEffectiveRangePct, offsetX + 0.12f);
            }
            else
            {
                dest.Tags |= MoveTacticalTags.ProjectileFast;
                float projTravel = Math.Min(0.85f, MathF.Abs(speedX) * Math.Min(lifetime, 1.6f));
                dest.MinEffectiveRangePct = 0.14f;
                dest.MaxEffectiveRangePct = Math.Max(dest.MaxEffectiveRangePct, projTravel);
            }

            if (lua.Contains("Trajectories.Homing"))
            {
                dest.Tags |= MoveTacticalTags.ProjectileHoming | MoveTacticalTags.AntiAir;
            }

            if (lua.Contains("Trajectories.Gravity"))
            {
                dest.MinEffectiveRangePct = 0.18f;
                dest.MaxEffectiveRangePct = Math.Max(dest.MaxEffectiveRangePct, 0.52f);
            }
        }

        // B. Detect Movement / Teleport / Lunge / Retreat inside on_customlogic
        if (skill.CustomLogic)
        {
            if (lua.Contains("TargetOpponent") && lua.Contains("NewVec2"))
            {
                // Teleports directly to opponent
                dest.Tags |= MoveTacticalTags.TeleportBlink | MoveTacticalTags.AerialDive;
                dest.MinEffectiveRangePct = 0.05f;
                dest.MaxEffectiveRangePct = 0.85f;
            }
            else if (lua.Contains("Translate") || lua.Contains("SetVelocity"))
            {
                bool isRetreat = lua.Contains("backDir") || lua.Contains("retreatDir");
                if (isRetreat)
                {
                    dest.Tags |= MoveTacticalTags.RetreatingEvade;
                    dest.SelfAdvanceDeltaPct = -character.WalkSpeedPct * 0.25f;
                }
                else
                {
                    dest.Tags |= MoveTacticalTags.AdvancingDash;
                    float mult = 1.6f;
                    int activeWindow = skill.StartupFrames + skill.ActiveFrames;
                    float advancePct = character.WalkSpeedPct * mult * (activeWindow / 60f);
                    dest.SelfAdvanceDeltaPct = advancePct;
                    dest.MaxEffectiveRangePct = Math.Min(0.65f, dest.MaxEffectiveRangePct + advancePct);
                }

                if (lua.Contains("IsGrounded = false"))
                {
                    dest.Tags |= MoveTacticalTags.AerialDive;
                }
            }
        }

        // C. Detect On-Hit / On-Counter / Passive Effects
        if (lua.Contains("ApplyLaunchForce"))
        {
            dest.Tags |= MoveTacticalTags.ComboLauncher;
        }

        // Detect Vacuum Pull
        var hitForceMatch = Regex.Match(lua, @"ApplyHitForce\s*\([^,]+,[^,]+,[^,]+,[^,]+,\s*(-?[0-9.]+|[A-Z_]+)");
        if (hitForceMatch.Success)
        {
            string kbToken = hitForceMatch.Groups[1].Value;
            if (float.TryParse(kbToken, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float kbVal))
            {
                dest.TargetKnockbackPct = kbVal;
                if (kbVal < -0.02f)
                {
                    dest.Tags |= MoveTacticalTags.VacuumPull;
                }
                else if (kbVal <= 0.03f && skill.HitStun >= 0.8f)
                {
                    dest.Tags |= MoveTacticalTags.HardFreezeStun;
                }
            }
        }

        // Detect Self-Heal
        if (lua.Contains(".Health +") || lua.Contains("HEAL"))
        {
            dest.Tags |= MoveTacticalTags.SelfHeal;
        }

        // Detect Cooldown Reset
        if (lua.Contains("SkillCooldowns") && lua.Contains("= 0"))
        {
            dest.Tags |= MoveTacticalTags.CooldownReset;
        }
    }

    private static float ExtractFloatProperty(string lua, string key, float defaultVal)
    {
        var match = Regex.Match(lua, $@"{key}\s*=\s*(-?[0-9.]+)");
        if (match.Success && float.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
        {
            return val;
        }
        return defaultVal;
    }

    private static Dictionary<string, string> ExtractSkillSourceBlocks(string rawLua)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(rawLua)) return result;

        int skillsIdx = rawLua.IndexOf("skills", StringComparison.Ordinal);
        if (skillsIdx < 0) return result;

        int braceStart = rawLua.IndexOf('{', skillsIdx);
        if (braceStart < 0) return result;

        int i = braceStart + 1;
        while (i < rawLua.Length)
        {
            while (i < rawLua.Length && (char.IsWhiteSpace(rawLua[i]) || rawLua[i] == ',')) i++;
            if (i >= rawLua.Length || rawLua[i] == '}') break;

            // Skip comments
            if (i + 1 < rawLua.Length && rawLua[i] == '-' && rawLua[i + 1] == '-')
            {
                while (i < rawLua.Length && rawLua[i] != '\n') i++;
                continue;
            }

            // Read skill identifier
            int nameStart = i;
            while (i < rawLua.Length && (char.IsLetterOrDigit(rawLua[i]) || rawLua[i] == '_')) i++;
            string skillName = rawLua[nameStart..i].Trim();

            int openBrace = rawLua.IndexOf('{', i);
            if (openBrace < 0) break;

            int depth = 1;
            int cursor = openBrace + 1;
            while (cursor < rawLua.Length && depth > 0)
            {
                if (rawLua[cursor] == '{') depth++;
                else if (rawLua[cursor] == '}') depth--;
                cursor++;
            }

            if (!string.IsNullOrEmpty(skillName))
            {
                result[skillName] = rawLua[openBrace..cursor];
            }
            i = cursor;
        }

        return result;
    }
}