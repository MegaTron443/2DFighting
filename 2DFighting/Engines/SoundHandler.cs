using MoonSharp.Interpreter;
using Raylib_cs;

public enum AudioBusType
{
    Bgm,
    SfxWorld,
    SfxCharacter
}

public class AudioBusState
{
    public float BaseVolume { get; set; } = 1.0f;
    public float DuckMultiplier { get; set; } = 1.0f;
    public float TargetDuck { get; set; } = 1.0f;
    public int HoldFrames { get; set; } = 0;
    public float RecoverySpeed { get; set; } = 2.0f;
    public float LowPassCutoffHz { get; set; } = 20000f;

    public float EffectiveVolume => Math.Clamp(BaseVolume * DuckMultiplier, 0f, 1f);
}

[MoonSharpUserData]
public class SoundHandler : IDisposable
{
    public static SoundHandler? Current { get; private set; }

    private float _masterVolume = 1.0f;
    private readonly List<Character_Sounds> _activeSoundBanks = new();
    private Character_Sounds? _sharedSoundBank;

    private readonly Dictionary<string, AudioBusState> _buses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bgm"] = new AudioBusState(),
        ["sfx_world"] = new AudioBusState(),
        ["sfx_character"] = new AudioBusState()
    };

    private readonly Dictionary<string, Sound> _proceduralSounds = new(StringComparer.OrdinalIgnoreCase);

    public float Volume
    {
        get => _masterVolume;
        set => _masterVolume = Math.Clamp(value, 0f, 1f);
    }

    public SoundHandler(GameData data)
    {
        _masterVolume = Math.Clamp(data.AppConfig.Sound.Volume / 100f, 0f, 1f);
        LoadSharedSoundBanks();
        GenerateProceduralCosmicSounds();

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

    private void GenerateProceduralCosmicSounds()
    {
        _proceduralSounds["cosmic_sub_drop"] = CreateSineSweepSound(startHz: 68f, endHz: 32f, durationSec: 0.45f, volume: 0.95f);
        _proceduralSounds["cosmic_heavy_slash"] = CreateSineSweepSound(startHz: 140f, endHz: 42f, durationSec: 0.25f, volume: 0.85f);
        _proceduralSounds["cosmic_light_slash"] = CreateSineSweepSound(startHz: 880f, endHz: 1480f, durationSec: 0.12f, volume: 0.65f);
        _proceduralSounds["cosmic_recall"] = CreateSineSweepSound(startHz: 180f, endHz: 620f, durationSec: 0.22f, volume: 0.80f);
        _proceduralSounds["prism_chime"] = CreateSineSweepSound(startHz: 1046.5f, endHz: 2093f, durationSec: 0.30f, volume: 0.60f);
    }

    private static unsafe Sound CreateSineSweepSound(float startHz, float endHz, float durationSec, float volume)
    {
        const int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * durationSec);
        short* pcm = (short*)Raylib.MemAlloc((int)(sampleCount * sizeof(short)));

        float phase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float freq = startHz * MathF.Pow(endHz / startHz, t);
            phase += (MathF.Tau * freq) / sampleRate;

            float attack = Math.Clamp(t / 0.05f, 0f, 1f);
            float decay = MathF.Cos(t * MathF.PI * 0.5f);
            float env = attack * decay * volume;

            pcm[i] = (short)(MathF.Sin(phase) * env * short.MaxValue);
        }

        Wave wave = new Wave
        {
            sampleRate = sampleRate,
            sampleSize = 16,
            channels = 1,
            data = pcm
        };

        Sound sound = Raylib.LoadSoundFromWave(wave);
        Raylib.UnloadWave(wave);
        return sound;
    }

    public void UpdateTick(float dt)
    {
        foreach (var bus in _buses.Values)
        {
            if (bus.HoldFrames > 0)
            {
                bus.HoldFrames--;
                bus.DuckMultiplier = bus.TargetDuck;
            }
            else if (bus.DuckMultiplier < 1.0f)
            {
                bus.DuckMultiplier = Math.Min(1.0f, bus.DuckMultiplier + (bus.RecoverySpeed * dt));
                bus.LowPassCutoffHz = Math.Min(20000f, bus.LowPassCutoffHz + (18000f * bus.RecoverySpeed * dt));
            }
        }
    }

    public float GetBusEffectiveVolume(string busName)
    {
        if (_buses.TryGetValue(busName, out var bus))
            return _masterVolume * bus.EffectiveVolume;
        return _masterVolume;
    }

    public static void SetBusVolumeFromLua(string busName, float volume)
    {
        if (Current == null) return;
        if (Current._buses.TryGetValue(busName, out var bus))
        {
            bus.BaseVolume = Math.Clamp(volume, 0f, 1f);
        }
    }

    public static void SetBusLowPassFromLua(string busName, float cutoffHz)
    {
        if (Current == null) return;
        if (Current._buses.TryGetValue(busName, out var bus))
        {
            bus.LowPassCutoffHz = Math.Clamp(cutoffHz, 60f, 20000f);
            // Approximation in standard Raylib: lower high-frequency energy by scaling duck slightly when muffled
            float muffleFactor = Math.Clamp(0.35f + (0.65f * (bus.LowPassCutoffHz / 20000f)), 0.1f, 1f);
            bus.DuckMultiplier = Math.Min(bus.DuckMultiplier, muffleFactor);
        }
    }

    public static void TriggerDuckFromLua(float targetGain = 0.05f, int holdFrames = 10, float recoverySeconds = 0.6f)
    {
        if (Current == null || PhysicsEngine.IsTrainingMode) return;

        float speed = recoverySeconds > 0.01f ? (1f - targetGain) / recoverySeconds : 10f;
        string[] duckedBuses = { "bgm", "sfx_world" };

        foreach (string b in duckedBuses)
        {
            if (Current._buses.TryGetValue(b, out var bus))
            {
                bus.TargetDuck = Math.Clamp(targetGain, 0f, 1f);
                bus.DuckMultiplier = bus.TargetDuck;
                bus.HoldFrames = Math.Max(bus.HoldFrames, holdFrames);
                bus.RecoverySpeed = speed;
            }
        }
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

        if (string.IsNullOrEmpty(soundName)) return;

        Current.PlayEx(player, soundName, bus, pitch, volumeMult);
    }

    public void PlayEx(Player? player, string soundNameOrType, string busName = "sfx_character", float pitch = 1.0f, float volumeMult = 1.0f)
    {
        if (PhysicsEngine.IsTrainingMode || _masterVolume <= 0f) return;

        float finalVolume = Math.Clamp(GetBusEffectiveVolume(busName) * volumeMult, 0f, 1f);
        float finalPitch = Math.Clamp(pitch, 0.2f, 3.0f);

        if (_proceduralSounds.TryGetValue(soundNameOrType, out Sound procSound))
        {
            DispatchRaylibSound(procSound, finalVolume, finalPitch);
            return;
        }

        if (Enum.TryParse<SoundType>(soundNameOrType, ignoreCase: true, out var parsedType) &&
            parsedType != SoundType.Skill)
        {
            PlaySoundInternal(player, parsedType, finalVolume, finalPitch);
            return;
        }

        PlaySkillSoundInternal(player, soundNameOrType, finalVolume, finalPitch);
    }

    private static void DispatchRaylibSound(Sound sound, float volume, float pitch)
    {
        Raylib.SetSoundVolume(sound, volume);
        Raylib.SetSoundPitch(sound, pitch);
        Raylib.PlaySound(sound);
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
                    _activeSoundBanks[i].TryGetSound(soundType, out Sound sound))
                {
                    DispatchRaylibSound(sound, volume, pitch);
                    return;
                }
            }

            if (_sharedSoundBank != null && _sharedSoundBank.TryGetSound(soundType, out Sound sharedSound))
            {
                DispatchRaylibSound(sharedSound, volume, pitch);
                return;
            }
            return;
        }

        if (_sharedSoundBank != null && _sharedSoundBank.TryGetSound(soundType, out Sound globalShared))
        {
            DispatchRaylibSound(globalShared, volume, pitch);
            return;
        }

        for (int i = 0; i < _activeSoundBanks.Count; i++)
        {
            if (_activeSoundBanks[i].TryGetSound(soundType, out Sound charSound))
            {
                DispatchRaylibSound(charSound, volume, pitch);
                return;
            }
        }
    }

    private void PlaySkillSoundInternal(Player? player, string skillName, float volume, float pitch)
    {
        if (PhysicsEngine.IsTrainingMode || volume <= 0f || player?.CurrentCharacter == null) return;

        string targetName = player.CurrentCharacter.Name;
        for (int i = 0; i < _activeSoundBanks.Count; i++)
        {
            if (_activeSoundBanks[i].CharacterName == targetName &&
                _activeSoundBanks[i].TryGetSound(SoundType.Skill, skillName, out Sound sound))
            {
                DispatchRaylibSound(sound, volume, pitch);
                return;
            }
        }

        if (_sharedSoundBank != null && _sharedSoundBank.TryGetSound(SoundType.Skill, skillName, out Sound sharedSkillSound))
        {
            DispatchRaylibSound(sharedSkillSound, volume, pitch);
        }
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

        foreach (var proc in _proceduralSounds.Values)
        {
            Raylib.UnloadSound(proc);
        }
        _proceduralSounds.Clear();

        if (Current == this) Current = null;
    }
}