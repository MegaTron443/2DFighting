-- ==========================================
-- TITAN (Armored Juggernaut)
-- ==========================================

local STOMP_KNOCKBACK_PCT       = 0.28
local FORTRESS_HEAL_AMOUNT      = 15
local FORTRESS_COUNTER_DAMAGE   = 26
local FORTRESS_COUNTER_STUN     = 1.05
local FORTRESS_KNOCKBACK_PCT    = 0.32

local TACKLE_END_FRAME          = 15
local TACKLE_SPEED_MULT         = 1.75

local COLOSSUS_BREAK_DAMAGE     = 42
local COLOSSUS_BREAK_STUN       = 1.30
local COLOSSUS_KNOCKBACK_PCT    = 0.36

local GEYSER_LAUNCH_X_PCT       = 0.05
local GEYSER_LAUNCH_Y_PCT       = -0.82

local CATACLYSM_BREAK_DAMAGE    = 52
local CATACLYSM_BREAK_STUN      = 1.50
local CATACLYSM_KNOCKBACK_PCT   = 0.40

local Character = {
    name = "Titan",

    physics = {
        width_pct = 0.065,
        height_pct = 0.23,
        walk_speed_pct = 0.19,
        jump_force_pct = -0.60,
        gravity_pct = 1.60,
        friction = 0.88
    },

    rendering = {
        sprite_sheet = "Assets/Characters/titan_sheet.png",
        frame_width = 128,
        frame_height = 128,
        draw_offset_x_pct = -0.55,
        draw_offset_y_pct = -0.15
    },

    animations = {
        idle = { start_frame = 0, frame_count = 4, speed = 6 },
        walk = { start_frame = 4, frame_count = 6, speed = 9 },
        jump = { start_frame = 10, frame_count = 2, speed = 5 },
        attack1 = { start_frame = 12, frame_count = 5, speed = 12 }
    },

    skills = {
        -- 1. Heavy Backhand
        heavy_palm = {
            input_trigger = Keys.KeyTop,
            damage = 12,
            startup_frames = 5,
            active_frames = 4,
            recovery_frames = 10,
            hitbox = { x_offset_pct = 0.70, y_offset_pct = 0.12, width_pct = 0.80, height_pct = 0.28 },
            hitStun = 0.28,
            cooldown_time = 0.50
        },

        -- 2. Quake Stomp (Low shockwave)
        quake_stomp = {
            input_trigger = Keys.KeyBottom,
            damage = 18,
            attack_height = "low",
            startup_frames = 10,
            active_frames = 5,
            recovery_frames = 18,
            hitbox = { x_offset_pct = 0.55, y_offset_pct = 0.65, width_pct = 1.35, height_pct = 0.35 },
            hitStun = 0.50,
            cooldown_time = 1.8,
            on_hit = function(attacker, defender, skill)
                ApplyHitForce(attacker, defender, skill.Damage, skill.HitStun, STOMP_KNOCKBACK_PCT)
            end
        },

        -- 3. Fortress Aegis (Armored Counter)
        fortress_aegis = {
            input_trigger = Keys.KeyLeft,
            damage = 22,
            startup_frames = 3,
            active_frames = 14,
            recovery_frames = 18,
            hitbox = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 1.15, height_pct = 1.0 },
            hitStun = 0.85,
            cooldown_time = 3.5,
            is_counter = true,
            on_counter = function(defender, attacker)
                defender.Health = math.min(defender.MAX_HEALTH, defender.Health + FORTRESS_HEAL_AMOUNT)
                ApplyHitForce(defender, attacker, FORTRESS_COUNTER_DAMAGE, FORTRESS_COUNTER_STUN, FORTRESS_KNOCKBACK_PCT)
            end
        },

        -- 4. Armored Juggernaut Charge
        juggernaut_tackle = {
            input_trigger = Keys.KeyRight,
            damage = 22,
            startup_frames = 9,
            active_frames = 7,
            recovery_frames = 16,
            is_hyperframes = true,
            hyper_start_frame = 3,
            hyper_frames = 12,
            hitbox = { x_offset_pct = 0.65, y_offset_pct = 0.15, width_pct = 1.00, height_pct = 0.65 },
            hitStun = 0.52,
            cooldown_time = 2.8,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                local dir = player.FacingRight and 1 or -1
                if character.CurrentActionFrame < TACKLE_END_FRAME then
                    player:Translate(dir * character.WalkSpeed * TACKLE_SPEED_MULT * dt, 0)
                end
            end
        },

        -- 5. Colossus Hammer (Overhead + Block Break)
        colossus_hammer = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom,
            damage = 32,
            attack_height = "overhead",
            startup_frames = 18,
            active_frames = 6,
            recovery_frames = 22,
            is_hyperframes = true,
            hyper_start_frame = 5,
            hyper_frames = 16,
            hitbox = { x_offset_pct = 0.70, y_offset_pct = 0.0, width_pct = 1.35, height_pct = 0.75 },
            hitStun = 0.75,
            cooldown_time = 5.0,
            is_blockbreak = true,
            on_blockbreak = function(attacker, defender)
                ApplyHitForce(attacker, defender, COLOSSUS_BREAK_DAMAGE, COLOSSUS_BREAK_STUN, COLOSSUS_KNOCKBACK_PCT)
            end
        },

        -- 6. Grounded Sky-Ripper Anti-Air
        sky_ripper = {
            input_trigger = Keys.KeyTop + Keys.KeyRight,
            damage = 28,
            attack_height = "overhead",
            startup_frames = 7,
            active_frames = 8,
            recovery_frames = 18,
            is_hyperframes = true,
            hyper_start_frame = 2,
            hyper_frames = 8,
            hitbox = { x_offset_pct = 0.55, y_offset_pct = -0.45, width_pct = 0.95, height_pct = 1.10 },
            hitStun = 0.90,
            cooldown_time = 3.8,
            on_hit = function(attacker, defender, skill)
                ApplyLaunchForce(attacker, defender, skill.Damage, skill.HitStun, GEYSER_LAUNCH_X_PCT, GEYSER_LAUNCH_Y_PCT)
            end
        },

        -- 7. Earthshatter Leap
        earthshatter = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom + Keys.KeyRight,
            damage = 44,
            startup_frames = 11,
            active_frames = 10,
            recovery_frames = 26,
            is_hyperframes = true,
            hyper_start_frame = 2,
            hyper_frames = 19,
            hitbox = { x_offset_pct = 0.40, y_offset_pct = 0.05, width_pct = 1.65, height_pct = 0.85 },
            hitStun = 1.0,
            cooldown_time = 8.5,
            is_blockbreak = true,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                local dir = player.FacingRight and 1 or -1
                local f = character.CurrentActionFrame
                if f < 10 then
                    player.IsGrounded = false
                    player:Translate(dir * character.WalkSpeed * 1.1 * dt, -character.WalkSpeed * 2.1 * dt)
                elseif f < 21 then
                    player:Translate(dir * character.WalkSpeed * 1.8 * dt, character.WalkSpeed * 2.9 * dt)
                end
            end,
            on_blockbreak = function(attacker, defender)
                ApplyHitForce(attacker, defender, CATACLYSM_BREAK_DAMAGE, CATACLYSM_BREAK_STUN, CATACLYSM_KNOCKBACK_PCT)
            end
        }
    }
}

return Character