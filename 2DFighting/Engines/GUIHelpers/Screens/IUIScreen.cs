public interface IUIScreen
{
    void Initialize();
    void Update(ButtonContext context);
    void Draw(GameData data);
    void Dispose() { }
}
