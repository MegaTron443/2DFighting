using System.Numerics;
using Raylib_cs;

Raylib.InitWindow(800, 600, "2D Fighting Game");
Raylib.SetTargetFPS(60);

PhysicsEngine physics = new PhysicsEngine();
Timer timer = new Timer();
LuaEngine lua = new LuaEngine();

physics.Initialize();
timer.Initialize();
lua.Initialize();

Player p1 = new Player("Player 1", isAI: false, playerIndex: 0, new Vector2(150, 300));
Player p2 = new Player("Player 2", isAI: false, playerIndex: 1, new Vector2(550, 300));

// Initial character assignment (or performed inside GUIEngine menus)
p1.ChangeCharacter(lua.CreateCharacter("brawler"));
p2.ChangeCharacter(lua.CreateCharacter("brawler"));
GameState.SetState(State.Playing);

while (!Raylib.WindowShouldClose())
{
    float dt = Raylib.GetFrameTime();
    timer.Update(dt);

    GameData data = new GameData(p1, p2, timer, lua);

    GUIEngine.Update(data);
    p1.HandleInput();
    p2.HandleInput();

    physics.Update(data);

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.RAYWHITE);

    Raylib.DrawLine(0, (int)PhysicsEngine.GroundY, 800, (int)PhysicsEngine.GroundY, Color.BLACK);

    p1.Draw();
    p2.Draw();
    GUIEngine.Update(data);

    Raylib.EndDrawing();
}

Raylib.CloseWindow();