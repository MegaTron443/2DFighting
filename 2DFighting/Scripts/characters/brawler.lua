-- ==========================================
-- BRAWLER CONSTANTS (No Magic Numbers)
-- ==========================================
-- Parry Constants
local PARRY_HEAL_AMOUNT    = 10
local PARRY_COUNTER_DAMAGE = 30
local PARRY_COUNTER_STUN   = 1.10
local PARRY_KNOCKBACK_PCT  = 0.25

-- Lunge Punch Constants
local LUNGE_ACTIVE_END_FRAME = 12
local LUNGE_SPEED_MULTIPLIER = 1.6

-- Low Sweep Constants
local SWEEP_KNOCKBACK_PCT = 0.22

-- Guard Crush Constants
local GUARD_CRUSH_BONUS_DAMAGE  = 65
local GUARD_CRUSH_BONUS_STUN    = 1.40
local GUARD_CRUSH_KNOCKBACK_PCT = 0.30

-- Rising Uppercut Constants
local UPPERCUT_LAUNCH_FRAME       = 2
local UPPERCUT_SELF_SPEED_X_MULT  = 0.65
local UPPERCUT_SELF_JUMP_Y_MULT   = 1.05
local UPPERCUT_TARGET_LAUNCH_X_PCT = 0.08 -- Was 180 (bugged)! Now 8% screen width/sec
local UPPERCUT_TARGET_LAUNCH_Y_PCT = -0.78

-- Meteor Dive Constants
local METEOR_ASCEND_END_FRAME  = 8
local METEOR_DIVE_END_FRAME    = 18
local METEOR_ASCEND_X_MULT     = 1.0
local METEOR_ASCEND_Y_MULT     = 2.0
local METEOR_DIVE_X_MULT       = 2.2
local METEOR_DIVE_Y_MULT       = 2.6
local METEOR_BREAK_DAMAGE      = 90
local METEOR_BREAK_STUN        = 1.80
local METEOR_BREAK_KNOCKBACK_PCT = 0.35

Character = {
    name = "Brawler",
    
    -- Physics & Core Body
    physics = {
        width_pct = 0.05,
        height_pct = 0.20,
        walk_speed_pct = 0.26,
        jump_force_pct = -0.68,
        gravity_pct = 1.45,
        friction = 0.85
    },

    -- Sprite / Visual offsets
    rendering = {
        sprite_sheet = "Assets/Characters/brawler_sheet.png",
        frame_width = 128,
        frame_height = 128,
        draw_offset_x_pct = -0.55,
        draw_offset_y_pct = -0.15
    },

    -- Animations
    animations = {
        idle = { start_frame = 0, frame_count = 4, speed = 8 },
        walk = { start_frame = 4, frame_count = 6, speed = 12 },
        jump = { start_frame = 10, frame_count = 2, speed = 6 },
        attack1 = { start_frame = 12, frame_count = 5, speed = 15 }
    },

    -- Skill/Move Definitions
    skills = {
        -- 1. Fast Jab (Uses default 2-state ApplyHitForce)
        quick_jab = {
            input_trigger = Keys.KeyTop,
            damage = 10,
            startup_frames = 3,
            active_frames = 3,
            recovery_frames = 6,
            hitbox = { x_offset_pct = 0.75, y_offset_pct = 0.08, width_pct = 0.70, height_pct = 0.20 },
            hitStun = 0.20,
            cooldown_time = 0.3
        },

        -- 2. Low Sweep (Uses custom on_hit with extra heavy ground pushback)
        low_sweep = {
            input_trigger = Keys.KeyBottom,
            damage = 22,
            attack_height = "low",
            startup_frames = 7,
            active_frames = 4,
            recovery_frames = 14,
            hitbox = { x_offset_pct = 0.70, y_offset_pct = 0.65, width_pct = 1.10, height_pct = 0.30 },
            hitStun = 0.45,
            cooldown_time = 1.2,
            on_hit = function(attacker, defender, skill)
                -- Fixed: 0.22 (22% screen width/sec) instead of 550!
                ApplyHitForce(attacker, defender, skill.Damage, skill.HitStun, SWEEP_KNOCKBACK_PCT)
            end
        },

        -- 3. Parry Counter (Uses exposed ApplyHitForce inside on_counter)
        iron_parry = {
            input_trigger = Keys.KeyLeft,
            damage = 20,
            startup_frames = 2,
            active_frames = 12,
            recovery_frames = 15,
            hitbox = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 1.1, height_pct = 1.0 },
            hitStun = 0.75,
            cooldown_time = 2.5,
            is_counter = true,
            on_counter = function(defender, attacker)
                defender.Health = math.min(defender.MAX_HEALTH, defender.Health + PARRY_HEAL_AMOUNT)
                ApplyHitForce(defender, attacker, PARRY_COUNTER_DAMAGE, PARRY_COUNTER_STUN, PARRY_KNOCKBACK_PCT)
            end
        },

        -- 4. Lunge Dash Punch (Custom movement logic + default 2-state hit force)
        lunge_punch = {
            input_trigger = Keys.KeyRight,
            damage = 28,
            startup_frames = 6,
            active_frames = 6,
            recovery_frames = 12,
            hitbox = { x_offset_pct = 0.85, y_offset_pct = 0.25, width_pct = 0.90, height_pct = 0.35 },
            hitStun = 0.45,
            cooldown_time = 2.0,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                local dir = player.FacingRight and 1 or -1
                if character.CurrentActionFrame < LUNGE_ACTIVE_END_FRAME then
                    player:Translate(dir * character.WalkSpeed * LUNGE_SPEED_MULTIPLIER * dt, 0)
                end
            end
        },

        -- 5. Guard Crush Slam (Combo: Top + Bottom + on_blockbreak using ApplyHitForce)
        guard_crush = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom,
            damage = 45,
            startup_frames = 14,
            active_frames = 5,
            recovery_frames = 18,
            is_hyperframes = true,
            hyper_start_frame = 3,
            hyper_frames = 14,
            hitbox = { x_offset_pct = 0.80, y_offset_pct = 0.15, width_pct = 1.25, height_pct = 0.55 },
            hitStun = 0.70,
            cooldown_time = 4.0,
            is_blockbreak = true,
            on_blockbreak = function(attacker, defender)
                ApplyHitForce(attacker, defender, GUARD_CRUSH_BONUS_DAMAGE, GUARD_CRUSH_BONUS_STUN, GUARD_CRUSH_KNOCKBACK_PCT)
            end
        },

        -- 6. Rising Uppercut (Combo: Top + Right + Custom Logic)
        rising_uppercut = {
            input_trigger = Keys.KeyTop + Keys.KeyRight,
            damage = 38,
            attack_height = "overhead",
            startup_frames = 5,
            active_frames = 8,
            recovery_frames = 16,
            hitbox = { x_offset_pct = 0.65, y_offset_pct = -0.35, width_pct = 0.85, height_pct = 0.95 },
            hitStun = 0.85,
            cooldown_time = 3.0,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                local dir = player.FacingRight and 1 or -1
                local frame = character.CurrentActionFrame
                if frame == UPPERCUT_LAUNCH_FRAME then
                    player.IsGrounded = false
                    player:SetVelocity(dir * character.WalkSpeed * UPPERCUT_SELF_SPEED_X_MULT, character.JumpForce * UPPERCUT_SELF_JUMP_Y_MULT)
                end
            end,
            on_hit = function(attacker, defender, skill)
                -- Fixed: 0.08 X pct instead of 180!
                ApplyLaunchForce(attacker, defender, skill.Damage, skill.HitStun, UPPERCUT_TARGET_LAUNCH_X_PCT, UPPERCUT_TARGET_LAUNCH_Y_PCT)
            end
        },

        -- 7. Meteor Dive (3-Button Combo: Top + Bottom + Right)
        meteor_dive = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom + Keys.KeyRight,
            damage = 75,
            startup_frames = 8,
            active_frames = 10,
            recovery_frames = 22,
            is_hyperframes = true,
            hyper_start_frame = 0,
            hyper_frames = 18,
            hitbox = { x_offset_pct = 0.50, y_offset_pct = 0.10, width_pct = 1.50, height_pct = 0.80 },
            hitStun = 1.0,
            cooldown_time = 6.0,
            is_blockbreak = true,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                local dir = player.FacingRight and 1 or -1
                local frame = character.CurrentActionFrame
                if frame < METEOR_ASCEND_END_FRAME then
                    player.IsGrounded = false
                    player:Translate(dir * character.WalkSpeed * METEOR_ASCEND_X_MULT * dt, -character.WalkSpeed * METEOR_ASCEND_Y_MULT * dt)
                elseif frame < METEOR_DIVE_END_FRAME then
                    player:Translate(dir * character.WalkSpeed * METEOR_DIVE_X_MULT * dt, character.WalkSpeed * METEOR_DIVE_Y_MULT * dt)
                end
            end,
            on_blockbreak = function(attacker, defender)
                ApplyHitForce(attacker, defender, METEOR_BREAK_DAMAGE, METEOR_BREAK_STUN, METEOR_BREAK_KNOCKBACK_PCT)
            end
        }
    }
}