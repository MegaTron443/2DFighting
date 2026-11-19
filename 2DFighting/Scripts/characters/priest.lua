-- ============================================================
-- CHROMATIC INQUISITOR PRIEST (Dynamic Grayscale LUT + Auras)
-- ============================================================

local function SetAura(character, auraId)
    character.CustomData["aura"] = auraId
    if auraId == 0 then
        -- WHITE: Purity / Smite / Heal
        character:SetPartShaderColor("body",   "u_PrimaryColor", 245, 245, 255)
        character:SetPartShaderColor("halo",   "u_GlowColor",    255, 240, 175)
        character:SetPartShaderColor("censer", "u_PrimaryColor", 255, 235, 160)
    elseif auraId == 1 then
        -- RED: Solar Heat / Sunburn
        character:SetPartShaderColor("body",   "u_PrimaryColor", 235, 70, 45)
        character:SetPartShaderColor("halo",   "u_GlowColor",    255, 130, 30)
        character:SetPartShaderColor("censer", "u_PrimaryColor", 255, 90, 40)
    elseif auraId == 2 then
        -- DARK: Forbidden Heresy / Vampirism
        character:SetPartShaderColor("body",   "u_PrimaryColor", 32, 18, 42)
        character:SetPartShaderColor("halo",   "u_GlowColor",    195, 25, 55)
        character:SetPartShaderColor("censer", "u_PrimaryColor", 65, 15, 35)
    elseif auraId == 3 then
        -- PURPLE: Arcane Sorcery / Warp
        character:SetPartShaderColor("body",   "u_PrimaryColor", 145, 75, 245)
        character:SetPartShaderColor("halo",   "u_GlowColor",    210, 120, 255)
        character:SetPartShaderColor("censer", "u_PrimaryColor", 170, 90, 255)
    end
end

local Character = {
    name = "ChromaticPriest",

    physics = {
        width_pct      = 0.05,
        height_pct     = 0.20,
        walk_speed_pct = 0.25,
        jump_force_pct = -0.64,
        gravity_pct    = 1.35,
        friction       = 0.86
    },

    rendering = {
        frame_width  = 128,
        frame_height = 128,
        body_color   = { 240, 240, 245 },
        shader       = "sprite_lut",

        parts = {
            body = {
                z_order    = 1,
                shader     = "sprite_lut",
                width_pct  = 1.0,
                height_pct = 1.0,
                color      = { 240, 240, 245 }
            },
            halo = {
                z_order      = 2,
                shader       = "sprite_lut",
                is_socket    = true,
                additive     = true,
                x_offset_pct = 0.0,
                y_offset_pct = -0.68,
                width_pct    = 0.85,
                height_pct   = 0.14,
                color        = { 255, 240, 180 }
            },
            censer = {
                z_order      = 3,
                shader       = "sprite_lut",
                is_socket    = true,
                x_offset_pct = 0.60,
                y_offset_pct = 0.05,
                width_pct    = 0.35,
                height_pct   = 0.28,
                color        = { 255, 230, 150 }
            }
        }
    },

    animations = {
        idle = { start_frame = 0, frame_count = 4, speed = 7.0 },
        walk = { start_frame = 4, frame_count = 6, speed = 9.0 }
    },

    on_passive = function(player, character, dt)
        if character.CustomData["aura"] == nil then
            SetAura(character, 0)
        end

        local t = (character.CustomData["t"] or 0) + dt
        character.CustomData["t"] = t
        local bobY = -0.68 + math.sin(t * 4.2) * 0.04
        local tilt = math.sin(t * 2.5) * 8.0
        character:SetPartOffset("halo", 0.0, bobY, tilt)
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
                    SetAura(character, 0)
                    PlaySound(player, { sound = "prism_chime", pitch = 1.20, volume = 0.8 })
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
                    SetAura(character, 0)
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
                    SetAura(character, 1)
                    PlaySound(player, { sound = "cosmic_heavy_slash", pitch = 1.05, volume = 0.85 })
                end
            end,
            on_hit = function(owner, hit_player, skill)
                local stacks = (owner.CurrentCharacter.CustomData["sunburn"] or 0) + 1
                if stacks >= 3 then
                    owner.CurrentCharacter.CustomData["sunburn"] = 0
                    ApplyLaunchForce(owner, hit_player, 10, 0.45, 0.10, -0.75)
                    SetHitstop(5)
                    TriggerScreenShake(10, 10, 10)
                    TriggerPaletteShift(255, 80, 30, 0.55, 8, 0.35)
                else
                    owner.CurrentCharacter.CustomData["sunburn"] = stacks
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
                    SetAura(character, 2)
                    if player.Health > 8 then
                        player.Health = player.Health - 7
                    end
                    PlaySound(player, { sound = "cosmic_sub_drop", pitch = 0.80, volume = 0.9 })
                end
            end,
            on_hit = function(owner, hit_player, skill)
                owner.Health = math.min(owner.MAX_HEALTH, owner.Health + 12)
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
                    SetAura(character, 3)
                    character:SpawnAfterimage(160, 70, 255)
                    PlaySound(player, { sound = "cosmic_light_slash", pitch = 1.65, volume = 0.85 })
                elseif character.CurrentActionFrame == 5 then
                    local dir = player.FacingRight and 1 or -1
                    player:SetVelocity(dir * 1300, 0)
                    character:SpawnAfterimage(200, 100, 255)
                end
            end
        },

        -- 6. CHROMATIC SHIFT (Detonates current Aura color!)
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
                local aura = owner.CurrentCharacter.CustomData["aura"] or 0
                if aura == 0 then
                    ApplyHitForce(owner, hit_player, 10, 0.35, 0.25)
                elseif aura == 1 then
                    TriggerPaletteShift(255, 80, 20, 0.5, 6, 0.3)
                elseif aura == 2 then
                    owner.Health = math.min(owner.MAX_HEALTH, owner.Health + 8)
                else
                    SetHitstop(8)
                end
                PlaySound(owner, { sound = "prism_chime", pitch = 1.0 + (aura * 0.2), volume = 0.9 })
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
                local cycle = math.floor(character.CurrentActionFrame / 2) % 4
                SetAura(character, cycle)
                character:SpawnAfterimage(255, 220, 120)

                if character.CurrentActionFrame == 10 then
                    PlaySound(player, {
                        sound = "prism_chime",
                        pitch = 1.80,
                        volume = 1.0,
                        duck_world = 0.05,
                        duck_frames = 16,
                        duck_recovery = 0.70
                    })
                end
            end,
            on_hit = function(owner, hit_player, skill)
                owner.Health = math.min(owner.MAX_HEALTH, owner.Health + 14)
                SetHitstop(10)
                TriggerImpactFrame(6, 1)
                TriggerScreenShake(16, 16, 18)
                TriggerPaletteShift(255, 240, 210, 0.85, 12, 0.60)
            end
        }
    }
}

return Character