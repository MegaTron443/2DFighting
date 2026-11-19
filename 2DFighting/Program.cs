using Raylib_cs;

const int TrainingModeSubSteps = 1000;

AppConfig config = AppConfig.Load("config.json");
int sw = config.Display.ScreenWidth;
int sh = config.Display.ScreenHeight;

Raylib.InitWindow(sw, sh, config.Display.Title);
Raylib.SetExitKey(KeyboardKey.KEY_NULL);
Raylib.SetTargetFPS(config.Display.TargetFps);
Raylib.InitAudioDevice();

EngineServices services = new EngineServices(config);
services.InitializeAll();

GameState.SetState(State.MainMenu);

while (GameState.CurrentState != State.Exit && !Raylib.WindowShouldClose())
{
    float dt = Raylib.GetFrameTime();
    services.CleanupMatchSession();

    GameData data = services.CreateGameData();
    Player? p1 = services.Player1;
    Player? p2 = services.Player2;

    GUIEngine.Update(ref p1, ref p2, data);
    services.Player1 = p1;
    services.Player2 = p2;

    services.EnsureMatchSoundHandler();
    data = services.CreateGameData();

    HandlePauseInput();

    bool isPlaying = GameState.CurrentState == State.Playing && p1 != null && p2 != null;
    bool isTraining = services.Match.IsTrainingMode;

    if (isPlaying)
    {
        services.Timer.Update(dt);
        p1!.PollInput();
        p2!.PollInput();

        int subSteps = isTraining ? TrainingModeSubSteps : 1;
        while (services.Timer.CheckFixedTick())
        {
            for (int step = 0; step < subSteps; step++)
            {
                if (GameState.CurrentState != State.Playing) break;

                if (services.Match.HitstopTimer <= 0f)
                {
                    p1.ConsumeInput();
                    p2.ConsumeInput();
                }
                services.Physics.Update(data);
            }
        }
    }

    float alpha = services.Timer.Alpha;

    if (isPlaying)
    {
        CameraManager.Update(dt, p1!.Position, p2!.Position, SceneManager.CurrentScene?.Bounds);
    }

    if (isPlaying && !isTraining)
    {
        PostFXManager.BeginSceneCapture();
        DrawArenaScene(p1!, p2!, services.Projectiles, alpha);
        PostFXManager.EndSceneCapture();
    }

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.RAYWHITE);

    if (isPlaying)
    {
        if (!isTraining)
        {
            PostFXManager.PresentScene();
        }
        else
        {
            DrawArenaScene(p1!, p2!, services.Projectiles, alpha);
            string trainText = $"Episodes Learned: {MacroTacticalModel.SharedInstance.EpisodesTrained}";
            Raylib.DrawText(trainText, 20, Raylib.GetScreenHeight() - 40, 22, Color.DARKGREEN);
        }
    }

    GUIEngine.Draw(data);
    Raylib.EndDrawing();
}

void DrawArenaScene(Player p1, Player p2, ProjectileManager projectiles, float alpha)
{
    CameraManager.BeginMode();

    SceneManager.DrawBackground(CameraManager.Target);

    Rectangle b = SceneManager.CurrentScene?.Bounds ?? new Rectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
    Raylib.DrawLine((int)b.x, (int)PhysicsEngine.GroundY, (int)(b.x + b.width), (int)PhysicsEngine.GroundY, Color.BLACK);

    p1.Draw(alpha);
    p2.Draw(alpha);
    projectiles.Draw(alpha);
    VFXManager.Draw();

    SceneManager.DrawForeground(CameraManager.Target);

    CameraManager.EndMode();
}

void HandlePauseInput()
{
    if (Raylib.IsKeyPressed(KeyboardKey.KEY_CAPS_LOCK))
    {
        Character.ShowDebugBoxes = !Character.ShowDebugBoxes;
    }

    if (Raylib.IsKeyPressed(config.Controls.Pause))
    {
        if (GameState.CurrentState == State.Playing)
            GameState.SetState(State.Paused);
        else if (GameState.CurrentState == State.Paused)
            GameState.SetState(State.Playing);
    }
}

services.DisposeAll();
Raylib.CloseAudioDevice();
Raylib.CloseWindow();
