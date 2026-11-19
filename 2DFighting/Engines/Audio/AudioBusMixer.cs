using System;
using System.Collections.Generic;

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

public class AudioBusMixer
{
    private const float MinLowPassHz = 60f;
    private const float MaxLowPassHz = 20000f;
    private const float LowPassRecoveryHzPerSec = 18000f;
    private static readonly string[] DuckedBuses = { "bgm", "sfx_world" };

    private float _masterVolume = 1.0f;
    private readonly Dictionary<string, AudioBusState> _buses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bgm"] = new AudioBusState(),
        ["sfx_world"] = new AudioBusState(),
        ["sfx_character"] = new AudioBusState()
    };

    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Math.Clamp(value, 0f, 1f);
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
                bus.LowPassCutoffHz = Math.Min(MaxLowPassHz, bus.LowPassCutoffHz + (LowPassRecoveryHzPerSec * dt));
            }
        }
    }

    public float GetEffectiveVolume(string busName)
    {
        if (_buses.TryGetValue(busName, out var bus))
            return _masterVolume * bus.EffectiveVolume;

        DiagnosticsLogger.LogSilentIssueOnce(
            $"invalid_audio_bus_{busName}",
            $"Unknown audio bus '{busName}'; defaulting to master volume.",
            LogSeverity.Warning
        );
        return _masterVolume;
    }

    public void SetBusVolume(string busName, float volume)
    {
        if (_buses.TryGetValue(busName, out var bus))
        {
            bus.BaseVolume = Math.Clamp(volume, 0f, 1f);
        }
        else
        {
            DiagnosticsLogger.LogSilentIssueOnce(
                $"set_invalid_bus_vol_{busName}",
                $"SetAudioBusVolume called with unknown bus '{busName}'.",
                LogSeverity.Warning
            );
        }
    }

    public void SetBusLowPass(string busName, float cutoffHz)
    {
        if (_buses.TryGetValue(busName, out var bus))
        {
            bus.LowPassCutoffHz = Math.Clamp(cutoffHz, MinLowPassHz, MaxLowPassHz);
            float muffleFactor = Math.Clamp(0.35f + (0.65f * (bus.LowPassCutoffHz / MaxLowPassHz)), 0.1f, 1f);
            bus.DuckMultiplier = Math.Min(bus.DuckMultiplier, muffleFactor);
        }
    }

    public void TriggerDuck(float targetGain = 0.05f, int holdFrames = 10, float recoverySeconds = 0.6f)
    {
        float speed = recoverySeconds > 0.01f ? (1f - targetGain) / recoverySeconds : 10f;

        foreach (string b in DuckedBuses)
        {
            if (_buses.TryGetValue(b, out var bus))
            {
                bus.TargetDuck = Math.Clamp(targetGain, 0f, 1f);
                bus.DuckMultiplier = bus.TargetDuck;
                bus.HoldFrames = Math.Max(bus.HoldFrames, holdFrames);
                bus.RecoverySpeed = speed;
            }
        }
    }
}
