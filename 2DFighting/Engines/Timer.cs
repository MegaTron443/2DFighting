public class Timer
{
    public const float FixedDeltaTime = 1f / 60f;
    private const float MaxAccumulator = 0.25f;

    private float _deltaTime;
    private float _elapsedTime;
    private float _accumulator;

    public float DeltaTime => _deltaTime;
    public float ElapsedTime => _elapsedTime;

    public void Initialize()
    {
        _deltaTime = 0f;
        _elapsedTime = 0f;
        _accumulator = 0f;
    }

    public void Update(float variableDeltaTime)
    {
        _deltaTime = variableDeltaTime;
        _elapsedTime += variableDeltaTime;

        _accumulator += MathF.Min(variableDeltaTime, MaxAccumulator);
    }

    public bool CheckFixedTick()
    {
        if (_accumulator >= FixedDeltaTime)
        {
            _accumulator -= FixedDeltaTime;
            return true;
        }
        return false;
    }
}