-- ==========================================
-- TITAN (Armored Grappler & Juggernaut)
-- ==========================================

local regenAcc = 0.0

local Character = {
    name = "Titan",

    physics = {
        width_pct = 0.066,
        height_pct = 0.23,
        walk_speed_pct = 0.18,
        jump_force_pct = -0.58,
        gravity_pct = 1.62,
        friction = 0.88
    },

    rendering = {
        body_color = { 65, 130, 75 }, -- Emerald Goliath
        sprite_sheet = "Assets/Characters/titan_sheet.png",
        frame_width = 128,
        frame_height = 128
    },

    -- Passive: Titan regenerates 1 HP every 1.5 seconds while grounded and idle!
    on_passive = function(player, character, dt)
        if player.IsGrounded and not player.IsInHitstun and player.Health > 0 and player.Health < player.MAX_HEALTH then
            regenAcc = regenAcc + dt
            if regenAcc >= 1.5 then
                regenAcc = 0.0
                player.Health = math.min(player.MAX_HEALTH, player.Health + 1)
            end
        end
    end,

    skills = {
        -- 1. Vacuum Thunder Clap (Negative knockback pulls the opponent INTO grab range!)
        vacuum_clap = {
            input_trigger = Keys.KeyTop,
            damage = 10,
            startup_frames = 6,
            active_frames = 4,
            recovery_frames = 10,
            hitbox = { x_offset_pct = 0.65, y_offset_pct = 0.05, width_pct = 1.45, height_pct = 0.55 },
            hitStun = 0.42,
            cooldown_time = 0.9,
            on_hit = function(attacker, defender, skill)
                -- Negative knockback (-0.18) sucks the opponent directly toward Titan!
                ApplyHitForce(attacker, defender, skill.Damage, skill.HitStun, -0.18)
            end
        },

        -- 2. Seismic Boulder Toss (Arcing heavy rock projectile)
        boulder_toss = {
            input_trigger = Keys.KeyBottom,
            damage = 20,
            startup_frames = 13,
            active_frames = 3,
            recovery_frames = 18,
            hitbox = { x_offset_pct = 0.60, y_offset_pct = 0.30, width_pct = 0.70, height_pct = 0.50 },
            hitStun = 0.55,
            cooldown_time = 2.4,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                if character.CurrentActionFrame == 13 then
                    CreateProjectile(player, {
                        x_offset_pct = 0.06,
                        y_offset_pct = -0.06,
                        speed_x_pct = 0.38,
                        speed_y_pct = -0.52,
                        width_pct = 0.05,
                        height_pct = 0.08,
                        damage = 22,
                        attack_height = "overhead",
                        hitStun = 0.60,
                        color = { 140, 110, 80 },
                        trajectory = Trajectories.Gravity(1350, 0.0)
                    })
                end
            end
        },

        -- 3. Iron Fortress Counter (Heals 18 HP and blasts attacker full-screen)
        fortress_aegis = {
            input_trigger = Keys.KeyLeft,
            damage = 24,
            startup_frames = 3,
            active_frames = 14,
            recovery_frames = 18,
            hitbox = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 1.15, height_pct = 1.0 },
            hitStun = 0.90,
            cooldown_time = 3.5,
            is_counter = true,
            on_counter = function(defender, attacker)
                defender.Health = math.min(defender.MAX_HEALTH, defender.Health + 18)
                ApplyHitForce(defender, attacker, 26, 1.0, 0.35)
            end
        },

        -- 4. Armored Freight-Train Charge
        juggernaut_tackle = {
            input_trigger = Keys.KeyRight,
            damage = 22,
            startup_frames = 9,
            active_frames = 8,
            recovery_frames = 16,
            is_hyperframes = true,
            hyper_start_frame = 2,
            hyper_frames = 14,
            hitbox = { x_offset_pct = 0.60, y_offset_pct = 0.15, width_pct = 1.05, height_pct = 0.65 },
            hitStun = 0.55,
            cooldown_time = 2.8,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                local dir = player.FacingRight and 1 or -1
                if character.CurrentActionFrame < 16 then
                    player:Translate(dir * character.WalkSpeed * 1.95 * dt, 0)
                end
            end
        },

        -- 5. Spinebuster Command Grab (Unblockable close-range grab that launches sky-high!)
        spinebuster_grab = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom,
            damage = 38,
            startup_frames = 7,
            active_frames = 4,
            recovery_frames = 22,
            is_hyperframes = true,
            hyper_start_frame = 2,
            hyper_frames = 8,
            hitbox = { x_offset_pct = 0.50, y_offset_pct = 0.10, width_pct = 0.85, height_pct = 0.70 },
            hitStun = 1.05,
            cooldown_time = 4.5,
            is_blockbreak = true,
            on_hit = function(attacker, defender, skill)
                ApplyLaunchForce(attacker, defender, skill.Damage, skill.HitStun, 0.04, -0.88)
            end,
            on_blockbreak = function(attacker, defender)
                ApplyLaunchForce(attacker, defender, 42, 1.15, 0.04, -0.88)
            end
        },

        -- 6. Anti-Air Bear Hug
        sky_snatch = {
            input_trigger = Keys.KeyTop + Keys.KeyRight,
            damage = 28,
            attack_height = "overhead",
            startup_frames = 6,
            active_frames = 8,
            recovery_frames = 18,
            is_hyperframes = true,
            hyper_start_frame = 1,
            hyper_frames = 9,
            hitbox = { x_offset_pct = 0.55, y_offset_pct = -0.45, width_pct = 0.95, height_pct = 1.10 },
            hitStun = 0.90,
            cooldown_time = 3.5,
            on_hit = function(attacker, defender, skill)
                ApplyLaunchForce(attacker, defender, skill.Damage, skill.HitStun, 0.05, -0.80)
            end
        },

        -- 7. Cataclysmic Slam (Armored Leap + Ground Shockwave Projectiles in both directions)
        cataclysm_slam = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom + Keys.KeyRight,
            damage = 42,
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
                elseif f < 20 then
                    player:Translate(dir * character.WalkSpeed * 1.8 * dt, character.WalkSpeed * 2.9 * dt)
                elseif f == 20 then
                    -- Spawn a low ground shockwave wave upon landing!
                    CreateProjectile(player, {
                        x_offset_pct = 0.08,
                        y_offset_pct = 0.06,
                        speed_x_pct = 0.52,
                        width_pct = 0.04,
                        height_pct = 0.06,
                        damage = 18,
                        attack_height = "low",
                        color = { 190, 150, 90 }
                    })
                end
            end
        }
    }
}

return Character