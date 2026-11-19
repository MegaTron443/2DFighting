using System;
using System.Collections.Generic;
using Raylib_cs;

public class ProceduralSoundBank : IDisposable
{
    private const int SampleRate = 44100;
    private const float AttackFraction = 0.05f;

    private readonly Dictionary<string, Sound> _sounds = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsSoundValid(Sound sound)
    {
        return Raylib.IsAudioDeviceReady() && sound.frameCount > 0 && sound.stream.sampleRate > 0;
    }

    public void GenerateDefaults()
    {
        if (!Raylib.IsAudioDeviceReady())
        {
            DiagnosticsLogger.LogSilentIssueOnce(
                "audio_device_not_ready",
                "Audio device is not ready; procedural sounds were not generated.",
                LogSeverity.Warning
            );
            return;
        }

        Register("default",            CreateSineSweepSound(startHz: 420f,    endHz: 260f,  durationSec: 0.10f, volume: 0.50f));
        Register("cosmic_sub_drop",    CreateSineSweepSound(startHz: 68f,     endHz: 32f,   durationSec: 0.45f, volume: 0.95f));
        Register("cosmic_heavy_slash", CreateSineSweepSound(startHz: 140f,    endHz: 42f,   durationSec: 0.25f, volume: 0.85f));
        Register("cosmic_light_slash", CreateSineSweepSound(startHz: 880f,    endHz: 1480f, durationSec: 0.12f, volume: 0.65f));
        Register("cosmic_recall",      CreateSineSweepSound(startHz: 180f,    endHz: 620f,  durationSec: 0.22f, volume: 0.80f));
        Register("prism_chime",        CreateSineSweepSound(startHz: 1046.5f, endHz: 2093f, durationSec: 0.30f, volume: 0.60f));
    }

    private void Register(string name, Sound sound)
    {
        if (IsSoundValid(sound))
        {
            _sounds[name] = sound;
        }
        else
        {
            DiagnosticsLogger.LogSilentIssueOnce(
                $"proc_sound_failed_{name}",
                $"Failed to generate procedural sound '{name}'.",
                LogSeverity.Error
            );
        }
    }

    public static unsafe Sound CreateSineSweepSound(float startHz, float endHz, float durationSec, float volume)
    {
        int sampleCount = Math.Max(1, (int)(SampleRate * durationSec));
        short* pcm = (short*)Raylib.MemAlloc((int)(sampleCount * sizeof(short)));

        float phase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float freq = startHz * MathF.Pow(endHz / startHz, t);
            phase += (MathF.Tau * freq) / SampleRate;

            float attack = Math.Clamp(t / AttackFraction, 0f, 1f);
            float decay = MathF.Cos(t * MathF.PI * 0.5f);
            float env = attack * decay * volume;

            pcm[i] = (short)(MathF.Sin(phase) * env * short.MaxValue);
        }

        Wave wave = new Wave
        {
            sampleCount = (uint)sampleCount,
            sampleRate = SampleRate,
            sampleSize = 16,
            channels = 1,
            data = pcm
        };

        Sound sound = Raylib.LoadSoundFromWave(wave);
        Raylib.UnloadWave(wave);
        return sound;
    }

    public bool TryGet(string name, out Sound sound)
    {
        if (_sounds.TryGetValue(name, out sound) && IsSoundValid(sound))
            return true;

        sound = default;
        return false;
    }

    public void Dispose()
    {
        foreach (var proc in _sounds.Values)
        {
            if (IsSoundValid(proc))
            {
                Raylib.UnloadSound(proc);
            }
        }
        _sounds.Clear();
    }
}
