using Raylib_cs;

const int TrainingModeSubSteps = 1000;

AppConfig config = AppConfig.Load("config.json");
int sw = config.Display.ScreenWidth;
int sh = config.Display.ScreenHeight;

Raylib.SetConfigFlags(ConfigFlags.FLAG_MSAA_4X_HINT);
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
    SteamManager.RunCallbacks();
    services.OnlineSession.PollNonMatchTraffic(services);
    services.SyncSessionForCurrentState();

    GameData data = services.CreateGameData();
    Player? p1 = services.Player1;
    Player? p2 = services.Player2;

    GUIEngine.Update(ref p1, ref p2, data);
    services.Player1 = p1;
    services.Player2 = p2;

    services.EnsureMatchSoundHandler();
    data = services.CreateGameData();

    HandlePauseInput();

    bool isLiveArena = GameState.IsLiveArena && p1 != null && p2 != null;
    bool isOnlinePlaying = services.OnlineSession.IsActive && GameState.CurrentState == State.Playing;
    bool isTraining = !isOnlinePlaying && GameState.CurrentState == State.Playing && services.Match.IsTrainingMode;

    if (isLiveArena)
    {
        services.Timer.Update(dt);

        if (isOnlinePlaying)
        {
            services.OnlineSession.SampleHardwareEdgesForRenderFrame(services);
            while (services.Timer.CheckFixedTick())
            {
                if (!GameState.IsLiveArena || !services.OnlineSession.IsActive) break;
                services.OnlineSession.StepFixedTick(data);
            }
        }
        else
        {
            p1!.PollInput();
            p2!.PollInput();

            int subSteps = isTraining ? TrainingModeSubSteps : 1;
            while (services.Timer.CheckFixedTick())
            {
                for (int step = 0; step < subSteps; step++)
                {
                    if (!GameState.IsLiveArena) break;

                    if (services.Match.HitstopTimer <= 0f)
                    {
                        p1.ConsumeInput();
                        p2.ConsumeInput();
                    }
                    services.Physics.Update(data);
                }
            }
        }
    }

    float alpha = services.Timer.Alpha;

    if (isLiveArena)
    {
        if (GameState.CurrentState == State.MainMenu)
        {
            CameraManager.UpdateLobby(dt, p1!.Position, SceneManager.CurrentScene);
        }
        else
        {
            CameraManager.Update(dt, p1!.Position, p2!.Position, SceneManager.CurrentScene?.Bounds);
        }
    }

    ShaderManager.BeginFrame();

    bool usePostFx = isLiveArena && !isTraining && PostFXManager.HasActiveEffects;
    if (usePostFx)
    {
        PostFXManager.BeginSceneCapture();
        DrawArenaScene(p1!, p2!, services.Projectiles, data, alpha);
        PostFXManager.EndSceneCapture();
    }

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.RAYWHITE);

    if (isLiveArena)
    {
        if (usePostFx)
        {
            PostFXManager.PresentScene();
        }
        else
        {
            DrawArenaScene(p1!, p2!, services.Projectiles, data, alpha);
            if (isTraining)
            {
                string trainText = $"Episodes Learned: {MacroTacticalModel.SharedInstance.EpisodesTrained}";
                Raylib.DrawText(trainText, 20, Raylib.GetScreenHeight() - 40, 22, Color.DARKGREEN);
            }
        }
    }

    GUIEngine.Draw(data);
    ShaderManager.EndShader();
    Raylib.EndDrawing();
}

void DrawArenaScene(Player p1, Player p2, ProjectileManager projectiles, GameData data, float alpha)
{
    CameraManager.BeginMode();

    SceneManager.DrawBackground(CameraManager.Target);

    Rectangle b = SceneManager.CurrentScene?.Bounds ?? new Rectangle(0, 0, PhysicsEngine.LogicalWidth, PhysicsEngine.LogicalHeight);
    Raylib.DrawLine((int)b.x, (int)PhysicsEngine.GroundY, (int)(b.x + b.width), (int)PhysicsEngine.GroundY, new Color(120, 130, 175, 90));

    GUIEngine.DrawWorld(data);
    SceneManager.DrawPlatforms();

    p1.Draw(alpha);
    p2.Draw(alpha);
    projectiles.Draw(alpha);
    VFXManager.Draw();

    SceneManager.DrawForeground(CameraManager.Target);

    ShaderManager.EndShader();
    CameraManager.EndMode();
}

void HandlePauseInput()
{
    if (Raylib.IsKeyPressed(KeyboardKey.KEY_CAPS_LOCK))
    {
        Character.ShowDebugBoxes = !Character.ShowDebugBoxes;
    }

    if (Raylib.IsKeyPressed(config.Controls.Pause) && !services.OnlineSession.IsActive)
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
