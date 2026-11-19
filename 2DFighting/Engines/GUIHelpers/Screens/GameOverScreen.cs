using System.Collections.Generic;
using Raylib_cs;

public class GameOverScreen : IUIScreen
{
    private readonly List<Button> _buttons = new();

    public void Initialize()
    {
        _buttons.Clear();
        _buttons.Add(
            Button.Create("Main Menu")
                .WithSize(0.25f, 0.08f)
                .At(0.5f, 0.5f)
                .Action(_ => GameState.SetState(State.MainMenu))
        );
    }

    public void Update(ButtonContext context)
    {
        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Update(context);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.KEY_ENTER))
        {
            GameState.SetState(State.MainMenu);
        }
    }

    public void Draw(GameData data)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int titleSize = (int)(sh * 0.06f);
        int winnerSize = (int)(sh * 0.04f);
        int subSize = (int)(sh * 0.03f);

        string title = "GAME OVER";
        int titleW = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, (sw - titleW) / 2, (int)(sh * 0.24f), titleSize, Color.RED);

        string winnerText = data.Services?.Match.MatchWinnerText ?? PhysicsEngine.MatchWinnerText;
        if (!string.IsNullOrEmpty(winnerText))
        {
            int winW = Raylib.MeasureText(winnerText, winnerSize);
            Raylib.DrawText(winnerText, (sw - winW) / 2, (int)(sh * 0.33f), winnerSize, Color.DARKGREEN);
        }

        string hint = "Press Enter or Click Below to Return to Menu";
        int hintW = Raylib.MeasureText(hint, subSize);
        Raylib.DrawText(hint, (sw - hintW) / 2, (int)(sh * 0.41f), subSize, Color.DARKGRAY);

        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Draw();
        }
    }
}
