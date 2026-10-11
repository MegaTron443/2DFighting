using System.Numerics;
using Raylib_cs;

AppConfig config = AppConfig.Load("config.json");
int sw = config.Display.ScreenWidth;
int sh = config.Display.ScreenHeight;

Raylib.InitWindow(sw, sh, config.Display.Title);
Raylib.SetTargetFPS(config.Display.TargetFps);

PhysicsEngine physics = new PhysicsEngine();
Timer timer = new Timer();
LuaEngine lua = new LuaEngine();

physics.Initialize();
timer.Initialize();
lua.Initialize();

Player? p1 = null;
Player? p2 = null;

GameState.SetState(State.MainMenu);

while (GameState.CurrentState != State.Exit && !Raylib.WindowShouldClose())
{
    float dt = Raylib.GetFrameTime();
    timer.Update(dt);

    if (GameState.CurrentState == State.GameOver && (p1 != null || p2 != null))
    {
        p1 = null;
        p2 = null;
    }

    GameData data = new GameData(p1, p2, timer, lua, config);

    GUIEngine.Update(ref p1, ref p2, data);
    HandlePauseInput();

    if (GameState.CurrentState == State.Playing && p1 != null && p2 != null)
    {
        while (timer.CheckFixedTick())
        {
            p1.HandleInput();
            p2.HandleInput();

            physics.Update(data);
        }
    }

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.RAYWHITE);

    if (GameState.CurrentState == State.Playing && p1 != null && p2 != null)
    {
        p1.Draw();
        p2.Draw();
    }

    GUIEngine.Draw(data);

    Raylib.EndDrawing();
}

void HandlePauseInput()
{
    if (Raylib.IsKeyPressed(config.Controls.Pause))
    {
        if (GameState.CurrentState == State.Playing)
            GameState.SetState(State.Paused);
        else if (GameState.CurrentState == State.Paused)
            GameState.SetState(State.Playing);
    }
}

Raylib.CloseWindow();