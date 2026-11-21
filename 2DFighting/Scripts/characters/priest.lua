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
        idle = { start_frame = 0, frame_count = 4, speed = 7.0 },
        walk = { start_frame = 4, frame_count = 6, speed = 9.0 }
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
    end,

    skills = {
        -- 1. WHITE: Sanctified Ray (Smite)
        Sanctified_Ray = {
            input_trigger   = Keys.KeyRight,
            damage          = 14,
            attack_height   = "mid",
            startup_frames  = 6,
            active_frames   = 5,
            recovery_frames = 8,
            hitStun         = 0.30,
            cooldown_time   = 0.5,
            hitbox          = { x_offset_pct = 0.75, y_offset_pct = 0.0, width_pct = 1.15, height_pct = 0.75 },
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 1 then
                    ShiftWaterVector(character, 245, 248, 255, 0.45)
                    PlaySound(player, { sound = "prism_chime", pitch = 1.20, volume = 0.8 })
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

        -- 2. WHITE: Benediction Pulse (Heal)
        Benediction_Pulse = {
            input_trigger   = Keys.KeyBottom + Keys.KeyRight,
            damage          = 0,
            startup_frames  = 14,
            active_frames   = 4,
            recovery_frames = 10,
            cooldown_time   = 4.5,
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 1 then
                    ShiftWaterVector(character, 245, 248, 255, 0.50)
                elseif character.CurrentActionFrame == 14 then
                    player.Health = math.min(player.MAX_HEALTH, player.Health + 12)
                    PlaySound(player, { sound = "prism_chime", pitch = 1.50, volume = 0.9 })
                    TriggerPaletteShift(255, 250, 220, 0.35, 6, 0.30)
                end
            end
        },

        -- 3. RED: Crimson Censer Arc (Sunburn Passive Buildup)
        Crimson_Censer = {
            input_trigger   = Keys.KeyTop,
            damage          = 16,
            attack_height   = "mid",
            startup_frames  = 7,
            active_frames   = 5,
            recovery_frames = 9,
            hitStun         = 0.32,
            cooldown_time   = 0.8,
            hitbox          = { x_offset_pct = 0.70, y_offset_pct = 0.15, width_pct = 1.25, height_pct = 0.50 },
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 1 then
                    ShiftWaterVector(character, 255, 55, 35, 0.50)
                    PlaySound(player, { sound = "cosmic_heavy_slash", pitch = 1.05, volume = 0.85 })
                end
            end,
            on_hit = function(owner, hit_player, skill)
                local ch   = owner.CurrentCharacter
                local mult = ch.CustomData["dmg_mult"] or 1.0
                local ls   = ch.CustomData["lifesteal"] or 0.0
                local dmg  = math.floor(skill.Damage * mult)

                local stacks = (ch.CustomData["sunburn"] or 0) + 1
                if stacks >= 3 then
                    ch.CustomData["sunburn"] = 0
                    ApplyLaunchForce(owner, hit_player, dmg + 10, 0.45, 0.10, -0.75)
                    SetHitstop(5)
                    TriggerScreenShake(10, 10, 10)
                    TriggerPaletteShift(255, 80, 30, 0.55, 8, 0.35)
                else
                    ch.CustomData["sunburn"] = stacks
                    ApplyHitForce(owner, hit_player, dmg, skill.HitStun, 0.16)
                end

                if ls > 0.05 then
                    owner.Health = math.min(owner.MAX_HEALTH, owner.Health + math.floor(dmg * ls))
                end
            end
        },

        -- 4. DARK: Abyssal Heresy (Self-HP Cost + Guard Break + Vampirism)
        Abyssal_Heresy = {
            input_trigger   = Keys.KeyLeft,
            damage          = 26,
            attack_height   = "overhead",
            startup_frames  = 9,
            active_frames   = 5,
            recovery_frames = 12,
            is_blockbreak   = true,
            hitStun         = 0.45,
            cooldown_time   = 2.2,
            hitbox          = { x_offset_pct = 0.70, y_offset_pct = 0.05, width_pct = 1.20, height_pct = 0.65 },
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 1 then
                    ShiftWaterVector(character, 38, 12, 32, 0.55)
                    if player.Health > 8 then
                        player.Health = player.Health - 7
                    end
                    PlaySound(player, { sound = "cosmic_sub_drop", pitch = 0.80, volume = 0.9 })
                end
            end,
            on_hit = function(owner, hit_player, skill)
                local mult = owner.CurrentCharacter.CustomData["dmg_mult"] or 1.0
                local ls   = owner.CurrentCharacter.CustomData["lifesteal"] or 0.0
                local dmg  = math.floor(skill.Damage * mult)

                ApplyHitForce(owner, hit_player, dmg, skill.HitStun, 0.20)
                owner.Health = math.min(owner.MAX_HEALTH, owner.Health + 12 + math.floor(dmg * ls))
                SetHitstop(6)
                TriggerScreenShake(12, 12, 12)
                TriggerPaletteShift(140, 15, 45, 0.65, 8, 0.40)
            end
        },

        -- 5. PURPLE: Violet Warp (Arcane Blink Teleport)
        Violet_Warp = {
            input_trigger   = Keys.KeyBottom,
            damage          = 12,
            startup_frames  = 5,
            active_frames   = 4,
            recovery_frames = 7,
            hitStun         = 0.28,
            cooldown_time   = 1.6,
            hitbox          = { x_offset_pct = 0.30, y_offset_pct = 0.10, width_pct = 1.00, height_pct = 0.75 },
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 1 then
                    ShiftWaterVector(character, 155, 55, 255, 0.50)
                    character:SpawnAfterimage(160, 70, 255)
                    PlaySound(player, { sound = "cosmic_light_slash", pitch = 1.65, volume = 0.85 })
                elseif character.CurrentActionFrame == 5 then
                    local dir = player.FacingRight and 1 or -1
                    player:SetVelocityPct(dir * VIOLET_WARP_VEL_X_PCT, 0)
                    character:SpawnAfterimage(200, 100, 255)
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

        -- 6. CHROMATIC SHIFT (Detonates dominant Water Vector channel!)
        Chromatic_Shift = {
            input_trigger   = Keys.KeyTop + Keys.KeyLeft,
            damage          = 14,
            startup_frames  = 5,
            active_frames   = 6,
            recovery_frames = 10,
            hitStun         = 0.35,
            cooldown_time   = 2.0,
            hitbox          = { x_offset_pct = 0.40, y_offset_pct = 0.05, width_pct = 1.40, height_pct = 0.85 },
            on_hit          = function(owner, hit_player, skill)
                local ch = owner.CurrentCharacter
                local _, _, _, redDom, arcDom, pure, abyss = GetWaterState(ch)
                local mult = ch.CustomData["dmg_mult"] or 1.0
                local dmg  = math.floor(skill.Damage * mult)

                if redDom >= arcDom and redDom >= pure and redDom >= abyss and redDom > 0.15 then
                    -- Red dominant: Solar detonation + heavy burn flash
                    ApplyHitForce(owner, hit_player, dmg + 8, 0.40, 0.24)
                    TriggerPaletteShift(255, 80, 20, 0.55, 6, 0.30)
                    PlaySound(owner, { sound = "prism_chime", pitch = 1.20, volume = 0.9 })
                elseif abyss >= arcDom and abyss >= pure and abyss > 0.15 then
                    -- Dark dominant: Abyssal drain
                    ApplyHitForce(owner, hit_player, dmg, 0.38, 0.16)
                    owner.Health = math.min(owner.MAX_HEALTH, owner.Health + 10)
                    TriggerPaletteShift(120, 15, 45, 0.50, 6, 0.30)
                    PlaySound(owner, { sound = "prism_chime", pitch = 0.85, volume = 0.9 })
                elseif arcDom >= pure and arcDom > 0.15 then
                    -- Purple dominant: Arcane freeze hitstop
                    ApplyHitForce(owner, hit_player, dmg, 0.45, 0.12)
                    SetHitstop(8)
                    PlaySound(owner, { sound = "prism_chime", pitch = 1.55, volume = 0.9 })
                else
                    -- White / Cyan dominant: Purifying knockback wave
                    ApplyHitForce(owner, hit_player, dmg, 0.35, 0.28)
                    PlaySound(owner, { sound = "prism_chime", pitch = 1.05, volume = 0.9 })
                end
            end
        },

        -- 7. ULTIMATE: LUX AETERNA (Full-Spectrum Rainbow Prism Beam)
        Lux_Aeterna = {
            input_trigger     = Keys.KeyTop + Keys.KeyBottom,
            damage            = 38,
            attack_height     = "mid",
            startup_frames    = 10,
            active_frames     = 12,
            recovery_frames   = 16,
            is_hyperframes    = true,
            hyper_start_frame = 2,
            hyper_frames      = 14,
            is_blockbreak     = true,
            hitStun           = 0.70,
            cooldown_time     = 8.5,
            hitbox            = { x_offset_pct = 0.75, y_offset_pct = 0.05, width_pct = 2.80, height_pct = 0.75 },
            is_customlogic    = true,
            on_customlogic    = function(player, character, dt)
                local f = character.CurrentActionFrame
                local r = 128 + math.floor(127 * math.sin(f * 0.45))
                local g = 128 + math.floor(127 * math.sin(f * 0.45 + 2.09))
                local b = 128 + math.floor(127 * math.sin(f * 0.45 + 4.18))
                ShiftWaterVector(character, r, g, b, 0.65)
                character:SpawnAfterimage(r, g, b)

                if f == 10 then
                    PlaySound(player, {
                        sound         = "prism_chime",
                        pitch         = 1.80,
                        volume        = 1.0,
                        duck_world    = 0.05,
                        duck_frames   = 16,
                        duck_recovery = 0.70
                    })
                end
            end,
            on_hit = function(owner, hit_player, skill)
                local mult = owner.CurrentCharacter.CustomData["dmg_mult"] or 1.0
                local ls   = owner.CurrentCharacter.CustomData["lifesteal"] or 0.0
                local dmg  = math.floor(skill.Damage * mult)

                ApplyHitForce(owner, hit_player, dmg, skill.HitStun, 0.26)
                owner.Health = math.min(owner.MAX_HEALTH, owner.Health + 14 + math.floor(dmg * ls))
                SetHitstop(10)
                TriggerImpactFrame(6, 1)
                TriggerScreenShake(16, 16, 18)
                TriggerPaletteShift(255, 240, 210, 0.85, 12, 0.60)
            end
        }
    }
}

return Character