using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public sealed class SkillBalanceGene
{
    public int Damage { get; set; }
    public int StartupFrames { get; set; }
    public int ActiveFrames { get; set; }
    public int RecoveryFrames { get; set; }
    public float HitStun { get; set; }
    public float CooldownTime { get; set; }

    public SkillBalanceGene Clone() => new()
    {
        Damage = Damage,
        StartupFrames = StartupFrames,
        ActiveFrames = ActiveFrames,
        RecoveryFrames = RecoveryFrames,
        HitStun = HitStun,
        CooldownTime = CooldownTime
    };
}

public sealed class CharacterBalanceGenome
{
    public string CharacterId { get; set; } = string.Empty;
    public string BaseScriptHash { get; set; } = string.Empty;
    public float FitnessScore { get; set; }
    public int Generation { get; set; }
    public float WinRate { get; set; }
    public float AvgMaxCombo { get; set; }
    public float SkillDiversity { get; set; }
    public float WalkSpeedPct { get; set; }
    public float JumpForcePct { get; set; }
    public Dictionary<string, SkillBalanceGene> Skills { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public CharacterBalanceGenome Clone()
    {
        var copy = new CharacterBalanceGenome
        {
            CharacterId = CharacterId,
            BaseScriptHash = BaseScriptHash,
            FitnessScore = FitnessScore,
            Generation = Generation,
            WinRate = WinRate,
            AvgMaxCombo = AvgMaxCombo,
            SkillDiversity = SkillDiversity,
            WalkSpeedPct = WalkSpeedPct,
            JumpForcePct = JumpForcePct,
            Skills = new Dictionary<string, SkillBalanceGene>(Skills.Count, StringComparer.OrdinalIgnoreCase)
        };

        foreach (var kvp in Skills)
        {
            copy.Skills[kvp.Key] = kvp.Value.Clone();
        }
        return copy;
    }

    public static CharacterBalanceGenome FromCharacter(Character character)
    {
        var genome = new CharacterBalanceGenome
        {
            CharacterId = !string.IsNullOrWhiteSpace(character.CharacterId)
                ? character.CharacterId
                : character.Name.Trim().ToLowerInvariant(),
            BaseScriptHash = !string.IsNullOrEmpty(character.BaseScriptHash)
                ? character.BaseScriptHash
                : character.ScriptHash,
            WalkSpeedPct = character.WalkSpeedPct,
            JumpForcePct = character.JumpForcePct
        };

        foreach (var kvp in character.Skills)
        {
            SkillData skill = kvp.Value;
            genome.Skills[kvp.Key] = new SkillBalanceGene
            {
                Damage = skill.Damage,
                StartupFrames = skill.StartupFrames,
                ActiveFrames = skill.ActiveFrames,
                RecoveryFrames = skill.RecoveryFrames,
                HitStun = skill.HitStun,
                CooldownTime = skill.CooldownTime
            };
        }

        return genome;
    }

    public string ComputeCompactSignature()
    {
        var sb = new StringBuilder(256);
        sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F4}|{1:F4}", WalkSpeedPct, JumpForcePct);

        if (Skills.Count > 0)
        {
            var keys = new List<string>(Skills.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                SkillBalanceGene g = Skills[key];
                sb.Append('|')
                  .Append(key)
                  .Append(':')
                  .Append(g.Damage).Append(',')
                  .Append(g.StartupFrames).Append(',')
                  .Append(g.ActiveFrames).Append(',')
                  .Append(g.RecoveryFrames).Append(',')
                  .AppendFormat(CultureInfo.InvariantCulture, "{0:F3},{1:F3}", g.HitStun, g.CooldownTime);
            }
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash.AsSpan(0, 4)).ToLowerInvariant();
    }
}

public static class BalanceOverrideStore
{
    private static readonly object _sync = new();
    private static readonly Dictionary<string, CharacterBalanceGenome?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static volatile bool _isEnabled = true;

    public static bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    public static string BalanceDir => Path.Combine(ModManager.CacheDir, "Balance");

    private static string GetGenomePath(string characterId)
    {
        string normalized = NormalizeId(characterId);
        return Path.Combine(BalanceDir, $"{normalized}_balance.json");
    }

    public static bool TryGetGenome(string characterId, string? expectedBaseScriptHash, out CharacterBalanceGenome? genome)
    {
        genome = null;
        if (string.IsNullOrWhiteSpace(characterId))
            return false;

        string normalized = NormalizeId(characterId);

        lock (_sync)
        {
            if (!_cache.TryGetValue(normalized, out CharacterBalanceGenome? cached))
            {
                cached = LoadFromDiskLocked(normalized);
                _cache[normalized] = cached;
            }

            if (cached == null)
                return false;

            if (!string.IsNullOrEmpty(expectedBaseScriptHash) &&
                !string.IsNullOrEmpty(cached.BaseScriptHash) &&
                !string.Equals(cached.BaseScriptHash, expectedBaseScriptHash, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            genome = cached.Clone();
            return true;
        }
    }

    public static void SaveGenome(CharacterBalanceGenome? genome)
    {
        if (genome == null || string.IsNullOrWhiteSpace(genome.CharacterId))
            return;

        string normalized = NormalizeId(genome.CharacterId);
        CharacterBalanceGenome toSave = genome.Clone();
        toSave.CharacterId = normalized;

        lock (_sync)
        {
            _cache[normalized] = toSave.Clone();

            try
            {
                string dir = BalanceDir;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string path = GetGenomePath(normalized);
                string tempPath = path + ".tmp";
                string json = JsonSerializer.Serialize(toSave, JsonOptions);
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, path, overwrite: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BalanceOverrideStore] Save failed for '{normalized}': {ex.Message}");
            }
        }
    }

    public static int ClearAllGenomes()
    {
        int deleted = 0;
        lock (_sync)
        {
            _cache.Clear();

            try
            {
                string dir = BalanceDir;
                if (Directory.Exists(dir))
                {
                    string[] files = Directory.GetFiles(dir, "*_balance.json");
                    for (int i = 0; i < files.Length; i++)
                    {
                        File.Delete(files[i]);
                        deleted++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BalanceOverrideStore] ClearAllGenomes failed: {ex.Message}");
            }
        }
        return deleted;
    }

    public static int GetSavedCount()
    {
        lock (_sync)
        {
            try
            {
                string dir = BalanceDir;
                if (!Directory.Exists(dir))
                    return 0;
                return Directory.GetFiles(dir, "*_balance.json").Length;
            }
            catch
            {
                return 0;
            }
        }
    }

    private static CharacterBalanceGenome? LoadFromDiskLocked(string normalizedId)
    {
        string path = GetGenomePath(normalizedId);
        try
        {
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            CharacterBalanceGenome? loaded = JsonSerializer.Deserialize<CharacterBalanceGenome>(json, JsonOptions);
            if (loaded == null)
                return null;

            loaded.CharacterId = string.IsNullOrWhiteSpace(loaded.CharacterId) ? normalizedId : NormalizeId(loaded.CharacterId);
            loaded.Skills = loaded.Skills != null
                ? new Dictionary<string, SkillBalanceGene>(loaded.Skills, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, SkillBalanceGene>(StringComparer.OrdinalIgnoreCase);

            return loaded;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BalanceOverrideStore] Load failed for '{normalizedId}': {ex.Message}");
            return null;
        }
    }

    private static string NormalizeId(string characterId)
    {
        string trimmed = characterId.Trim().ToLowerInvariant();
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char c in invalid)
        {
            trimmed = trimmed.Replace(c, '_');
        }
        return trimmed;
    }
}
