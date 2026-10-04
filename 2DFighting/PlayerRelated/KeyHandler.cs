using Raylib_cs;
public class KeyHandler
{
    private AppConfig _appConfig;

    public KeyHandler(AppConfig appConfig)
    {
        _appConfig = appConfig;
    }

    public void HandleInput(Character CurrentCharacter, Player player, int playerIndex)
    {
        if (CurrentCharacter == null) return;

        PlayerControls controls = playerIndex == 0 ? _appConfig.Controls.P1 : _appConfig.Controls.P2;

        float dir = 0;
        if (Raylib.IsKeyDown(controls.Right)) dir += 1;
        if (Raylib.IsKeyDown(controls.Left)) dir -= 1;

        CurrentCharacter.Move(player, dir);
        if (Raylib.IsKeyPressed(controls.Jump)) CurrentCharacter.Jump(player);
        if (Raylib.IsKeyPressed(controls.LightPunch)) CurrentCharacter.ExecuteSkill(player, "light_punch");
        if (Raylib.IsKeyPressed(controls.HeavyKick)) CurrentCharacter.ExecuteSkill(player, "heavy_kick");
    }
}