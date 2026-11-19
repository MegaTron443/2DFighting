public interface IUIScreen
{
    void Initialize();
    void Update(ButtonContext context);
    void DrawWorld(GameData data) { }
    void Draw(GameData data);
    void Dispose() { }
}
