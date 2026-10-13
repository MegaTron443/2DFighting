using System;
using System.IO;
using System.Numerics;
using MoonSharp.Interpreter;

[Flags]
public enum ActionButtonMask
{
    None = 0,
    KeyTop = 1 << 0,
    KeyBottom = 1 << 1,
    KeyLeft = 1 << 2,
    KeyRight = 1 << 3
}

public class LuaEngine
{
    private static readonly string RootPath = AppDomain.CurrentDomain.BaseDirectory;

    public Script ScriptRuntime { get; }

    public LuaEngine()
    {
        UserData.RegisterType<Character>();
        UserData.RegisterType<Player>();
        UserData.RegisterType<SkillData>();
        UserData.RegisterType<PhysicsEngine>();
        UserData.RegisterType<Vector2>();

        ScriptRuntime = new Script();
        ScriptRuntime.Globals["NewVec2"] = (Func<float, float, Vector2>)((x, y) => new Vector2(x, y));
        ScriptRuntime.Globals["ApplyHitForce"] = (Action<Player, Player, int, float, float>)PhysicsEngine.ApplyHitForce;
    }

    public void Initialize()
    {
        string charPath = Path.Combine(RootPath, "Scripts", "characters");
        if (!Directory.Exists(charPath))
        {
            Directory.CreateDirectory(charPath);
        }

        RegisterKeysEnvironment();
    }

    private void RegisterKeysEnvironment()
    {
        string helperScript = @"
            local meta = {}
            meta.__add = function(a, b)
                local v1 = type(a) == 'table' and a.value or a
                local v2 = type(b) == 'table' and b.value or b
                local result = { value = (v1 + v2) }
                setmetatable(result, meta)
                return result
            end

            local function makeKey(val)
                local t = { value = val }
                setmetatable(t, meta)
                return t
            end

            Keys = {
                KeyTop    = makeKey(1),   -- 1 << 0
                KeyBottom = makeKey(2),   -- 1 << 1
                KeyLeft   = makeKey(4),   -- 1 << 2
                KeyRight  = makeKey(8),   -- 1 << 3
            }
        ";
        ScriptRuntime.DoString(helperScript);
    }

    public Character CreateCharacter(string characterId)
    {
        string path = Path.Combine(RootPath, "Scripts", "characters", $"{characterId}.lua");
        if (!File.Exists(path))
        {
            path = Path.Combine(RootPath, "Scripts", "lua", $"{characterId}.lua");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Character script '{characterId}.lua' not found in Scripts/characters/ or Scripts/lua/");
        }

        return new Character(path, ScriptRuntime);
    }

    public void Update(float deltaTime) { }
    public void Draw() { }
}