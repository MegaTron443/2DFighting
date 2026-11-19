using System.Collections.Generic;
using Raylib_cs;

public class PauseOverlay : IUIScreen
{
    private readonly List<Button> _buttons = new();

    public void Initialize()
    {
        _buttons.Clear();
        _buttons.AddRange(new[]
        {
            Button.Create("Resume")
                .WithSize(0.25f, 0.08f)
                .At(0.5f, 0.38f)
                .Action(_ => GameState.SetState(State.Playing)),

            Button.Create("Settings")
                .WithSize(0.25f, 0.08f)
                .At(0.5f, 0.48f)
                .Action(_ => GameState.SetState(State.Settings)),

            Button.Create("Quit to Menu")
                .WithSize(0.25f, 0.08f)
                .At(0.5f, 0.58f)
                .Action(_ => GameState.SetState(State.MainMenu))
        });
    }

    public void Update(ButtonContext context)
    {
        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Update(context);
        }
    }

    public void Draw(GameData data)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int titleSize = (int)(sh * 0.05f);
        int subSize = (int)(sh * 0.03f);

        string title = "Game Paused";
        int titleW = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, (sw - titleW) / 2, (int)(sh * 0.25f), titleSize, Color.BLACK);

        string hint = $"Press {data.AppConfig.Controls.Pause} to resume";
        int hintW = Raylib.MeasureText(hint, subSize);
        Raylib.DrawText(hint, (sw - hintW) / 2, (int)(sh * 0.34f), subSize, Color.DARKGRAY);

        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Draw();
        }
    }
}
