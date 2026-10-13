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
            hitbox = { x_offset_pct = 0.75, y_offset_pct = 0.20, width_pct = 0.70, height_pct = 0.20 },
            hitStun = 0.20,
            cooldown_time = 0.3
        },

        -- 2. Low Sweep (Uses custom on_hit with extra heavy ground pushback)
        low_sweep = {
            input_trigger = Keys.KeyBottom,
            damage = 22,
            startup_frames = 7,
            active_frames = 4,
            recovery_frames = 14,
            hitbox = { x_offset_pct = 0.70, y_offset_pct = 0.65, width_pct = 1.10, height_pct = 0.30 },
            hitStun = 0.45,
            cooldown_time = 1.2,
            on_hit = function(attacker, defender, skill)
                -- Custom Lua hit logic using the exposed helper (550 knockback on ground, 0 in air)
                ApplyHitForce(attacker, defender, skill.Damage, skill.HitStun, 550)
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
                defender.Health = math.min(100, defender.Health + 10)
                ApplyHitForce(defender, attacker, 30, 1.1, 600)
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
                if character.CurrentActionFrame < 12 then
                    player:Translate(dir * character.WalkSpeed * 2.2 * dt, 0)
                end
            end
        },

        -- 5. Guard Crush Slam (Combo: Top + Bottom + on_blockbreak using ApplyHitForce)
        guard_crush = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom,
            damage = 45,
            startup_frames = 11,
            active_frames = 5,
            recovery_frames = 18,
            hitbox = { x_offset_pct = 0.80, y_offset_pct = 0.15, width_pct = 1.25, height_pct = 0.55 },
            hitStun = 0.70,
            cooldown_time = 4.0,
            is_blockbreak = true,
            on_blockbreak = function(attacker, defender)
                ApplyHitForce(attacker, defender, 65, 1.4, 700)
            end
        },

        -- 6. Rising Uppercut (Combo: Top + Right + Custom Logic)
        rising_uppercut = {
            input_trigger = Keys.KeyTop + Keys.KeyRight,
            damage = 38,
            startup_frames = 5,
            active_frames = 8,
            recovery_frames = 16,
            hitbox = { x_offset_pct = 0.65, y_offset_pct = -0.35, width_pct = 0.85, height_pct = 0.95 },
            hitStun = 0.65,
            cooldown_time = 3.0,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                local dir = player.FacingRight and 1 or -1
                local frame = character.CurrentActionFrame
                if frame >= 2 and frame <= 12 then
                    player.IsGrounded = false
                    player:Translate(dir * character.WalkSpeed * 0.9 * dt, -character.WalkSpeed * 1.8 * dt)
                end
            end
        },

        -- 7. Meteor Dive (3-Button Combo: Top + Bottom + Right)
        meteor_dive = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom + Keys.KeyRight,
            damage = 75,
            startup_frames = 8,
            active_frames = 10,
            recovery_frames = 22,
            hitbox = { x_offset_pct = 0.50, y_offset_pct = 0.10, width_pct = 1.50, height_pct = 0.80 },
            hitStun = 1.0,
            cooldown_time = 6.0,
            is_blockbreak = true,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                local dir = player.FacingRight and 1 or -1
                local frame = character.CurrentActionFrame
                if frame < 8 then
                    player.IsGrounded = false
                    player:Translate(dir * character.WalkSpeed * 1.2 * dt, -character.WalkSpeed * 2.5 * dt)
                elseif frame < 18 then
                    player:Translate(dir * character.WalkSpeed * 2.8 * dt, character.WalkSpeed * 3.2 * dt)
                end
            end,
            on_blockbreak = function(attacker, defender)
                ApplyHitForce(attacker, defender, 90, 1.8, 850)
            end
        }
    }
}