-- ==========================================
-- PYROMANCER (Artillery & Trap Zoner)
-- ==========================================

local Character = {
    name = "Pyromancer",

    physics = {
        width_pct = 0.045,
        height_pct = 0.195,
        walk_speed_pct = 0.24,
        jump_force_pct = -0.66,
        gravity_pct = 1.38,
        friction = 0.84
    },

    rendering = {
        body_color = { 215, 55, 25 }, -- Crimson Flame
        sprite_sheet = "Assets/Characters/pyromancer_sheet.png",
        frame_width = 128,
        frame_height = 128
    },

    skills = {
        -- 1. Sine-Wave Flame Serpent (Travels in a wave pattern across the screen)
        flame_serpent = {
            input_trigger = Keys.KeyTop,
            damage = 12,
            startup_frames = 8,
            active_frames = 2,
            recovery_frames = 14,
            hitbox = { x_offset_pct = 0.6, y_offset_pct = 0.2, width_pct = 0.4, height_pct = 0.2 },
            hitStun = 0.30,
            cooldown_time = 1.1,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                if character.CurrentActionFrame == 8 then
                    CreateProjectile(player, {
                        x_offset_pct = 0.05,
                        y_offset_pct = -0.02,
                        speed_x_pct = 0.46,
                        width_pct = 0.035,
                        height_pct = 0.045,
                        damage = 14,
                        hitStun = 0.32,
                        color = { 255, 90, 0 },
                        trajectory = Trajectories.SineWave(240, 12.0)
                    })
                end
            end
        },

        -- 2. Bouncing Magma Bomb (Arcs through the air and bounces along the floor)
        magma_lob = {
            input_trigger = Keys.KeyBottom,
            damage = 16,
            startup_frames = 10,
            active_frames = 2,
            recovery_frames = 16,
            hitbox = { x_offset_pct = 0.5, y_offset_pct = 0.5, width_pct = 0.3, height_pct = 0.3 },
            hitStun = 0.42,
            cooldown_time = 1.8,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                if character.CurrentActionFrame == 10 then
                    CreateProjectile(player, {
                        x_offset_pct = 0.04,
                        y_offset_pct = -0.05,
                        speed_x_pct = 0.34,
                        speed_y_pct = -0.58,
                        width_pct = 0.038,
                        height_pct = 0.055,
                        damage = 18,
                        attack_height = "low",
                        hitStun = 0.45,
                        lifetime = 3.2,
                        color = { 255, 165, 0 },
                        trajectory = Trajectories.Gravity(1450, 0.72)
                    })
                end
            end
        },

        -- 3. Cinder Blink Counter (Teleports backwards to safety & leaves an explosion)
        cinder_blink = {
            input_trigger = Keys.KeyLeft,
            damage = 14,
            startup_frames = 2,
            active_frames = 11,
            recovery_frames = 14,
            hitbox = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 1.1, height_pct = 1.0 },
            hitStun = 0.75,
            cooldown_time = 3.2,
            is_counter = true,
            on_counter = function(defender, attacker)
                -- Spawn a lingering blast where Pyromancer was standing, then blink away!
                CreateProjectile(defender, {
                    x_offset_pct = 0.0,
                    y_offset_pct = 0.0,
                    speed_x_pct = 0.0,
                    width_pct = 0.09,
                    height_pct = 0.18,
                    damage = 22,
                    hitStun = 0.85,
                    lifetime = 0.35,
                    color = { 255, 60, 0 }
                })
                local retreatDir = defender.FacingRight and -1 or 1
                defender:Translate(retreatDir * 350, 0)
            end
        },

        -- 4. Stationary Flame Pillar Trap (Blocks ground approaches for 2.2 seconds)
        flame_pillar = {
            input_trigger = Keys.KeyRight,
            damage = 15,
            startup_frames = 12,
            active_frames = 3,
            recovery_frames = 15,
            hitbox = { x_offset_pct = 0.5, y_offset_pct = 0.2, width_pct = 0.3, height_pct = 0.3 },
            hitStun = 0.55,
            cooldown_time = 3.4,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                if character.CurrentActionFrame == 12 then
                    CreateProjectile(player, {
                        x_offset_pct = 0.16,
                        y_offset_pct = -0.02,
                        speed_x_pct = 0.0,
                        speed_y_pct = 0.0,
                        width_pct = 0.045,
                        height_pct = 0.22,
                        damage = 20,
                        hitStun = 0.65,
                        lifetime = 2.2,
                        color = { 255, 210, 40 },
                        on_hit = function(owner, target, proj)
                            ApplyLaunchForce(owner, target, proj.Damage, proj.HitStun, 0.06, -0.68)
                        end
                    })
                end
            end
        },

        -- 5. Unblockable Magma Geyser (Spawns at mid-range to break turtles)
        magma_geyser = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom,
            damage = 26,
            startup_frames = 16,
            active_frames = 4,
            recovery_frames = 18,
            hitbox = { x_offset_pct = 0.6, y_offset_pct = 0.1, width_pct = 0.5, height_pct = 0.5 },
            hitStun = 0.70,
            cooldown_time = 4.8,
            is_blockbreak = true,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                if character.CurrentActionFrame == 16 then
                    CreateProjectile(player, {
                        x_offset_pct = 0.26,
                        y_offset_pct = -0.04,
                        speed_x_pct = 0.0,
                        speed_y_pct = 0.0,
                        width_pct = 0.06,
                        height_pct = 0.26,
                        damage = 28,
                        hitStun = 0.80,
                        lifetime = 0.45,
                        is_blockbreak = true,
                        color = { 255, 40, 0 },
                        on_hit = function(owner, target, proj)
                            ApplyLaunchForce(owner, target, proj.Damage, proj.HitStun, 0.05, -0.75)
                        end
                    })
                end
            end
        },

        -- 6. Phoenix Updraft (Jet-propels Pyromancer into the air while dropping fire below)
        phoenix_updraft = {
            input_trigger = Keys.KeyTop + Keys.KeyRight,
            damage = 20,
            attack_height = "overhead",
            startup_frames = 5,
            active_frames = 8,
            recovery_frames = 14,
            hitbox = { x_offset_pct = 0.2, y_offset_pct = -0.3, width_pct = 1.1, height_pct = 1.1 },
            hitStun = 0.75,
            cooldown_time = 3.5,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                if character.CurrentActionFrame == 4 then
                    local backDir = player.FacingRight and -1 or 1
                    player.IsGrounded = false
                    player:SetVelocity(backDir * character.WalkSpeed * 0.8, character.JumpForce * 1.10)
                end
            end
        },

        -- 7. Hellfire Barrage (Fires 3 staggered fireballs on frames 8, 14, and 20)
        hellfire_barrage = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom + Keys.KeyRight,
            damage = 36,
            startup_frames = 8,
            active_frames = 14,
            recovery_frames = 20,
            hitbox = { x_offset_pct = 0.6, y_offset_pct = 0.1, width_pct = 0.6, height_pct = 0.6 },
            hitStun = 0.85,
            cooldown_time = 7.5,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                local f = character.CurrentActionFrame
                if f == 8 or f == 14 or f == 20 then
                    local yOff = (f == 14) and -0.06 or 0.01
                    create_projectile(
                        player,
                        NewVec2(0.06, yOff),
                        NewVec2(0.58, 0.0),
                        function(owner, target, proj)
                            ApplyHitForce(owner, target, 14, 0.45, 0.12)
                        end,
                        Trajectories.Linear()
                    )
                end
            end
        }
    }
}

return Character