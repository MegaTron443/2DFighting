public class PlayingHudScreen : IUIScreen
{
    public void Initialize() { }

    public void Update(ButtonContext context) { }

    public void Draw(GameData data)
    {
        CombatHUD.Draw(data);
    }
}
