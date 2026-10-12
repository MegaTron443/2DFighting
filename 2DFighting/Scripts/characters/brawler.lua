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
            input_trigger = "J",
            damage = 15,
            startup_frames = 4,
            active_frames = 3,
            recovery_frames = 8,
            hitbox = { x_offset_pct = 0.80, y_offset_pct = 0.20, width_pct = 0.75, height_pct = 0.25 },
            hitStun = 0.25,
            cooldown_time = 10.0
        },
        heavy_kick = {
            input_trigger = "K",
            damage = 35,
            startup_frames = 9,
            active_frames = 5,
            recovery_frames = 16,
            hitbox = { x_offset_pct = 0.80, y_offset_pct = 0.50, width_pct = 1.0, height_pct = 0.35 },
            hitStun = 0.5,
            cooldown_time = 2.0
        }
    }
}