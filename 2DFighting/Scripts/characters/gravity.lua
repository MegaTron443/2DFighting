-- ============================================================
-- GRAVITY ODACHI SAMURAI (Compound Shader + Stance Switcher)
-- ============================================================

local SPLIT_SLAM_VEL_Y_PCT      = 1.35
local COUNTER_SLAM_VEL_Y_PCT    = 1.55
local WELLSPRING_PULL_VEL_X_PCT = 0.50
local WELLSPRING_PULL_VEL_Y_PCT = -0.18
local COLLAPSE_DASH_VEL_X_PCT   = 1.05

local function SetStance(character, stance)
    character.CustomData["stance"] = stance
    if stance == 0 then
        character.WalkSpeedPct = 0.21
        character.GravityPct   = 1.48
        character:SetPartVisible("sword", true)
        character:SetPartShaderColor("sword", "u_GlowColor", 215, 165, 255)
    else
        character.WalkSpeedPct = 0.33
        character.GravityPct   = 1.05
        character:SetPartVisible("sword", true)
        character:SetPartShaderColor("sword", "u_GlowColor", 240, 200, 255)
    end
end

local Character = {
    name = "GravitySamurai",
    description = [[
STANCE SWITCHER / COSMIC BLADEMASTER
Void-forged samurai shifting between Fused heavy odachi strikes and Split high-mobility gravity blades.

PASSIVE:
Custom stance engine (Fused = heavy/high gravity, Split = fast/lightweight) + cosmic blade aura.

MOVE LIST:
- Normal Slash (Right): Heavy odachi cleave in Fused stance or rapid flurry in Split stance.
- Gravity Upper (Top): Fused zeroes victim gravity on launch; Split slams aerial foes downward.
- Event Horizon (Left): Overhead gravity shockwave projectile.
- Anchor Counter (Bottom): Heavy counter slam flattening attackers into the floor.
- Stance Switch (Top+Right): Shifts gravity stance and empowers the cosmic edge.
- Wellspring Pull (Bottom+Left): Gravity vacuum slash yanking enemies toward you with radial pinch.
- Collapse Execution (Top+Bottom): Armored dash impale; executes foes below 20 HP in a black hole.
]],

    physics = {
        width_pct      = 0.055,
        height_pct     = 0.21,
        walk_speed_pct = 0.21,
        jump_force_pct = -0.66,
        gravity_pct    = 1.48,
        friction       = 0.84
    },

    rendering = {
        frame_width  = 256,
        frame_height = 256,
        body_color   = { 45, 38, 65 },
        shader       = "gravity_ink",

        parts = {
            left_leg = {
                sprite_sheet = "Assets/Characters/gravity/left_leg.png",
                shader       = "gravity_ink",
                z_order      = 1,
                x_offset_pct = -0.17,
                y_offset_pct = 0.36,
                width_pct    = 0.30,
                height_pct   = 0.26
            },
            right_leg = {
                sprite_sheet = "Assets/Characters/gravity/right_leg.png",
                shader       = "gravity_ink",
                z_order      = 1,
                x_offset_pct = 0.17,
                y_offset_pct = 0.36,
                width_pct    = 0.30,
                height_pct   = 0.26
            },
            body = {
                sprite_sheet = "Assets/Characters/gravity/body.png",
                shader       = "gravity_ink",
                z_order      = 2,
                x_offset_pct = 0.0,
                y_offset_pct = 0.04,
                width_pct    = 1.00,
                height_pct   = 0.46
            },
            head = {
                sprite_sheet = "Assets/Characters/gravity/head.png",
                shader       = "gravity_ink",
                z_order      = 3,
                x_offset_pct = 0.0,
                y_offset_pct = -0.27,
                width_pct    = 0.62,
                height_pct   = 0.19
            },
            hat = {
                sprite_sheet = "Assets/Characters/gravity/hat.png",
                shader       = "gravity_ink",
                z_order      = 4,
                x_offset_pct = 0.0,
                y_offset_pct = -0.39,
                width_pct    = 1.65,
                height_pct   = 0.14
            },
            sword = {
                sprite_sheet = "Assets/Characters/gravity/sword.png",
                shader       = "gravity_ink",
                z_order      = 5,
                is_socket    = true,
                visible      = true,
                x_offset_pct = 1.0,
                y_offset_pct = 0.24,
                width_pct    = 1.55,
                height_pct   = 0.62,
                rotation     = -72
            }
        }
    },

    animations = {
        idle = { start_frame = 0, frame_count = 1, speed = 6.0 },
        walk = { start_frame = 0, frame_count = 1, speed = 10.0 },
        Heavy_Slash = {
            keyframes = {
                { frame = 0, duration = 8, afterimage = false, weapon_x = -0.35, weapon_y = -0.35, weapon_angle = -45 },
                { frame = 0, duration = 2, afterimage = true,  weapon_x = 0.35,  weapon_y = -0.10, weapon_angle = 35  },
                { frame = 0, duration = 2, afterimage = true,  weapon_x = 0.45,  weapon_y = 0.05,  weapon_angle = 75  },
                { frame = 0, duration = 7, afterimage = false, weapon_x = 0.30,  weapon_y = 0.15,  weapon_angle = 90  }
            }
        },
        Upper_Slash = {
            keyframes = {
                { frame = 0, duration = 6, afterimage = false, weapon_x = 0.15,  weapon_y = 0.25,  weapon_angle = 110 },
                { frame = 0, duration = 2, afterimage = true,  weapon_x = 0.40,  weapon_y = -0.25, weapon_angle = 10  },
                { frame = 0, duration = 2, afterimage = true,  weapon_x = 0.25,  weapon_y = -0.45, weapon_angle = -45 },
                { frame = 0, duration = 6, afterimage = false, weapon_x = 0.15,  weapon_y = -0.40, weapon_angle = -35 }
            }
        }
    },

    on_passive = function(player, character, dt)
        if character.CustomData["stance"] == nil then
            SetStance(character, 0)

            character:SetPartShaderVec2("sword", "u_SingularityUV", 0.21, 0.21)
            character:SetPartShaderFloat("sword", "u_SingularityScale", 0.24)
            character:SetPartShaderFloat("sword", "u_SingularityAngle", 0.75) -- Tilts accretion disk along the guard!

            -- Left & Right boot black holes
            character:SetPartShaderVec2("left_leg", "u_SingularityUV", 0.50, 0.90)
            character:SetPartShaderFloat("left_leg", "u_SingularityScale", 0.42)
            character:SetPartShaderFloat("left_leg", "u_SingularityAngle", 0.0)

            character:SetPartShaderVec2("right_leg", "u_SingularityUV", 0.50, 0.90)
            character:SetPartShaderFloat("right_leg", "u_SingularityScale", 0.42)
            character:SetPartShaderFloat("right_leg", "u_SingularityAngle", 0.0)

            character:SetShaderFloat("u_ParallaxStrength", 0.16)
            character:SetShaderFloat("u_SpaceScrollSpeed", 0.03)
        end

        character:SetShaderVec2("u_CharacterPos", player.PosX, player.PosY)
        character:SetShaderVec2("u_CharacterVelocity", player.Velocity.X, player.Velocity.Y)

        local targetFlare = (character.ActiveSkill ~= nil) and 1.35 or 0.0
        local curFlare = character.CustomData["flare"] or 0.0
        curFlare = curFlare + (targetFlare - curFlare) * math.min(1.0, dt * 12.0)
        character.CustomData["flare"] = curFlare
        character:SetShaderFloat("u_AttackIntensity", curFlare)
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
                    hit_player:SetVelocityPct(0, SPLIT_SLAM_VEL_Y_PCT)
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
                attacker:SetVelocityPct(0, COUNTER_SLAM_VEL_Y_PCT)
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
                Log(character.CustomData["stance"])
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
                hit_player:SetVelocityPct(dir * WELLSPRING_PULL_VEL_X_PCT, WELLSPRING_PULL_VEL_Y_PCT)
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
                    player:SetVelocityPct(dir * COLLAPSE_DASH_VEL_X_PCT, 0)
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