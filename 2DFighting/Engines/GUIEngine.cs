using Raylib_cs;
public static class GUIEngine
{
    public static void Update(GameData data)
    {
        if (GameState.CurrentState == State.MainMenu)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.KEY_ONE))
            {
                Character brawler = data.LuaEngine.CreateCharacter("brawler");
                data.Player1.ChangeCharacter(brawler);
            }

            if (Raylib.IsKeyPressed(KeyboardKey.KEY_TWO))
            {
                Character ninja = data.LuaEngine.CreateCharacter("ninja");
                data.Player2.ChangeCharacter(ninja);
            }

            if (data.Player1.CurrentCharacter != null && data.Player2.CurrentCharacter != null)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.KEY_ENTER))
                {
                    GameState.SetState(State.Playing);
                }
            }
        }
    }
}