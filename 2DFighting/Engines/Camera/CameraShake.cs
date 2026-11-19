using System.Numerics;

public class CameraShake
{
    private float _intensity;
    private float _duration;
    private float _timer;
    private readonly Random _rng = new();

    public Vector2 CurrentOffset { get; private set; }

    public void Trigger(float intensity, float duration)
    {
        if (intensity >= _intensity || _timer <= 0f)
        {
            _intensity = intensity;
            _duration = MathF.Max(duration, 0.001f);
            _timer = _duration;
        }
    }

    public void Update(float dt)
    {
        if (_timer <= 0f)
        {
            CurrentOffset = Vector2.Zero;
            return;
        }

        _timer = MathF.Max(0f, _timer - dt);
        float decay = _timer / _duration;
        float mag = _intensity * decay * decay;

        CurrentOffset = new Vector2(
            ((float)_rng.NextDouble() * 2f - 1f) * mag,
            ((float)_rng.NextDouble() * 2f - 1f) * mag
        );
    }
}