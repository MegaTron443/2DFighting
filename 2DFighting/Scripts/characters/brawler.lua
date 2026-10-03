Character = {
    name = "Brawler",
    
    -- Physics & Core Body
    physics = {
        width = 60.0,
        height = 110.0,
        walk_speed = 320.0,
        jump_force = -650.0,
        gravity = 1400.0,
        friction = 0.85
    },

    -- Sprite / Visual offsets
    rendering = {
        sprite_sheet = "Assets/Characters/brawler_sheet.png",
        frame_width = 128,
        frame_height = 128,
        draw_offset_x = -34.0,
        draw_offset_y = -18.0
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
            -- Hitbox relative to player's root (X, Y, Width, Height)
            hitbox = { x_offset = 50.0, y_offset = 25.0, width = 45.0, height = 25.0 }
        },
        heavy_kick = {
            input_trigger = "K",
            damage = 35,
            startup_frames = 9,
            active_frames = 5,
            recovery_frames = 16,
            hitbox = { x_offset = 55.0, y_offset = 40.0, width = 60.0, height = 30.0 }
        }
    }
}