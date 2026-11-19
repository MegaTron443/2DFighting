# 2D Fighting Game — Lua API & Character Scripting Reference

This document is the authoritative technical reference for authoring characters, compound animated rigs, custom shaders, procedural audio triggers, post-processing effects, projectiles, and combat skills in `2DFighting` via MoonSharp Lua.

---

## Table of Contents
1. [Architecture & MoonSharp Conventions](#architecture--moonsharp-conventions)
2. [Global Helper Functions](#global-helper-functions)
   - [Core Math, Physics & Combat](#core-math-physics--combat)
   - [Projectiles](#projectiles)
   - [Audio & Bus Mixing](#audio--bus-mixing)
   - [Post-Processing & Camera FX](#post-processing--camera-fx)
   - [Visual Effects (`SpawnVFX`)](#visual-effects-spawnvfx)
3. [Input Triggers (`Keys`)](#input-triggers-keys)
4. [Player Object API](#player-object-api)
5. [Character Object API](#character-object-api)
   - [Physics & Stance-Mutable Viewport Ratios](#physics--stance-mutable-viewport-ratios)
   - [Combat State, Cooldowns & Custom Data](#combat-state-cooldowns--custom-data)
   - [Compound Sprite Parts, Shaders & Afterimages](#compound-sprite-parts-shaders--afterimages)
6. [SkillData Object API](#skilldata-object-api)
7. [Projectile System](#projectile-system)
8. [Character Lua Table Schema](#character-lua-table-schema)
9. [Real Character Implementation Examples](#real-character-implementation-examples)

---

## Architecture & MoonSharp Conventions

- **Script Discovery & Modding**:
  - Vanilla character scripts live in `Scripts/characters/<id>.lua` (and legacy `Scripts/lua/`).
  - Mods placed under `Mods/<ModName>/` or `Mods/<ModName>.zip` override scripts, textures, shaders, and `.wav` assets automatically via `ModManager` (in order of mod priority over `BaseDir`).
  - Each script executes in an isolated Lua environment (`__index` linked to `Script.Globals`), so local helper functions and variables inside one character file never collide with another fighter.
- **Dot vs. Colon Syntax**:
  - Read or assign properties with `.` (`player.Health`, `character.WalkSpeedPct = 0.33`).
  - Call C# instance methods with `:` (`player:SetVelocity(0, -600)`, `character:SetPartVisible("sword_heavy", false)`).
- **Coordinate System & Resolution Independence**:
  - Origin `(0, 0)` is top-left; $+X$ is right, $+Y$ is down.
  - Negative $Y$ velocities move or launch entities upward.
  - Physics, hitboxes, UI, and projectiles use viewport percentage ratios (`_pct`, where `1.0` equals 100% of screen width or height) so characters scale cleanly across all resolutions (`800x600` up to `1920x1080`).
- **Fixed Simulation Tick**:
  - Simulation runs deterministically at 60 Hz (`Timer.FixedDeltaTime = 1 / 60`).
  - `character.CurrentActionFrame` increments by `1` per physics tick while a skill is active.
  - During hitstop (`SetHitstop`), `PhysicsEngine` freezes entity ticks and plays camera shake / impact frames.

---

## Global Helper Functions

### Core Math, Physics & Combat

#### `NewVec2(x, y)`
Creates and returns a C# `System.Numerics.Vector2` in pixel coordinates (or ratio vectors for shorthand projectile helpers).
```lua
player.Position = NewVec2(attacker.PosX - 110, attacker.PosY)
```

#### `ApplyHitForce(attacker, defender, damage, hitStunSeconds, groundKnockbackPct)`
Applies combo-scaled damage (`MinComboScaling = 0.40`, `-12%` per combo hit), cancels non-armored active skills, sets `defender.HitstunTimer`, increments `attacker.ComboCount`, and applies horizontal velocity (`groundKnockbackPct` of screen width/sec).
- Positive `groundKnockbackPct` knocks away from `attacker`; negative values pull `defender` toward `attacker`.
- If `defender` has active Hyper Armor (`IsHyperArmorActive`), damage still applies, while skill interruption and knockback are ignored.
- If `defender` is airborne (`IsGrounded == false`), `defender` enters air stall (`IsAirStalled = true`, velocity zeroed).
```lua
-- Vacuum pull toward attacker:
ApplyHitForce(attacker, defender, skill.Damage, skill.HitStun, -0.18)
```

#### `ApplyLaunchForce(attacker, defender, damage, hitStunSeconds, launchXPct, launchYPct)`
Applies combo-scaled damage, cancels non-armored skills, sets `IsGrounded = false` and `IsAirStalled = false`, and launches `defender` with velocity `(dir * launchXPct * screenWidth, launchYPct * screenHeight)`. Use negative `launchYPct` to launch upward.
```lua
ApplyLaunchForce(attacker, defender, skill.Damage, skill.HitStun, 0.08, -0.82)
```

#### `SetHitstop(frames)`
Extends global combat hitstop (`PhysicsEngine.HitstopTimer`) to at least `frames` simulation frames (`frames * Timer.FixedDeltaTime`).
```lua
SetHitstop(8) -- Freezes simulation impact for 8 frames (approx. 133 ms)
```

#### `Log(value)`
Logs an informational message through `DiagnosticsLogger` (unique per call in debug builds).

---

### Projectiles

#### `CreateProjectile(owner, configTable)`
Spawns and registers a configurable `Projectile` into `ProjectileManager` and returns the `Projectile` instance. See [Projectile Table Keys](#configuration-table-createprojectile).

#### `create_projectile(owner, offsetPctVec2, velocityPctVec2[, onHitFunc, trajectoryFunc])`
Shorthand projectile spawner taking percentage `NewVec2` offset and velocity vectors with optional callbacks:
```lua
create_projectile(
    player,
    NewVec2(0.06, -0.02),
    NewVec2(0.58, 0.0),
    function(owner, target, proj)
        ApplyHitForce(owner, target, 14, 0.45, 0.12)
    end,
    Trajectories.Linear()
)
```

---

### Audio & Bus Mixing

Audio calls are automatically muted/skipped when `PhysicsEngine.IsTrainingMode` is `true` so turbo RL training runs silently at full speed.

#### `PlaySound(playerOrNil, soundNameOrConfig)`
Plays a sound from (in lookup priority):
1. **Built-in Procedural Synth Bank**: `"default"`, `"cosmic_sub_drop"`, `"cosmic_heavy_slash"`, `"cosmic_light_slash"`, `"cosmic_recall"`, `"prism_chime"`.
2. **General `SoundType` Enum**: `"Hurt"`, `"Block"`, `"BlockBreak"`, `"OnCounter"`, `"Death"`, `"RoundStart"`, `"CustomOnHit"`.
3. **Character / Shared Skill Audio**: `Assets/Sounds/<CharacterName>/Skill_<skillName>.wav` (or `<skillName>.wav`, plus `_*.wav` variants and `Shared/` fallbacks).

Accepts either a plain string or a configuration table:
```lua
-- Simple string:
PlaySound(player, "cosmic_recall")

-- Full config table with pitch, volume, bus routing, and automatic audio ducking:
PlaySound(player, {
    sound         = "cosmic_sub_drop", -- or key "name"
    bus           = "sfx_character",   -- "bgm", "sfx_world", or "sfx_character" (default)
    pitch         = 0.80,              -- clamped to [0.2, 3.0]
    volume        = 1.0,               -- multiplier on bus effective volume
    duck_world    = 0.04,              -- optional: triggers TriggerAudioDuck
    duck_frames   = 14,                -- hold frames for ducking (default 10)
    duck_recovery = 0.65               -- recovery fade duration in seconds (default 0.6)
})
```

#### `TriggerAudioDuck(targetGain, holdFrames, recoverySeconds)`
Ducks `"bgm"` and `"sfx_world"` buses down to `targetGain` (`0.0..1.0`) for `holdFrames` frames, then recovers smoothly over `recoverySeconds`.

#### `SetAudioBusVolume(busName, volume)`
Sets base volume (`0.0..1.0`) for `"bgm"`, `"sfx_world"`, or `"sfx_character"`.

#### `SetAudioBusLowPass(busName, cutoffHz)`
Approximates a low-pass muffle on the given bus (`60..20000` Hz).

---

### Post-Processing & Camera FX

Post-processing passes automatically skip in `IsTrainingMode`. Built-in post-process shader ID: `"postfx_cosmic"`.

| Function | Parameters | Description |
| :--- | :--- | :--- |
| `TriggerScreenShake(xPx, yPx, frames)` | `xPixels`, `yPixels`, `durationFrames = 10` | Directional camera shake + proportional RGB chromatic aberration split. |
| `TriggerImpactFrame(frames, mode)` | `frameCount = 5`, `mode = 1` | `mode = 1`: high-contrast 2-tone monochrome flash; `mode = 2`: inverted cosmic void. |
| `TriggerPaletteShift(r, g, b, intensity, holdFrames, fadeSec)` | RGB (`0..255` or `0..1`), `intensity = 0.85`, `holdFrames = 10`, `fadeOutSeconds = 0.45` | Shifts screen luminance toward the specified tint palette and fades out. |
| `TriggerGravityPinch(amount, holdFrames, fadeSec, cx, cy)` | `pinchAmount = 0.45` (`-0.8..0.85`), `holdFrames = 8`, `fadeOutSeconds = 0.35`, `centerX = 0.5`, `centerY = 0.5` | Radial gravitational lens warp centered at normalized UV `(cx, cy)`. |
| `EnablePostFX(shaderId, priority)` | `shaderId`, `priority = 0` | Enables a custom fragment shader pass (`Assets/Shaders/<id>.fs` or built-in). |
| `DisablePostFX(shaderId)` | `shaderId` | Removes an active post-processing pass. |
| `SetPostFXFloat(shaderId, uniform, val)` | `shaderId`, `uniformName`, `float` | Sets a float uniform on an active pass (auto-enables pass if inactive). |
| `SetPostFXVec2(shaderId, uniform, x, y)` | `shaderId`, `uniformName`, `x`, `y` | Sets a `vec2` uniform on an active pass. |
| `SetPostFXColor(shaderId, uniform, r, g, b)` | `shaderId`, `uniformName`, `r`, `g`, `b` | Sets a `vec3` RGB color uniform (accepts `0..255` or `0..1`). |

---

### Visual Effects (`SpawnVFX`)

#### `SpawnVFX(owner, configTable)`
Spawns a transient sprite-sheet or procedural vector effect.
- **Presets**: `"HitSpark"`, `"HeavyHitSpark"`, `"BlockSpark"`, `"BlockBreak"`, `"CounterFlash"`, `"Shockwave"`, `"Explosion"`, `"Dust"`, `"HealGlow"`, `"Afterimage"`.
- **Config Keys**:
  - `preset` (or `type`) *(string)*: Preset name (default `"Shockwave"`).
  - `spawn_at_target` *(bool)*: If `true`, anchors at `owner.TargetOpponent` instead of `owner`.
  - `x_offset_pct`, `y_offset_pct` *(float)*: Offset from anchor in screen ratios (X flipped by facing).
  - `scale` *(float)*: Radius scale multiplier (default `1.0`).
  - `lifetime` *(float)*: Custom duration in seconds (`> 0` overrides preset default).
  - `color` *(table)*: `{ R, G, B }` (`0..255`).
  - `sprite` *(string)*: Optional override name under `Assets/VFX/<name>.png`, with `frame_width` and `frame_height` (default `128`).

```lua
SpawnVFX(player, {
    preset = "Explosion",
    spawn_at_target = true,
    scale = 1.4,
    color = { 160, 60, 255 }
})
```

---

## Input Triggers (`Keys`)

| Constant | Bitmask | Value | Description |
| :--- | :--- | :--- | :--- |
| `Keys.KeyTop` | `1 << 0` | `1` | Top action key |
| `Keys.KeyBottom` | `1 << 1` | `2` | Bottom action key |
| `Keys.KeyLeft` | `1 << 2` | `4` | Left action key |
| `Keys.KeyRight` | `1 << 3` | `8` | Right action key |

Combine with `+` for multi-button chords (e.g. `Keys.KeyTop + Keys.KeyBottom`, or 3-button ultimates `Keys.KeyTop + Keys.KeyBottom + Keys.KeyRight`). The input buffer resolves multi-button chords within a 3–5 frame plink window, prioritizing the highest bit-count match.

---

## Player Object API

Passed to callbacks as `player`, `owner`, `attacker`, `defender`, or `hit_player`.

### Position Handling
- **Read-Only Scalars**: Use `player.PosX` and `player.PosY` (pixel center coordinates).
- **Mutating Position**: MoonSharp copies C# structs by value, so assign a full `Vector2`:
  ```lua
  player.Position = NewVec2(attacker.PosX - 110, attacker.PosY)
  player:SavePreviousState() -- Prevents interpolation streaks on teleport
  ```

### Properties

| Property | Type | Access | Description |
| :--- | :--- | :--- | :--- |
| `player.Name` | `string` | Read | Display name (`"Player 1"`, `"Player 2"`). |
| `player.Health` | `int` | R/W | Current HP (`0` to `player.MAX_HEALTH` = `350`). |
| `player.MAX_HEALTH` | `int` | Read | Constant `350`. |
| `player.HitstunTimer` | `float` | R/W | Remaining hitstun in seconds (*lowercase `s`*). |
| `player.InvulnerableTimer` | `float` | R/W | Remaining i-frames in seconds. |
| `player.TechPopupTimer` | `float` | R/W | Remaining `"TECH!"` indicator timer. |
| `player.IsGrounded` | `bool` | R/W | `true` when standing on `PhysicsEngine.GroundY`. |
| `player.IsCrouching` | `bool` | R/W | `true` when crouching (shrinks hurtbox to `65%` height). |
| `player.IsAirStalled` | `bool` | R/W | `true` when suspended mid-air in hitstun with zero gravity. |
| `player.IsHoldingBack` | `bool` | R/W | `true` when holding away from facing direction. |
| `player.FacingRight` | `bool` | R/W | Facing direction (`true` = right). |
| `player.IsInHitstun` | `bool` | Read | `HitstunTimer > 0`. |
| `player.IsStunned` | `bool` | Read | Alias for `IsInHitstun`. |
| `player.IsInvulnerable` | `bool` | Read | `InvulnerableTimer > 0`. |
| `player.IsNeutralBlocking` | `bool` | Read | Eligible to block (`IsGrounded`, idle/back/crouch, no active skill, not stunned). |
| `player.IsAttacking` | `bool` | Read | `CurrentCharacter.ActiveSkill ~= nil`. |
| `player.IsAttackingLow` | `bool` | Read | Active skill has `attack_height = "low"`. |
| `player.InRecoveryFrames` | `bool` | Read | `CurrentActionFrame >= StartupFrames + ActiveFrames`. |
| `player.CanTech` | `bool` | Read | Airborne in hitstun with `< 0.15s` remaining. |
| `player.ComboCount` | `int` | R/W | Current consecutive hit count. |
| `player.RoundsWon` | `int` | R/W | Match rounds won. |
| `player.TargetOpponent` | `Player` | Read | Reference to the opposing `Player`. |
| `player.CurrentCharacter` | `Character` | Read | Reference to the equipped `Character`. |

### Methods (`:`)
- `player:Translate(dxPx, dyPx)` — Moves `Position` by pixel delta.
- `player:SetVelocity(vxPxPerSec, vyPxPerSec)` — Sets `Velocity` in pixels/sec.
- `player:SavePreviousState()` — Sets `PreviousPosition = Position` (call after teleports).

---

## Character Object API

Passed as `character` in `on_passive` and `on_customlogic`, or accessed via `player.CurrentCharacter`.

### Physics & Stance-Mutable Viewport Ratios

| Property | Type | Access | Description |
| :--- | :--- | :--- | :--- |
| `character.Name` | `string` | Read | Character name from Lua table. |
| `character.Width` / `Height` | `float` | Read | Computed dimensions in pixels (`screen * Pct`). |
| `character.WalkSpeed` | `float` | Read | Computed horizontal walk speed in pixels/sec. |
| `character.JumpForce` | `float` | Read | Computed upward jump velocity in pixels/sec (negative). |
| `character.Gravity` | `float` | Read | Computed gravity in pixels/sec². |
| `character.WidthPct` | `float` | R/W | Hurtbox width ratio (`0.0..1.0`). |
| `character.HeightPct` | `float` | R/W | Hurtbox height ratio (`0.0..1.0`). |
| `character.WalkSpeedPct` | `float` | R/W | Walk speed ratio (mutable at runtime for stance switching!). |
| `character.JumpForcePct` | `float` | R/W | Jump impulse ratio. |
| `character.GravityPct` | `float` | R/W | Gravity ratio (mutable at runtime for weight/stance shifts!). |
| `character.Friction` | `float` | R/W | Ground friction damping multiplier. |

> **Screen Resolution Derivation:**
> ```lua
> local screen_w = character.Width / character.WidthPct
> local screen_h = character.Height / character.HeightPct
> ```

### Combat State, Cooldowns & Custom Data

| Property | Type | Description |
| :--- | :--- | :--- |
| `character.ActiveSkill` | `SkillData` or `nil` | Currently executing `SkillData`, or `nil` when idle. |
| `character.CurrentActionFrame` | `int` | Tick counter (`0, 1, 2...`) while `ActiveSkill` is active. |
| `character.HasHit` | `bool` | `true` once the current skill connects. |
| `character.IsHitboxActive` | `bool` | `true` during `[StartupFrames, StartupFrames + ActiveFrames)`. |
| `character.IsHyperArmorActive` | `bool` | `true` during `[HyperStartFrame, HyperStartFrame + HyperFrames)`. |
| `character.Skills` | `Dictionary<string, SkillData>` | All parsed skills keyed by skill table name. |
| `character.SkillCooldowns["name"]` | `Dictionary<string, float>` | Remaining cooldown in seconds per skill; write `0` to reset cooldowns immediately. |
| `character.CustomData["key"]` | `Dictionary<string, float>` | Per-character persistent `float` storage (e.g., stances, aura IDs, stacks, timers). |

### Compound Sprite Parts, Shaders & Afterimages

- `character:SetPartVisible(partName, visible)` — Toggles visibility of a named part in `rendering.parts`.
- `character:IsPartVisible(partName)` *(bool)* — Returns whether a part is visible.
- `character:SetPartOffset(partName, offsetXPct, offsetYPct, rotationDeg = 0)` — Dynamically offsets and rotates a `SpritePart` relative to character `Width`/`Height`.
- `character:SetPartShaderFloat(partName, uniform, val)` — Sets a float uniform on a single part's `RenderMaterial`.
- `character:SetPartShaderColor(partName, uniform, r, g, b)` — Sets a color (`vec3`) uniform (`0..255` or `0..1`) on a part (e.g. `"u_PrimaryColor"`, `"u_SecondaryColor"`, `"u_GlowColor"`).
- `character:SetShaderColor(uniform, r, g, b)` — Sets a color uniform on `BaseMaterial` and all parts.
- `character:SetShaderFloat(uniform, val)` — Sets a float uniform on `BaseMaterial` and all parts.
- `character:SpawnAfterimage(r = 165, g = 50, b = 255)` — Emits a tinted ghost snapshot into the 6-slot afterimage ring buffer.

**Built-in Sprite Shader (`"sprite_lut"`) Uniforms**:
- `u_PrimaryColor` *(vec3)*: Mid-tone / primary color tint.
- `u_SecondaryColor` *(vec3)*: Shadow / dark band tint.
- `u_GlowColor` *(vec3)*: Bright energy highlight tint (`luma >= 0.88`).
- `u_PortalMode` *(float)*: When `> 0.5`, renders a screen-space scrolling cosmic starfield inside bright pixels.
- `u_Time` *(float)* & `u_Resolution` *(vec2)*: Bound automatically every draw call by `RenderMaterial`.

---

## SkillData Object API

Passed as `skill` to `on_hit(attacker, defender, skill)`.

| Property | Type | Description |
| :--- | :--- | :--- |
| `skill.Name` | `string` | Skill key name from `skills = { ... }`. |
| `skill.AnimationClipName` | `string?` | Animation clip name (`animation` field or defaults to `Name`). |
| `skill.TriggerMask` | `int` | Resolved integer bitmask from `input_trigger`. |
| `skill.Damage` | `int` | Base damage. |
| `skill.Height` | `AttackHeight` | `AttackHeight.Mid`, `AttackHeight.Low`, or `AttackHeight.Overhead`. |
| `skill.StartupFrames` | `int` | Startup ticks before hitbox activates. |
| `skill.ActiveFrames` | `int` | Active hitbox ticks. |
| `skill.RecoveryFrames` | `int` | Recovery ticks after active frames. |
| `skill.HitStun` | `float` | Hitstun duration in seconds. |
| `skill.CooldownTime` | `float` | Cooldown in seconds. |
| `skill.IsBlockBreak` | `bool` | Unblockable flag. |
| `skill.IsCounter` | `bool` | Counter/parry stance flag. |
| `skill.IsHyperFrames` | `bool` | Hyper armor enabled flag. |
| `skill.HyperStartFrame` | `int` | Frame hyper armor begins. |
| `skill.HyperFrames` | `int` | Duration of hyper armor in frames. |

---

## Projectile System

### Configuration Table (`CreateProjectile`)

| Key | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `x_offset_pct` | `float` | `0.06` | Spawn horizontal offset from owner (% screen width; flipped by facing). |
| `y_offset_pct` | `float` | `0.0` | Spawn vertical offset from owner (% screen height). |
| `speed_x_pct` | `float` | `0.45` | Initial horizontal speed (% screen width/sec; flipped by facing). |
| `speed_y_pct` | `float` | `0.0` | Initial vertical speed (% screen height/sec). |
| `width_pct` | `float` | `0.035` | Hitbox width (% screen width). |
| `height_pct` | `float` | `0.055` | Hitbox height (% screen height). |
| `damage` | `int` | `12` | Base damage. |
| `hitStun` | `float` | `0.30` | Hitstun seconds on impact. |
| `knockback_pct` | `float` | `0.14` | Ground knockback ratio on impact. |
| `lifetime` | `float` | `3.0` | Max duration in seconds before expiration. |
| `attack_height` | `string` | `"mid"` | `"mid"`, `"low"`, or `"overhead"`. |
| `is_blockbreak` | `bool` | `false` | If `true`, breaks neutral guard. |
| `destroy_on_hit` | `bool` | `true` | If `false`, pierces targets and remains active. |
| `color` | `table` | `{ 255, 140, 0 }` | Fill color `{ R, G, B }`. |
| `trajectory` | `function` | Linear | `function(proj, dt)` per-tick movement callback. |
| `on_hit` | `function` | `nil` | `function(owner, hit_player, proj)` custom hit callback (overrides default `ApplyHitForce`). |

### Built-In `Trajectories`

- `Trajectories.Linear()` — Constant velocity.
- `Trajectories.SineWave(amplitudePx, frequency)` — Vertical cosine oscillation around horizontal path.
- `Trajectories.Gravity(gravityPxPerSec, bounceFactor)` — Downward gravity; bounces if `bounceFactor > 0`, otherwise destroys on ground contact.
- `Trajectories.Boomerang(turnAroundTime, accelPx)` — Reverses horizontally and calls `proj:ResetHit()` after `turnAroundTime` seconds.
- `Trajectories.Homing(turnRate)` — Steers vertical velocity toward `proj.Target.PosY`.

### Projectile Instance Properties & Methods (`proj`)

- **Properties**: `PosX`, `PosY`, `VelX`, `VelY`, `GroundY`, `Width`, `Height`, `Damage`, `HitStun`, `KnockbackPct`, `ElapsedTime`, `Lifetime`, `DestroyOnHit`, `IsActive`, `HasHit`, `FacingRight`, `IsBlockBreak`, `Owner` (`Player`), `Target` (`Player`), `Position` (writable via `NewVec2`).
- **Methods**: `proj:SetVelocity(vx, vy)`, `proj:Translate(dx, dy)`, `proj:ResetHit()`, `proj:Destroy()`, `proj:SavePreviousState()`.

---

## Character Lua Table Schema

```lua
local Character = {
    name = "FighterName",

    physics = {
        width_pct      = 0.05,
        height_pct     = 0.20,
        walk_speed_pct = 0.25,
        jump_force_pct = -0.65,
        gravity_pct    = 1.40,
        friction       = 0.85
    },

    rendering = {
        sprite_sheet      = "Assets/Characters/my_sheet.png", -- Optional; supports fallback boxes + compound parts
        frame_width       = 128,
        frame_height      = 128,
        draw_offset_x_pct = 0.0,
        draw_offset_y_pct = 0.0,
        body_color        = { 45, 38, 65 },
        shader            = "sprite_lut", -- Optional base shader

        -- Optional Z-ordered compound layers & socketed props:
        parts = {
            body = {
                z_order      = 1,
                shader       = "sprite_lut",
                width_pct    = 1.0,
                height_pct   = 0.78,
                y_offset_pct = 0.11,
                color        = { 55, 48, 80 }
            },
            weapon = {
                z_order      = 2,
                shader       = "sprite_lut",
                is_socket    = true,  -- Follows weapon_x / weapon_y / weapon_angle keyframes
                additive     = true,  -- Additive blending
                visible      = true,
                x_offset_pct = 0.65,
                y_offset_pct = -0.05,
                width_pct    = 0.25,
                height_pct   = 1.05,
                rotation     = -25,
                color        = { 175, 60, 255 }
            }
        }
    },

    animations = {
        -- Standard strip clips:
        idle = { start_frame = 0, frame_count = 4, speed = 8.0 },
        walk = { start_frame = 4, frame_count = 6, speed = 10.0 },

        -- Tick-accurate keyframe clips with weapon socket poses & afterimages:
        Heavy_Slash = {
            keyframes = {
                { frame = 10, duration = 8, afterimage = false, weapon_x = -0.35, weapon_y = -0.25, weapon_angle = -75 },
                { frame = 11, duration = 2, afterimage = true,  weapon_x = 0.90,  weapon_y = -0.05, weapon_angle = 45  }
            }
        }
    },

    on_passive = function(player, character, dt)
        -- Runs every fixed physics tick
    end,

    skills = {
        my_skill = {
            input_trigger     = Keys.KeyTop,
            animation         = "Heavy_Slash", -- Defaults to skill key name if omitted
            damage            = 18,
            attack_height     = "mid",         -- "mid", "low", or "overhead"
            startup_frames    = 6,
            active_frames     = 4,
            recovery_frames   = 10,
            hitbox            = { x_offset_pct = 0.70, y_offset_pct = 0.10, width_pct = 1.10, height_pct = 0.45 },
            hitStun           = 0.35,
            cooldown_time     = 1.2,
            is_hyperframes    = false,
            hyper_start_frame = 0,
            hyper_frames      = 0,
            is_blockbreak     = false,
            on_blockbreak     = function(attacker, defender) end,
            is_counter        = false,
            on_counter        = function(defender, attacker) end,
            is_customlogic    = false,
            on_customlogic    = function(player, character, dt) end,
            on_hit            = function(attacker, defender, skill) end
        }
    }
}

return Character
```

---

## Real Character Implementation Examples

The snippets below come directly from the roster scripts in `Scripts/characters/`.

### 1. Compound Parts, Stance Switching & Cinematic Execution (`gravity.lua`)
Demonstrates dynamic `CustomData` stance toggling, mutable `WalkSpeedPct`/`GravityPct`, `SetPartVisible`, `SetPartShaderColor`, audio ducking, hitstop, impact frames, palette shifts, and gravity pinch warps:

```lua
local function SetStance(character, stance)
    character.CustomData["stance"] = stance
    if stance == 0 then
        -- FUSED STANCE: Heavy odachi
        character.WalkSpeedPct = 0.21
        character.GravityPct   = 1.48
        character:SetPartVisible("sword_heavy", true)
        character:SetPartShaderColor("sword_core", "u_GlowColor", 160, 50, 255)
    else
        -- SPLIT STANCE: Lightweight agile core
        character.WalkSpeedPct = 0.33
        character.GravityPct   = 1.05
        character:SetPartVisible("sword_heavy", false)
        character:SetPartShaderColor("sword_core", "u_GlowColor", 220, 120, 255)
    end
end

-- Ultimate dash impale with Hyper Armor + <20 HP cinematic execute:
Collapse_Execution = {
    input_trigger     = Keys.KeyTop + Keys.KeyBottom,
    animation         = "Heavy_Slash",
    damage            = 38,
    startup_frames    = 8,
    active_frames     = 8,
    recovery_frames   = 16,
    is_hyperframes    = true,
    hyper_start_frame = 2,
    hyper_frames      = 12,
    is_blockbreak     = true,
    hitStun           = 0.75,
    cooldown_time     = 8.0,
    hitbox            = { x_offset_pct = 0.65, y_offset_pct = 0.05, width_pct = 1.35, height_pct = 0.65 },
    is_customlogic    = true,
    on_customlogic    = function(player, character, dt)
        if character.CurrentActionFrame >= 4 and character.CurrentActionFrame <= 12 then
            local dir = player.FacingRight and 1 or -1
            player:SetVelocity(dir * 1150, 0)
            character:SpawnAfterimage(160, 35, 255)
        end
    end,
    on_hit = function(owner, hit_player, skill)
        SetStance(owner.CurrentCharacter, 0)
        if hit_player.Health <= 20 then
            ApplyHitForce(owner, hit_player, 999, 1.0, 0.0)
            SetHitstop(14)
            TriggerImpactFrame(8, 2)
            TriggerScreenShake(18, 26, 22)
            TriggerGravityPinch(0.75, 14, 0.55, 0.5, 0.5)
            TriggerPaletteShift(145, 25, 255, 0.95, 16, 0.75)
            PlaySound(owner, {
                sound = "cosmic_sub_drop",
                pitch = 0.65,
                volume = 1.0,
                duck_world = 0.0,
                duck_frames = 18,
                duck_recovery = 0.85
            })
        end
    end
}
```

### 2. Dynamic Grayscale LUT Aura & Passive Socket Animation (`priest.lua`)
Demonstrates oscillating a halo prop in `on_passive` with `SetPartOffset` and dynamically recoloring parts on skill start:

```lua
on_passive = function(player, character, dt)
    local t = (character.CustomData["t"] or 0) + dt
    character.CustomData["t"] = t
    local bobY = -0.68 + math.sin(t * 4.2) * 0.04
    local tilt = math.sin(t * 2.5) * 8.0
    character:SetPartOffset("halo", 0.0, bobY, tilt)
end
```

### 3. Returning Boomerang & Teleport Counter (`phantom.lua`)
Demonstrates a piercing boomerang projectile that pulls the defender on its return trip, and a parry counter that teleports behind the attacker:

```lua
shadow_chakram = {
    input_trigger = Keys.KeyTop,
    damage = 9,
    startup_frames = 6, active_frames = 2, recovery_frames = 11,
    hitbox = { x_offset_pct = 0.65, y_offset_pct = 0.1, width_pct = 0.5, height_pct = 0.25 },
    hitStun = 0.28, cooldown_time = 1.4,
    is_customlogic = true,
    on_customlogic = function(player, character, dt)
        if character.CurrentActionFrame == 6 then
            CreateProjectile(player, {
                x_offset_pct = 0.05, y_offset_pct = -0.01, speed_x_pct = 0.62,
                width_pct = 0.032, height_pct = 0.035,
                damage = 10, hitStun = 0.32, lifetime = 1.5,
                destroy_on_hit = false,
                color = { 180, 80, 255 },
                trajectory = Trajectories.Boomerang(0.55, 1950),
                on_hit = function(owner, target, proj)
                    local pull = (proj.ElapsedTime > 0.55) and -0.10 or 0.08
                    ApplyHitForce(owner, target, proj.Damage, proj.HitStun, pull)
                end
            })
        end
    end
},

substitution_jutsu = {
    input_trigger = Keys.KeyLeft,
    damage = 18,
    startup_frames = 2, active_frames = 10, recovery_frames = 14,
    hitbox = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 1.1, height_pct = 1.0 },
    hitStun = 0.80, cooldown_time = 3.0,
    is_counter = true,
    on_counter = function(defender, attacker)
        local behindOffset = attacker.FacingRight and -110 or 110
        defender.Position = NewVec2(attacker.PosX + behindOffset, attacker.PosY)
        defender.FacingRight = attacker.FacingRight
        defender:SavePreviousState()
        ApplyLaunchForce(defender, attacker, 20, 0.85, 0.05, -0.68)
    end
}
```

### 4. Cooldown Resets & Layered Mixup Waves (`chronomancer.lua`)
Demonstrates directly resetting `SkillCooldowns` in Lua and spawning simultaneous low + overhead homing projectiles:

```lua
overclock_surge = {
    input_trigger = Keys.KeyTop + Keys.KeyBottom,
    damage = 22,
    startup_frames = 6, active_frames = 5, recovery_frames = 12,
    hitbox = { x_offset_pct = -0.20, y_offset_pct = -0.10, width_pct = 1.40, height_pct = 1.10 },
    hitStun = 0.65, cooldown_time = 5.5,
    is_blockbreak = true, is_customlogic = true,
    on_customlogic = function(player, character, dt)
        if character.CurrentActionFrame == 6 then
            player.Health = math.max(1, player.Health - 12)
            character.SkillCooldowns["chrono_bolt"] = 0
            character.SkillCooldowns["stasis_mine"] = 0
            character.SkillCooldowns["time_skip_strike"] = 0
        end
    end
}
```
