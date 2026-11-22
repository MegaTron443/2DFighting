-- ==========================================
-- LANCER (Long-Reach Footsies & Zoner - Wind Lancer Palette)
-- ==========================================

local POKE_KNOCKBACK_PCT         = 0.25
local SWEEP_KNOCKBACK_PCT        = 0.28
local DEFLECT_COUNTER_DAMAGE     = 20
local DEFLECT_COUNTER_STUN       = 0.95
local DEFLECT_KNOCKBACK_PCT      = 0.35
local DEFLECT_BACKSTEP_VEL_X_PCT = 0.32

local IMPALE_BREAK_DAMAGE        = 35
local IMPALE_BREAK_STUN          = 1.15
local IMPALE_KNOCKBACK_PCT       = 0.34

local SKY_PIERCE_LAUNCH_X_PCT    = 0.12
local SKY_PIERCE_LAUNCH_Y_PCT    = -0.75

local DRAGOON_BREAK_DAMAGE       = 45
local DRAGOON_BREAK_STUN         = 1.35
local DRAGOON_KNOCKBACK_PCT      = 0.36

local Character = {
    name = "Lancer",
    description = [[
FOOTSIES / LONG-REACH ZONER
Disciplined polearm duelist who dominates mid-screen spacing with disjointed thrusts and retreating counters.

PASSIVE:
None — excels at range control and whiff punishing.

MOVE LIST:
- Spear Poke (Top): Fast, long-range horizontal thrust for neutral control.
- Halberd Reap (Bottom): Sweeping low slice with massive horizontal reach.
- Royal Deflect (Left): Parry stance that backsteps and punishes the attacker.
- Retreating Vault (Right): Strikes forward while leaping backward to maintain ideal spacing.
- Grand Impale (Top+Bottom): Extreme-reach piercing lance thrust that shatters guards.
- Sky Crescent (Top+Right): Overhead anti-air arc launching jumping foes.
- Dragoon Dive (Top+Bottom+Right): Armored aerial vault and steep downward block-breaking plunge.
]],

    physics = {
        width_pct      = 0.048,
        height_pct     = 0.205,
        walk_speed_pct = 0.28,
        jump_force_pct = -0.70,
        gravity_pct    = 1.42,
        friction       = 0.84
    },

    rendering = {
        frame_width  = 256,
        frame_height = 256,
        body_color   = { 34, 145, 84 },
        shader       = "lancer_wind",

        parts = {
            cape = {
                sprite_sheet    = "Assets/Characters/lancer/Cape.png",
                shader          = "lancer_wind",
                z_order         = 1,
                x_offset_pct    = -0.06,
                y_offset_pct    = 0.06,
                width_pct       = 1.00,
                height_pct      = 0.56,
                rotation        = 5,
                color           = { 34, 145, 84 },
                secondary_color = { 15, 72, 42 },
                glow_color      = { 80, 225, 140 }
            },
            left_arm = {
                sprite_sheet    = "Assets/Characters/lancer/Left_arm.png",
                shader          = "lancer_wind",
                z_order         = 2,
                x_offset_pct    = -0.35,
                y_offset_pct    = 0.04,
                width_pct       = 0.26,
                height_pct      = 0.43,
                rotation        = 14,
                color           = { 176, 188, 204 },
                secondary_color = { 72, 82, 98 },
                glow_color      = { 80, 225, 140 }
            },
            left_leg = {
                sprite_sheet    = "Assets/Characters/lancer/Left_leg.png",
                shader          = "lancer_wind",
                z_order         = 3,
                x_offset_pct    = -0.18,
                y_offset_pct    = 0.33,
                width_pct       = 0.27,
                height_pct      = 0.34,
                rotation        = 8,
                color           = { 172, 184, 200 },
                secondary_color = { 68, 78, 94 },
                glow_color      = { 80, 225, 140 }
            },
            right_leg = {
                sprite_sheet    = "Assets/Characters/lancer/Right_leg.png",
                shader          = "lancer_wind",
                z_order         = 4,
                x_offset_pct    = 0.18,
                y_offset_pct    = 0.33,
                width_pct       = 0.27,
                height_pct      = 0.34,
                rotation        = -8,
                color           = { 64, 70, 82 },
                secondary_color = { 32, 36, 44 },
                glow_color      = { 80, 225, 140 }
            },
            body = {
                sprite_sheet    = "Assets/Characters/lancer/Body.png",
                shader          = "lancer_wind",
                z_order         = 5,
                x_offset_pct    = 0.0,
                y_offset_pct    = 0.02,
                width_pct       = 0.66,
                height_pct      = 0.44,
                rotation        = 3,
                color           = { 176, 188, 204 },
                secondary_color = { 54, 60, 72 },
                glow_color      = { 80, 225, 140 }
            },
            head = {
                sprite_sheet    = "Assets/Characters/lancer/Head.png",
                shader          = "lancer_wind",
                z_order         = 6,
                x_offset_pct    = 0.02,
                y_offset_pct    = -0.28,
                width_pct       = 0.62,
                height_pct      = 0.25,
                rotation        = 0,
                color           = { 34, 145, 84 },
                secondary_color = { 15, 72, 42 },
                glow_color      = { 80, 225, 140 }
            },
            hair = {
                sprite_sheet    = "Assets/Characters/lancer/Hair.png",
                shader          = "lancer_wind",
                z_order         = 7,
                x_offset_pct    = 0.00,
                y_offset_pct    = -0.38, -- Lifted above eyebrows & covers top of Head.png
                width_pct       = 0.68,
                height_pct      = 0.17,
                rotation        = 0,
                color           = { 242, 246, 252 },
                secondary_color = { 195, 204, 218 },
                glow_color      = { 80, 225, 140 }
            },
            lance = {
                sprite_sheet    = "Assets/Characters/lancer/Lance.png",
                shader          = "lancer_wind",
                z_order         = 8,
                is_socket       = true,
                visible         = true,
                x_offset_pct    = 0.64,
                y_offset_pct    = 0.46,              -- Drops grip directly onto the right hand circle
                width_pct       = 1.65,
                height_pct      = 0.72,
                rotation        = -65,               -- 20 degrees upward
                color           = { 188, 200, 216 },
                secondary_color = { 56, 62, 74 },
                glow_color      = { 80, 225, 140 }
            },
            right_arm = {
                sprite_sheet    = "Assets/Characters/lancer/Right_arm.png",
                shader          = "lancer_wind",
                z_order         = 9,
                x_offset_pct    = 0.32,
                y_offset_pct    = 0.04,
                width_pct       = 0.26,
                height_pct      = 0.43,
                rotation        = -16,
                color           = { 176, 188, 204 },
                secondary_color = { 72, 82, 98 },
                glow_color      = { 80, 225, 140 }
            }
        }
    },

    animations = {
        idle = { start_frame = 0, frame_count = 1, speed = 6.0 },
        walk = { start_frame = 0, frame_count = 1, speed = 10.0 },

        -- Horizontal spear thrust
        Lance_Poke = {
            keyframes = {
                { frame = 0, duration = 4, afterimage = false, weapon_x = 0.38, weapon_y = 0.32, weapon_angle = -55 },
                { frame = 0, duration = 3, afterimage = true,  weapon_x = 1.02, weapon_y = 0.26, weapon_angle = -45 },
                { frame = 0, duration = 6, afterimage = false, weapon_x = 0.68, weapon_y = 0.32, weapon_angle = -60 }
            }
        },

        -- Low sweeping slice
        Lance_Sweep = {
            keyframes = {
                { frame = 0, duration = 7,  afterimage = false, weapon_x = 0.44, weapon_y = 0.14, weapon_angle = -95 },
                { frame = 0, duration = 4,  afterimage = true,  weapon_x = 0.92, weapon_y = 0.42, weapon_angle = -20 },
                { frame = 0, duration = 10, afterimage = false, weapon_x = 0.64, weapon_y = 0.34, weapon_angle = -55 }
            }
        },

        -- Overhead anti-air arc
        Lance_Sky = {
            keyframes = {
                { frame = 0, duration = 5,  afterimage = false, weapon_x = 0.50, weapon_y = 0.36,  weapon_angle = -35  },
                { frame = 0, duration = 4,  afterimage = true,  weapon_x = 0.60, weapon_y = -0.06, weapon_angle = -115 },
                { frame = 0, duration = 8,  afterimage = false, weapon_x = 0.58, weapon_y = 0.16,  weapon_angle = -85  }
            }
        },

        -- Steep downward plunge
        Lance_Dive = {
            keyframes = {
                { frame = 0, duration = 7,  afterimage = false, weapon_x = 0.44, weapon_y = 0.02, weapon_angle = -120 },
                { frame = 0, duration = 10, afterimage = true,  weapon_x = 0.68, weapon_y = 0.42, weapon_angle = -10  },
                { frame = 0, duration = 10, afterimage = false, weapon_x = 0.60, weapon_y = 0.34, weapon_angle = -55  }
            }
        }
    },

    on_passive = function(player, character, dt)
        if character.CustomData["shader_init"] == nil then
            character.CustomData["shader_init"] = 1.0
            character:SetPartShaderFloat("cape",      "u_PartMode", 0.0)
            character:SetPartShaderFloat("left_leg",  "u_PartMode", 0.0)
            character:SetPartShaderFloat("right_leg", "u_PartMode", 0.0)
            character:SetPartShaderFloat("head",      "u_PartMode", 1.0)
            character:SetPartShaderFloat("body",      "u_PartMode", 2.0)
            character:SetPartShaderFloat("lance",     "u_PartMode", 4.0)
            character:SetPartShaderFloat("right_arm", "u_PartMode", 5.0)
            character:SetPartShaderFloat("left_arm",  "u_PartMode", 5.0)
            character:SetPartShaderFloat("hair", "u_PartMode", 0.0)
        end

        local targetFlare = (character.ActiveSkill ~= nil) and 1.25 or 0.0
        local curFlare = character.CustomData["flare"] or 0.0
        curFlare = curFlare + (targetFlare - curFlare) * math.min(1.0, dt * 12.0)
        character.CustomData["flare"] = curFlare
        character:SetShaderFloat("u_AttackIntensity", curFlare)

        local animT = (character.CustomData["anim_t"] or 0.0) + dt
        character.CustomData["anim_t"] = animT

        local breathe = math.sin(animT * 3.8) * 0.012
        local capeWave = math.sin(animT * 4.5) * 3.5
        local speedX = math.abs(player.Velocity.X)
        local isWalking = player.IsGrounded and speedX > 15.0

        local lean = isWalking and 8.0 or 3.0
        character:SetPartOffset("body", 0.0, 0.02 + breathe, lean)
        character:SetPartOffset("head", 0.02, -0.28 + breathe * 1.1, 0.0)
        character:SetPartOffset("hair", -0.02, -0.42 + breathe * 1.15, 0.0)
        character:SetPartOffset("cape", -0.06, 0.06 + breathe, (isWalking and 14.0 or 5.0) + capeWave)

        if player.IsCrouching then
            character:SetPartOffset("body",      0.02,  0.05, 12.0)
            character:SetPartOffset("head", 0.05, -0.25, 0.0)
            character:SetPartOffset("hair", 0.03, -0.35, 0.0)
            character:SetPartOffset("cape",     -0.07,  0.09, 10.0)
            character:SetPartOffset("left_leg",  -0.24,  0.34, 20.0)
            character:SetPartOffset("right_leg",  0.24,  0.34, -20.0)
            character:SetPartOffset("left_arm",  -0.33,  0.07, 20.0)
            character:SetPartOffset("right_arm",  0.30,  0.07, -24.0)
        elseif isWalking then
            local stride = math.sin(animT * 14.0)
            character:SetPartOffset("left_leg",  -0.16 - stride * 0.16, 0.33,  stride * 22.0)
            character:SetPartOffset("right_leg",  0.16 + stride * 0.16, 0.33, -stride * 22.0)
            character:SetPartOffset("left_arm",  -0.35 + stride * 0.05, 0.04,  14.0 - stride * 12.0)
            character:SetPartOffset("right_arm",  0.32 - stride * 0.04, 0.04, -16.0 + stride * 8.0)
        else
            character:SetPartOffset("left_leg",  -0.18, 0.33,  8.0)
            character:SetPartOffset("right_leg",  0.18, 0.33, -8.0)
            character:SetPartOffset("left_arm",  -0.35, 0.04 + breathe,  14.0 + capeWave * 0.4)
            character:SetPartOffset("right_arm",  0.32, 0.04 + breathe, -16.0 - capeWave * 0.4)
        end

        -- Idle lance lowered directly into right hand at hip level, angled 20 degrees upward (-65 deg)
        if character.ActiveSkill == nil then
            local lanceBob = math.sin(animT * 3.2) * 0.012
            local lanceRot = -65.0 + math.sin(animT * 2.5) * 2.0
            character:SetPartOffset("lance", 0.64, 0.46 + lanceBob, lanceRot)
        end
    end,

    skills = {
        -- 1. Long-Range Spear Poke
        spear_poke = {
            input_trigger   = Keys.KeyTop,
            animation       = "Lance_Poke",
            damage          = 9,
            startup_frames  = 4,
            active_frames   = 3,
            recovery_frames = 9,
            hitbox          = { x_offset_pct = 0.85, y_offset_pct = 0.12, width_pct = 1.35, height_pct = 0.18 },
            hitStun         = 0.26,
            cooldown_time   = 0.45,
            on_hit = function(attacker, defender, skill)
                ApplyHitForce(attacker, defender, skill.Damage, skill.HitStun, POKE_KNOCKBACK_PCT)
            end
        },

        -- 2. Halberd Low Reap
        halberd_reap = {
            input_trigger   = Keys.KeyBottom,
            animation       = "Lance_Sweep",
            damage          = 14,
            attack_height   = "low",
            startup_frames  = 8,
            active_frames   = 4,
            recovery_frames = 16,
            hitbox          = { x_offset_pct = 0.80, y_offset_pct = 0.65, width_pct = 1.55, height_pct = 0.28 },
            hitStun         = 0.45,
            cooldown_time   = 1.5,
            on_hit = function(attacker, defender, skill)
                ApplyHitForce(attacker, defender, skill.Damage, skill.HitStun, SWEEP_KNOCKBACK_PCT)
            end
        },

        -- 3. Evasive Backstep Parry
        royal_deflect = {
            input_trigger   = Keys.KeyLeft,
            animation       = "Lance_Poke",
            damage          = 16,
            startup_frames  = 2,
            active_frames   = 11,
            recovery_frames = 16,
            hitbox          = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 1.20, height_pct = 1.0 },
            hitStun         = 0.80,
            cooldown_time   = 3.0,
            is_counter      = true,
            on_counter = function(defender, attacker)
                local backDir = defender.FacingRight and -1 or 1
                defender:SetVelocityPct(backDir * DEFLECT_BACKSTEP_VEL_X_PCT, 0)
                ApplyHitForce(defender, attacker, DEFLECT_COUNTER_DAMAGE, DEFLECT_COUNTER_STUN, DEFLECT_KNOCKBACK_PCT)
            end
        },

        -- 4. Retreating Vault Thrust
        retreating_vault = {
            input_trigger   = Keys.KeyRight,
            animation       = "Lance_Poke",
            damage          = 16,
            startup_frames  = 5,
            active_frames   = 6,
            recovery_frames = 13,
            hitbox          = { x_offset_pct = 0.70, y_offset_pct = 0.20, width_pct = 1.40, height_pct = 0.35 },
            hitStun         = 0.46,
            cooldown_time   = 2.2,
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                local backDir = player.FacingRight and -1 or 1
                if character.CurrentActionFrame < 10 then
                    player:Translate(backDir * character.WalkSpeed * 1.15 * dt, 0)
                end
            end
        },

        -- 5. Armor-Piercing Impale
        grand_impale = {
            input_trigger   = Keys.KeyTop + Keys.KeyBottom,
            animation       = "Lance_Poke",
            damage          = 25,
            startup_frames  = 14,
            active_frames   = 5,
            recovery_frames = 20,
            hitbox          = { x_offset_pct = 0.90, y_offset_pct = 0.20, width_pct = 1.85, height_pct = 0.30 },
            hitStun         = 0.68,
            cooldown_time   = 4.2,
            is_blockbreak   = true,
            on_blockbreak = function(attacker, defender)
                ApplyHitForce(attacker, defender, IMPALE_BREAK_DAMAGE, IMPALE_BREAK_STUN, IMPALE_KNOCKBACK_PCT)
            end
        },

        -- 6. Skyward Crescent Anti-Air
        sky_crescent = {
            input_trigger   = Keys.KeyTop + Keys.KeyRight,
            animation       = "Lance_Sky",
            damage          = 22,
            attack_height   = "overhead",
            startup_frames  = 6,
            active_frames   = 7,
            recovery_frames = 16,
            hitbox          = { x_offset_pct = 0.65, y_offset_pct = -0.50, width_pct = 1.15, height_pct = 1.15 },
            hitStun         = 0.85,
            cooldown_time   = 3.2,
            on_hit = function(attacker, defender, skill)
                ApplyLaunchForce(attacker, defender, skill.Damage, skill.HitStun, SKY_PIERCE_LAUNCH_X_PCT, SKY_PIERCE_LAUNCH_Y_PCT)
            end
        },

        -- 7. Dragoon High-Jump Dive
        dragoon_dive = {
            input_trigger     = Keys.KeyTop + Keys.KeyBottom + Keys.KeyRight,
            animation         = "Lance_Dive",
            damage            = 36,
            attack_height     = "overhead",
            startup_frames    = 9,
            active_frames     = 10,
            recovery_frames   = 22,
            is_hyperframes    = true,
            hyper_start_frame = 2,
            hyper_frames      = 15,
            hitbox            = { x_offset_pct = 0.60, y_offset_pct = 0.05, width_pct = 1.60, height_pct = 0.85 },
            hitStun           = 0.92,
            cooldown_time     = 7.5,
            is_blockbreak     = true,
            is_customlogic    = true,
            on_customlogic = function(player, character, dt)
                local dir = player.FacingRight and 1 or -1
                local f = character.CurrentActionFrame
                if f < 8 then
                    player.IsGrounded = false
                    player:Translate(dir * character.WalkSpeed * 0.8 * dt, -character.WalkSpeed * 2.5 * dt)
                elseif f < 19 then
                    player:Translate(dir * character.WalkSpeed * 2.4 * dt, character.WalkSpeed * 2.8 * dt)
                end
            end,
            on_blockbreak = function(attacker, defender)
                ApplyHitForce(attacker, defender, DRAGOON_BREAK_DAMAGE, DRAGOON_BREAK_STUN, DRAGOON_KNOCKBACK_PCT)
            end
        }
    }
}

return Character