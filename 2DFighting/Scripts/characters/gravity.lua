-- ============================================================
-- GRAVITY ODACHI SAMURAI (Compound Shader + Stance Switcher)
-- ============================================================

local function SetStance(character, stance)
    character.CustomData["stance"] = stance
    if stance == 0 then
        -- FUSED STANCE: Heavy, slow, huge black+purple odachi
        character.WalkSpeedPct = 0.21
        character.GravityPct   = 1.48
        character:SetPartVisible("sword_heavy", true)
        character:SetPartShaderColor("sword_core", "u_GlowColor", 160, 50, 255)
    else
        -- SPLIT STANCE: Agile, floaty, lightweight purple core only
        character.WalkSpeedPct = 0.33
        character.GravityPct   = 1.05
        character:SetPartVisible("sword_heavy", false)
        character:SetPartShaderColor("sword_core", "u_GlowColor", 220, 120, 255)
    end
end

local Character = {
    name = "GravitySamurai",

    physics = {
        width_pct      = 0.055,
        height_pct     = 0.21,
        walk_speed_pct = 0.21,
        jump_force_pct = -0.66,
        gravity_pct    = 1.48,
        friction       = 0.84
    },

    rendering = {
        frame_width  = 128,
        frame_height = 128,
        body_color   = { 45, 38, 65 },
        shader       = "sprite_lut",

        parts = {
            body = {
                z_order      = 1,
                shader       = "sprite_lut",
                width_pct    = 1.0,
                height_pct   = 0.78,
                y_offset_pct = 0.11,
                color        = { 55, 48, 80 }
            },
            head_portal = {
                z_order      = 2,
                shader       = "sprite_lut",
                portal_mode  = true,
                width_pct    = 0.65,
                height_pct   = 0.26,
                y_offset_pct = -0.40,
                color        = { 255, 255, 255 }
            },
            sword_core = {
                z_order      = 3,
                shader       = "sprite_lut",
                is_socket    = true,
                additive     = true,
                x_offset_pct = 0.65,
                y_offset_pct = -0.05,
                width_pct    = 0.22,
                height_pct   = 1.05,
                rotation     = -25,
                color        = { 175, 60, 255 }
            },
            sword_heavy = {
                z_order      = 4,
                shader       = "sprite_lut",
                is_socket    = true,
                visible      = true,
                x_offset_pct = 0.72,
                y_offset_pct = -0.08,
                width_pct    = 0.42,
                height_pct   = 1.35,
                rotation     = -25,
                color        = { 22, 16, 32 }
            }
        }
    },

    animations = {
        idle = { start_frame = 0, frame_count = 4, speed = 6.0 },
        walk = { start_frame = 4, frame_count = 6, speed = 10.0 },
        Heavy_Slash = {
            keyframes = {
                { frame = 10, duration = 8, afterimage = false, weapon_x = -0.35, weapon_y = -0.25, weapon_angle = -75 },
                { frame = 11, duration = 2, afterimage = true,  weapon_x = 0.90,  weapon_y = -0.05, weapon_angle = 45  },
                { frame = 12, duration = 2, afterimage = true,  weapon_x = 1.15,  weapon_y = 0.10,  weapon_angle = 88  },
                { frame = 13, duration = 7, afterimage = false, weapon_x = 0.85,  weapon_y = 0.20,  weapon_angle = 105 }
            }
        },
        Upper_Slash = {
            keyframes = {
                { frame = 14, duration = 6, afterimage = false, weapon_x = 0.30,  weapon_y = 0.35,  weapon_angle = 125 },
                { frame = 15, duration = 2, afterimage = true,  weapon_x = 0.80,  weapon_y = -0.30, weapon_angle = -25 },
                { frame = 16, duration = 2, afterimage = true,  weapon_x = 0.50,  weapon_y = -0.60, weapon_angle = -80 },
                { frame = 17, duration = 6, afterimage = false, weapon_x = 0.35,  weapon_y = -0.50, weapon_angle = -70 }
            }
        }
    },

    on_passive = function(player, character, dt)
        if character.CustomData["stance"] == nil then
            SetStance(character, 0)
        end

        local t = (character.CustomData["t"] or 0) + dt
        character.CustomData["t"] = t
        local pulse = 150 + math.floor(math.sin(t * 6.0) * 40)
        character:SetPartShaderColor("sword_core", "u_GlowColor", pulse, 60, 255)
    end,

    skills = {
        -- 1. Normal Slash (Horizon Cleave / Singularity Flurry)
        Normal_Slash = {
            input_trigger   = Keys.KeyRight,
            animation       = "Heavy_Slash",
            damage          = 16,
            attack_height   = "mid",
            startup_frames  = 8,
            active_frames   = 4,
            recovery_frames = 9,
            hitStun         = 0.32,
            cooldown_time   = 0.35,
            hitbox          = { x_offset_pct = 0.75, y_offset_pct = 0.10, width_pct = 1.20, height_pct = 0.45 },
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                local stance = character.CustomData["stance"] or 0
                if character.CurrentActionFrame == 2 then
                    if stance == 0 then
                        PlaySound(player, { sound = "cosmic_heavy_slash", pitch = 0.85, volume = 0.9 })
                    else
                        PlaySound(player, { sound = "cosmic_light_slash", pitch = 1.55, volume = 0.75 })
                        character:SpawnAfterimage(195, 80, 255)
                    end
                end
            end,
            on_hit = function(owner, hit_player, skill)
                local stance = owner.CurrentCharacter.CustomData["stance"] or 0
                if stance == 0 then
                    SetHitstop(4)
                    TriggerScreenShake(8, 3, 8)
                else
                    SetHitstop(2)
                    TriggerScreenShake(3, 1, 4)
                end
            end
        },

        -- 2. Zero-G Upper (Fused Launcher) / Crushing Singularity (Split Overhead)
        Gravity_Upper = {
            input_trigger   = Keys.KeyTop,
            animation       = "Upper_Slash",
            damage          = 20,
            attack_height   = "overhead",
            startup_frames  = 7,
            active_frames   = 5,
            recovery_frames = 10,
            hitStun         = 0.48,
            cooldown_time   = 1.2,
            hitbox          = { x_offset_pct = 0.65, y_offset_pct = -0.25, width_pct = 1.05, height_pct = 0.90 },
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 5 then
                    character:SpawnAfterimage(150, 40, 255)
                    PlaySound(player, { sound = "cosmic_heavy_slash", pitch = 1.15, volume = 0.9 })
                end
            end,
            on_hit = function(owner, hit_player, skill)
                local stance = owner.CurrentCharacter.CustomData["stance"] or 0
                if stance == 0 then
                    hit_player.IsAirStalled = true
                    ApplyLaunchForce(owner, hit_player, skill.Damage, 0.55, 0.08, -0.82)
                    SetHitstop(5)
                    TriggerScreenShake(0, 12, 10)
                    TriggerPaletteShift(140, 40, 255, 0.45, 6, 0.35)
                else
                    hit_player.IsAirStalled = false
                    hit_player:SetVelocity(0, 900)
                    SetHitstop(4)
                    TriggerScreenShake(0, 16, 12)
                    PlaySound(owner, { sound = "cosmic_sub_drop", pitch = 1.05, duck_world = 0.15, duck_frames = 8, duck_recovery = 0.4 })
                end
            end
        },

        -- 3. Event Horizon (Overhead Gravity Wave Projectile)
        Event_Horizon = {
            input_trigger   = Keys.KeyLeft,
            animation       = "Heavy_Slash",
            damage          = 16,
            attack_height   = "overhead",
            startup_frames  = 10,
            active_frames   = 4,
            recovery_frames = 12,
            hitStun         = 0.35,
            cooldown_time   = 1.8,
            hitbox          = { x_offset_pct = 0.70, y_offset_pct = 0.10, width_pct = 1.10, height_pct = 0.50 },
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 10 then
                    PlaySound(player, { sound = "cosmic_sub_drop", pitch = 1.10, volume = 0.85 })
                    TriggerScreenShake(6, 6, 8)
                    CreateProjectile(player, {
                        x_offset_pct = 0.06,
                        y_offset_pct = 0.0,
                        speed_x_pct  = 0.48,
                        width_pct    = 0.045,
                        height_pct   = 0.065,
                        damage       = 15,
                        hitStun      = 0.35,
                        color        = { 155, 50, 255 }
                    })
                end
            end
        },

        -- 4. Anchor Counter (Parry -> Upfling & Down Slam)
        Anchor_Counter = {
            input_trigger   = Keys.KeyBottom,
            damage          = 24,
            startup_frames  = 3,
            active_frames   = 14,
            recovery_frames = 12,
            hitbox          = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 1.1, height_pct = 1.0 },
            hitStun         = 0.65,
            is_counter      = true,
            cooldown_time   = 2.5,
            on_counter      = function(owner, attacker)
                SetHitstop(8)
                TriggerImpactFrame(4, 1)
                TriggerScreenShake(0, 22, 16)
                TriggerPaletteShift(135, 25, 255, 0.75, 10, 0.45)
                PlaySound(owner, {
                    sound = "cosmic_sub_drop",
                    pitch = 0.80,
                    duck_world = 0.04,
                    duck_frames = 14,
                    duck_recovery = 0.65
                })
                ApplyHitForce(owner, attacker, 24, 0.65, 0.15)
                attacker:SetVelocity(0, 1100)
            end
        },

        -- 5. Jettison Cleave / Magnetic Recall (Stance Switch)
        Stance_Switch = {
            input_trigger   = Keys.KeyTop + Keys.KeyRight,
            damage          = 14,
            startup_frames  = 6,
            active_frames   = 5,
            recovery_frames = 8,
            hitStun         = 0.30,
            cooldown_time   = 1.0,
            hitbox          = { x_offset_pct = 0.70, y_offset_pct = 0.15, width_pct = 1.30, height_pct = 0.40 },
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 6 then
                    local stance = character.CustomData["stance"] or 0
                    if stance == 0 then
                        SetStance(character, 1)
                        TriggerScreenShake(10, 0, 8)
                        PlaySound(player, { sound = "cosmic_heavy_slash", pitch = 0.75, volume = 0.95 })
                    else
                        SetStance(character, 0)
                        character:SpawnAfterimage(180, 60, 255)
                        TriggerScreenShake(8, 4, 8)
                        PlaySound(player, { sound = "cosmic_recall", pitch = 1.0, volume = 0.95 })
                    end
                end
            end
        },

        -- 6. Wellspring Pull (Gravity Vacuum Wave)
        Wellspring_Pull = {
            input_trigger   = Keys.KeyBottom + Keys.KeyLeft,
            damage          = 10,
            attack_height   = "mid",
            startup_frames  = 6,
            active_frames   = 6,
            recovery_frames = 8,
            hitStun         = 0.30,
            cooldown_time   = 1.5,
            hitbox          = { x_offset_pct = 0.80, y_offset_pct = 0.10, width_pct = 1.60, height_pct = 0.55 },
            on_hit          = function(owner, hit_player, skill)
                local dir = owner.FacingRight and -1 or 1
                hit_player:SetVelocity(dir * 520, -120)
                TriggerGravityPinch(0.28, 6, 0.25, 0.5, 0.5)
                PlaySound(owner, { sound = "cosmic_recall", pitch = 0.85, volume = 0.8 })
            end
        },

        -- 7. Collapse Execution (Ultimate Dash Impale + <20% HP Black-Hole Execute)
        Collapse_Execution = {
            input_trigger     = Keys.KeyTop + Keys.KeyBottom,
            animation         = "Heavy_Slash",
            damage            = 38,
            attack_height     = "mid",
            startup_frames    = 8,
            active_frames     = 8,
            recovery_frames   = 16,
            is_hyperframes    = true,
            hyper_start_frame = 2,
            hyper_frames      = 12,
            is_blockbreak     = true,
            hitStun           = 0.75,
            cooldown_time     = 8.0,
            hitbox            = { x_offset_pct = 0.65, y_offset_pct = 0.05, width_pct = 1.35, height_pct = 0.65 },
            is_customlogic    = true,
            on_customlogic    = function(player, character, dt)
                if character.CurrentActionFrame >= 4 and character.CurrentActionFrame <= 12 then
                    local dir = player.FacingRight and 1 or -1
                    player:SetVelocity(dir * 1150, 0)
                    character:SpawnAfterimage(160, 35, 255)
                end
            end,
            on_hit = function(owner, hit_player, skill)
                SetStance(owner.CurrentCharacter, 0)

                local isExecute = (hit_player.Health <= 20)
                if isExecute then
                    ApplyHitForce(owner, hit_player, 999, 1.0, 0.0)
                    SetHitstop(14)
                    TriggerImpactFrame(8, 2)
                    TriggerScreenShake(18, 26, 22)
                    TriggerGravityPinch(0.75, 14, 0.55, 0.5, 0.5)
                    TriggerPaletteShift(145, 25, 255, 0.95, 16, 0.75)
                    PlaySound(owner, {
                        sound = "cosmic_sub_drop",
                        pitch = 0.65,
                        volume = 1.0,
                        duck_world = 0.0,
                        duck_frames = 18,
                        duck_recovery = 0.85
                    })
                else
                    SetHitstop(8)
                    TriggerImpactFrame(5, 1)
                    TriggerScreenShake(10, 20, 16)
                    TriggerGravityPinch(0.45, 10, 0.40, 0.5, 0.5)
                    TriggerPaletteShift(145, 35, 255, 0.75, 10, 0.45)
                    PlaySound(owner, {
                        sound = "cosmic_sub_drop",
                        pitch = 0.85,
                        volume = 1.0,
                        duck_world = 0.04,
                        duck_frames = 14,
                        duck_recovery = 0.65
                    })
                end
            end
        }
    }
}

return Character