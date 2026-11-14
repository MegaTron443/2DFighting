using System;
using System.Collections.Generic;
using Raylib_cs;

public class SoundHandler : IDisposable
{
    public static SoundHandler? Current { get; private set; }
    private float _volume = 1.0f;
    private readonly List<Character_Sounds> _activeSoundBanks = new();
    private Character_Sounds? _sharedSoundBank;

    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 1f);
    }

    public SoundHandler(GameData data)
    {
        _volume = Math.Clamp(data.AppConfig.Sound.Volume / 100f, 0f, 1f);

        LoadSharedSoundBanks();

        if (data.Player1?.CurrentCharacter != null)
        {
            _activeSoundBanks.Add(new Character_Sounds(data.Player1.CurrentCharacter));
        }

        if (data.Player2?.CurrentCharacter != null &&
            data.Player2.CurrentCharacter.Name != data.Player1?.CurrentCharacter?.Name)
        {
            _activeSoundBanks.Add(new Character_Sounds(data.Player2.CurrentCharacter));
        }
        Current = this;
    }

    private void LoadSharedSoundBanks()
    {
        _sharedSoundBank = Character_Sounds.CreateSharedBank();
    }

    public void PlaySound(SoundType soundType)
    {
        if (PhysicsEngine.IsTrainingMode || _volume <= 0f) return;

        if (_sharedSoundBank != null && _sharedSoundBank.TryGetSound(soundType, out Sound sharedSound))
        {
            Raylib.SetSoundVolume(sharedSound, _volume);
            Raylib.PlaySound(sharedSound);
            return;
        }

        for (int i = 0; i < _activeSoundBanks.Count; i++)
        {
            if (_activeSoundBanks[i].TryGetSound(soundType, out Sound charSound))
            {
                Raylib.SetSoundVolume(charSound, _volume);
                Raylib.PlaySound(charSound);
                return;
            }
        }

        DiagnosticsLogger.LogSilentIssueOnce(
            $"missing_global_sound_{soundType}",
            $"SoundType.{soundType} was not found in SharedSoundBank or any active character bank.",
            LogSeverity.Warning
        );
    }

    public void PlaySound(Player? player, SoundType soundType)
    {
        if (PhysicsEngine.IsTrainingMode || _volume <= 0f) return;

        if (player?.CurrentCharacter != null)
        {
            string targetName = player.CurrentCharacter.Name;

            for (int i = 0; i < _activeSoundBanks.Count; i++)
            {
                if (_activeSoundBanks[i].CharacterName == targetName &&
                    _activeSoundBanks[i].TryGetSound(soundType, out Sound sound))
                {
                    Raylib.SetSoundVolume(sound, _volume);
                    Raylib.PlaySound(sound);
                    return;
                }
            }

            if (_sharedSoundBank != null && _sharedSoundBank.TryGetSound(soundType, out Sound sharedSound))
            {
                DiagnosticsLogger.LogSilentIssueOnce(
                    $"fallback_shared_{targetName}_{soundType}",
                    $"Character '{targetName}' missing SoundType.{soundType}; using SharedSoundBank.",
                    LogSeverity.Info
                );
                Raylib.SetSoundVolume(sharedSound, _volume);
                Raylib.PlaySound(sharedSound);
                return;
            }

            DiagnosticsLogger.LogSilentIssueOnce(
                $"missing_char_sound_{targetName}_{soundType}",
                $"SoundType.{soundType} missing for '{targetName}' and not present in SharedSoundBank.",
                LogSeverity.Warning
            );
            return;
        }

        PlaySound(soundType);
    }

    public void PlaySkillSound(Player? player, string skillName)
    {
        if (PhysicsEngine.IsTrainingMode || _volume <= 0f) return;
        if (player?.CurrentCharacter == null) return;

        string targetName = player.CurrentCharacter.Name;

        for (int i = 0; i < _activeSoundBanks.Count; i++)
        {
            if (_activeSoundBanks[i].CharacterName == targetName &&
                _activeSoundBanks[i].TryGetSound(SoundType.Skill, skillName, out Sound sound))
            {
                Raylib.SetSoundVolume(sound, _volume);
                Raylib.PlaySound(sound);
                return;
            }
        }

        if (_sharedSoundBank != null && _sharedSoundBank.TryGetSound(SoundType.Skill, skillName, out Sound sharedSkillSound))
        {
            DiagnosticsLogger.LogSilentIssueOnce(
                $"fallback_shared_skill_{targetName}_{skillName}",
                $"Character '{targetName}' missing skill sound '{skillName}'; using SharedSoundBank Skill.wav.",
                LogSeverity.Info
            );
            Raylib.SetSoundVolume(sharedSkillSound, _volume);
            Raylib.PlaySound(sharedSkillSound);
            return;
        }

        DiagnosticsLogger.LogSilentIssueOnce(
            $"missing_skill_sound_{targetName}_{skillName}",
            $"Skill sound '{skillName}' missing for '{targetName}' and no fallback Skill.wav in SharedSoundBank.",
            LogSeverity.Warning
        );
    }

    public void ReloadCharacterBanks(Player? p1, Player? p2)
    {
        foreach (var bank in _activeSoundBanks)
        {
            bank.Dispose();
        }
        _activeSoundBanks.Clear();

        if (p1?.CurrentCharacter != null)
        {
            _activeSoundBanks.Add(new Character_Sounds(p1.CurrentCharacter));
        }

        if (p2?.CurrentCharacter != null &&
            p2.CurrentCharacter.Name != p1?.CurrentCharacter?.Name)
        {
            _activeSoundBanks.Add(new Character_Sounds(p2.CurrentCharacter));
        }
    }

    public static void PlayFromLua(Player? player, string soundNameOrType)
    {
        if (Current == null || PhysicsEngine.IsTrainingMode || string.IsNullOrEmpty(soundNameOrType))
            return;

        if (Enum.TryParse<SoundType>(soundNameOrType, ignoreCase: true, out var soundType) &&
            soundType != SoundType.Skill)
        {
            Current.PlaySound(player, soundType);
        }
        else
        {
            Current.PlaySkillSound(player, soundNameOrType);
        }
    }

    public void Dispose()
    {
        foreach (var bank in _activeSoundBanks)
        {
            bank.Dispose();
        }
        _activeSoundBanks.Clear();

        _sharedSoundBank?.Dispose();
        _sharedSoundBank = null;
        if (Current == this) Current = null;
    }
}