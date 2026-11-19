using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using Raylib_cs;

[MoonSharpUserData]
public class SoundHandler : IDisposable
{
    public static SoundHandler? Current { get; private set; }

    private readonly List<Character_Sounds> _activeSoundBanks = new();
    private Character_Sounds? _sharedSoundBank;
    private readonly ProceduralSoundBank _proceduralBank = new();

    public AudioBusMixer Mixer { get; } = new();

    public float Volume
    {
        get => Mixer.MasterVolume;
        set => Mixer.MasterVolume = value;
    }

    public SoundHandler(GameData data)
    {
        Mixer.MasterVolume = Math.Clamp(data.AppConfig.Sound.Volume / 100f, 0f, 1f);
        LoadSharedSoundBanks();
        _proceduralBank.GenerateDefaults();
        ReloadCharacterBanks(data.Player1, data.Player2);
        Current = this;
    }

    public static bool IsSoundValid(Sound sound) => ProceduralSoundBank.IsSoundValid(sound);

    private void LoadSharedSoundBanks()
    {
        _sharedSoundBank = Character_Sounds.CreateSharedBank();
    }

    public void UpdateTick(float dt) => Mixer.UpdateTick(dt);

    public float GetBusEffectiveVolume(string busName) => Mixer.GetEffectiveVolume(busName);

    public static void SetBusVolumeFromLua(string busName, float volume) =>
        Current?.Mixer.SetBusVolume(busName, volume);

    public static void SetBusLowPassFromLua(string busName, float cutoffHz) =>
        Current?.Mixer.SetBusLowPass(busName, cutoffHz);

    public static void TriggerDuckFromLua(float targetGain = 0.05f, int holdFrames = 10, float recoverySeconds = 0.6f)
    {
        if (Current == null || PhysicsEngine.IsTrainingMode) return;
        Current.Mixer.TriggerDuck(targetGain, holdFrames, recoverySeconds);
    }

    public static void PlayFromLua(Player? player, DynValue soundOrConfig)
    {
        if (Current == null || PhysicsEngine.IsTrainingMode || soundOrConfig.IsNil())
            return;

        string soundName = "";
        string bus = "sfx_character";
        float pitch = 1.0f;
        float volumeMult = 1.0f;

        if (soundOrConfig.Type == DataType.String)
        {
            soundName = soundOrConfig.String;
        }
        else if (soundOrConfig.Type == DataType.Table)
        {
            Table tbl = soundOrConfig.Table;
            soundName = tbl.Get("sound").String ?? tbl.Get("name").String ?? "";
            bus = tbl.Get("bus").String ?? "sfx_character";
            pitch = (float)(tbl.Get("pitch").CastToNumber() ?? 1.0);
            volumeMult = (float)(tbl.Get("volume").CastToNumber() ?? 1.0);

            DynValue duckVal = tbl.Get("duck_world");
            if (!duckVal.IsNil())
            {
                float duckGain = (float)(duckVal.CastToNumber() ?? 0.05);
                int duckFrames = (int)(tbl.Get("duck_frames").CastToNumber() ?? 10);
                float duckRecov = (float)(tbl.Get("duck_recovery").CastToNumber() ?? 0.6);
                TriggerDuckFromLua(duckGain, duckFrames, duckRecov);
            }
        }

        if (string.IsNullOrEmpty(soundName))
        {
            DiagnosticsLogger.LogSilentIssueOnce(
                "lua_playsound_empty",
                "PlaySound called from Lua with an empty or missing sound name.",
                LogSeverity.Warning
            );
            return;
        }

        Current.PlayEx(player, soundName, bus, pitch, volumeMult);
    }

    public void PlayEx(Player? player, string soundNameOrType, string busName = "sfx_character", float pitch = 1.0f, float volumeMult = 1.0f)
    {
        if (PhysicsEngine.IsTrainingMode || Volume <= 0f) return;

        float finalVolume = Math.Clamp(GetBusEffectiveVolume(busName) * volumeMult, 0f, 1f);
        float finalPitch = Math.Clamp(pitch, 0.2f, 3.0f);

        // 1. Procedural sound bank
        if (_proceduralBank.TryGet(soundNameOrType, out Sound procSound))
        {
            DispatchRaylibSound(procSound, finalVolume, finalPitch);
            return;
        }

        // 2. General SoundType enum
        if (Enum.TryParse<SoundType>(soundNameOrType, ignoreCase: true, out var parsedType) &&
            parsedType != SoundType.Skill)
        {
            PlaySoundInternal(player, parsedType, finalVolume, finalPitch);
            return;
        }

        // 3. Skill sound bank (with automatic fallback inside)
        PlaySkillSoundInternal(player, soundNameOrType, finalVolume, finalPitch);
    }

    private static bool DispatchRaylibSound(Sound sound, float volume, float pitch)
    {
        if (!IsSoundValid(sound))
        {
            DiagnosticsLogger.LogSilentIssueOnce(
                "dispatch_invalid_sound",
                "Attempted to play an unloaded or invalid Raylib Sound (frameCount == 0). Blocked to prevent native crash.",
                LogSeverity.Error
            );
            return false;
        }

        Raylib.SetSoundVolume(sound, volume);
        Raylib.SetSoundPitch(sound, pitch);
        Raylib.PlaySound(sound);
        return true;
    }

    private void PlayFallbackSound(string missingKey, float volume, float pitch)
    {
        DiagnosticsLogger.LogSilentIssueOnce(
            $"sound_missing_fallback_{missingKey}",
            $"Sound '{missingKey}' not found in character or shared banks; using procedural fallback.",
            LogSeverity.Warning
        );

        if (_proceduralBank.TryGet("default", out Sound fallback))
        {
            DispatchRaylibSound(fallback, volume * 0.6f, pitch);
        }
    }

    public void PlaySound(SoundType soundType)
    {
        float vol = GetBusEffectiveVolume("sfx_world");
        PlaySoundInternal(null, soundType, vol, 1.0f);
    }

    public void PlaySound(Player? player, SoundType soundType)
    {
        string bus = (soundType == SoundType.Hurt || soundType == SoundType.Block || soundType == SoundType.Death)
            ? "sfx_world"
            : "sfx_character";
        float vol = GetBusEffectiveVolume(bus);
        PlaySoundInternal(player, soundType, vol, 1.0f);
    }

    public void PlaySkillSound(Player? player, string skillName)
    {
        float vol = GetBusEffectiveVolume("sfx_character");
        PlaySkillSoundInternal(player, skillName, vol, 1.0f);
    }

    private void PlaySoundInternal(Player? player, SoundType soundType, float volume, float pitch)
    {
        if (PhysicsEngine.IsTrainingMode || volume <= 0f) return;

        if (player?.CurrentCharacter != null)
        {
            string targetName = player.CurrentCharacter.Name;
            for (int i = 0; i < _activeSoundBanks.Count; i++)
            {
                if (_activeSoundBanks[i].CharacterName == targetName &&
                    _activeSoundBanks[i].TryGetSound(soundType, out Sound sound) &&
                    DispatchRaylibSound(sound, volume, pitch))
                {
                    return;
                }
            }

            if (_sharedSoundBank != null &&
                _sharedSoundBank.TryGetSound(soundType, out Sound sharedSound) &&
                DispatchRaylibSound(sharedSound, volume, pitch))
            {
                return;
            }

            PlayFallbackSound($"{targetName}:{soundType}", volume, pitch);
            return;
        }

        if (_sharedSoundBank != null &&
            _sharedSoundBank.TryGetSound(soundType, out Sound globalShared) &&
            DispatchRaylibSound(globalShared, volume, pitch))
        {
            return;
        }

        for (int i = 0; i < _activeSoundBanks.Count; i++)
        {
            if (_activeSoundBanks[i].TryGetSound(soundType, out Sound charSound) &&
                DispatchRaylibSound(charSound, volume, pitch))
            {
                return;
            }
        }

        PlayFallbackSound($"Global:{soundType}", volume, pitch);
    }

    private void PlaySkillSoundInternal(Player? player, string skillName, float volume, float pitch)
    {
        if (PhysicsEngine.IsTrainingMode || volume <= 0f) return;

        string targetName = player?.CurrentCharacter?.Name ?? "Global";

        if (player?.CurrentCharacter != null)
        {
            for (int i = 0; i < _activeSoundBanks.Count; i++)
            {
                if (_activeSoundBanks[i].CharacterName == targetName &&
                    _activeSoundBanks[i].TryGetSound(SoundType.Skill, skillName, out Sound sound) &&
                    DispatchRaylibSound(sound, volume, pitch))
                {
                    return;
                }
            }
        }

        if (_sharedSoundBank != null &&
            _sharedSoundBank.TryGetSound(SoundType.Skill, skillName, out Sound sharedSkillSound) &&
            DispatchRaylibSound(sharedSkillSound, volume, pitch))
        {
            return;
        }

        PlayFallbackSound($"{targetName}:Skill:{skillName}", volume, pitch);
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

    public void Dispose()
    {
        foreach (var bank in _activeSoundBanks)
        {
            bank.Dispose();
        }
        _activeSoundBanks.Clear();

        _sharedSoundBank?.Dispose();
        _sharedSoundBank = null;

        _proceduralBank.Dispose();

        if (Current == this) Current = null;
    }
}
