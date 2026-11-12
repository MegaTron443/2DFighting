-- ==========================================
-- PHANTOM (Boomerang & Teleport Trickster)
-- ==========================================

local Character = {
    name = "Phantom",

    physics = {
        width_pct = 0.043,
        height_pct = 0.185,
        walk_speed_pct = 0.34,
        jump_force_pct = -0.74,
        gravity_pct = 1.48,
        friction = 0.82
    },

    rendering = {
        body_color = { 115, 40, 195 }, -- Violet Shadow
        sprite_sheet = "Assets/Characters/phantom_sheet.png",
        frame_width = 128,
        frame_height = 128
    },

    skills = {
        -- 1. Returning Shadow Chakram (Flies out, reverses direction, and can hit twice!)
        shadow_chakram = {
            input_trigger = Keys.KeyTop,
            damage = 9,
            startup_frames = 6,
            active_frames = 2,
            recovery_frames = 11,
            hitbox = { x_offset_pct = 0.65, y_offset_pct = 0.1, width_pct = 0.5, height_pct = 0.25 },
            hitStun = 0.28,
            cooldown_time = 1.4,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                if character.CurrentActionFrame == 6 then
                    CreateProjectile(player, {
                        x_offset_pct = 0.05,
                        y_offset_pct = -0.01,
                        speed_x_pct = 0.62,
                        width_pct = 0.032,
                        height_pct = 0.035,
                        damage = 10,
                        hitStun = 0.32,
                        lifetime = 1.5,
                        destroy_on_hit = false, -- Pierces and returns!
                        color = { 180, 80, 255 },
                        trajectory = Trajectories.Boomerang(0.55, 1950),
                        on_hit = function(owner, target, proj)
                            -- Pulls the target slightly toward Phantom on return hit!
                            local pull = (proj.ElapsedTime > 0.55) and -0.10 or 0.08
                            ApplyHitForce(owner, target, proj.Damage, proj.HitStun, pull)
                        end
                    })
                end
            end
        },

        -- 2. Shadow Sweep (Fast low poke with zero pushback for mixups)
        shadow_sweep = {
            input_trigger = Keys.KeyBottom,
            damage = 12,
            attack_height = "low",
            startup_frames = 5,
            active_frames = 4,
            recovery_frames = 11,
            hitbox = { x_offset_pct = 0.65, y_offset_pct = 0.68, width_pct = 1.05, height_pct = 0.28 },
            hitStun = 0.42,
            cooldown_time = 1.0,
            on_hit = function(attacker, defender, skill)
                ApplyHitForce(attacker, defender, skill.Damage, skill.HitStun, 0.04)
            end
        },

        -- 3.Substitution Counter (Teleports directly BEHIND the attacker and launches them!)
        substitution_jutsu = {
            input_trigger = Keys.KeyLeft,
            damage = 18,
            startup_frames = 2,
            active_frames = 10,
            recovery_frames = 14,
            hitbox = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 1.1, height_pct = 1.0 },
            hitStun = 0.80,
            cooldown_time = 3.0,
            is_counter = true,
            on_counter = function(defender, attacker)
                local behindOffset = attacker.FacingRight and -110 or 110
                defender.Position = NewVec2(attacker.PosX + behindOffset, attacker.PosY)
                defender.FacingRight = attacker.FacingRight
                ApplyLaunchForce(defender, attacker, 20, 0.85, 0.05, -0.68)
            end
        },

        -- 4. Phantom Cross-Up Dash (Dashes clean through the opponent to the other side)
        phantom_dash = {
            input_trigger = Keys.KeyRight,
            damage = 16,
            startup_frames = 4,
            active_frames = 8,
            recovery_frames = 10,
            hitbox = { x_offset_pct = 0.20, y_offset_pct = 0.15, width_pct = 1.10, height_pct = 0.60 },
            hitStun = 0.48,
            cooldown_time = 2.0,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                local dir = player.FacingRight and 1 or -1
                if character.CurrentActionFrame < 11 then
                    player:Translate(dir * character.WalkSpeed * 2.55 * dt, 0)
                end
            end
        },

        -- 5. Kunai Fan (Leaps into the air and throws 3 angled Kunai downward)
        kunai_fan = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom,
            damage = 18,
            attack_height = "overhead",
            startup_frames = 7,
            active_frames = 4,
            recovery_frames = 14,
            hitbox = { x_offset_pct = 0.5, y_offset_pct = 0.1, width_pct = 0.6, height_pct = 0.5 },
            hitStun = 0.50,
            cooldown_time = 3.2,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                local f = character.CurrentActionFrame
                if f == 2 then
                    local backDir = player.FacingRight and -1 or 1
                    player.IsGrounded = false
                    player:SetVelocity(backDir * character.WalkSpeed * 0.6, character.JumpForce * 0.85)
                elseif f == 8 then
                    -- Spawn 3-way Kunai spread
                    create_projectile(player, NewVec2(0.04, 0.0), NewVec2(0.55, 0.25))
                    create_projectile(player, NewVec2(0.04, 0.0), NewVec2(0.48, 0.50))
                    create_projectile(player, NewVec2(0.04, 0.0), NewVec2(0.38, 0.75))
                end
            end
        },

        -- 6. Crescent Moon Somersault
        crescent_somersault = {
            input_trigger = Keys.KeyTop + Keys.KeyRight,
            damage = 21,
            attack_height = "overhead",
            startup_frames = 4,
            active_frames = 7,
            recovery_frames = 15,
            hitbox = { x_offset_pct = 0.55, y_offset_pct = -0.35, width_pct = 0.90, height_pct = 1.0 },
            hitStun = 0.82,
            cooldown_time = 3.0,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                if character.CurrentActionFrame == 2 then
                    local dir = player.FacingRight and 1 or -1
                    player.IsGrounded = false
                    player:SetVelocity(dir * character.WalkSpeed * 0.9, character.JumpForce * 0.95)
                end
            end,
            on_hit = function(attacker, defender, skill)
                ApplyLaunchForce(attacker, defender, skill.Damage, skill.HitStun, 0.06, -0.72)
            end
        },

        -- 7. Izuna Drop Assassination (Teleports directly above the opponent and slams down)
        izuna_drop = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom + Keys.KeyRight,
            damage = 34,
            attack_height = "overhead",
            startup_frames = 9,
            active_frames = 8,
            recovery_frames = 20,
            hitbox = { x_offset_pct = 0.1, y_offset_pct = 0.1, width_pct = 1.3, height_pct = 0.9 },
            hitStun = 0.95,
            cooldown_time = 7.0,
            is_blockbreak = true,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                local f = character.CurrentActionFrame
                if f == 5 and player.TargetOpponent ~= nil then
                    player.IsGrounded = false
                    player.Position = NewVec2(player.TargetOpponent.PosX, player.TargetOpponent.PosY - 260)
                    player:SetVelocity(0, 0)
                elseif f >= 8 and f < 17 then
                    player:Translate(0, character.WalkSpeed * 3.1 * dt)
                end
            end
        }
    }
}

return Character