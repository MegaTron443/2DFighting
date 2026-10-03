using System;
using System.IO;
using MoonSharp.Interpreter;

public class LuaEngine
{
    private static readonly string RootPath = AppDomain.CurrentDomain.BaseDirectory;

    public Script ScriptRuntime { get; }

    public LuaEngine()
    {
        UserData.RegisterType<Character>();
        UserData.RegisterType<Player>();
        UserData.RegisterType<SkillData>();

        ScriptRuntime = new Script();
    }

    public void Initialize()
    {
        string charPath = Path.Combine(RootPath, "Scripts", "characters");
        if (!Directory.Exists(charPath))
        {
            Directory.CreateDirectory(charPath);
        }
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