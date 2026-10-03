public class Timer
{
    private float _deltaTime;
    private float _elapsedTime;

    public float DeltaTime => _deltaTime;
    public float ElapsedTime => _elapsedTime;

    public void Initialize()
    {
        _deltaTime = 0f;
        _elapsedTime = 0f;
    }

    public void Update(float deltaTime)
    {
        _deltaTime = deltaTime;
        _elapsedTime += deltaTime;
    }
}