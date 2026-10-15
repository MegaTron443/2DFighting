-- ==========================================
-- LANCER (Long-Reach Footsies & Zoner)
-- ==========================================

local POKE_KNOCKBACK_PCT         = 0.25
local SWEEP_KNOCKBACK_PCT        = 0.28
local DEFLECT_COUNTER_DAMAGE     = 20
local DEFLECT_COUNTER_STUN       = 0.95
local DEFLECT_KNOCKBACK_PCT      = 0.35

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

    physics = {
        width_pct = 0.048,
        height_pct = 0.205,
        walk_speed_pct = 0.28,
        jump_force_pct = -0.70,
        gravity_pct = 1.42,
        friction = 0.84
    },

    rendering = {
        sprite_sheet = "Assets/Characters/lancer_sheet.png",
        frame_width = 128,
        frame_height = 128,
        draw_offset_x_pct = -0.55,
        draw_offset_y_pct = -0.15
    },

    animations = {
        idle = { start_frame = 0, frame_count = 4, speed = 8 },
        walk = { start_frame = 4, frame_count = 6, speed = 12 },
        jump = { start_frame = 10, frame_count = 2, speed = 6 },
        attack1 = { start_frame = 12, frame_count = 5, speed = 15 }
    },

    skills = {
        -- 1. Long-Range Spear Poke
        spear_poke = {
            input_trigger = Keys.KeyTop,
            damage = 9,
            startup_frames = 4,
            active_frames = 3,
            recovery_frames = 9,
            hitbox = { x_offset_pct = 0.85, y_offset_pct = 0.12, width_pct = 1.35, height_pct = 0.18 },
            hitStun = 0.26,
            cooldown_time = 0.45,
            on_hit = function(attacker, defender, skill)
                ApplyHitForce(attacker, defender, skill.Damage, skill.HitStun, POKE_KNOCKBACK_PCT)
            end
        },

        -- 2. Halberd Low Reap
        halberd_reap = {
            input_trigger = Keys.KeyBottom,
            damage = 14,
            attack_height = "low",
            startup_frames = 8,
            active_frames = 4,
            recovery_frames = 16,
            hitbox = { x_offset_pct = 0.80, y_offset_pct = 0.65, width_pct = 1.55, height_pct = 0.28 },
            hitStun = 0.45,
            cooldown_time = 1.5,
            on_hit = function(attacker, defender, skill)
                ApplyHitForce(attacker, defender, skill.Damage, skill.HitStun, SWEEP_KNOCKBACK_PCT)
            end
        },

        -- 3. Evasive Backstep Parry
        royal_deflect = {
            input_trigger = Keys.KeyLeft,
            damage = 16,
            startup_frames = 2,
            active_frames = 11,
            recovery_frames = 16,
            hitbox = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 1.20, height_pct = 1.0 },
            hitStun = 0.80,
            cooldown_time = 3.0,
            is_counter = true,
            on_counter = function(defender, attacker)
                local backDir = defender.FacingRight and -1 or 1
                defender:SetVelocity(backDir * 280, 0)
                ApplyHitForce(defender, attacker, DEFLECT_COUNTER_DAMAGE, DEFLECT_COUNTER_STUN, DEFLECT_KNOCKBACK_PCT)
            end
        },

        -- 4. Retreating Vault Thrust (Pokes while backflipping away to create space)
        retreating_vault = {
            input_trigger = Keys.KeyRight,
            damage = 16,
            startup_frames = 5,
            active_frames = 6,
            recovery_frames = 13,
            hitbox = { x_offset_pct = 0.70, y_offset_pct = 0.20, width_pct = 1.40, height_pct = 0.35 },
            hitStun = 0.46,
            cooldown_time = 2.2,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                local backDir = player.FacingRight and -1 or 1
                if character.CurrentActionFrame < 10 then
                    player:Translate(backDir * character.WalkSpeed * 1.15 * dt, 0)
                end
            end
        },

        -- 5. Armor-Piercing Impale (Extreme horizontal reach)
        grand_impale = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom,
            damage = 25,
            startup_frames = 14,
            active_frames = 5,
            recovery_frames = 20,
            hitbox = { x_offset_pct = 0.90, y_offset_pct = 0.20, width_pct = 1.85, height_pct = 0.30 },
            hitStun = 0.68,
            cooldown_time = 4.2,
            is_blockbreak = true,
            on_blockbreak = function(attacker, defender)
                ApplyHitForce(attacker, defender, IMPALE_BREAK_DAMAGE, IMPALE_BREAK_STUN, IMPALE_KNOCKBACK_PCT)
            end
        },

        -- 6. Skyward Crescent Anti-Air
        sky_crescent = {
            input_trigger = Keys.KeyTop + Keys.KeyRight,
            damage = 22,
            attack_height = "overhead",
            startup_frames = 6,
            active_frames = 7,
            recovery_frames = 16,
            hitbox = { x_offset_pct = 0.65, y_offset_pct = -0.50, width_pct = 1.15, height_pct = 1.15 },
            hitStun = 0.85,
            cooldown_time = 3.2,
            on_hit = function(attacker, defender, skill)
                ApplyLaunchForce(attacker, defender, skill.Damage, skill.HitStun, SKY_PIERCE_LAUNCH_X_PCT, SKY_PIERCE_LAUNCH_Y_PCT)
            end
        },

        -- 7. Dragoon High-Jump Dive
        dragoon_dive = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom + Keys.KeyRight,
            damage = 36,
            attack_height = "overhead",
            startup_frames = 9,
            active_frames = 10,
            recovery_frames = 22,
            is_hyperframes = true,
            hyper_start_frame = 2,
            hyper_frames = 15,
            hitbox = { x_offset_pct = 0.60, y_offset_pct = 0.05, width_pct = 1.60, height_pct = 0.85 },
            hitStun = 0.92,
            cooldown_time = 7.5,
            is_blockbreak = true,
            is_customlogic = true,
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