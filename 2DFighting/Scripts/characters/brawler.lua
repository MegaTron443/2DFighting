Character = {
    name = "Brawler",
    
    -- Physics & Core Body
    physics = {
        width_pct = 0.05,
        height_pct = 0.20,
        walk_speed_pct = 0.25,
        jump_force_pct = -0.65,
        gravity_pct = 1.40,
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

    -- Skill/Move Definitions (Fighting game frame data)
    skills = {
        light_punch = {
            input_trigger = Keys.KeyTop,
            damage = 15,
            startup_frames = 4,
            active_frames = 3,
            recovery_frames = 8,
            hitbox = { x_offset_pct = 0.80, y_offset_pct = 0.20, width_pct = 0.75, height_pct = 0.25 },
            hitStun = 0.25,
            cooldown_time = 0.5
        },
        heavy_kick = {
            input_trigger = Keys.KeyBottom,
            damage = 35,
            startup_frames = 9,
            active_frames = 5,
            recovery_frames = 16,
            hitbox = { x_offset_pct = 0.80, y_offset_pct = 0.50, width_pct = 1.0, height_pct = 0.35 },
            hitStun = 0.5,
            cooldown_time = 2.0
        },
        super_slam = {
            input_trigger = Keys.KeyTop + Keys.KeyBottom,
            damage = 60,
            startup_frames = 12,
            active_frames = 6,
            recovery_frames = 20,
            hitbox = { x_offset_pct = 0.85, y_offset_pct = 0.30, width_pct = 1.2, height_pct = 0.4 },
            hitStun = 0.8,
            cooldown_time = 5.0,
            is_blockbreak = true
        },
        parry_stance = {
            input_trigger = Keys.KeyLeft,
            damage = 25,
            startup_frames = 2,
            active_frames = 12,
            recovery_frames = 14,
            hitbox = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 1.0, height_pct = 1.0 },
            hitStun = 0.6,
            cooldown_time = 3.0,
            is_counter = true
        }
    }
}