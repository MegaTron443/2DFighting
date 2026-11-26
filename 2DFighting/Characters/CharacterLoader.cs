using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using MoonSharp.Interpreter;
using Raylib_cs;

public static class CharacterLoader
{
    private static readonly Dictionary<string, Texture2D> _textureCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] ForwardedGlobals =
    {
        "Keys", "Trajectories", "ApplyHitForce", "ApplyLaunchForce",
        "create_projectile", "CreateProjectile", "PhysicsEngine", "NewVec2",
        "math", "Log", "PlaySound", "SpawnVFX", "TriggerAudioDuck", "SetAudioBusVolume",
        "SetAudioBusLowPass", "EnablePostFX", "DisablePostFX", "SetPostFXFloat",
        "SetPostFXVec2", "SetPostFXColor", "TriggerScreenShake", "TriggerImpactFrame",
        "TriggerPaletteShift", "TriggerGravityPinch", "SetHitstop"
    };

    public static Texture2D GetOrLoadSpriteSheet(string fullPath)
    {
        if (_textureCache.TryGetValue(fullPath, out Texture2D cached))
            return cached;

        Texture2D loaded = Raylib.LoadTexture(fullPath);
        if (loaded.id > 0)
        {
            Raylib.SetTextureFilter(loaded, TextureFilter.TEXTURE_FILTER_BILINEAR);
            Raylib.SetTextureWrap(loaded, TextureWrap.TEXTURE_WRAP_CLAMP);
        }
        _textureCache[fullPath] = loaded;
        return loaded;
    }

    public static void UnloadUnusedSpriteSheets(IEnumerable<string> activePaths)
    {
        HashSet<string> activeSet = activePaths is HashSet<string> existing && Equals(existing.Comparer, StringComparer.OrdinalIgnoreCase)
            ? existing
            : new HashSet<string>(activePaths, StringComparer.OrdinalIgnoreCase);

        List<string>? toUnload = null;
        foreach (string key in _textureCache.Keys)
        {
            if (!activeSet.Contains(key))
            {
                toUnload ??= new List<string>();
                toUnload.Add(key);
            }
        }

        if (toUnload == null) return;

        for (int i = 0; i < toUnload.Count; i++)
        {
            string key = toUnload[i];
            if (_textureCache.Remove(key, out Texture2D tex) && tex.id > 0)
            {
                Raylib.UnloadTexture(tex);
            }
        }
    }

    public static void UnloadAllSpriteSheets()
    {
        foreach (var tex in _textureCache.Values)
        {
            if (tex.id > 0) Raylib.UnloadTexture(tex);
        }
        _textureCache.Clear();
    }

    public static void LoadFromLua(
        Character character,
        string path,
        Script script,
        bool skipGpuAssets = false,
        bool applyStoredBalance = true)
    {
        Table env = CreateIsolatedEnvironment(script);

        string code = File.ReadAllText(path);
        character.RawLuaCode = code;
        character.ScriptHash = ModManager.ComputeStringHash(code);
        character.BaseScriptHash = character.ScriptHash;
        DynValue result = LuaEngine.DoStringSandboxed(script, code, env, path, LuaEngine.MaxLoadInstructions);

        Table? t = result.Type == DataType.Table ? result.Table : env.Get("Character").Table;
        if (t == null)
            throw new Exception($"Table 'Character' missing in: {path}");

        character.CharacterId = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        character.WorkshopId = ModManager.GetWorkshopIdForScriptPath(path);
        character.Name = t.Get("name").String ?? "Fighter";
        string? desc = t.Get("description").String;
        character.Description = string.IsNullOrWhiteSpace(desc) ? "No description provided." : desc.Trim();
        character.OnPassive = t.Get("on_passive");

        bool headless = skipGpuAssets || PhysicsEngine.IsHeadlessThread;
        ParsePhysics(character, t.Get("physics").Table);
        ParseRendering(character, t.Get("rendering").Table, headless);
        ParseAnimations(character, t.Get("animations").Table);
        ParseSkills(character, t.Get("skills").Table);

        character.BaseStatsSnapshot = CharacterBalanceGenome.FromCharacter(character);

        bool appliedBalance = false;
        if (applyStoredBalance &&
            BalanceOverrideStore.IsEnabled &&
            BalanceOverrideStore.TryGetGenome(character.CharacterId, character.BaseScriptHash, out CharacterBalanceGenome? genome) &&
            genome != null)
        {
            ApplyGenome(character, genome);
            appliedBalance = true;
        }

        if (!headless && (appliedBalance || LuaMoveTranslator.HasValidCache(character.Name, character.ScriptHash)))
        {
            character.RawLuaCode = string.Empty;
        }
    }

    public static void ApplyGenome(Character character, CharacterBalanceGenome genome)
    {
        if (character.BaseStatsSnapshot != null)
        {
            ApplyRawValues(character, character.BaseStatsSnapshot);
        }

        ApplyRawValues(character, genome);

        string baseHash = !string.IsNullOrEmpty(character.BaseScriptHash)
            ? character.BaseScriptHash
            : character.ScriptHash;
        character.BaseScriptHash = baseHash;
        character.ScriptHash = $"{baseHash}_ga_{genome.ComputeCompactSignature()}";
        character.TransientSemanticsCache = null;

        LuaMoveTranslator.AnalyzeCharacter(character, out _);
    }

    public static void ResetToBaseStats(Character character)
    {
        if (character.BaseStatsSnapshot != null)
        {
            ApplyRawValues(character, character.BaseStatsSnapshot);
        }

        if (!string.IsNullOrEmpty(character.BaseScriptHash))
        {
            character.ScriptHash = character.BaseScriptHash;
        }
        character.TransientSemanticsCache = null;
        LuaMoveTranslator.AnalyzeCharacter(character, out _);
    }

    private static void ApplyRawValues(Character character, CharacterBalanceGenome genome)
    {
        if (genome.WalkSpeedPct > 0.01f)
        {
            character.WalkSpeedPct = Math.Clamp(genome.WalkSpeedPct, 0.08f, 0.65f);
        }
        if (genome.JumpForcePct < -0.05f)
        {
            character.JumpForcePct = Math.Clamp(genome.JumpForcePct, -1.25f, -0.30f);
        }

        foreach (var kvp in genome.Skills)
        {
            if (!character.Skills.TryGetValue(kvp.Key, out SkillData? skill))
                continue;

            SkillBalanceGene gene = kvp.Value;
            skill.Damage = gene.Damage;
            skill.StartupFrames = gene.StartupFrames;
            skill.ActiveFrames = gene.ActiveFrames;
            skill.RecoveryFrames = gene.RecoveryFrames;
            skill.HitStun = gene.HitStun;
            skill.CooldownTime = gene.CooldownTime;
        }
    }

    private static Table CreateIsolatedEnvironment(Script script)
    {
        Table env = new Table(script);
        Table meta = new Table(script);
        meta["__index"] = script.Globals;
        env.MetaTable = meta;

        foreach (string g in ForwardedGlobals)
        {
            env[g] = script.Globals.Get(g);
        }

        return env;
    }

    private static void ParsePhysics(Character character, Table? phys)
    {
        if (phys == null) return;

        character.WidthPct = (float)(phys.Get("width_pct").CastToNumber() ?? 0.05);
        character.HeightPct = (float)(phys.Get("height_pct").CastToNumber() ?? 0.20);
        character.WalkSpeedPct = (float)(phys.Get("walk_speed_pct").CastToNumber() ?? 0.25);
        character.JumpForcePct = (float)(phys.Get("jump_force_pct").CastToNumber() ?? -0.65);
        character.GravityPct = (float)(phys.Get("gravity_pct").CastToNumber() ?? 1.40);
        character.Friction = (float)(phys.Get("friction").CastToNumber() ?? 0.85);
    }

    private static void ParseRendering(Character character, Table? rend, bool headless)
    {
        if (rend == null) return;

        CharacterRenderer renderer = character.Renderer;
        renderer.DrawOffsetXPct = (float)(rend.Get("draw_offset_x_pct").CastToNumber() ?? 0.0);
        renderer.DrawOffsetYPct = (float)(rend.Get("draw_offset_y_pct").CastToNumber() ?? 0.0);
        renderer.FrameWidth = (int)(rend.Get("frame_width").CastToNumber() ?? 128);
        renderer.FrameHeight = (int)(rend.Get("frame_height").CastToNumber() ?? 128);

        string? sheetRelPath = rend.Get("sprite_sheet").String;
        if (!string.IsNullOrEmpty(sheetRelPath))
        {
            string? fullSheetPath = ModManager.ResolveFile(sheetRelPath);
            if (fullSheetPath != null && File.Exists(fullSheetPath))
            {
                renderer.SpriteSheetPath = fullSheetPath;
                if (!headless)
                {
                    renderer.SpriteSheet = GetOrLoadSpriteSheet(fullSheetPath);
                }
            }
        }

        var colTbl = rend.Get("body_color").Table;
        if (colTbl != null)
        {
            character.PrimaryColor = new Color(
                (int)(colTbl.Get(1).CastToNumber() ?? 30),
                (int)(colTbl.Get(2).CastToNumber() ?? 80),
                (int)(colTbl.Get(3).CastToNumber() ?? 180),
                255
            );
        }

        string? baseShader = rend.Get("shader").String;
        if (!string.IsNullOrEmpty(baseShader))
        {
            renderer.BaseMaterial.ShaderId = baseShader;
            renderer.BaseMaterial.SetColor("u_PrimaryColor", character.PrimaryColor.r, character.PrimaryColor.g, character.PrimaryColor.b);
        }

        ParseParts(renderer, rend.Get("parts").Table, headless);
    }

    private static void ParseParts(CharacterRenderer renderer, Table? partsTbl, bool headless)
    {
        if (partsTbl == null) return;

        foreach (var pair in partsTbl.Pairs)
        {
            string partName = pair.Key.String;
            Table? pTbl = pair.Value.Table;
            if (pTbl == null) continue;

            var part = new SpritePart
            {
                Name = partName,
                ZOrder = (int)(pTbl.Get("z_order").CastToNumber() ?? 0),
                Visible = pTbl.Get("visible").IsNil() || pTbl.Get("visible").CastToBool(),
                OffsetXPct = (float)(pTbl.Get("x_offset_pct").CastToNumber() ?? 0.0),
                OffsetYPct = (float)(pTbl.Get("y_offset_pct").CastToNumber() ?? 0.0),
                WidthPct = (float)(pTbl.Get("width_pct").CastToNumber() ?? 1.0),
                HeightPct = (float)(pTbl.Get("height_pct").CastToNumber() ?? 1.0),
                RotationDeg = (float)(pTbl.Get("rotation").CastToNumber() ?? 0.0),
                IsSocketProp = pTbl.Get("is_socket").CastToBool()
            };

            string? partSheet = pTbl.Get("sprite_sheet").String;
            if (!string.IsNullOrEmpty(partSheet))
            {
                string? resolvedPartTex = ModManager.ResolveFile(partSheet);
                if (resolvedPartTex != null && File.Exists(resolvedPartTex))
                {
                    part.TexturePath = resolvedPartTex;
                    if (!headless)
                    {
                        part.Texture = GetOrLoadSpriteSheet(resolvedPartTex);
                    }
                }
            }

            part.Material.ShaderId = pTbl.Get("shader").String ?? renderer.BaseMaterial.ShaderId;

            if (pTbl.Get("additive").CastToBool())
                part.Material.BlendMode = BlendMode.BLEND_ADDITIVE;

            if (pTbl.Get("portal_mode").CastToBool())
                part.Material.SetFloat("u_PortalMode", 1.0f);

            part.IsAtlas = pTbl.Get("is_atlas").CastToBool();

            var pCol = pTbl.Get("color").Table;
            if (pCol != null)
            {
                int r = (int)(pCol.Get(1).CastToNumber() ?? 255);
                int g = (int)(pCol.Get(2).CastToNumber() ?? 255);
                int b = (int)(pCol.Get(3).CastToNumber() ?? 255);
                part.FallbackColor = new Color(r, g, b, 255);
                part.Material.SetColor("u_PrimaryColor", r, g, b);
            }

            var sCol = pTbl.Get("secondary_color").Table;
            if (sCol != null)
            {
                part.Material.SetColor(
                    "u_SecondaryColor",
                    (float)(sCol.Get(1).CastToNumber() ?? 35),
                    (float)(sCol.Get(2).CastToNumber() ?? 30),
                    (float)(sCol.Get(3).CastToNumber() ?? 55)
                );
            }

            var gCol = pTbl.Get("glow_color").Table;
            if (gCol != null)
            {
                part.Material.SetColor(
                    "u_GlowColor",
                    (float)(gCol.Get(1).CastToNumber() ?? 185),
                    (float)(gCol.Get(2).CastToNumber() ?? 65),
                    (float)(gCol.Get(3).CastToNumber() ?? 255)
                );
            }

            renderer.AddPart(partName, part);
        }

        renderer.SortParts();
    }

    private static void ParseAnimations(Character character, Table? animTable)
    {
        if (animTable == null) return;

        foreach (var pair in animTable.Pairs)
        {
            var clipTable = pair.Value.Table;
            if (clipTable == null) continue;

            var clip = new AnimationClip
            {
                StartFrame = (int)(clipTable.Get("start_frame").CastToNumber() ?? 0),
                FrameCount = (int)(clipTable.Get("frame_count").CastToNumber() ?? 1),
                Speed = (float)(clipTable.Get("speed").CastToNumber() ?? 8.0)
            };

            var keyframesTbl = clipTable.Get("keyframes").Table;
            if (keyframesTbl != null)
            {
                clip.Keyframes = new List<KeyframeStep>();
                foreach (var kfPair in keyframesTbl.Pairs)
                {
                    Table? kf = kfPair.Value.Table;
                    if (kf == null) continue;
                    clip.Keyframes.Add(new KeyframeStep
                    {
                        AtlasFrame = (int)(kf.Get("frame").CastToNumber() ?? 0),
                        HoldTicks = (int)(kf.Get("duration").CastToNumber() ?? 4),
                        SpawnAfterimage = kf.Get("afterimage").CastToBool(),
                        WeaponOffsetPct = new Vector2(
                            (float)(kf.Get("weapon_x").CastToNumber() ?? 0.0),
                            (float)(kf.Get("weapon_y").CastToNumber() ?? 0.0)
                        ),
                        WeaponAngleDeg = (float)(kf.Get("weapon_angle").CastToNumber() ?? 0.0)
                    });
                }
            }

            character.Animations[pair.Key.String] = clip;
        }
    }

    private static void ParseSkills(Character character, Table? skillsTable)
    {
        if (skillsTable == null) return;

        foreach (var pair in skillsTable.Pairs)
        {
            var sTable = pair.Value.Table;
            var hb = sTable.Get("hitbox").Table;

            Rectangle hbPctRect = new Rectangle(0, 0, 0, 0);
            if (hb != null)
            {
                hbPctRect = new Rectangle(
                    (float)(hb.Get("x_offset_pct").CastToNumber() ?? hb.Get("x_offset").CastToNumber() ?? 0.0),
                    (float)(hb.Get("y_offset_pct").CastToNumber() ?? hb.Get("y_offset").CastToNumber() ?? 0.0),
                    (float)(hb.Get("width_pct").CastToNumber() ?? hb.Get("width").CastToNumber() ?? 0.2),
                    (float)(hb.Get("height_pct").CastToNumber() ?? hb.Get("height").CastToNumber() ?? 0.2)
                );
            }

            var triggerObj = sTable.Get("input_trigger");
            int triggerMask = 0;
            if (triggerObj.Type == DataType.Table)
            {
                triggerMask = (int)(triggerObj.Table.Get("value").CastToNumber() ?? 0);
            }
            else if (triggerObj.Type == DataType.Number)
            {
                triggerMask = (int)triggerObj.Number;
            }

            string heightStr = (sTable.Get("attack_height").String ?? "mid").ToLowerInvariant();
            AttackHeight atkHeight = heightStr switch
            {
                "low"      => AttackHeight.Low,
                "overhead" => AttackHeight.Overhead,
                _          => AttackHeight.Mid
            };

            character.SkillController.AddSkill(pair.Key.String, new SkillData
            {
                Name = pair.Key.String,
                AnimationClipName = sTable.Get("animation").String ?? pair.Key.String,
                TriggerMask = triggerMask,
                Damage = (int)(sTable.Get("damage").CastToNumber() ?? 10),
                Height = atkHeight,
                StartupFrames = (int)(sTable.Get("startup_frames").CastToNumber() ?? 0),
                ActiveFrames = (int)(sTable.Get("active_frames").CastToNumber() ?? 0),
                RecoveryFrames = (int)(sTable.Get("recovery_frames").CastToNumber() ?? 0),
                IsHyperFrames = sTable.Get("is_hyperframes").CastToBool(),
                HyperStartFrame = (int)(sTable.Get("hyper_start_frame").CastToNumber() ?? 0),
                HyperFrames = (int)(sTable.Get("hyper_frames").CastToNumber() ?? 0),
                HitboxOffsetPct = hbPctRect,
                HitStun = (float)(sTable.Get("hitStun").CastToNumber() ?? 0.25),
                CooldownTime = (float)(sTable.Get("cooldown_time").CastToNumber() ?? 0.0),
                IsBlockBreak = sTable.Get("is_blockbreak").CastToBool(),
                OnBlockBreak = sTable.Get("on_blockbreak"),
                IsCounter = sTable.Get("is_counter").CastToBool(),
                OnCounter = sTable.Get("on_counter"),
                CustomLogic = sTable.Get("is_customlogic").CastToBool(),
                OnCustomLogic = sTable.Get("on_customlogic"),
                OnHit = sTable.Get("on_hit"),
                ShortLabel = pair.Key.String.Length > 5 ? pair.Key.String[..5].ToUpperInvariant() : pair.Key.String.ToUpperInvariant()
            });
        }
    }
}
