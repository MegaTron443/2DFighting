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
        UserData.RegisterType<Projectile>();
        UserData.RegisterType<Vector2>();

        ScriptRuntime = new Script();

        ScriptRuntime.Globals["PhysicsEngine"] = UserData.CreateStatic<PhysicsEngine>();
        ScriptRuntime.Globals["NewVec2"] = (Func<float, float, Vector2>)((x, y) => new Vector2(x, y));
        ScriptRuntime.Globals["ApplyHitForce"] = (Action<Player, Player, int, float, float>)PhysicsEngine.ApplyHitForce;
        ScriptRuntime.Globals["ApplyLaunchForce"] = (Action<Player, Player, int, float, float, float>)PhysicsEngine.ApplyLaunchForce;

        ScriptRuntime.Globals["create_projectile"] = (Func<Player, Vector2, Vector2, DynValue?, DynValue?, Projectile>)ProjectileFactory.CreateShorthand;
        ScriptRuntime.Globals["CreateProjectile"] = (Func<Player, Table, Projectile>)ProjectileFactory.CreateFromTable;
    }

    public void Initialize()
    {
        string charPath = Path.Combine(RootPath, "Scripts", "characters");
        if (!Directory.Exists(charPath))
        {
            Directory.CreateDirectory(charPath);
        }
        RegisterKeysAndTrajectoriesEnvironment();
    }

    private void RegisterKeysAndTrajectoriesEnvironment()
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

            Trajectories = {
                Linear = function()
                    return function(proj, dt) end
                end,

                SineWave = function(amplitudePx, frequency)
                    return function(proj, dt)
                        local vy = math.cos(proj.ElapsedTime * frequency) * amplitudePx
                        proj:SetVelocity(proj.VelX, vy)
                    end
                end,

                Gravity = function(gravityPxPerSec, bounceFactor)
                    return function(proj, dt)
                        local vy = proj.VelY + (gravityPxPerSec * dt)
                        local groundY = proj.GroundY - (proj.Height * 0.5)
                        if proj.PosY >= groundY and vy > 0 then
                            if bounceFactor and bounceFactor > 0 then
                                vy = -vy * bounceFactor
                            else
                                proj:Destroy()
                                return
                            end
                        end
                        proj:SetVelocity(proj.VelX, vy)
                    end
                end,

                Boomerang = function(turnAroundTime, accelPx)
                    local flipped = false
                    return function(proj, dt)
                        local dir = proj.FacingRight and 1 or -1
                        local vx = proj.VelX - (dir * accelPx * dt)
                        proj:SetVelocity(vx, proj.VelY)
                        if not flipped and proj.ElapsedTime >= turnAroundTime then
                            flipped = true
                            proj:ResetHit()
                        end
                    end
                end,

                Homing = function(turnRate)
                    return function(proj, dt)
                        local target = proj.Target
                        if target == nil then return end
                        local dy = target.PosY - proj.PosY
                        local vy = proj.VelY + (dy * turnRate * dt)
                        proj:SetVelocity(proj.VelX, vy)
                    end
                end
            }
        ";
        ScriptRuntime.DoString(helperScript);
    }

    public Character CreateCharacter(string characterId)
    {
        string? resolvedPath = ModManager.ResolveCharacterScript(characterId);
        if (resolvedPath != null)
        {
            return new Character(resolvedPath, ScriptRuntime);
        }

        string fallbackPath = Path.Combine(RootPath, "Scripts", "characters", $"{characterId}.lua");
        if (!File.Exists(fallbackPath))
        {
            throw new FileNotFoundException($"Character script '{characterId}.lua' not found in Scripts/characters/ or Mods/.");
        }
        return new Character(fallbackPath, ScriptRuntime);
    }

    public void Update(float deltaTime) { }
    public void Draw() { }
}