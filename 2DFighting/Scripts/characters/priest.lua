-- ============================================================
-- CHROMATIC INQUISITOR PRIEST (Dynamic Grayscale LUT + Auras)
-- ============================================================

local VIOLET_WARP_VEL_X_PCT = 1.15

local function Clamp(v, lo, hi)
    if v < lo then return lo end
    if v > hi then return hi end
    return v
end

-- Shifts the water vector toward a target RGB color by `weight` (0.0 to 1.0)
local function ShiftWaterVector(character, tr, tg, tb, weight)
    local w = weight or 0.45
    local cr = character.CustomData["target_r"] or 70.0
    local cg = character.CustomData["target_g"] or 195.0
    local cb = character.CustomData["target_b"] or 255.0

    character.CustomData["target_r"] = Clamp(cr + (tr - cr) * w, 15.0, 255.0)
    character.CustomData["target_g"] = Clamp(cg + (tg - cg) * w, 15.0, 255.0)
    character.CustomData["target_b"] = Clamp(cb + (tb - cb) * w, 15.0, 255.0)
end

-- Returns normalized channel strengths (0..1) + dominant archetype
local function GetWaterState(character)
    local r = (character.CustomData["target_r"] or 70.0) / 255.0
    local g = (character.CustomData["target_g"] or 195.0) / 255.0
    local b = (character.CustomData["target_b"] or 255.0) / 255.0

    local luma   = (r + g + b) / 3.0
    local redDom = math.max(0.0, r - (g + b) * 0.5)       -- High when Red/Solar
    local arcDom = math.max(0.0, ((r + b) * 0.5) - g)     -- High when Purple/Arcane
    local pure   = (luma > 0.72) and (luma - 0.72) / 0.28 or 0.0 -- High when White/Pure
    local abyss  = (luma < 0.32) and (0.32 - luma) / 0.32 or 0.0 -- High when Dark/Abyss

    return r, g, b, redDom, arcDom, pure, abyss
end

-- Applies live stat buffs from the current Water Vector
local function ApplyWaterBuffs(player, character, dt)
    local _, _, _, redDom, arcDom, pure, abyss = GetWaterState(character)

    -- Arcane (Purple/Blue) -> faster movement & lighter gravity
    character.WalkSpeedPct = 0.25 + (arcDom * 0.10)
    character.GravityPct   = 1.35 - (arcDom * 0.28)

    -- Pure (White) -> gentle passive HP regen (up to ~3 HP/sec)
    if pure > 0.15 and player.IsAlive and player.Health < player.MAX_HEALTH then
        local acc = (character.CustomData["regen_acc"] or 0.0) + (pure * 3.0 * dt)
        if acc >= 1.0 then
            local heal = math.floor(acc)
            player.Health = math.min(player.MAX_HEALTH, player.Health + heal)
            acc = acc - heal
        end
        character.CustomData["regen_acc"] = acc
    end

    -- Expose damage & lifesteal multipliers for skill on_hit callbacks
    -- Red -> up to +45% bonus damage | Abyss -> up to +35% lifesteal
    character.CustomData["dmg_mult"]  = 1.0 + (redDom * 0.45) + (abyss * 0.20)
    character.CustomData["lifesteal"] = abyss * 0.35
end

local Character = {
    name = "ChromaticPriest",
    description = [[
AURA SHIFTER / INQUISITOR
Chromatic zealot whose attacks channel 4 distinct sacred/forbidden auras (White, Red, Dark, Purple).

PASSIVE:
Maintains an orbiting halo and active Aura (0=White, 1=Red, 2=Dark, 3=Purple) updated per skill.

MOVE LIST:
- Sanctified Ray (Right): Attunes White aura and fires a disciplined mid-range smite.
- Benediction Pulse (Bottom+Right): Attunes White aura and restores 12 HP.
- Crimson Censer (Top): Attunes Red aura; 3 hits ignite Sunburn to launch the opponent.
- Abyssal Heresy (Left): Attunes Dark aura; spends 7 HP for an unblockable overhead stealing 12 HP.
- Violet Warp (Bottom): Attunes Purple aura and blinks forward leaving arcane afterimages.
- Chromatic Shift (Top+Left): Detonates active aura bonus (knockback / burn flash / heal / extra hitstop).
- Lux Aeterna (Top+Bottom): Armored full-spectrum block-breaking prism beam that heals 14 HP on hit.
]],

    physics = {
        width_pct      = 0.05,
        height_pct     = 0.20,
        walk_speed_pct = 0.25,
        jump_force_pct = -0.64,
        gravity_pct    = 1.35,
        friction       = 0.86
    },

    rendering = {
        frame_width  = 256,
        frame_height = 256,
        body_color   = { 240, 240, 245 },
        shader       = "priest_water",

        parts = {
            left_leg = {
                sprite_sheet = "Assets/Characters/priest/left_leg.png",
                shader       = "priest_water",
                z_order      = 1,
                x_offset_pct = -0.22,
                y_offset_pct = 0.35,
                width_pct    = 0.28,
                height_pct   = 0.28,
                rotation     = 8
            },
            right_leg = {
                sprite_sheet = "Assets/Characters/priest/right_leg.png",
                shader       = "priest_water",
                z_order      = 1,
                x_offset_pct = 0.22,
                y_offset_pct = 0.35,
                width_pct    = 0.28,
                height_pct   = 0.28,
                rotation     = -8
            },
            body = {
                sprite_sheet = "Assets/Characters/priest/body.png",
                shader       = "priest_water",
                z_order      = 2,
                x_offset_pct = 0.0,
                y_offset_pct = 0.04,
                width_pct    = 1.05,
                height_pct   = 0.48
            },
            head = {
                sprite_sheet = "Assets/Characters/priest/head.png",
                shader       = "priest_water",
                z_order      = 3,
                x_offset_pct = 0.0,
                y_offset_pct = -0.27,
                width_pct    = 0.52,
                height_pct   = 0.19
            },
            hat = {
                sprite_sheet = "Assets/Characters/priest/hat.png",
                shader       = "priest_water",
                z_order      = 4,
                x_offset_pct = 0.0,
                y_offset_pct = -0.40,
                width_pct    = 1.40,
                height_pct   = 0.22,
                rotation     = -3
            },
            staff = {
                sprite_sheet = "Assets/Characters/priest/staff.png",
                shader       = "priest_water",
                z_order      = 5,
                is_socket    = true,
                x_offset_pct = -0.68,
                y_offset_pct = 0.02,
                width_pct    = 0.55,
                height_pct   = 1.05,
                rotation     = 6
            }
        }
    },

    animations = {
        idle = { start_frame = 0, frame_count = 1, speed = 6.0 },
        walk = { start_frame = 0, frame_count = 1, speed = 10.0 },

        Staff_Backswing = {
            keyframes = {
                { frame = 0, duration = 5, afterimage = false, weapon_x = -0.35, weapon_y = -0.10, weapon_angle = -58 },
                { frame = 0, duration = 4, afterimage = true,  weapon_x = -0.55, weapon_y =  0.04, weapon_angle = -105 },
                { frame = 0, duration = 8, afterimage = false, weapon_x = -0.15, weapon_y =  0.02, weapon_angle = -25 }
            }
        },

        Staff_Heal = {
            keyframes = {
                { frame = 0, duration = 8,  afterimage = false, weapon_x = 0.0, weapon_y = -0.22, weapon_angle = 0 },
                { frame = 0, duration = 12, afterimage = false, weapon_x = 0.0, weapon_y = -0.30, weapon_angle = 0 }
            }
        },

        Staff_Uppercut = {
            keyframes = {
                { frame = 0, duration = 5, afterimage = false, weapon_x =  0.10, weapon_y =  0.28, weapon_angle =  110 },
                { frame = 0, duration = 3, afterimage = true,  weapon_x =  0.45, weapon_y = -0.22, weapon_angle =   15 },
                { frame = 0, duration = 4, afterimage = true,  weapon_x =  0.25, weapon_y = -0.46, weapon_angle =  -45 },
                { frame = 0, duration = 7, afterimage = false, weapon_x =  0.12, weapon_y = -0.32, weapon_angle =  -20 }
            }
        },

        Staff_Cast = {
            keyframes = {
                { frame = 0, duration = 6, afterimage = false, weapon_x = 0.05, weapon_y = -0.05, weapon_angle = -60 },
                { frame = 0, duration = 6, afterimage = true,  weapon_x = 0.25, weapon_y =  0.02, weapon_angle = -90 },
                { frame = 0, duration = 7, afterimage = false, weapon_x = 0.10, weapon_y =  0.02, weapon_angle = -40 }
            }
        },

        Staff_Abyssal = {
            keyframes = {
                { frame = 0, duration = 7,  afterimage = false, weapon_x = 0.00, weapon_y =  0.05, weapon_angle = -75  },
                { frame = 0, duration = 10, afterimage = true,  weapon_x = 0.28, weapon_y = -0.04, weapon_angle = -115 },
                { frame = 0, duration = 8,  afterimage = false, weapon_x = 0.10, weapon_y =  0.02, weapon_angle = -55  }
            }
        },

        Staff_Warp = {
            keyframes = {
                { frame = 0, duration = 11, afterimage = false, weapon_x = -0.10, weapon_y = -0.25, weapon_angle = -15 },
                { frame = 0, duration = 5,  afterimage = true,  weapon_x =  0.40, weapon_y =  0.00, weapon_angle =  60 },
                { frame = 0, duration = 7,  afterimage = false, weapon_x =  0.15, weapon_y =  0.02, weapon_angle =  20 }
            }
        },

        Staff_Ultimate = {
            keyframes = {
                { frame = 0, duration = 12, afterimage = false, weapon_x = 0.0,  weapon_y = -0.36, weapon_angle =  0 },
                { frame = 0, duration = 36, afterimage = true,  weapon_x = 0.45, weapon_y = -0.10, weapon_angle = 78 },
                { frame = 0, duration = 12, afterimage = false, weapon_x = 0.15, weapon_y =  0.00, weapon_angle = 25 }
            }
        }
    },

    on_passive = function(player, character, dt)
        if character.CustomData["init"] == nil then
            character.CustomData["init"] = 1.0
            character.CustomData["water_r"]  = 70.0
            character.CustomData["water_g"]  = 195.0
            character.CustomData["water_b"]  = 255.0
            character.CustomData["target_r"] = 70.0
            character.CustomData["target_g"] = 195.0
            character.CustomData["target_b"] = 255.0

            character:SetShaderColor("u_ClothColor1", 40, 44, 66)
            character:SetShaderColor("u_ClothColor2", 222, 226, 236)
            character:SetShaderColor("u_MetalColor",  130, 142, 165)

            character:SetPartShaderFloat("body", "u_HasPotion", 1.0)
            character:SetPartShaderVec2("body",  "u_PotionUV",   0.20, 0.78)
            character:SetPartShaderVec2("body",  "u_PotionSize", 0.14, 0.15)
            character:SetPartShaderColor("body", "u_PotionColor", 45, 235, 125)
        end

        -- Smoothly lerp water color toward target vector
        local lerpT = math.min(1.0, dt * 6.5)
        local wr = (character.CustomData["water_r"] or 70.0)
        local wg = (character.CustomData["water_g"] or 195.0)
        local wb = (character.CustomData["water_b"] or 255.0)

        wr = wr + ((character.CustomData["target_r"] or 70.0)  - wr) * lerpT
        wg = wg + ((character.CustomData["target_g"] or 195.0) - wg) * lerpT
        wb = wb + ((character.CustomData["target_b"] or 255.0) - wb) * lerpT

        character.CustomData["water_r"] = wr
        character.CustomData["water_g"] = wg
        character.CustomData["water_b"] = wb
        character:SetShaderColor("u_WaterColor", wr, wg, wb)

        -- Apply dynamic vector buffs
        ApplyWaterBuffs(player, character, dt)

        -- Procedural breathing, crouch, walk stride & staff float
        local t = (character.CustomData["t"] or 0.0) + dt
        character.CustomData["t"] = t

        local breathe = math.sin(t * 3.2) * 0.012
        local isWalking = player.IsGrounded and math.abs(player.Velocity.X) > 15.0

        if player.IsCrouching then
            character:SetPartOffset("body",      0.0,   0.06,  8.0)
            character:SetPartOffset("head",      0.02, -0.25,  0.0)
            character:SetPartOffset("hat",       0.02, -0.38, -8.0)
            character:SetPartOffset("left_leg",  -0.28, 0.35,  18.0)
            character:SetPartOffset("right_leg",  0.28, 0.35, -18.0)
        elseif isWalking then
            local stride = math.sin(t * 13.0)
            character:SetPartOffset("body",      0.0,   0.04 + breathe,  5.0)
            character:SetPartOffset("head",      0.0,  -0.27 + breathe,  0.0)
            character:SetPartOffset("hat",       0.0,  -0.40 + breathe, -6.0)
            character:SetPartOffset("left_leg",  -0.18 - stride * 0.15, 0.35,  stride * 20.0)
            character:SetPartOffset("right_leg",  0.18 + stride * 0.15, 0.35, -stride * 20.0)
        else
            character:SetPartOffset("body",      0.0,   0.04 + breathe,  0.0)
            character:SetPartOffset("head",      0.0,  -0.27 + breathe,  0.0)
            character:SetPartOffset("hat",       0.0,  -0.40 + breathe, -3.0)
            character:SetPartOffset("left_leg",  -0.22, 0.35,  8.0)
            character:SetPartOffset("right_leg",  0.22, 0.35, -8.0)
        end

        if character.ActiveSkill == nil then
            local staffBob = math.sin(t * 2.6 + 0.8) * 0.02
            character:SetPartOffset("staff", -0.68, 0.02 + staffBob, 6.0 + math.sin(t * 2.0) * 2.5)
        end

        local hotTimer = character.CustomData["hot_timer"] or 0.0
        if hotTimer > 0.0 then
            hotTimer = hotTimer - dt
            character.CustomData["hot_timer"] = hotTimer
            local hotTick = (character.CustomData["hot_tick"] or 0.0) + dt
            if hotTick >= 0.35 then
                hotTick = hotTick - 0.35
                if player.IsAlive then
                    player.Health = math.min(player.MAX_HEALTH, player.Health + 3)
                    SpawnVFX(player, {
                        preset   = "HealGlow",
                        scale    = 1.05,
                        lifetime = 0.32,
                        color    = { 245, 250, 255 }
                    })
                end
            end
            character.CustomData["hot_tick"] = hotTick
        end
    end,

    
    skills = {
        -- 1. KeyRight: Normal Attack (Staff Backswing + White Light Flash)
        Sanctified_Ray = {
            input_trigger   = Keys.KeyRight,
            animation       = "Staff_Backswing",
            damage          = 14,
            attack_height   = "mid",
            startup_frames  = 6,
            active_frames   = 5,
            recovery_frames = 8,
            hitStun         = 0.30,
            cooldown_time   = 0.45,
            hitbox          = { x_offset_pct = 0.55, y_offset_pct = 0.0, width_pct = 1.45, height_pct = 0.80 },
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 1 then
                    ShiftWaterVector(character, 245, 248, 255, 0.45)
                    PlaySound(player, { sound = "prism_chime", pitch = 1.25, volume = 0.8 })
                elseif character.CurrentActionFrame == 6 then
                    SpawnVFX(player, {
                        preset       = "CounterFlash",
                        x_offset_pct = 0.08,
                        y_offset_pct = -0.02,
                        scale        = 1.05,
                        lifetime     = 0.20,
                        color        = { 255, 255, 255 }
                    })
                end
            end,
            on_hit = function(owner, hit_player, skill)
                local mult = owner.CurrentCharacter.CustomData["dmg_mult"] or 1.0
                local ls   = owner.CurrentCharacter.CustomData["lifesteal"] or 0.0
                local dmg  = math.floor(skill.Damage * mult)
                ApplyHitForce(owner, hit_player, dmg, skill.HitStun, 0.16)
                if ls > 0.05 then
                    owner.Health = math.min(owner.MAX_HEALTH, owner.Health + math.floor(dmg * ls))
                end
            end
        },

        -- 2. KeyBottom: Benediction Aura (Heal Over Time + White Aura)
        Benediction_Pulse = {
            input_trigger   = Keys.KeyBottom,
            animation       = "Staff_Heal",
            damage          = 0,
            startup_frames  = 10,
            active_frames   = 4,
            recovery_frames = 8,
            cooldown_time   = 5.0,
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 1 then
                    ShiftWaterVector(character, 245, 248, 255, 0.55)
                elseif character.CurrentActionFrame == 10 then
                    character.CustomData["hot_timer"] = 2.15 -- 6 ticks x 3 HP = 18 HP over time
                    character.CustomData["hot_tick"]  = 0.35 -- Trigger first tick immediately
                    SpawnVFX(player, { preset = "Shockwave", scale = 1.25, lifetime = 0.35, color = { 245, 250, 255 } })
                    PlaySound(player, { sound = "prism_chime", pitch = 1.50, volume = 0.9 })
                    TriggerPaletteShift(245, 250, 255, 0.25, 5, 0.25)
                end
            end
        },

        -- 3. KeyTop: Crimson Fireball (Red Projectile + 3-Hit Sunburn Passive)
        Crimson_Fireball = {
            input_trigger   = Keys.KeyTop,
            animation       = "Staff_Cast",
            damage          = 0,
            attack_height   = "mid",
            startup_frames  = 7,
            active_frames   = 4,
            recovery_frames = 9,
            cooldown_time   = 0.95,
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 1 then
                    ShiftWaterVector(character, 255, 55, 35, 0.50)
                elseif character.CurrentActionFrame == 7 then
                    PlaySound(player, { sound = "cosmic_heavy_slash", pitch = 1.20, volume = 0.80 })
                    SpawnVFX(player, { preset = "HitSpark", x_offset_pct = 0.06, scale = 0.75, color = { 255, 90, 35 } })

                    CreateProjectile(player, {
                        shader       = "fireball",
                        x_offset_pct = 0.06,
                        y_offset_pct = -0.02,
                        speed_x_pct  = 0.46,
                        width_pct    = 0.055,
                        height_pct   = 0.072,
                        damage       = 14,
                        hitStun      = 0.32,
                        lifetime     = 2.2,
                        color        = { 255, 70, 30 },
                        on_hit       = function(owner, hit_player, proj)
                            local ch   = owner.CurrentCharacter
                            local mult = ch.CustomData["dmg_mult"] or 1.0
                            local ls   = ch.CustomData["lifesteal"] or 0.0
                            local dmg  = math.floor(14 * mult)

                            local stacks = (ch.CustomData["sunburn"] or 0) + 1
                            if stacks >= 3 then
                                ch.CustomData["sunburn"] = 0
                                ApplyLaunchForce(owner, hit_player, dmg + 12, 0.50, 0.10, -0.78)
                                SetHitstop(6)
                                TriggerScreenShake(12, 12, 10)
                                TriggerPaletteShift(255, 80, 30, 0.55, 8, 0.35)
                                SpawnVFX(owner, { preset = "Explosion", spawn_at_target = true, scale = 1.15, color = { 255, 85, 25 } })
                            else
                                ch.CustomData["sunburn"] = stacks
                                ApplyHitForce(owner, hit_player, dmg, 0.32, 0.14)
                            end

                            if ls > 0.05 then
                                owner.Health = math.min(owner.MAX_HEALTH, owner.Health + math.floor(dmg * ls))
                            end
                        end
                    })
                end
            end
        },

        -- 4. KeyTop + KeyRight: Staff Uppercut (Trail Shader Colored by Current Water Vector)
        Chromatic_Uppercut = {
            input_trigger   = Keys.KeyTop + Keys.KeyRight,
            animation       = "Staff_Uppercut",
            damage          = 18,
            attack_height   = "mid",
            startup_frames  = 6,
            active_frames   = 6,
            recovery_frames = 10,
            hitStun         = 0.45,
            cooldown_time   = 1.4,
            hitbox          = { x_offset_pct = 0.40, y_offset_pct = -0.35, width_pct = 1.55, height_pct = 1.15 },
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 6 then
                    local wr = math.floor(character.CustomData["water_r"] or 70)
                    local wg = math.floor(character.CustomData["water_g"] or 195)
                    local wb = math.floor(character.CustomData["water_b"] or 255)

                    SpawnVFX(player, {
                        preset       = "SlashArc",
                        x_offset_pct = 0.05,
                        y_offset_pct = -0.05,
                        scale        = 1.15,
                        lifetime     = 0.22,
                        flip_y       = true, -- Bottom-to-top uppercut arc
                        rotation     = -10,
                        color        = { wr, wg, wb }
                    })
                    PlaySound(player, { sound = "cosmic_light_slash", pitch = 1.25, volume = 0.85 })
                end
            end,
            on_hit = function(owner, hit_player, skill)
                local ch   = owner.CurrentCharacter
                local mult = ch.CustomData["dmg_mult"] or 1.0
                local ls   = ch.CustomData["lifesteal"] or 0.0
                local dmg  = math.floor(skill.Damage * mult)

                ApplyLaunchForce(owner, hit_player, dmg, skill.HitStun, 0.08, -0.70)
                if ls > 0.05 then
                    owner.Health = math.min(owner.MAX_HEALTH, owner.Health + math.floor(dmg * ls))
                end
                SetHitstop(4)
                TriggerScreenShake(6, 10, 8)
            end
        },

        -- 5. KeyLeft: Abyssal Tentacles (3 Auto-Projectiles with Persistent Dark Trail + Lifesteal)
        Abyssal_Heresy = {
            input_trigger   = Keys.KeyLeft,
            animation       = "Staff_Abyssal",
            damage          = 0,
            attack_height   = "overhead",
            startup_frames  = 9,
            active_frames   = 6,
            recovery_frames = 11,
            is_blockbreak   = true,
            cooldown_time   = 2.4,
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 1 then
                    ShiftWaterVector(character, 38, 12, 32, 0.55)
                    if player.Health > 8 then
                        player.Health = player.Health - 7
                    end
                    PlaySound(player, { sound = "cosmic_sub_drop", pitch = 0.80, volume = 0.90 })
                elseif character.CurrentActionFrame == 9 then
                    local baseOffsetsY = { -0.09, 0.0, 0.09 }
                    local dir = player.FacingRight and 1.0 or -1.0

                    for idx = 1, 3 do
                        -- Randomize height, length, speed, and shader seed per tentacle
                        local randY      = baseOffsetsY[idx] + (math.random() - 0.5) * 0.06
                        local maxLen     = 0.34 + math.random() * 0.20
                        local speed      = 6.0 + math.random() * 3.5
                        local seed       = math.random() * 100.0
                        local driftY     = (math.random() - 0.5) * 28.0 -- Subtle random straight drift

                        local baseX = nil
                        local baseY = nil

                        local proj = CreateProjectile(player, {
                            shader             = "abyss_tentacle",
                            rotate_to_velocity = false,
                            x_offset_pct       = 0.08,
                            y_offset_pct       = randY,
                            speed_x_pct        = 0.0,
                            speed_y_pct        = 0.0,
                            width_pct          = 0.02,
                            height_pct         = 0.032, -- Thinner hitbox to match thinner tentacle
                            damage             = 5,
                            hitStun            = 0.42,
                            is_blockbreak      = true,
                            attack_height      = "overhead",
                            destroy_on_hit     = false,
                            lifetime           = 0.58,
                            scroll_speed       = speed,
                            color              = { 15, 10, 20 },
                            trajectory         = function(p, pDt)
                                if baseX == nil then
                                    baseX = p.PosX
                                    baseY = p.PosY
                                end

                                baseY = baseY + (driftY * pDt)

                                local progress = p.ElapsedTime / p.Lifetime
                                local growT
                                if progress < 0.30 then
                                    local t = progress / 0.30
                                    growT = 1.0 - (1.0 - t) * (1.0 - t)
                                elseif progress < 0.78 then
                                    growT = 1.0
                                else
                                    growT = 1.0 - ((progress - 0.78) / 0.22) * 0.50
                                end

                                p.WidthPct = math.max(0.02, maxLen * growT)
                                p.Position = NewVec2(baseX + dir * (p.Width * 0.5), baseY)
                                p.Material:SetFloat("u_Progress", progress)
                            end,
                            on_hit             = function(owner, hit_player, p)
                                local mult = owner.CurrentCharacter.CustomData["dmg_mult"] or 1.0
                                local ls   = owner.CurrentCharacter.CustomData["lifesteal"] or 0.0
                                local dmg  = math.floor(10 * mult)
                                ApplyHitForce(owner, hit_player, dmg, 0.42, 0.14)
                                owner.Health = math.min(owner.MAX_HEALTH, owner.Health + 5 + math.floor(dmg * ls))
                                SetHitstop(4)
                                TriggerScreenShake(8, 8, 8)
                            end
                        })

                        if proj ~= nil then
                            proj.Material:SetFloat("u_Seed", seed)
                        end
                    end
                end
            end
        },

        -- 6. KeyLeft + KeyBottom: Violet Warp (Longer Startup + Departure & Arrival Auras)
        Violet_Warp = {
            input_trigger   = Keys.KeyLeft + Keys.KeyBottom,
            animation       = "Staff_Warp",
            damage          = 12,
            startup_frames  = 12,
            active_frames   = 4,
            recovery_frames = 8,
            hitStun         = 0.28,
            cooldown_time   = 1.8,
            hitbox          = { x_offset_pct = 0.25, y_offset_pct = 0.05, width_pct = 1.05, height_pct = 0.80 },
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 1 then
                    ShiftWaterVector(character, 155, 55, 255, 0.55)
                    -- Departure aura
                    SpawnVFX(player, { preset = "Shockwave", scale = 1.15, lifetime = 0.30, color = { 175, 70, 255 } })
                    character:SpawnAfterimage(160, 70, 255)
                    PlaySound(player, { sound = "cosmic_light_slash", pitch = 1.55, volume = 0.85 })
                elseif character.CurrentActionFrame == 12 then
                    local dir = player.FacingRight and 1 or -1
                    player:SetVelocityPct(dir * VIOLET_WARP_VEL_X_PCT, 0)
                    character:SpawnAfterimage(210, 110, 255)
                elseif character.CurrentActionFrame == 15 then
                    -- Arrival aura
                    player:SetVelocityPct(0, 0)
                    SpawnVFX(player, { preset = "Shockwave", scale = 1.25, lifetime = 0.28, color = { 210, 120, 255 } })
                    SpawnVFX(player, { preset = "CounterFlash", scale = 0.85, lifetime = 0.20, color = { 210, 140, 255 } })
                    PlaySound(player, { sound = "prism_chime", pitch = 1.60, volume = 0.75 })
                end
            end,
            on_hit = function(owner, hit_player, skill)
                local mult = owner.CurrentCharacter.CustomData["dmg_mult"] or 1.0
                local ls   = owner.CurrentCharacter.CustomData["lifesteal"] or 0.0
                local dmg  = math.floor(skill.Damage * mult)
                ApplyHitForce(owner, hit_player, dmg, skill.HitStun, 0.14)
                if ls > 0.05 then
                    owner.Health = math.min(owner.MAX_HEALTH, owner.Health + math.floor(dmg * ls))
                end
            end
        },

        -- 7. KeyBottom + KeyTop + KeyRight: LUX AETERNA (Huge Overhead Prism + Cancelable Multihit Rainbow Water Ray, 0 Stun, High Dmg)
        Lux_Aeterna = {
            input_trigger     = Keys.KeyBottom + Keys.KeyTop + Keys.KeyRight,
            animation         = "Staff_Ultimate",
            damage            = 0,
            attack_height     = "mid",
            startup_frames    = 14,
            active_frames     = 36,
            recovery_frames   = 12,
            is_hyperframes    = false, -- Cancelable on hit!
            is_blockbreak     = true,
            hitStun           = 0.0,   -- No stun so opponent can dodge out
            cooldown_time     = 9.0,
            is_customlogic    = true,
            on_customlogic    = function(player, character, dt)
                local f = character.CurrentActionFrame
                local r = 128 + math.floor(127 * math.sin(f * 0.45))
                local g = 128 + math.floor(127 * math.sin(f * 0.45 + 2.09))
                local b = 128 + math.floor(127 * math.sin(f * 0.45 + 4.18))
                ShiftWaterVector(character, r, g, b, 0.65)

                local opp = player.TargetOpponent

                -- Frame 1: Lock target X & telegraph at top of screen above opponent
                if f == 1 then
                    character.CustomData["ult_x"] = (opp ~= nil) and opp.PosX or (player.PosX + (player.FacingRight and 260 or -260))
                    PlaySound(player, { sound = "prism_chime", pitch = 1.35, volume = 0.90 })
                    SpawnVFX(player, {
                        preset          = "Shockwave",
                        spawn_at_target = true,
                        y_offset_pct    = -0.55,
                        scale           = 1.25,
                        lifetime        = 0.30,
                        color           = { 255, 255, 255 }
                    })
                end

                -- Frame 14: Drop massive vertical Rainbow Ray from sky to ground
                if f == 28 then
                    PlaySound(player, {
                        sound         = "prism_chime",
                        pitch         = 1.80,
                        volume        = 1.0,
                        duck_world    = 0.08,
                        duck_frames   = 18,
                        duck_recovery = 0.65
                    })
                    TriggerScreenShake(8, 12, 16)

                    local targetX = character.CustomData["ult_x"] or player.PosX
                    local tickAcc = 0.0

                    local beam = CreateProjectile(player, {
                        shader             = "rainbow_water",
                        rotate_to_velocity = false,
                        x_offset_pct       = 0.25,
                        y_offset_pct       = 0.0,
                        speed_x_pct        = 0.0,
                        speed_y_pct        = 0.0,
                        width_pct          = 0.18,
                        height_pct         = 0.95,
                        damage             = 16,
                        hitStun            = 0.0,
                        is_blockbreak      = true,
                        destroy_on_hit     = false,
                        lifetime           = 0.65,
                        scroll_speed       = 3.4,
                        color              = { 255, 255, 255 },
                        trajectory         = function(proj, pDt)
                            if player.IsInHitstun or character.ActiveSkill == nil then
                                proj:Destroy()
                                return
                            end
                            tickAcc = tickAcc + pDt
                            if tickAcc >= 0.11 then
                                tickAcc = 0.0
                                proj:ResetHit()
                            end
                        end,
                        on_hit             = function(owner, hit_player, proj)
                            local mult = owner.CurrentCharacter.CustomData["dmg_mult"] or 1.0
                            local dmg  = math.floor(16 * mult)
                            ApplyHitForce(owner, hit_player, dmg, 0.0, 0.0)
                        end
                    })

                    if beam ~= nil then
                        pcall(function()
                            beam.Position = NewVec2(targetX, beam.GroundY * 0.5)
                        end)
                    end
                end
            end
        }
    }
}

return Character