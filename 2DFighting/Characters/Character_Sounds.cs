using System;
using System.Collections.Generic;
using System.IO;
using Raylib_cs;

public enum SoundType
{
    Hurt,
    Block,
    BlockBreak,
    Skill,
    OnCounter,
    Death,
    RoundStart,
    CustomOnHit,
}

public class Character_Sounds : IDisposable
{
    public const string SHARED_BANK_NAME = "__SHARED__";

    public string CharacterName { get; }
    public bool IsSharedBank => CharacterName == SHARED_BANK_NAME;
    public Dictionary<SoundType, List<Sound>> Sounds { get; } = new();
    public Dictionary<string, List<Sound>> SkillSounds { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Character_Sounds(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        CharacterName = character.Name;
        LoadSoundsForCharacter(character);
    }

    private Character_Sounds()
    {
        CharacterName = SHARED_BANK_NAME;
        LoadSharedSounds();
    }

    public static Character_Sounds CreateSharedBank() => new Character_Sounds();

    private string? GetCharacterFolder()
    {
        string[][] candidateSegments =
        {
            new[] { "Assets", "Sounds", CharacterName },
            new[] { "Assets", "Sounds", CharacterName.ToLowerInvariant() },
            new[] { "Characters", "assets", CharacterName.ToLowerInvariant(), "sounds" }
        };

        foreach (string[] segments in candidateSegments)
        {
            var matches = ModManager.ResolveDirectories(segments);
            if (matches.Count > 0)
                return matches[0];
        }

        DiagnosticsLogger.LogSilentIssueOnce(
            $"sound_folder_missing_{CharacterName}",
            $"No dedicated sound folder found for '{CharacterName}'. Will use SharedSoundBank fallbacks. (Checked: Assets/Sounds/{CharacterName} in Base & Mods)",
            LogSeverity.Info
        );
        return null;
    }

    private static string? GetSharedFolder()
    {
        string[][] candidateSegments =
        {
            new[] { "Assets", "Sounds", "Shared" },
            new[] { "Assets", "Sounds", "Default" },
            new[] { "Assets", "Sounds" },
            new[] { "Characters", "assets", "shared", "sounds" }
        };

        foreach (string[] segments in candidateSegments)
        {
            var matches = ModManager.ResolveDirectories(segments);
            if (matches.Count > 0)
                return matches[0];
        }

        DiagnosticsLogger.LogSilentIssueOnce(
            "sound_folder_missing_shared",
            "Shared sound folder not found. Create 'Assets/Sounds/Shared/' to provide global fallback audio.",
            LogSeverity.Warning
        );
        return null;
    }

    private void LoadSharedSounds()
    {
        string? sharedFolder = GetSharedFolder();
        if (sharedFolder == null) return;

        LoadGeneralSoundTypesFromFolder(sharedFolder);
        AddSkillSoundFiles(sharedFolder, "default", "Skill.wav", "Skill_default*.wav");
    }

    private void LoadSoundsForCharacter(Character character)
    {
        string? charFolder = GetCharacterFolder();
        if (charFolder == null) return;

        LoadGeneralSoundTypesFromFolder(charFolder);
        AddSkillSoundFiles(charFolder, "default", "Skill.wav", "Skill_default*.wav");

        foreach (string skillName in character.Skills.Keys)
        {
            AddSkillSoundFiles(
                charFolder,
                skillName,
                $"Skill_{skillName}.wav",
                $"Skill_{skillName}_*.wav",
                $"{skillName}.wav",
                $"{skillName}_*.wav"
            );
        }
    }

    private void LoadGeneralSoundTypesFromFolder(string folder)
    {
        if (!Directory.Exists(folder)) return;

        foreach (SoundType type in Enum.GetValues<SoundType>())
        {
            if (type == SoundType.Skill) continue;

            var soundList = new List<Sound>();

            string singlePath = Path.Combine(folder, $"{type}.wav");
            if (File.Exists(singlePath))
            {
                soundList.Add(Raylib.LoadSound(singlePath));
            }

            string[] matchingFiles = Directory.GetFiles(folder, $"{type}_*.wav");
            foreach (string file in matchingFiles)
            {
                soundList.Add(Raylib.LoadSound(file));
            }

            if (soundList.Count > 0)
            {
                Sounds[type] = soundList;
            }
        }
    }

    private void AddSkillSoundFiles(string folder, string key, params string[] patterns)
    {
        if (!Directory.Exists(folder)) return;

        var list = SkillSounds.TryGetValue(key, out var existing) ? existing : new List<Sound>();

        foreach (string pattern in patterns)
        {
            if (pattern.Contains('*'))
            {
                foreach (string file in Directory.GetFiles(folder, pattern))
                {
                    list.Add(Raylib.LoadSound(file));
                }
            }
            else
            {
                string fullPath = Path.Combine(folder, pattern);
                if (File.Exists(fullPath))
                {
                    list.Add(Raylib.LoadSound(fullPath));
                }
            }
        }

        if (list.Count > 0)
        {
            SkillSounds[key] = list;
        }
    }

    public bool TryGetSound(SoundType type, out Sound sound)
    {
        if (type == SoundType.Skill)
        {
            DiagnosticsLogger.LogSilentIssueOnce(
                $"sound_{CharacterName}_{type}_noskill",
                $"PlaySound(SoundType.Skill) called on '{CharacterName}' without a skillName; falling back to 'default' skill sound.",
                LogSeverity.Info
            );
            return TryGetSound(SoundType.Skill, "default", out sound);
        }

        if (Sounds.TryGetValue(type, out var list) && list.Count > 0)
        {
            sound = list.Count == 1 ? list[0] : list[Random.Shared.Next(list.Count)];
            return true;
        }

        sound = default;
        return false;
    }

    public bool TryGetSound(SoundType type, string skillName, out Sound sound)
    {
        if (type == SoundType.Skill)
        {
            if (!string.IsNullOrEmpty(skillName) &&
                SkillSounds.TryGetValue(skillName, out var skillList) &&
                skillList.Count > 0)
            {
                sound = skillList.Count == 1 ? skillList[0] : skillList[Random.Shared.Next(skillList.Count)];
                return true;
            }

            if (SkillSounds.TryGetValue("default", out var defaultList) && defaultList.Count > 0)
            {
                if (!IsSharedBank && !string.Equals(skillName, "default", StringComparison.OrdinalIgnoreCase))
                {
                    DiagnosticsLogger.LogSilentIssueOnce(
                        $"sound_{CharacterName}_skill_default_{skillName}",
                        $"Character '{CharacterName}' has no custom audio for skill '{skillName}'; using character default Skill.wav.",
                        LogSeverity.Info
                    );
                }
                sound = defaultList.Count == 1 ? defaultList[0] : defaultList[Random.Shared.Next(defaultList.Count)];
                return true;
            }

            sound = default;
            return false;
        }

        DiagnosticsLogger.LogSilentIssueOnce(
            $"sound_{CharacterName}_wrong_overload_{type}",
            $"TryGetSound(SoundType.{type}, \"{skillName}\") called for non-skill SoundType. Routing to standard TryGetSound.",
            LogSeverity.Warning
        );
        return TryGetSound(type, out sound);
    }

    public void Dispose()
    {
        foreach (var soundList in Sounds.Values)
        {
            foreach (var s in soundList)
            {
                Raylib.UnloadSound(s);
            }
        }
        Sounds.Clear();

        foreach (var skillList in SkillSounds.Values)
        {
            foreach (var s in skillList)
            {
                Raylib.UnloadSound(s);
            }
        }
        SkillSounds.Clear();
    }
}