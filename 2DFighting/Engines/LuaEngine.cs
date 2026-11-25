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
    private static readonly object _userDataLock = new();
    private static bool _typesRegistered = false;

    public Script ScriptRuntime { get; }
    private readonly Dictionary<string, Character> _characterCache = new(StringComparer.OrdinalIgnoreCase);

    private static void EnsureTypesRegistered()
    {
        if (_typesRegistered) return;
        lock (_userDataLock)
        {
            if (_typesRegistered) return;
            UserData.RegisterType<Character>();
            UserData.RegisterType<Player>();
            UserData.RegisterType<SkillData>();
            UserData.RegisterType<LuaFloatStore>();
            UserData.RegisterType<PhysicsEngine>();
            UserData.RegisterType<Projectile>();
            UserData.RegisterType<Vector2>();
            UserData.RegisterType<RenderMaterial>();
            UserData.RegisterType(typeof(PostFXManager));
            UserData.RegisterType<Scene>();
            UserData.RegisterType<SceneLayer>();
            _typesRegistered = true;
        }
    }

    public LuaEngine(bool isHeadless = false)
    {
        EnsureTypesRegistered();

        ScriptRuntime = new Script();

        ScriptRuntime.Globals["PhysicsEngine"] = UserData.CreateStatic<PhysicsEngine>();
        ScriptRuntime.Globals["NewVec2"] = (Func<float, float, Vector2>)((x, y) => new Vector2(x, y));
        ScriptRuntime.Globals["ApplyHitForce"] = (Action<Player, Player, int, float, float>)PhysicsEngine.ApplyHitForce;
        ScriptRuntime.Globals["ApplyLaunchForce"] = (Action<Player, Player, int, float, float, float>)PhysicsEngine.ApplyLaunchForce;
        ScriptRuntime.Globals["create_projectile"] = (Func<Player, Vector2, Vector2, DynValue?, DynValue?, Projectile>)ProjectileFactory.CreateShorthand;
        ScriptRuntime.Globals["CreateProjectile"] = (Func<Player, Table, Projectile>)ProjectileFactory.CreateFromTable;

        if (isHeadless)
        {
            ScriptRuntime.Globals["PlaySound"] = (Action<Player?, DynValue>)((_, _) => { });
            ScriptRuntime.Globals["TriggerAudioDuck"] = (Action<float, int, float>)((_, _, _) => { });
            ScriptRuntime.Globals["SetAudioBusVolume"] = (Action<string, float>)((_, _) => { });
            ScriptRuntime.Globals["SetAudioBusLowPass"] = (Action<string, float>)((_, _) => { });
            ScriptRuntime.Globals["Log"] = (Action<DynValue>)(_ => { });
            ScriptRuntime.Globals["EnablePostFX"] = (Action<string, int>)((_, _) => { });
            ScriptRuntime.Globals["DisablePostFX"] = (Action<string>)(_ => { });
            ScriptRuntime.Globals["SetPostFXFloat"] = (Action<string, string, float>)((_, _, _) => { });
            ScriptRuntime.Globals["SetPostFXVec2"] = (Action<string, string, float, float>)((_, _, _, _) => { });
            ScriptRuntime.Globals["SetPostFXColor"] = (Action<string, string, float, float, float>)((_, _, _, _, _) => { });
            ScriptRuntime.Globals["TriggerScreenShake"] = (Action<float, float, int>)((_, _, _) => { });
            ScriptRuntime.Globals["TriggerImpactFrame"] = (Action<int, int>)((_, _) => { });
            ScriptRuntime.Globals["TriggerPaletteShift"] = (Action<float, float, float, float, int, float>)((_, _, _, _, _, _) => { });
            ScriptRuntime.Globals["TriggerGravityPinch"] = (Action<float, int, float, float, float>)((_, _, _, _, _) => { });
            ScriptRuntime.Globals["SetHitstop"] = (Action<float>)(_ => { });
            ScriptRuntime.Globals["SpawnVFX"] = (Action<Player, Table>)((_, _) => { });
            ScriptRuntime.Globals["AddSceneLayer"] = (Func<string, string?, string?, bool, SceneLayer?>)((_, _, _, _) => null);
            ScriptRuntime.Globals["GetSceneLayer"] = (Func<string, SceneLayer?>)(_ => null);
            ScriptRuntime.Globals["SetSceneLayerTexture"] = (Action<string, string>)((_, _) => { });
            ScriptRuntime.Globals["SetSceneLayerShader"] = (Action<string, string>)((_, _) => { });
            ScriptRuntime.Globals["SetSceneLayerFloat"] = (Action<string, string, float>)((_, _, _) => { });
            ScriptRuntime.Globals["SetSceneLayerVec2"] = (Action<string, string, float, float>)((_, _, _, _) => { });
            ScriptRuntime.Globals["SetSceneLayerColor"] = (Action<string, string, float, float, float>)((_, _, _, _, _) => { });
            ScriptRuntime.Globals["SetSceneLayerTint"] = (Action<string, int, int, int, int>)((_, _, _, _, _) => { });
            ScriptRuntime.Globals["TriggerCameraFocus"] = (Action<float, float, float, float>)((_, _, _, _) => { });
            ScriptRuntime.Globals["ClearCameraFocus"] = (Action)(() => { });
            ScriptRuntime.Globals["SetCameraZoomLimits"] = (Action<float, float>)((_, _) => { });
            ScriptRuntime.Globals["GetCameraZoom"] = (Func<float>)(() => 1f);
        }
        else
        {
            ScriptRuntime.Globals["PlaySound"] = (Action<Player?, DynValue>)SoundHandler.PlayFromLua;
            ScriptRuntime.Globals["TriggerAudioDuck"] = (Action<float, int, float>)SoundHandler.TriggerDuckFromLua;
            ScriptRuntime.Globals["SetAudioBusVolume"] = (Action<string, float>)SoundHandler.SetBusVolumeFromLua;
            ScriptRuntime.Globals["SetAudioBusLowPass"] = (Action<string, float>)SoundHandler.SetBusLowPassFromLua;

            ScriptRuntime.Globals["Log"] = (Action<DynValue>)(msg =>
            {
                DiagnosticsLogger.LogSilentIssueOnce(
                    $"lua_log_{Guid.NewGuid()}",
                    msg.ToPrintString(),
                    LogSeverity.Info
                );
            });

            ScriptRuntime.Globals["EnablePostFX"] = (Action<string, int>)PostFXManager.EnablePassFromLua;
            ScriptRuntime.Globals["DisablePostFX"] = (Action<string>)PostFXManager.DisablePassFromLua;
            ScriptRuntime.Globals["SetPostFXFloat"] = (Action<string, string, float>)PostFXManager.SetUniformFloatFromLua;
            ScriptRuntime.Globals["SetPostFXVec2"] = (Action<string, string, float, float>)PostFXManager.SetUniformVec2FromLua;
            ScriptRuntime.Globals["SetPostFXColor"] = (Action<string, string, float, float, float>)PostFXManager.SetUniformColorFromLua;
            ScriptRuntime.Globals["TriggerScreenShake"] = (Action<float, float, int>)PostFXManager.TriggerScreenShakeFromLua;
            ScriptRuntime.Globals["TriggerImpactFrame"] = (Action<int, int>)PostFXManager.TriggerImpactFrameFromLua;
            ScriptRuntime.Globals["TriggerPaletteShift"] = (Action<float, float, float, float, int, float>)PostFXManager.TriggerPaletteShiftFromLua;
            ScriptRuntime.Globals["TriggerGravityPinch"] = (Action<float, int, float, float, float>)PostFXManager.TriggerGravityPinchFromLua;
            ScriptRuntime.Globals["SetHitstop"] = (Action<float>)(frames => PhysicsEngine.HitstopTimer = MathF.Max(PhysicsEngine.HitstopTimer, frames * Timer.FixedDeltaTime));

            ScriptRuntime.Globals["SpawnVFX"] = (Action<Player, Table>)VFXManager.SpawnFromLua;

            ScriptRuntime.Globals["AddSceneLayer"] = (Func<string, string?, string?, bool, SceneLayer?>)SceneManager.AddLayerFromLua;
            ScriptRuntime.Globals["GetSceneLayer"] = (Func<string, SceneLayer?>)SceneManager.GetLayerFromLua;
            ScriptRuntime.Globals["SetSceneLayerTexture"] = (Action<string, string>)SceneManager.SetLayerTextureFromLua;
            ScriptRuntime.Globals["SetSceneLayerShader"] = (Action<string, string>)SceneManager.SetLayerShaderFromLua;
            ScriptRuntime.Globals["SetSceneLayerFloat"] = (Action<string, string, float>)SceneManager.SetLayerFloatFromLua;
            ScriptRuntime.Globals["SetSceneLayerVec2"] = (Action<string, string, float, float>)SceneManager.SetLayerVec2FromLua;
            ScriptRuntime.Globals["SetSceneLayerColor"] = (Action<string, string, float, float, float>)SceneManager.SetLayerColorFromLua;
            ScriptRuntime.Globals["SetSceneLayerTint"] = (Action<string, int, int, int, int>)SceneManager.SetLayerTintFromLua;

            ScriptRuntime.Globals["TriggerCameraFocus"] = (Action<float, float, float, float>)CameraManager.TriggerFocusFromLua;
            ScriptRuntime.Globals["ClearCameraFocus"] = (Action)CameraManager.ClearFocusFromLua;
            ScriptRuntime.Globals["SetCameraZoomLimits"] = (Action<float, float>)CameraManager.SetZoomLimitsFromLua;
            ScriptRuntime.Globals["GetCameraZoom"] = (Func<float>)CameraManager.GetZoomFromLua;
        }

        ScriptRuntime.Globals["SetHitbox"] = (Action<SkillData, float, float, float, float>)((skill, xOffsetPct, yOffsetPct, widthPct, heightPct) =>
        {
            skill.SetHitbox(xOffsetPct, yOffsetPct, widthPct, heightPct);
        });

        ScriptRuntime.Globals["__det_random"] = (Func<DynValue, DynValue, double>)((arg1, arg2) =>
        {
            if (arg1.IsNil())
                return PhysicsEngine.NextRandomDouble();

            int first = (int)Math.Floor(arg1.CastToNumber() ?? 1.0);
            if (arg2.IsNil())
            {
                if (first < 1) return 1.0;
                return PhysicsEngine.NextRandomRange(1, first);
            }

            int second = (int)Math.Floor(arg2.CastToNumber() ?? first);
            if (second < first) return first;
            return PhysicsEngine.NextRandomRange(first, second);
        });

        ScriptRuntime.Globals["__det_randomseed"] = (Action<double>)(seedVal =>
        {
            PhysicsEngine.SeedRng(unchecked((uint)Math.Floor(seedVal)));
        });
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
            if math ~= nil then
                math.random = function(m, n)
                    return __det_random(m, n)
                end
                math.randomseed = function(seed)
                    __det_randomseed(seed or 1)
                end
            end
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

    public Character CreateCharacter(string characterId, int slot = 0)
    {
        string cacheKey = $"{characterId}_{slot}";
        if (_characterCache.TryGetValue(cacheKey, out Character? cached))
        {
            cached.EnsureTexturesLoaded();
            cached.ResetCombatState();
            return cached;
        }

        string? resolvedPath = ModManager.ResolveCharacterScript(characterId)
            ?? Path.Combine(RootPath, "Scripts", "characters", $"{characterId}.lua");

        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException($"Character script '{characterId}.lua' not found.");

        var character = new Character(resolvedPath, ScriptRuntime);
        _characterCache[cacheKey] = character;
        return character;
    }

    public Character CreateHeadlessCharacter(string characterId)
    {
        string? resolvedPath = ModManager.ResolveCharacterScript(characterId)
            ?? Path.Combine(RootPath, "Scripts", "characters", $"{characterId}.lua");

        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException($"Character script '{characterId}.lua' not found.");

        return Character.CreateHeadless(resolvedPath, ScriptRuntime);
    }

    public Character CreateCharacterFromPath(string resolvedPath, int slot = 1, bool applyStoredBalance = false)
    {
        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException($"Character script '{resolvedPath}' not found.");

        string charId = Path.GetFileNameWithoutExtension(resolvedPath).ToLowerInvariant();
        string cacheKey = $"net_{charId}_{slot}_{resolvedPath}";
        if (_characterCache.TryGetValue(cacheKey, out Character? cached))
        {
            cached.EnsureTexturesLoaded();
            cached.ResetCombatState();
            return cached;
        }

        var character = Character.CreateFromPath(resolvedPath, ScriptRuntime, applyStoredBalance);
        _characterCache[cacheKey] = character;
        return character;
    }

    public void ClearCharacterCache() => _characterCache.Clear();
}