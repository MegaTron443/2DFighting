-- ==========================================
-- CHRONOMANCER (Time Manipulation & Homing)
-- ==========================================

local Character = {
    name = "Chronomancer",

    physics = {
        width_pct = 0.046,
        height_pct = 0.20,
        walk_speed_pct = 0.27,
        jump_force_pct = -0.69,
        gravity_pct = 1.32,
        friction = 0.85
    },

    rendering = {
        body_color = { 0, 165, 185 }, -- Cyan Time-Weaver
        sprite_sheet = "Assets/Characters/chronomancer_sheet.png",
        frame_width = 128,
        frame_height = 128
    },

    skills = {
        -- 1. Homing Chrono Bolt (Curves vertically to track jumping or crouching opponents)
        chrono_bolt = {
            input_trigger = Keys.KeyTop,
            damage = 11,
            startup_frames = 7,
            active_frames = 2,
            recovery_frames = 12,
            hitbox = { x_offset_pct = 0.6, y_offset_pct = 0.1, width_pct = 0.5, height_pct = 0.2 },
            hitStun = 0.28,
            cooldown_time = 1.0,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                if character.CurrentActionFrame == 7 then
                    CreateProjectile(player, {
                        x_offset_pct = 0.05,
                        y_offset_pct = -0.02,
                        speed_x_pct = 0.44,
                        width_pct = 0.03,
                        height_pct = 0.035,
                        damage = 11,
                        hitStun = 0.30,
                        color = { 0, 235, 255 },
                        trajectory = Trajectories.Homing(6.5)
                    })
                end
            end
        },

        -- 2. Stasis Time-Mine (Hovers in mid-air and FREEZES the opponent for 1.35s on contact!)
        stasis_mine = {
            input_trigger = Keys.KeyBottom,
            damage = 8,
            startup_frames = 10,
            active_frames = 2,
            recovery_frames = 14,
            hitbox = { x_offset_pct = 0.5, y_offset_pct = 0.2, width_pct = 0.4, height_pct = 0.4 },
            hitStun = 1.35,
            cooldown_time = 3.8,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                if character.CurrentActionFrame == 10 then
                    CreateProjectile(player, {
                        x_offset_pct = 0.14,
                        y_offset_pct = 0.0,
                        speed_x_pct = 0.0,
                        speed_y_pct = 0.0,
                        width_pct = 0.042,
                        height_pct = 0.075,
                        damage = 8,
                        hitStun = 1.35,
                        lifetime = 3.5,
                        color = { 80, 255, 220 },
                        on_hit = function(owner, target, proj)
                            -- Zero knockback + long stun = Frozen in time for a free combo!
                            ApplyHitForce(owner, target, proj.Damage, 1.35, 0.0)
                        end
                    })
                end
            end
        },

        -- 3. Temporal Rewind Counter (Freezes attacker in place and heals 15 HP)
        temporal_rewind = {
            input_trigger = Keys.KeyLeft,
            damage = 14,
            startup_frames = 2,
            active_frames = 11,
            recovery_frames = 15,
            hitbox = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 1.15, height_pct = 1.0 },
            hitStun = 1.25,
            cooldown_time = 3.2,
            is_counter = true,
            on_counter = function(defender, attacker)
                defender.Health = math.min(defender.MAX_HEALTH, defender.Health + 15)
                ApplyHitForce(defender, attacker, 16, 1.25, 0.02)
            end
        },

        -- 4. Time-Skip Lunge (Teleports forward on frame 4 into an instant strike)
        time_skip_strike = {
            input_trigger = Keys.KeyRight,
            damage = 18,
            startup_frames = 5,
            active_frames = 5,
            recovery_frames = 12,
            hitbox = { x_offset_pct = 0.75, y_offset_pct = 0.15, width_pct = 1.0, height_pct = 0.45 },
            hitStun = 0.48,
            cooldown_time = 2.2,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                if character.CurrentActionFrame == 4 then
                    local dir = player.FacingRight and 1 or -1
                    player:Translate(dir * 210, 0)
                end
            end
        },

        -- 5. Overclock Surge (Sacrifices 12 HP to instantly reset other cooldowns + shockwave!)
        overclock_surge = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom,
            damage = 22,
            startup_frames = 6,
            active_frames = 5,
            recovery_frames = 12,
            hitbox = { x_offset_pct = -0.20, y_offset_pct = -0.10, width_pct = 1.40, height_pct = 1.10 },
            hitStun = 0.65,
            cooldown_time = 5.5,
            is_blockbreak = true,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                if character.CurrentActionFrame == 6 then
                    player.Health = math.max(1, player.Health - 12)
                    character.SkillCooldowns["chrono_bolt"] = 0
                    character.SkillCooldowns["stasis_mine"] = 0
                    character.SkillCooldowns["time_skip_strike"] = 0
                end
            end
        },

        -- 6. Gravity Inversion Upper
        gravity_inversion = {
            input_trigger = Keys.KeyTop + Keys.KeyRight,
            damage = 20,
            attack_height = "overhead",
            startup_frames = 5,
            active_frames = 7,
            recovery_frames = 15,
            hitbox = { x_offset_pct = 0.65, y_offset_pct = -0.35, width_pct = 0.90, height_pct = 1.0 },
            hitStun = 0.90,
            cooldown_time = 3.2,
            on_hit = function(attacker, defender, skill)
                ApplyLaunchForce(attacker, defender, skill.Damage, skill.HitStun, 0.04, -0.82)
            end
        },

        -- 7. Paradox Convergence (Spawns slow + fast layered time waves)
        paradox_convergence = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom + Keys.KeyRight,
            damage = 34,
            startup_frames = 9,
            active_frames = 8,
            recovery_frames = 20,
            hitbox = { x_offset_pct = 0.6, y_offset_pct = 0.1, width_pct = 0.8, height_pct = 0.7 },
            hitStun = 0.95,
            cooldown_time = 7.5,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                if character.CurrentActionFrame == 9 then
                    -- Slow low wave + Fast overhead homing wave simultaneously (Hard-to-block setup!)
                    CreateProjectile(player, {
                        x_offset_pct = 0.05, y_offset_pct = 0.05, speed_x_pct = 0.25,
                        width_pct = 0.04, height_pct = 0.06, damage = 16, attack_height = "low",
                        color = { 0, 180, 255 }
                    })
                    CreateProjectile(player, {
                        x_offset_pct = 0.05, y_offset_pct = -0.06, speed_x_pct = 0.52,
                        width_pct = 0.04, height_pct = 0.06, damage = 18, attack_height = "overhead",
                        color = { 160, 255, 255 }, trajectory = Trajectories.Homing(4.0)
                    })
                end
            end
        }
    }
}

return Character