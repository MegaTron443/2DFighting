-- ==========================================
-- LANCER (Long-Reach Footsies & Zoner - Wind Lancer Palette)
-- ==========================================

local POKE_KNOCKBACK_PCT         = 0.25
local SWEEP_KNOCKBACK_PCT        = 0.28
local DEFLECT_COUNTER_DAMAGE     = 20
local DEFLECT_COUNTER_STUN       = 0.95
local DEFLECT_KNOCKBACK_PCT      = 0.35
local DEFLECT_BACKSTEP_VEL_X_PCT = 0.32

local IMPALE_BREAK_DAMAGE        = 35
local IMPALE_BREAK_STUN          = 1.15
local IMPALE_KNOCKBACK_PCT       = 0.34

local SKY_PIERCE_LAUNCH_X_PCT    = 0.12
local SKY_PIERCE_LAUNCH_Y_PCT    = -0.75

local DRAGOON_BREAK_DAMAGE       = 45
local DRAGOON_BREAK_STUN         = 1.35
local DRAGOON_KNOCKBACK_PCT      = 0.36

local Character = {
    name = "Lancer",
    description = [[
FOOTSIES / LONG-REACH ZONER
Disciplined polearm duelist who dominates mid-screen spacing with disjointed thrusts and retreating counters.

PASSIVE:
None — excels at range control and whiff punishing.

MOVE LIST:
- Spear Poke (Top): Fast, long-range horizontal thrust for neutral control.
- Halberd Reap (Bottom): Sweeping low slice with massive horizontal reach.
- Royal Deflect (Left): Parry stance that backsteps and punishes the attacker.
- Retreating Vault (Right): Strikes forward while leaping backward to maintain ideal spacing.
- Grand Impale (Top+Bottom): Extreme-reach piercing lance thrust that shatters guards.
- Sky Crescent (Top+Right): Overhead anti-air arc launching jumping foes.
- Dragoon Dive (Top+Bottom+Right): Armored aerial vault and steep downward block-breaking plunge.
]],

    physics = {
        width_pct      = 0.048,
        height_pct     = 0.205,
        walk_speed_pct = 0.28,
        jump_force_pct = -0.70,
        gravity_pct    = 1.42,
        friction       = 0.84
    },

    rendering = {
        frame_width  = 256,
        frame_height = 256,
        body_color   = { 34, 145, 84 },
        shader       = "lancer_wind",

        parts = {
            cape = {
                sprite_sheet    = "Assets/Characters/lancer/Cape.png",
                shader          = "lancer_wind",
                z_order         = 1,
                x_offset_pct    = -0.06,
                y_offset_pct    = 0.06,
                width_pct       = 1.00,
                height_pct      = 0.56,
                rotation        = 5,
                color           = { 34, 145, 84 },
                secondary_color = { 15, 72, 42 },
                glow_color      = { 80, 225, 140 }
            },
            left_arm = {
                sprite_sheet    = "Assets/Characters/lancer/Left_arm.png",
                shader          = "lancer_wind",
                z_order         = 2,
                x_offset_pct    = -0.35,
                y_offset_pct    = 0.04,
                width_pct       = 0.26,
                height_pct      = 0.43,
                rotation        = 14,
                color           = { 176, 188, 204 },
                secondary_color = { 72, 82, 98 },
                glow_color      = { 80, 225, 140 }
            },
            left_leg = {
                sprite_sheet    = "Assets/Characters/lancer/Left_leg.png",
                shader          = "lancer_wind",
                z_order         = 3,
                x_offset_pct    = -0.18,
                y_offset_pct    = 0.33,
                width_pct       = 0.27,
                height_pct      = 0.34,
                rotation        = 8,
                color           = { 172, 184, 200 },
                secondary_color = { 68, 78, 94 },
                glow_color      = { 80, 225, 140 }
            },
            right_leg = {
                sprite_sheet    = "Assets/Characters/lancer/Right_leg.png",
                shader          = "lancer_wind",
                z_order         = 4,
                x_offset_pct    = 0.18,
                y_offset_pct    = 0.33,
                width_pct       = 0.27,
                height_pct      = 0.34,
                rotation        = -8,
                color           = { 64, 70, 82 },
                secondary_color = { 32, 36, 44 },
                glow_color      = { 80, 225, 140 }
            },
            body = {
                sprite_sheet    = "Assets/Characters/lancer/Body.png",
                shader          = "lancer_wind",
                z_order         = 5,
                x_offset_pct    = 0.0,
                y_offset_pct    = 0.02,
                width_pct       = 0.66,
                height_pct      = 0.44,
                rotation        = 3,
                color           = { 176, 188, 204 },
                secondary_color = { 54, 60, 72 },
                glow_color      = { 80, 225, 140 }
            },
            head = {
                sprite_sheet    = "Assets/Characters/lancer/Head.png",
                shader          = "lancer_wind",
                z_order         = 6,
                x_offset_pct    = 0.02,
                y_offset_pct    = -0.28,
                width_pct       = 0.62,
                height_pct      = 0.25,
                rotation        = 0,
                color           = { 34, 145, 84 },
                secondary_color = { 15, 72, 42 },
                glow_color      = { 80, 225, 140 }
            },
            hair = {
                sprite_sheet    = "Assets/Characters/lancer/Hair.png",
                shader          = "lancer_wind",
                z_order         = 7,
                x_offset_pct    = 0.00,
                y_offset_pct    = -0.38, -- Lifted above eyebrows & covers top of Head.png
                width_pct       = 0.68,
                height_pct      = 0.17,
                rotation        = 0,
                color           = { 242, 246, 252 },
                secondary_color = { 195, 204, 218 },
                glow_color      = { 80, 225, 140 }
            },
            lance = {
                sprite_sheet    = "Assets/Characters/lancer/Lance.png",
                shader          = "lancer_wind",
                z_order         = 8,
                is_socket       = true,
                visible         = true,
                x_offset_pct    = 0.64,
                y_offset_pct    = 0.46,              -- Drops grip directly onto the right hand circle
                width_pct       = 1.65,
                height_pct      = 0.72,
                rotation        = -65,               -- 20 degrees upward
                color           = { 188, 200, 216 },
                secondary_color = { 56, 62, 74 },
                glow_color      = { 80, 225, 140 }
            },
            right_arm = {
                sprite_sheet    = "Assets/Characters/lancer/Right_arm.png",
                shader          = "lancer_wind",
                z_order         = 9,
                x_offset_pct    = 0.32,
                y_offset_pct    = 0.04,
                width_pct       = 0.26,
                height_pct      = 0.43,
                rotation        = -16,
                color           = { 176, 188, 204 },
                secondary_color = { 72, 82, 98 },
                glow_color      = { 80, 225, 140 }
            }
        }
    },

    animations = {
        idle = { start_frame = 0, frame_count = 1, speed = 6.0 },
        walk = { start_frame = 0, frame_count = 1, speed = 10.0 },

        Lance_Vertical_Poke = {
            keyframes = {
                { frame = 0, duration = 5, afterimage = false, weapon_x = -0.50, weapon_y =  0.04, weapon_angle = -45 },
                { frame = 0, duration = 4, afterimage = true,  weapon_x = -0.46, weapon_y = -0.78, weapon_angle = -60 },
                { frame = 0, duration = 8, afterimage = false, weapon_x = -0.22, weapon_y = -0.28, weapon_angle = -60 }
            }
        },

        -- Horizontal spear thrust
        Lance_Poke = {
            keyframes = {
                { frame = 0, duration = 4, afterimage = false, weapon_x = -0.35, weapon_y = -0.18, weapon_angle = 18 },
                { frame = 0, duration = 3, afterimage = true,  weapon_x =  0.55, weapon_y = -0.22, weapon_angle = 20 },
                { frame = 0, duration = 6, afterimage = false, weapon_x =  0.15, weapon_y = -0.10, weapon_angle = 10 }
            }
        },

        -- Low sweeping slice
        Lance_Sweep = {
            keyframes = {
                { frame = 0, duration = 7,  afterimage = false, weapon_x = 0.44, weapon_y = 0.14, weapon_angle = -95 },
                { frame = 0, duration = 4,  afterimage = true,  weapon_x = 0.92, weapon_y = 0.42, weapon_angle = -20 },
                { frame = 0, duration = 10, afterimage = false, weapon_x = 0.64, weapon_y = 0.34, weapon_angle = -55 }
            }
        },

        -- Overhead anti-air arc
        Lance_Sky = {
            keyframes = {
                { frame = 0, duration = 5,  afterimage = false, weapon_x = 0.50, weapon_y = 0.36,  weapon_angle = -35  },
                { frame = 0, duration = 4,  afterimage = true,  weapon_x = 0.60, weapon_y = -0.06, weapon_angle = -115 },
                { frame = 0, duration = 8,  afterimage = false, weapon_x = 0.58, weapon_y = 0.16,  weapon_angle = -85  }
            }
        },

        -- Steep downward plunge
        Lance_Dive = {
            keyframes = {
                { frame = 0, duration = 7,  afterimage = false, weapon_x = 0.44, weapon_y = 0.02, weapon_angle = -120 },
                { frame = 0, duration = 10, afterimage = true,  weapon_x = 0.68, weapon_y = 0.42, weapon_angle = -10  },
                { frame = 0, duration = 10, afterimage = false, weapon_x = 0.60, weapon_y = 0.34, weapon_angle = -55  }
            }
        }
    },

    on_passive = function(player, character, dt)
        if character.CustomData["shader_init"] == nil then
            character.CustomData["shader_init"] = 1.0
            character:SetPartShaderFloat("cape",      "u_PartMode", 0.0)
            character:SetPartShaderFloat("left_leg",  "u_PartMode", 0.0)
            character:SetPartShaderFloat("right_leg", "u_PartMode", 0.0)
            character:SetPartShaderFloat("head",      "u_PartMode", 1.0)
            character:SetPartShaderFloat("body",      "u_PartMode", 2.0)
            character:SetPartShaderFloat("lance",     "u_PartMode", 4.0)
            character:SetPartShaderFloat("right_arm", "u_PartMode", 5.0)
            character:SetPartShaderFloat("left_arm",  "u_PartMode", 5.0)
            character:SetPartShaderFloat("hair", "u_PartMode", 0.0)
        end

        local targetFlare = (character.ActiveSkill ~= nil) and 1.25 or 0.0
        local curFlare = character.CustomData["flare"] or 0.0
        curFlare = curFlare + (targetFlare - curFlare) * math.min(1.0, dt * 12.0)
        character.CustomData["flare"] = curFlare
        character:SetShaderFloat("u_AttackIntensity", curFlare)

        local animT = (character.CustomData["anim_t"] or 0.0) + dt
        character.CustomData["anim_t"] = animT

        local breathe = math.sin(animT * 3.8) * 0.012
        local capeWave = math.sin(animT * 4.5) * 3.5
        local speedX = math.abs(player.Velocity.X)
        local isWalking = player.IsGrounded and speedX > 15.0

        local lean = isWalking and 8.0 or 3.0
        character:SetPartOffset("body", 0.0, 0.02 + breathe, lean)
        character:SetPartOffset("head", 0.02, -0.28 + breathe * 1.1, 0.0)
        character:SetPartOffset("hair", -0.02, -0.42 + breathe * 1.15, 0.0)
        character:SetPartOffset("cape", -0.06, 0.06 + breathe, (isWalking and 14.0 or 5.0) + capeWave)

        if player.IsCrouching then
            character:SetPartOffset("body",      0.02,  0.05, 12.0)
            character:SetPartOffset("head", 0.05, -0.25, 0.0)
            character:SetPartOffset("hair", 0.03, -0.35, 0.0)
            character:SetPartOffset("cape",     -0.07,  0.09, 10.0)
            character:SetPartOffset("left_leg",  -0.24,  0.34, 20.0)
            character:SetPartOffset("right_leg",  0.24,  0.34, -20.0)
            character:SetPartOffset("left_arm",  -0.33,  0.07, 20.0)
            character:SetPartOffset("right_arm",  0.30,  0.07, -24.0)
        elseif isWalking then
            local stride = math.sin(animT * 14.0)
            character:SetPartOffset("left_leg",  -0.16 - stride * 0.16, 0.33,  stride * 22.0)
            character:SetPartOffset("right_leg",  0.16 + stride * 0.16, 0.33, -stride * 22.0)
            character:SetPartOffset("left_arm",  -0.35 + stride * 0.05, 0.04,  14.0 - stride * 12.0)
            character:SetPartOffset("right_arm",  0.32 - stride * 0.04, 0.04, -16.0 + stride * 8.0)
        else
            character:SetPartOffset("left_leg",  -0.18, 0.33,  8.0)
            character:SetPartOffset("right_leg",  0.18, 0.33, -8.0)
            character:SetPartOffset("left_arm",  -0.35, 0.04 + breathe,  14.0 + capeWave * 0.4)
            character:SetPartOffset("right_arm",  0.32, 0.04 + breathe, -16.0 - capeWave * 0.4)
        end

        -- Idle lance lowered directly into right hand at hip level, angled 20 degrees upward (-65 deg)
        if character.ActiveSkill == nil then
            local lanceBob = math.sin(animT * 3.2) * 0.012
            local lanceRot = -65.0 + math.sin(animT * 2.5) * 2.0
            character:SetPartOffset("lance", 0.64, 0.46 + lanceBob, lanceRot)
        end
    end,

    skills = {
        -- 1. KeyRight: Normal Poke + Short Wind Gust Extension (2 Distinct Hits, Lower Wind Dmg)
        spear_poke = {
            input_trigger   = Keys.KeyRight,
            animation       = "Lance_Poke",
            damage          = 11,
            attack_height   = "mid",
            startup_frames  = 4,
            active_frames   = 3,
            recovery_frames = 8,
            hitbox          = { x_offset_pct = 0.75, y_offset_pct = 0.39, width_pct = 1.75, height_pct = 0.22 },
            hitStun         = 0.26,
            cooldown_time   = 0.42,
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                local f = character.CurrentActionFrame
                local dir = player.FacingRight and 1 or -1

                -- Quick forward lunge lean & step during the thrust
                if f >= 4 and f <= 6 then
                    player:Translate(dir * character.WalkSpeed * 0.85 * dt, 0)
                    character:SetPartOffset("body",      0.06,  0.04,  16.0)
                    character:SetPartOffset("head",      0.10, -0.26,   4.0)
                    character:SetPartOffset("hair",      0.06, -0.40,   4.0)
                    character:SetPartOffset("left_leg",  -0.30, 0.34,  28.0)
                    character:SetPartOffset("right_leg",  0.28, 0.34, -22.0)
                end

                if f == 5 then
                    PlaySound(player, { sound = "cosmic_light_slash", pitch = 1.45, volume = 0.70 })
                    -- Tight wind burst that expires right past ~1.25x the lance hitbox
                    CreateProjectile(player, {
                        shader        = "cosmic_projectile",
                        x_offset_pct  = 0.065,
                        y_offset_pct  = 0.0,
                        speed_x_pct   = 0.45,
                        width_pct     = 0.045,
                        height_pct    = 0.040,
                        damage        = 4,
                        hitStun       = 0.20,
                        knockback_pct = 0.14,
                        lifetime      = 0.08,
                        color         = { 110, 240, 170 }
                    })
                end
            end,
            on_hit = function(attacker, defender, skill)
                ApplyHitForce(attacker, defender, skill.Damage, skill.HitStun, 0.16)
            end
        },

        -- 2. KeyBottom: Wind Vortex Slash Around Self (360° Heavy Knockback)
        wind_vortex_slash = {
            input_trigger   = Keys.KeyBottom,
            animation       = "Lance_Sweep",
            damage          = 14,
            attack_height   = "low",
            startup_frames  = 7,
            active_frames   = 6,
            recovery_frames = 14,
            -- Centered around Lancer to hit both sides
            hitbox          = { x_offset_pct = -0.85, y_offset_pct = -0.10, width_pct = 2.70, height_pct = 1.05 },
            hitStun         = 0.48,
            cooldown_time   = 1.8,
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 7 then
                    PlaySound(player, { sound = "cosmic_heavy_slash", pitch = 1.25, volume = 0.85 })
                    TriggerScreenShake(6, 4, 8)
                    -- Swirling wind vortex visual around Lancer
                    CreateProjectile(player, {
                        shader             = "wind_vortex",
                        rotate_to_velocity = false,
                        x_offset_pct       = 0.0,
                        y_offset_pct       = 0.02,
                        speed_x_pct        = 0.0,
                        speed_y_pct        = 0.0,
                        width_pct          = 0.16,
                        height_pct         = 0.22,
                        damage             = 0,
                        destroy_on_hit     = false,
                        lifetime           = 0.28,
                        scroll_speed       = 14.0,
                        color              = { 95, 235, 160 }
                    })
                end
            end,
            on_hit = function(attacker, defender, skill)
                -- Massive knockback away from Lancer
                ApplyLaunchForce(attacker, defender, skill.Damage, skill.HitStun, 0.42, -0.30)
                SetHitstop(4)
            end
        },

        -- 3. KeyLeft: Wind Parry Stance (Reverses Enemy Projectiles + Knockback Counter)
        royal_deflect = {
            input_trigger   = Keys.KeyLeft,
            animation       = "Lance_Poke",
            damage          = 18,
            startup_frames  = 2,
            active_frames   = 14,
            recovery_frames = 14,
            hitbox          = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 1.30, height_pct = 1.05 },
            hitStun         = 0.85,
            cooldown_time   = 2.6,
            is_counter      = true,
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                local f = character.CurrentActionFrame
                if f == 2 then
                    SpawnVFX(player, { preset = "BlockSpark", scale = 1.1, lifetime = 0.24, color = { 100, 240, 170 } })
                end

                -- During active parry frames, catch and reflect enemy projectiles back at them
                if f >= 2 and f <= 16 then
                    local list = PhysicsEngine.ActiveProjectiles
                    if list ~= nil then
                        for i = 1, #list do
                            local p = list[i]
                            if p ~= nil and p.IsActive and p.Owner ~= player then
                                local dx = math.abs(p.PosX - player.PosX)
                                local dy = math.abs(p.PosY - player.PosY)
                                if dx < 110 and dy < 110 then
                                    local dmg = math.max(12, p.Damage)
                                    p:Destroy()

                                    SpawnVFX(player, { preset = "CounterFlash", scale = 1.1, color = { 120, 255, 185 } })
                                    PlaySound(player, { sound = "prism_chime", pitch = 1.45, volume = 0.90 })
                                    SetHitstop(4)

                                    -- Fire reflected wind-infused projectile back
                                    CreateProjectile(player, {
                                        shader        = "wind_vortex",
                                        x_offset_pct  = 0.05,
                                        y_offset_pct  = 0.0,
                                        speed_x_pct   = 0.62,
                                        width_pct     = 0.055,
                                        height_pct    = 0.075,
                                        damage        = dmg,
                                        hitStun       = 0.55,
                                        knockback_pct = 0.34,
                                        lifetime      = 2.0,
                                        scroll_speed  = 14.0,
                                        color         = { 110, 255, 175 }
                                    })
                                    break
                                end
                            end
                        end
                    end
                end
            end,
            on_counter = function(defender, attacker)
                local backDir = defender.FacingRight and -1 or 1
                defender:SetVelocityPct(backDir * DEFLECT_BACKSTEP_VEL_X_PCT, 0)
                ApplyHitForce(defender, attacker, DEFLECT_COUNTER_DAMAGE, DEFLECT_COUNTER_STUN, 0.38)
                SetHitstop(6)
                TriggerScreenShake(8, 6, 10)
            end
        },

        -- 4. KeyTop: Skyward Launcher (Huge Upward Knockback to Start Air Combos)
        -- 4. KeyTop: Vertical Tornado Poke (Huge Upward Launch for Air Combos)
        gale_launcher = {
            input_trigger   = Keys.KeyTop,
            animation       = "Lance_Vertical_Poke",
            damage          = 14,
            attack_height   = "mid",
            startup_frames  = 6,
            active_frames   = 5,
            recovery_frames = 12,
            hitbox          = { x_offset_pct = -0.75, y_offset_pct = -0.70, width_pct = 2.5, height_pct = 1.00 },
            hitStun         = 0.95,
            cooldown_time   = 1.4,
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                local f = character.CurrentActionFrame

                -- Dip low on windup, then stretch upward on the vertical thrust
                if f >= 1 and f <= 4 then
                    character:SetPartOffset("body",      0.00,  0.06,  8.0)
                    character:SetPartOffset("head",      0.02, -0.24,  0.0)
                    character:SetPartOffset("hair",     -0.02, -0.38,  0.0)
                    character:SetPartOffset("left_leg",  -0.24, 0.35,  18.0)
                    character:SetPartOffset("right_leg",  0.24, 0.35, -18.0)
                elseif f >= 5 and f <= 9 then
                    character:SetPartOffset("body",      0.00, -0.04, -6.0)
                    character:SetPartOffset("head",      0.00, -0.33, -6.0)
                    character:SetPartOffset("hair",     -0.04, -0.47, -6.0)
                    character:SetPartOffset("right_arm", 0.24, -0.10, -135.0)
                end

                if f == 6 then
                    PlaySound(player, { sound = "cosmic_heavy_slash", pitch = 1.40, volume = 0.85 })
                    PlaySound(player, { sound = "cosmic_light_slash", pitch = 1.65, volume = 0.65 })

                    -- Base wind burst ring + upward spark
                    SpawnVFX(player, {
                        preset       = "Shockwave",
                        x_offset_pct = 0.02,
                        y_offset_pct = 0.04,
                        scale        = 1.15,
                        lifetime     = 0.24,
                        color        = { 100, 245, 170 }
                    })
                    SpawnVFX(player, {
                        preset       = "HitSpark",
                        x_offset_pct = 0.02,
                        y_offset_pct = -0.14,
                        scale        = 0.95,
                        lifetime     = 0.20,
                        color        = { 180, 255, 215 }
                    })

                    -- Rising tornado funnel visual matching the vertical launcher hitbox
                    CreateProjectile(player, {
                        shader             = "wind_tornado",
                        rotate_to_velocity = false,
                        x_offset_pct       = 0.0,
                        y_offset_pct       = -0.14,
                        speed_x_pct        = 0.0,
                        speed_y_pct        = 0.0,
                        width_pct          = 0.12,
                        height_pct         = 0.21,
                        damage             = 0,
                        destroy_on_hit     = false,
                        lifetime           = 0.36,
                        scroll_speed       = 13.0,
                        color              = { 95, 240, 165 },
                        trajectory         = function(proj, pDt)
                            proj.Material:SetFloat("u_Progress", proj.ElapsedTime / proj.Lifetime)
                        end
                    })
                end
            end,
            on_hit = function(attacker, defender, skill)
                -- Huge vertical launch for air-combo starter
                ApplyLaunchForce(attacker, defender, skill.Damage, skill.HitStun, 0.08, -0.92)
                SetHitstop(5)
                TriggerScreenShake(4, 10, 8)
            end
        },

        -- 5. KeyTop + KeyRight: Long-Reach Wind Uppercut (Green Wind Sweep Trail)
        sky_crescent = {
            input_trigger   = Keys.KeyTop + Keys.KeyRight,
            animation       = "Lance_Sky",
            damage          = 22,
            attack_height   = "overhead",
            startup_frames  = 6,
            active_frames   = 7,
            recovery_frames = 15,
            hitbox          = { x_offset_pct = 0.50, y_offset_pct = -0.55, width_pct = 2.05, height_pct = 1.35 },
            hitStun         = 0.85,
            cooldown_time   = 2.6,
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 6 then
                    PlaySound(player, { sound = "cosmic_heavy_slash", pitch = 1.30, volume = 0.90 })
                    SpawnVFX(player, {
                        preset       = "SlashArc",
                        x_offset_pct = 0.06,
                        y_offset_pct = -0.06,
                        scale        = 1.45,
                        lifetime     = 0.24,
                        flip_y       = true, -- Bottom-to-top green wind sweep
                        rotation     = -12,
                        color        = { 80, 235, 145 }
                    })
                end
            end,
            on_hit = function(attacker, defender, skill)
                ApplyLaunchForce(attacker, defender, skill.Damage, skill.HitStun, 0.12, -0.78)
                SetHitstop(5)
                TriggerScreenShake(6, 10, 10)
            end
        },

        -- 6. KeyTop + KeyBottom: Shoots a Traveling Vortex of Air
        tempest_shot = {
            input_trigger   = Keys.KeyTop + Keys.KeyBottom,
            animation       = "Lance_Poke",
            damage          = 0,
            startup_frames  = 9,
            active_frames   = 4,
            recovery_frames = 12,
            cooldown_time   = 2.8,
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 9 then
                    PlaySound(player, { sound = "cosmic_heavy_slash", pitch = 1.15, volume = 0.85 })
                    CreateProjectile(player, {
                        shader             = "wind_vortex",
                        rotate_to_velocity = false,
                        x_offset_pct       = 0.08,
                        y_offset_pct       = -0.02,
                        speed_x_pct        = 0.36,
                        width_pct          = 0.075,
                        height_pct         = 0.115,
                        damage             = 16,
                        hitStun            = 0.50,
                        knockback_pct      = 0.28,
                        is_blockbreak      = true,
                        lifetime           = 2.4,
                        scroll_speed       = 12.0,
                        color              = { 85, 235, 155 }
                    })
                end
            end
        },

        -- 7. KeyTop + KeyBottom + KeyRight: Dragoon Leap & Ground Stab -> Huge Pulling Wind Vortex (DoT)
        dragoon_dive = {
            input_trigger     = Keys.KeyTop + Keys.KeyBottom + Keys.KeyRight,
            animation         = "Lance_Dive",
            damage            = 16,
            attack_height     = "overhead",
            startup_frames    = 9,
            active_frames     = 10,
            recovery_frames   = 18,
            is_hyperframes    = true,
            hyper_start_frame = 2,
            hyper_frames      = 15,
            hitbox            = { x_offset_pct = 0.45, y_offset_pct = 0.05, width_pct = 1.55, height_pct = 0.85 },
            hitStun           = 0.65,
            cooldown_time     = 8.0,
            is_blockbreak     = true,
            is_customlogic    = true,
            on_customlogic = function(player, character, dt)
                local dir = player.FacingRight and 1 or -1
                local f = character.CurrentActionFrame

                if f < 8 then
                    player.IsGrounded = false
                    player:Translate(dir * character.WalkSpeed * 0.85 * dt, -character.WalkSpeed * 2.5 * dt)
                elseif f < 17 then
                    player:Translate(dir * character.WalkSpeed * 2.3 * dt, character.WalkSpeed * 2.9 * dt)
                elseif f == 17 then
                    -- Ground stab impact -> spawn huge pulling wind vortex with DoT ticks
                    TriggerScreenShake(12, 16, 16)
                    PlaySound(player, {
                        sound         = "cosmic_sub_drop",
                        pitch         = 1.05,
                        volume        = 0.95,
                        duck_world    = 0.08,
                        duck_frames   = 12,
                        duck_recovery = 0.50
                    })
                    SpawnVFX(player, { preset = "Shockwave", scale = 1.6, lifetime = 0.35, color = { 90, 240, 160 } })

                    local tickAcc = 0.0
                    CreateProjectile(player, {
                        shader             = "wind_vortex",
                        rotate_to_velocity = false,
                        x_offset_pct       = 0.05,
                        y_offset_pct       = -0.04,
                        speed_x_pct        = 0.0,
                        speed_y_pct        = 0.0,
                        width_pct          = 0.24,
                        height_pct         = 0.34,
                        damage             = 5,
                        hitStun            = 0.22,
                        knockback_pct      = 0.0,
                        is_blockbreak      = true,
                        destroy_on_hit     = false,
                        lifetime           = 2.2,
                        scroll_speed       = 15.0,
                        color              = { 80, 240, 155 },
                        trajectory         = function(proj, pDt)
                            local target = proj.Target
                            if target ~= nil then
                                local dx = proj.PosX - target.PosX
                                local dy = proj.PosY - target.PosY
                                local dist = math.sqrt(dx * dx + dy * dy)
                                -- Pull enemies toward the center of the vortex
                                if dist > 8.0 and dist < 290.0 then
                                    local pullX = (dx / dist) * 0.34
                                    local pullY = (dy / dist) * 0.22
                                    target:SetVelocityPct(pullX, pullY)
                                end
                            end

                            tickAcc = tickAcc + pDt
                            if tickAcc >= 0.22 then
                                tickAcc = 0.0
                                proj:ResetHit()
                            end
                        end,
                        on_hit             = function(owner, hit_player, proj)
                            ApplyHitForce(owner, hit_player, 5, 0.22, 0.0)
                        end
                    })
                end
            end,
            on_blockbreak = function(attacker, defender)
                ApplyHitForce(attacker, defender, 20, 0.85, 0.10)
            end
        }
    }
}

return Character