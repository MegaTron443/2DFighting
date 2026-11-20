-- ============================================================
-- GRAVITY ODACHI SAMURAI (Compound Shader + Dual-Stance Rewrite) 0% AI
-- ============================================================

local COUNTER_UPFLING_Y_PCT     = -0.88
local COUNTER_SLAM_VEL_Y_PCT    = 1.65
local AIR_PULL_SLAM_VEL_Y_PCT   = 1.55
local COLLAPSE_DASH_VEL_X_PCT   = 1.05
local LIGHT_PULL_VEL_X_PCT      = 0.52
local LIGHT_ULT_PUSH_VEL_X_PCT  = 0.72

-- Terraria Sky's Edge-style cosmic slash trail (VFX + Afterimage + Layered Audio)
local function SpawnCosmicSlashTrail(player, character, isHeavy, isUpper)
    local r, g, b = isHeavy and 185 or 110, isHeavy and 65 or 225, 255
    local scale   = isHeavy and 1.35 or 0.90

    SpawnVFX(player, {
        preset       = "SlashArc",
        x_offset_pct = isHeavy and 0.055 or 0.045,
        y_offset_pct = isUpper and -0.04 or 0.01,
        scale        = scale,
        lifetime     = isHeavy and 0.24 or 0.16,
        flip_y       = isUpper,
        rotation     = isUpper and -12 or 8,
        color        = { r, g, b }
    })

    if isHeavy then
        PlaySound(player, { sound = "cosmic_heavy_slash", pitch = isUpper and 1.05 or 0.82, volume = 0.90 })
        PlaySound(player, { sound = "prism_chime",        pitch = isUpper and 1.15 or 0.90, volume = 0.35 })
    else
        PlaySound(player, { sound = "cosmic_light_slash", pitch = 1.45, volume = 0.75 })
        PlaySound(player, { sound = "prism_chime",        pitch = 1.65, volume = 0.30 })
    end
end

-- Dynamically reconfigures physics, visuals, and skill frame/damage data per stance
local function SetStance(character, stance)
    character.CustomData["stance"] = stance

    local sNormal  = character.Skills["Normal_Slash"]
    local sUpper   = character.Skills["Gravity_Upper"]
    local sShoot   = character.Skills["Event_Horizon"]
    local sCounter = character.Skills["Anchor_Counter"]
    local sUlt     = character.Skills["Collapse_Execution"]

    if stance == 0 then
        -- HEAVY STANCE (2 Swords Combined)
        character.WalkSpeedPct = 0.20
        character.GravityPct   = 1.50
        character:SetPartVisible("sword", true)
        character:SetPartVisible("sword_2", true)
        character:SetPartShaderColor("sword",   "u_GlowColor", 195, 110, 255)
        character:SetPartShaderColor("sword_2", "u_GlowColor", 195, 110, 255)

        if sNormal then sNormal:SetHitbox(0.45, -0.10, 2.15, 0.85) end
        if sUpper  then sUpper:SetHitbox(0.40, -0.45, 1.95, 1.25)  end
        if sShoot  then sShoot:SetHitbox(0.45, -0.08, 2.20, 0.82)  end
        if sUlt    then sUlt:SetHitbox(0.50, 0.02, 2.45, 0.55)     end
    else
        -- LIGHTWEIGHT STANCE (Fast, Low Gravity, Utility/Control)
        character.WalkSpeedPct = 0.36
        character.GravityPct   = 0.92
        character:SetPartVisible("sword", true)
        character:SetPartVisible("sword_2", false)
        character:SetPartShaderColor("sword", "u_GlowColor", 130, 225, 255)

        if sNormal then sNormal:SetHitbox(0.40, 0.05, 1.45, 0.50)  end
        if sUpper  then sUpper:SetHitbox(0.40, -0.25, 1.30, 0.85)  end
        if sShoot  then sShoot:SetHitbox(0.40, 0.08, 1.35, 0.50)   end
        if sUlt    then sUlt:SetHitbox(-0.50, 0.00, 2.00, 0.90)    end
    end
end

local Character = {
    name = "GravitySamurai",
    description = [[
DUAL-STANCE COSMIC BLADEMASTER
Shifts between Heavy (Combined Odachi) and Lightweight (Split Gravity) stances.

HEAVY STANCE (Combined Blades):
- Normal Slash (Right): Slow, high-damage overhead cleave.
- Gravity Upper (Top): Block-breaking rising slash that inflicts low gravity.
- Event Horizon (Left): Heavy overhead slash + forward wave (45 deg downward in air).
- Anchor Counter (Bottom): Long-window parry that launches high then slams down.
- Collapse Execution (Top+Bottom): Armored stab dash; executes foes below 20 HP.

LIGHTWEIGHT STANCE (Low Gravity + High Speed):
- Normal Slash (Right): Rapid low-damage slash.
- Gravity Upper (Top): Summons block-crushing drop above foe; forces crouch & grounds aerial foes.
- Event Horizon (Left): Slow forward gravity orb that pulls foes in and deals tick damage.
- Anchor Counter (Bottom): Long-startup projectile behind foe that pulls them in with long stun.
- Collapse Execution (Top+Bottom): Ground stab emitting repulsive sphere pulses that push foes away with tick damage.

Stance Switch (Top+Right): Swaps between Heavy and Lightweight stances.
]],

    physics = {
        width_pct      = 0.055,
        height_pct     = 0.21,
        walk_speed_pct = 0.20,
        jump_force_pct = -0.66,
        gravity_pct    = 1.50,
        friction       = 0.84
    },

    rendering = {
        frame_width  = 256,
        frame_height = 256,
        body_color   = { 45, 38, 65 },
        shader       = "gravity_ink",

        parts = {
            left_leg = {
                sprite_sheet = "Assets/Characters/gravity/left_leg.png",
                shader       = "gravity_ink",
                z_order      = 1,
                x_offset_pct = -0.24,
                y_offset_pct = 0.34,
                width_pct    = 0.30,
                height_pct   = 0.31,
                rotation     = 10,
                color           = { 42, 46, 62 },   -- Dark gunmetal slate
                secondary_color = { 18, 20, 30 },
                glow_color      = { 255, 175, 70 }
            },
            right_leg = {
                sprite_sheet = "Assets/Characters/gravity/right_leg.png",
                shader       = "gravity_ink",
                z_order      = 1,
                x_offset_pct = 0.24,
                y_offset_pct = 0.34,
                width_pct    = 0.30,
                height_pct   = 0.31,
                rotation     = -10,
                color           = { 42, 46, 62 },
                secondary_color = { 18, 20, 30 },
                glow_color      = { 255, 175, 70 }
            },
            body = {
                sprite_sheet = "Assets/Characters/gravity/body.png",
                shader       = "gravity_ink",
                z_order      = 2,
                x_offset_pct = 0.0,
                y_offset_pct = 0.02,
                width_pct    = 0.96,
                height_pct   = 0.43,
                rotation     = 4,
                color           = { 68, 52, 118 },  -- Deep royal indigo robe
                secondary_color = { 24, 22, 44 },   -- Midnight navy shadows
                glow_color      = { 255, 185, 75 }
            },
            head = {
                sprite_sheet = "Assets/Characters/gravity/head.png",
                shader       = "gravity_ink",
                z_order      = 3,
                x_offset_pct = 0.03,
                y_offset_pct = -0.27,
                width_pct    = 0.56,
                height_pct   = 0.18,
                glow_color      = { 155, 95, 255 }
            },
            hat = {
                sprite_sheet = "Assets/Characters/gravity/hat.png",
                shader       = "gravity_ink",
                z_order      = 4,
                x_offset_pct = 0.02,
                y_offset_pct = -0.38,
                width_pct    = 1.55,
                height_pct   = 0.13,
                rotation     = -4,
                color           = { 34, 36, 52 },   -- Obsidian charcoal hat
                secondary_color = { 14, 15, 24 },
                glow_color      = { 255, 190, 85 }
            },
            sword = {
                sprite_sheet = "Assets/Characters/gravity/sword_1.png",
                shader       = "gravity_ink",
                z_order      = 5,
                is_socket    = true,
                visible      = true,
                x_offset_pct = 0.62,
                y_offset_pct = 0.16,
                width_pct    = 1.55,
                height_pct   = 0.62,
                rotation     = -62,
                color           = { 58, 64, 82 },   -- Forged dark steel blade
                secondary_color = { 20, 22, 32 },
                glow_color      = { 195, 105, 255 }
            },
            sword_2 = {
                sprite_sheet = "Assets/Characters/gravity/sword_2.png",
                shader       = "gravity_ink",
                z_order      = 6,
                is_socket    = true,
                visible      = true,
                x_offset_pct = 0.62,
                y_offset_pct = 0.16,
                width_pct    = 1.55,
                height_pct   = 0.62,
                rotation     = -62,
                color           = { 180, 75, 255 },
                glow_color      = { 195, 90, 255 }
            }
        }
    },

    animations = {
        idle = { start_frame = 0, frame_count = 1, speed = 6.0 },
        walk = { start_frame = 0, frame_count = 1, speed = 10.0 },

        Heavy_Overhead = {
            keyframes = {
                { frame = 0, duration = 11, afterimage = false, weapon_x = -0.45, weapon_y = -0.48, weapon_angle = -65 },
                { frame = 0, duration = 2,  afterimage = true,  weapon_x = 0.30,  weapon_y = -0.22, weapon_angle = 20  },
                { frame = 0, duration = 3,  afterimage = true,  weapon_x = 0.50,  weapon_y = 0.12,  weapon_angle = 88  },
                { frame = 0, duration = 10, afterimage = false, weapon_x = 0.35,  weapon_y = 0.20,  weapon_angle = 96  }
            }
        },

        Light_Slash = {
            keyframes = {
                { frame = 0, duration = 4, afterimage = false, weapon_x = -0.20, weapon_y = -0.15, weapon_angle = -25 },
                { frame = 0, duration = 3, afterimage = true,  weapon_x = 0.42,  weapon_y = 0.05,  weapon_angle = 65  },
                { frame = 0, duration = 5, afterimage = false, weapon_x = 0.22,  weapon_y = 0.12,  weapon_angle = 78  }
            }
        },

        Upper_Slash = {
            keyframes = {
                { frame = 0, duration = 6, afterimage = false, weapon_x = 0.15,  weapon_y = 0.30,  weapon_angle = 115 },
                { frame = 0, duration = 2, afterimage = true,  weapon_x = 0.42,  weapon_y = -0.20, weapon_angle = 10  },
                { frame = 0, duration = 3, afterimage = true,  weapon_x = 0.28,  weapon_y = -0.48, weapon_angle = -50 },
                { frame = 0, duration = 7, afterimage = false, weapon_x = 0.15,  weapon_y = -0.40, weapon_angle = -35 }
            }
        },

        Stab_Thrust = {
            keyframes = {
                { frame = 0, duration = 5,  afterimage = false, weapon_x = -0.40, weapon_y = 0.04, weapon_angle = 72 },
                { frame = 0, duration = 10, afterimage = true,  weapon_x = 0.68,  weapon_y = 0.02, weapon_angle = 72 },
                { frame = 0, duration = 10, afterimage = false, weapon_x = 0.30,  weapon_y = 0.06, weapon_angle = 72 }
            }
        },

        Ground_Stab = {
            keyframes = {
                { frame = 0, duration = 8,  afterimage = false, weapon_x = 0.05, weapon_y = -0.35, weapon_angle = 162 },
                { frame = 0, duration = 14, afterimage = true,  weapon_x = 0.10, weapon_y = 0.38,  weapon_angle = 162 },
                { frame = 0, duration = 12, afterimage = false, weapon_x = 0.08, weapon_y = 0.22,  weapon_angle = 162 }
            }
        }
    },

    on_passive = function(player, character, dt)
        if character.CustomData["stance"] == nil then
            SetStance(character, 0)

            character:SetPartShaderVec2("sword", "u_SingularityUV", 0.21, 0.21)
            character:SetPartShaderFloat("sword", "u_SingularityScale", 0.24)
            character:SetPartShaderFloat("sword", "u_SingularityAngle", 0.75)

            character:SetPartShaderVec2("left_leg", "u_SingularityUV", 0.50, 0.90)
            character:SetPartShaderFloat("left_leg", "u_SingularityScale", 0.42)
            character:SetPartShaderFloat("left_leg", "u_SingularityAngle", 0.0)

            character:SetPartShaderVec2("right_leg", "u_SingularityUV", 0.50, 0.90)
            character:SetPartShaderFloat("right_leg", "u_SingularityScale", 0.42)
            character:SetPartShaderFloat("right_leg", "u_SingularityAngle", 0.0)

            character:SetPartShaderFloat("body",    "u_SingularityScale", 0.0)
            character:SetPartShaderFloat("head",    "u_SingularityScale", 0.0)
            character:SetPartShaderFloat("hat",     "u_SingularityScale", 0.0)
            character:SetPartShaderFloat("sword_2", "u_SingularityScale", 0.0)

            character:SetShaderFloat("u_ParallaxStrength", 0.16)
            character:SetShaderFloat("u_SpaceScrollSpeed", 0.03)
        end

        character:SetShaderVec2("u_CharacterPos", player.PosX, player.PosY)
        character:SetShaderVec2("u_CharacterVelocity", player.Velocity.X, player.Velocity.Y)

        local targetFlare = (character.ActiveSkill ~= nil) and 1.35 or 0.0
        local curFlare = character.CustomData["flare"] or 0.0
        curFlare = curFlare + (targetFlare - curFlare) * math.min(1.0, dt * 12.0)
        character.CustomData["flare"] = curFlare
        character:SetShaderFloat("u_AttackIntensity", curFlare)

        local animT = (character.CustomData["anim_t"] or 0.0) + dt
        character.CustomData["anim_t"] = animT

        local breathe = math.sin(animT * 3.5) * 0.014
        local speedX = math.abs(player.Velocity.X)
        local isWalking = player.IsGrounded and speedX > 15.0

        -- Subtle upper-body breathing + forward lean when moving
        local lean = isWalking and 9.0 or 4.0
        character:SetPartOffset("body", 0.0, 0.02 + breathe, lean)
        character:SetPartOffset("head", 0.03, -0.27 + breathe * 1.1, 0.0)
        character:SetPartOffset("hat",  0.02, -0.38 + breathe * 1.2, isWalking and -8.0 or -4.0)

        if player.IsCrouching then
            character:SetPartOffset("body",      0.02,  0.04, 14.0)
            character:SetPartOffset("head",      0.06, -0.25, 0.0)
            character:SetPartOffset("hat",       0.05, -0.37, -10.0)
            character:SetPartOffset("left_leg",  -0.30, 0.34, 22.0)
            character:SetPartOffset("right_leg",  0.30, 0.34, -22.0)
            if character.ActiveSkill == nil then
                character:SetPartOffset("sword",   0.55, 0.22, -38.0)
                character:SetPartOffset("sword_2", 0.55, 0.22, -38.0)
            end
        elseif isWalking then
            local stride = math.sin(animT * 14.0)
            character:SetPartOffset("left_leg",  -0.18 - stride * 0.18, 0.34, stride * 24.0)
            character:SetPartOffset("right_leg",  0.18 + stride * 0.18, 0.34, -stride * 24.0)
        else
            character:SetPartOffset("left_leg",  -0.24, 0.34, 10.0)
            character:SetPartOffset("right_leg",  0.24, 0.34, -10.0)
        end

        -- Gentle telekinetic hover on the sword when not attacking
        if character.ActiveSkill == nil then
            local swordBob = math.sin(animT * 2.8 + 1.0) * 0.02
            local swordTilt = -62.0 + math.sin(animT * 2.2) * 3.5
            character:SetPartOffset("sword",   0.62, 0.16 + swordBob, swordTilt)
            character:SetPartOffset("sword_2", 0.62, 0.16 + swordBob, swordTilt)
        end

        -- 1. Low-Gravity Debuff Timer (from Heavy Uppercut)
        local lowGravTimer = character.CustomData["low_grav_timer"] or 0.0
        if lowGravTimer > 0.0 then
            lowGravTimer = lowGravTimer - dt
            character.CustomData["low_grav_timer"] = lowGravTimer
            if lowGravTimer <= 0.0 then
                local opp = player.TargetOpponent
                local origGrav = character.CustomData["low_grav_orig"]
                if opp ~= nil and opp.CurrentCharacter ~= nil and origGrav ~= nil then
                    opp.CurrentCharacter.GravityPct = origGrav
                end
                character.CustomData["low_grav_orig"] = nil
            end
        end

        -- 2. Delayed Counter Downslam Timer (from Heavy Anchor Counter)
        local slamTimer = character.CustomData["counter_slam_timer"] or 0.0
        if slamTimer > 0.0 then
            slamTimer = slamTimer - dt
            character.CustomData["counter_slam_timer"] = slamTimer
            if slamTimer <= 0.0 then
                local opp = player.TargetOpponent
                if opp ~= nil then
                    opp.IsAirStalled = false
                    opp:SetVelocityPct(0, COUNTER_SLAM_VEL_Y_PCT)
                    ApplyHitForce(player, opp, 16, 0.70, 0.05)
                    SetHitstop(6)
                    TriggerScreenShake(0, 24, 14)
                    SpawnVFX(player, {
                        preset          = "Explosion",
                        spawn_at_target = true,
                        scale           = 1.25,
                        color           = { 155, 45, 255 }
                    })
                    PlaySound(player, {
                        sound         = "cosmic_sub_drop",
                        pitch         = 0.75,
                        duck_world    = 0.04,
                        duck_frames   = 12,
                        duck_recovery = 0.55
                    })
                end
            end
        end
    end,

    skills = {
        -- 1. NORMAL SLASH (Heavy: Slow High-Dmg Overhead | Light: Fast Smaller Slash)
        Normal_Slash = {
            input_trigger   = Keys.KeyRight,
            animation       = "Heavy_Overhead",
            damage          = 26,
            attack_height   = "overhead",
            startup_frames  = 13,
            active_frames   = 5,
            recovery_frames = 12,
            hitStun         = 0.42,
            cooldown_time   = 0.48,
            hitbox = { x_offset_pct = 0.45, y_offset_pct = -0.10, width_pct = 2.15, height_pct = 0.85 },
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                local stance = character.CustomData["stance"] or 0
                if stance == 0 and character.CurrentActionFrame == 12 then
                    SpawnCosmicSlashTrail(player, character, true, false)
                elseif stance == 1 and character.CurrentActionFrame == 4 then
                    SpawnCosmicSlashTrail(player, character, false, false)
                end
            end,
            on_hit = function(owner, hit_player, skill)
                local stance = owner.CurrentCharacter.CustomData["stance"] or 0
                if stance == 0 then
                    ApplyHitForce(owner, hit_player, skill.Damage, skill.HitStun, 0.22)
                    SetHitstop(5)
                    TriggerScreenShake(10, 5, 10)
                else
                    ApplyHitForce(owner, hit_player, skill.Damage, skill.HitStun, 0.12)
                    SetHitstop(2)
                    TriggerScreenShake(3, 1, 4)
                end
            end
        },

        -- 2. UPPERCUT (Heavy: Rising Blockbreak + Low Gravity | Light: Avoidable Overhead Drop Projectile)
        Gravity_Upper = {
            input_trigger   = Keys.KeyTop,
            animation       = "Upper_Slash",
            damage          = 20,
            attack_height   = "mid",
            startup_frames  = 8,
            active_frames   = 5,
            recovery_frames = 10,
            hitStun         = 0.50,
            cooldown_time   = 1.4,
            is_blockbreak   = true,
            hitbox = { x_offset_pct = 0.40, y_offset_pct = -0.45, width_pct = 1.95, height_pct = 1.25 },
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                local stance = character.CustomData["stance"] or 0
                if stance == 0 then
                    if character.CurrentActionFrame == 7 then
                        SpawnCosmicSlashTrail(player, character, true, true)
                    end
                else
                    -- Lightweight: Summon high, telegraphed block-crushing drop above opponent
                    if character.CurrentActionFrame == 9 then
                        SpawnCosmicSlashTrail(player, character, false, true)
                        PlaySound(player, { sound = "prism_chime", pitch = 1.4, volume = 0.75 })

                        local opp = player.TargetOpponent
                        local dropped = false

                        local proj = CreateProjectile(player, {
                            shader        = "cosmic_projectile",
                            x_offset_pct  = 0.0,
                            y_offset_pct  = -0.58,
                            speed_x_pct   = 0.0,
                            speed_y_pct   = 0.01,
                            width_pct     = 0.065,
                            height_pct    = 0.080,
                            damage        = 14,
                            hitStun       = 0.85,
                            is_blockbreak = true,
                            lifetime      = 1.8,
                            color         = { 130, 210, 255 },
                            trajectory    = function(p, pDt)
                                -- 0.22s telegraph hover, then drop
                                if not dropped and p.ElapsedTime >= 0.22 then
                                    dropped = true
                                    p:SetVelocity(0, 480)
                                    PlaySound(player, { sound = "cosmic_sub_drop", pitch = 1.25, volume = 0.80 })
                                end
                            end,
                            on_hit        = function(owner, target, p)
                                target.IsCrouching = true
                                if not target.IsGrounded then
                                    target.IsAirStalled = false
                                    target:SetVelocityPct(0, AIR_PULL_SLAM_VEL_Y_PCT)
                                end
                                ApplyHitForce(owner, target, 14, 0.85, 0.04)
                                SetHitstop(5)
                                TriggerScreenShake(0, 14, 10)
                                SpawnVFX(owner, {
                                    preset          = "BlockBreak",
                                    spawn_at_target = true,
                                    scale           = 1.1,
                                    color           = { 130, 215, 255 }
                                })
                            end
                        })

                        if proj ~= nil and opp ~= nil then
                            pcall(function()
                                proj.Position = NewVec2(opp.PosX, opp.PosY - 360)
                            end)
                            -- Telegraph ring above opponent
                            SpawnVFX(player, {
                                preset          = "Shockwave",
                                spawn_at_target = true,
                                y_offset_pct    = -0.55,
                                scale           = 0.9,
                                lifetime        = 0.24,
                                color           = { 130, 225, 255 }
                            })
                        end
                    end
                end
            end,
            on_hit = function(owner, hit_player, skill)
                local ch = owner.CurrentCharacter
                local stance = ch.CustomData["stance"] or 0
                if stance == 0 then
                    if hit_player.CurrentCharacter ~= nil then
                        if ch.CustomData["low_grav_orig"] == nil then
                            ch.CustomData["low_grav_orig"] = hit_player.CurrentCharacter.GravityPct
                        end
                        hit_player.CurrentCharacter.GravityPct = ch.CustomData["low_grav_orig"] * 0.35
                        ch.CustomData["low_grav_timer"] = 3.5
                    end

                    ApplyLaunchForce(owner, hit_player, skill.Damage, 0.60, 0.08, -0.78)
                    SetHitstop(5)
                    TriggerScreenShake(0, 12, 10)
                    TriggerPaletteShift(140, 40, 255, 0.45, 6, 0.35)
                else
                    hit_player.IsCrouching = true
                    if not hit_player.IsGrounded then
                        hit_player.IsAirStalled = false
                        hit_player:SetVelocityPct(0, AIR_PULL_SLAM_VEL_Y_PCT)
                    end
                    ApplyHitForce(owner, hit_player, 10, 0.85, 0.04)
                end
            end
        },

        -- 3. SHOOT SKILL (Heavy: Overhead Slash + Forward/45-deg Air Wave | Light: Slow Pull-In Tick Orb)
        Event_Horizon = {
            input_trigger   = Keys.KeyLeft,
            animation       = "Heavy_Overhead",
            damage          = 22,
            attack_height   = "overhead",
            startup_frames  = 11,
            active_frames   = 4,
            recovery_frames = 12,
            hitStun         = 0.38,
            cooldown_time   = 1.8,
            hitbox = { x_offset_pct = 0.45, y_offset_pct = -0.08, width_pct = 2.20, height_pct = 0.82 },
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                local stance = character.CustomData["stance"] or 0
                if stance == 0 then
                    if character.CurrentActionFrame == 10 then
                        SpawnCosmicSlashTrail(player, character, true, false)
                        PlaySound(player, { sound = "cosmic_sub_drop", pitch = 1.12, volume = 0.80 })
                        TriggerScreenShake(6, 6, 8)

                        -- Grounded: straight forward | Airborne: 45-degree downward-forward
                        local inAir = not player.IsGrounded
                        CreateProjectile(player, {
                            shader       = "cosmic_projectile",
                            x_offset_pct = 0.06,
                            y_offset_pct = inAir and 0.04 or 0.0,
                            speed_x_pct  = inAir and 0.40 or 0.50,
                            speed_y_pct  = inAir and 0.40 or 0.0,
                            width_pct    = 0.048,
                            height_pct   = 0.15,
                            damage       = 12,
                            hitStun      = 0.32,
                            color        = { 165, 65, 255 }
                        })
                    end
                else
                    -- Lightweight: Slow forward tick-damage gravity orb with pull-inside mechanic
                    if character.CurrentActionFrame == 8 then
                        SpawnCosmicSlashTrail(player, character, false, false)
                        PlaySound(player, { sound = "cosmic_recall", pitch = 0.90, volume = 0.85 })

                        local tickAcc = 0.0
                        CreateProjectile(player, {
                            shader = "cosmic_projectile",
                            x_offset_pct   = 0.07,
                            y_offset_pct   = 0.0,
                            speed_x_pct    = 0.14, -- Moves slowly forward
                            width_pct      = 0.068,
                            height_pct     = 0.085,
                            damage         = 4,
                            hitStun        = 0.22,
                            destroy_on_hit = false,
                            lifetime       = 2.6,
                            color          = { 115, 215, 255 },
                            trajectory     = function(proj, pDt)
                                tickAcc = tickAcc + pDt
                                local target = proj.Target
                                if target ~= nil then
                                    local dx = proj.PosX - target.PosX
                                    local dy = proj.PosY - target.PosY
                                    local dist = math.sqrt(dx * dx + dy * dy)
                                    -- Pull enemy inside the orb when nearby
                                    if dist > 4.0 and dist < 210.0 then
                                        local pullX = (dx / dist) * 0.28
                                        local pullY = (dy / dist) * 0.16
                                        target:SetVelocityPct(pullX, pullY)
                                    end
                                end
                                -- Re-arm hitbox every 0.22s for multi-hit tick damage
                                if tickAcc >= 0.22 then
                                    tickAcc = 0.0
                                    proj:ResetHit()
                                end
                            end
                        })
                    end
                end
            end
        },

        -- 4. COUNTER (Heavy: Long Window -> Upfling -> Downslam | Light: Behind-Target Pull Projectile)
        Anchor_Counter = {
            input_trigger   = Keys.KeyBottom,
            animation       = "Heavy_Overhead",
            damage          = 26,
            startup_frames  = 2,
            active_frames   = 24,
            recovery_frames = 10,
            hitbox          = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 1.15, height_pct = 1.05 },
            hitStun         = 0.70,
            is_counter      = true,
            cooldown_time   = 2.4,
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                local stance = character.CustomData["stance"] or 0
                if stance == 1 and character.CurrentActionFrame == 13 then
                    -- Lightweight: Spawns blockable pull projectile behind opponent
                    SpawnCosmicSlashTrail(player, character, false, false)
                    PlaySound(player, { sound = "cosmic_recall", pitch = 0.80, volume = 0.90 })

                    local opp = player.TargetOpponent
                    local proj = CreateProjectile(player, {
                        shader = "cosmic_projectile",
                        x_offset_pct  = -0.35,
                        y_offset_pct  = 0.0,
                        speed_x_pct   = -0.42, -- Flies back toward player
                        width_pct     = 0.055,
                        height_pct    = 0.075,
                        damage        = 12,
                        hitStun       = 0.85,  -- Long stun
                        is_blockbreak = false, -- Blockable
                        lifetime      = 1.4,
                        color         = { 150, 225, 255 },
                        on_hit        = function(owner, target, p)
                            local pullDir = owner.FacingRight and -1 or 1
                            ApplyHitForce(owner, target, 12, 0.85, 0.0)
                            target:SetVelocityPct(pullDir * LIGHT_PULL_VEL_X_PCT, -0.14)
                            TriggerGravityPinch(0.30, 6, 0.25, 0.5, 0.5)
                            PlaySound(owner, { sound = "cosmic_recall", pitch = 1.15, volume = 0.85 })
                        end
                    })

                    if proj ~= nil and opp ~= nil then
                        local behindOffset = player.FacingRight and 300 or -300
                        pcall(function()
                            proj.Position = NewVec2(opp.PosX + behindOffset, opp.PosY)
                        end)
                    end
                end
            end,
            on_counter = function(owner, attacker)
                -- Step 1: High Upfling (at least as high as uppercut)
                SetHitstop(7)
                TriggerImpactFrame(4, 1)
                TriggerScreenShake(0, 14, 10)
                TriggerPaletteShift(135, 25, 255, 0.70, 8, 0.40)
                PlaySound(owner, { sound = "cosmic_heavy_slash", pitch = 1.20, volume = 0.95 })

                ApplyLaunchForce(owner, attacker, 10, 0.95, 0.06, COUNTER_UPFLING_Y_PCT)

                -- Step 2: Schedule Downslam in on_passive after apex
                owner.CurrentCharacter.CustomData["counter_slam_timer"] = 0.28
            end
        },

        -- 5. STANCE SWAP (Heavy <-> Lightweight)
        Stance_Switch = {
            input_trigger   = Keys.KeyTop + Keys.KeyRight,
            animation       = "Light_Slash",
            damage          = 10,
            startup_frames  = 5,
            active_frames   = 4,
            recovery_frames = 7,
            hitStun         = 0.25,
            cooldown_time   = 0.85,
            hitbox = { x_offset_pct = 0.45, y_offset_pct = 0.05, width_pct = 1.65, height_pct = 0.55 },
            is_customlogic  = true,
            on_customlogic  = function(player, character, dt)
                if character.CurrentActionFrame == 5 then
                    local stance = character.CustomData["stance"] or 0
                    if stance == 0 then
                        SetStance(character, 1)
                        SpawnVFX(player, { preset = "Shockwave", scale = 1.1, color = { 130, 225, 255 } })
                        TriggerScreenShake(8, 0, 6)
                        PlaySound(player, { sound = "prism_chime", pitch = 1.35, volume = 0.85 })
                    else
                        SetStance(character, 0)
                        SpawnVFX(player, { preset = "Shockwave", scale = 1.3, color = { 180, 65, 255 } })
                        character:SpawnAfterimage(180, 60, 255)
                        TriggerScreenShake(10, 4, 8)
                        PlaySound(player, { sound = "cosmic_recall", pitch = 0.85, volume = 0.95 })
                    end
                end
            end
        },

        -- 6. ULTIMATE (Heavy: Armored Stab Impale + <20 HP Execute | Light: Ground Stab Repulse Sphere Tick)
        Collapse_Execution = {
            input_trigger     = Keys.KeyTop + Keys.KeyBottom,
            animation         = "Stab_Thrust",
            damage            = 38,
            attack_height     = "mid",
            startup_frames    = 8,
            active_frames     = 9,
            recovery_frames   = 16,
            is_hyperframes    = true,
            hyper_start_frame = 2,
            hyper_frames      = 14,
            is_blockbreak     = true,
            hitStun           = 0.75,
            cooldown_time     = 8.0,
            hitbox = { x_offset_pct = 0.50, y_offset_pct = 0.02, width_pct = 2.45, height_pct = 0.55 },
            is_customlogic    = true,
            on_customlogic    = function(player, character, dt)
                local stance = character.CustomData["stance"] or 0

                if stance == 0 then
                    -- HEAVY ULTIMATE: Armored forward stab dash
                    if character.CurrentActionFrame >= 4 and character.CurrentActionFrame <= 13 then
                        local dir = player.FacingRight and 1 or -1
                        player:SetVelocityPct(dir * COLLAPSE_DASH_VEL_X_PCT, 0)
                        character:SpawnAfterimage(160, 35, 255)
                    end
                    if character.CurrentActionFrame == 6 then
                        SpawnCosmicSlashTrail(player, character, true, false)
                    end
                else
                    -- LIGHTWEIGHT ULTIMATE: Stab sword into ground -> repulsive sphere pulse + for-loop tick dmg
                    if character.CurrentActionFrame == 10 then
                        PlaySound(player, {
                            sound         = "cosmic_sub_drop",
                            pitch         = 0.95,
                            volume        = 1.0,
                            duck_world    = 0.08,
                            duck_frames   = 12,
                            duck_recovery = 0.55
                        })
                        TriggerScreenShake(14, 14, 14)
                        TriggerGravityPinch(-0.55, 10, 0.40, 0.5, 0.5) -- Negative pinch = outward sphere expansion

                        local opp = player.TargetOpponent
                        for tick = 1, 5 do
                            -- Concentric sphere-like VFX rings
                            SpawnVFX(player, {
                                preset       = "Shockwave",
                                x_offset_pct = 0.02,
                                y_offset_pct = 0.08,
                                scale        = 0.8 + (tick * 0.35),
                                lifetime     = 0.22 + (tick * 0.06),
                                color        = { 120 + (tick * 20), 210, 255 }
                            })

                            if opp ~= nil then
                                local pushDir = (opp.PosX >= player.PosX) and 1 or -1
                                ApplyHitForce(player, opp, 6, 0.55, 0.0)
                                opp:SetVelocityPct(pushDir * LIGHT_ULT_PUSH_VEL_X_PCT, -0.18)
                            end
                        end
                    end
                end
            end,
            on_hit = function(owner, hit_player, skill)
                local stance = owner.CurrentCharacter.CustomData["stance"] or 0
                if stance == 1 then
                    -- Lightweight ult damage/push is handled by the ground-stab sphere pulse
                    return
                end

                SetStance(owner.CurrentCharacter, 0)

                local isExecute = (hit_player.Health <= 20)
                if isExecute then
                    ApplyHitForce(owner, hit_player, 999, 1.0, 0.0)
                    SetHitstop(14)
                    TriggerImpactFrame(8, 2)
                    TriggerScreenShake(18, 26, 22)
                    TriggerGravityPinch(0.75, 14, 0.55, 0.5, 0.5)
                    TriggerPaletteShift(145, 25, 255, 0.95, 16, 0.75)
                    PlaySound(owner, {
                        sound         = "cosmic_sub_drop",
                        pitch         = 0.65,
                        volume        = 1.0,
                        duck_world    = 0.0,
                        duck_frames   = 18,
                        duck_recovery = 0.85
                    })
                else
                    ApplyHitForce(owner, hit_player, skill.Damage, skill.HitStun, 0.26)
                    SetHitstop(8)
                    TriggerImpactFrame(5, 1)
                    TriggerScreenShake(10, 20, 16)
                    TriggerGravityPinch(0.45, 10, 0.40, 0.5, 0.5)
                    TriggerPaletteShift(145, 35, 255, 0.75, 10, 0.45)
                    PlaySound(owner, {
                        sound         = "cosmic_sub_drop",
                        pitch         = 0.85,
                        volume        = 1.0,
                        duck_world    = 0.04,
                        duck_frames   = 14,
                        duck_recovery = 0.65
                    })
                end
            end
        }
    }
}

return Character