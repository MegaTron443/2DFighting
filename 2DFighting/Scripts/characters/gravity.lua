local Character = {
    name = "Gravity",

    physics = {
        width_pct = 0.046,
        height_pct = 0.20,
        walk_speed_pct = 0.27,
        jump_force_pct = -0.69,
        gravity_pct = 1.0,
        friction = 0.75
    },

    rendering = {
        body_color = { 0, 255, 255 },
        sprite_sheet = "Assets/Characters/gravity_sheet.png",
        frame_width = 128,
        frame_height = 128
    },

    skills = {
        Gravity_Crush = {
            input_trigger = Keys.KeyLeft,
            damage = 0,
            startup_frames = 5,
            active_frames = 3,
            recovery_frames = 10,
            hitbox = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 0.0, height_pct = 0.0 },
            hitStun = 0.0,
            cooldown_time = 1.5,
            is_customlogic = true,
            on_customlogic = function(player, character, dt)
                if character.CurrentActionFrame == 5 and player.TargetOpponent ~= nil then
                    local opponent = player.TargetOpponent
                    local screen_h = character.Height / character.HeightPct

                    local proj = CreateProjectile(player, {
                        x_offset_pct = 0.0,
                        y_offset_pct = 0.0,
                        speed_x_pct = 0.0,
                        speed_y_pct = 1.0,
                        width_pct = 0.15,
                        height_pct = 0.08,
                        damage = 18,
                        hitStun = 0.95,
                        attack_height = "overhead",
                        color = { 0, 255, 255 },
                        is_blockbreak = true,
                        destroy_on_hit = true,
                        on_hit = function(owner, hit_player, projectile)
                            ApplyHitForce(owner, hit_player, projectile.Damage, projectile.HitStun, 0.0)
                            hit_player.IsAirStalled = false
                            hit_player:SetVelocity(0.0, screen_h * 1.5)
                        end
                    })

                    proj.Position = NewVec2(opponent.PosX, screen_h * 0.1)
                    proj:SavePreviousState()
                end
            end
        }
    }
}

return Character