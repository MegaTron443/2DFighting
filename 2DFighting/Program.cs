using Raylib_cs;

AppConfig config = AppConfig.Load("config.json");
int sw = config.Display.ScreenWidth;
int sh = config.Display.ScreenHeight;

Raylib.InitWindow(sw, sh, config.Display.Title);
Raylib.SetTargetFPS(config.Display.TargetFps);
Raylib.InitAudioDevice();
PhysicsEngine physics = new PhysicsEngine();
Timer timer = new Timer();
LuaEngine lua = new LuaEngine();

physics.Initialize();
timer.Initialize();
lua.Initialize();

ModManager.Initialize();
LuaMoveTranslator.InitializeCache(lua);
GUIEngine.Initialize();

Player? p1 = null;
Player? p2 = null;
SoundHandler? soundHandler = null;

GameState.SetState(State.MainMenu);

while (GameState.CurrentState != State.Exit && !Raylib.WindowShouldClose())
{
    float dt = Raylib.GetFrameTime();
    if ((GameState.CurrentState == State.GameOver || GameState.CurrentState == State.MainMenu) &&
        (p1 != null || p2 != null || soundHandler != null))
    {
        soundHandler?.Dispose();
        soundHandler = null;
        p1 = null;
        p2 = null;
    }

    GameData data = new GameData(p1, p2, timer, lua, config, soundHandler);

    GUIEngine.Update(ref p1, ref p2, data);
    data = new GameData(p1, p2, timer, lua, config, soundHandler);

    if (GameState.CurrentState == State.Playing && soundHandler == null)
    {
        if (p1?.CurrentCharacter != null && p2?.CurrentCharacter != null)
        {
            soundHandler = new SoundHandler(data);
            soundHandler.PlaySound(SoundType.RoundStart);
            data = new GameData(p1, p2, timer, lua, config, soundHandler);
        }
    }

    HandlePauseInput();

    if (GameState.CurrentState == State.Playing && p1 != null && p2 != null)
    {
        timer.Update(dt);
        p1.PollInput();
        p2.PollInput();

        int subSteps = PhysicsEngine.IsTrainingMode ? 1000 : 1;
        while (timer.CheckFixedTick())
        {
            for (int step = 0; step < subSteps; step++)
            {
                if (GameState.CurrentState != State.Playing) break;

                if (PhysicsEngine.HitstopTimer <= 0f)
                {
                    p1.ConsumeInput();
                    p2.ConsumeInput();
                }
                physics.Update(data);
            }
        }
    }

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.RAYWHITE);

    if (GameState.CurrentState == State.Playing && p1 != null && p2 != null)
    {
        p1.Draw(timer.Alpha);
        p2.Draw(timer.Alpha);
        PhysicsEngine.DrawProjectiles(timer.Alpha);

        if (PhysicsEngine.IsTrainingMode)
        {
            string trainText = $"Episodes Learned: {MacroTacticalModel.SharedInstance.EpisodesTrained}";
            Raylib.DrawText(trainText, 20, Raylib.GetScreenHeight() - 40, 22, Color.DARKGREEN);
        }
    }

    GUIEngine.Draw(data);
    Raylib.EndDrawing();
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


MacroTacticalModel.SharedInstance.SaveWeights();
GUIEngine.Dispose();
Character.UnloadAllSpriteSheets();
soundHandler?.Dispose();
Raylib.CloseAudioDevice();
Raylib.CloseWindow();