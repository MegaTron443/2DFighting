public class SettingsScreen : IUIScreen
{
    public void Initialize() => SettingsMenu.Initialize();

    public void Update(ButtonContext context) => SettingsMenu.Update(context);

    public void Draw(GameData data) => SettingsMenu.Draw(data);
}
