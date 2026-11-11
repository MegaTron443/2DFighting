# 2D Fighting Game — Lua API & Scripting Reference

This document serves as the complete technical specification for creating characters, designing special moves, implementing custom physics/trajectories, and scripting combat behavior in the engine via MoonSharp.

---

## Table of Contents
1. [Architecture & MoonSharp Conventions](#architecture--moonsharp-conventions)
2. [Global Helper Functions](#global-helper-functions)
3. [Input Triggers (`Keys`)](#input-triggers-keys)
4. [Player Object API](#player-object-api)
   - [Read-Only Coordinate Scalars](#read-only-coordinate-scalars)
   - [Position Vector Assignment](#position-vector-assignment)
   - [State & Stats](#state--stats)
   - [Player Methods](#player-methods)
5. [Character Object API](#character-object-api)
   - [Dimensions & Physics (Pixels)](#dimensions--physics-pixels)
   - [Viewport Ratios (0.0 to 1.0)](#viewport-ratios-00-to-10)
   - [Resolution Calculation Trick](#resolution-calculation-trick)
   - [Combat State & Custom Variables](#combat-state--custom-variables)
6. [Projectile System](#projectile-system)
   - [Configuration Table (`CreateProjectile`)](#configuration-table-createprojectile)
   - [Built-In Trajectories (`Trajectories`)](#built-in-trajectories-trajectories)
   - [Projectile Instance Properties & Methods](#projectile-instance-properties--methods)
7. [Character Script Blueprint & Examples](#character-script-blueprint--examples)
   - [Standard Character Template](#standard-character-template)
   - [Advanced Skill Examples (Projectiles, Counters, Teleports)](#advanced-skill-examples)

---

## Architecture & MoonSharp Conventions

- **File Location**: Characters reside in `Scripts/characters/<name>.lua`. The file must return a Lua table defining `name`, `physics`, `rendering`, and `skills`.
- **Method Calls vs. Properties**:
  - Access properties with dot notation: `player.Health`, `character.Width`.
  - Invoke C# instance methods with colon syntax: `player:Translate(dx, dy)`, `proj:SetVelocity(vx, vy)`.
- **Coordinate System**:
  - The origin `(0, 0)` is the top-left corner of the screen.
  - Positive $X$ extends to the right; positive $Y$ extends downwards.
  - Negative $Y$ velocities launch or move entities upwards into the air.
- **Delta Time & Fixed Ticks**:
  - The game logic runs on a deterministic fixed physics tick (`Timer.FixedDeltaTime`).
  - Action frames (`character.CurrentActionFrame`) increment by 1 each physics tick.

---

## Global Helper Functions

These helper functions are bound directly into the Lua script's isolated global environment.

### `NewVec2(x, y)`
Creates and returns a C# `System.Numerics.Vector2` in absolute pixel coordinates.
- **Parameters**:
  - `x` *(number)*: Horizontal position in pixels.
  - `y` *(number)*: Vertical position in pixels.
- **Returns**: `Vector2`
- **Usage**:
  ```lua
  player.Position = NewVec2(400, 300)
  ```

---

### `ApplyHitForce(attacker, defender, damage, hitStunSeconds, groundKnockbackPct)`
Deals damage, triggers hit reaction, applies combo damage scaling, cancels active non-armored skills, increments the combo counter, and applies ground knockback velocity.

- **Parameters**:
  - `attacker` *(Player)*: The player dealing the hit.
  - `defender` *(Player)*: The player taking the hit.
  - `damage` *(number / int)*: Base raw damage before combo scaling.
  - `hitStunSeconds` *(number / float)*: Duration of hitstun in seconds (sets `defender.HitstunTimer`).
  - `groundKnockbackPct` *(number / float)*: Horizontal knockback velocity as a fraction of screen width per second ($0.15 = 15\%$ screen width/sec).
    - Positive values knock the target away from the attacker.
    - Negative values pull the target *toward* the attacker (e.g., vortex / vacuum moves).
- **Behavior**:
  - If the defender has active Hyper Armor (`character.IsHyperArmorActive`), damage is applied but skill interruption and knockback are ignored.
  - If grounded, horizontal velocity is applied. If airborne, the target enters air stall.
- **Usage**:
  ```lua
  ApplyHitForce(attacker, defender, 18, 0.45, 0.22)
  ```

---

### `ApplyLaunchForce(attacker, defender, damage, hitStunSeconds, launchXPct, launchYPct)`
Inflicts damage, cancels non-armored moves, and launches the target into the air (`IsGrounded = false`, `IsAirStalled = false`).

- **Parameters**:
  - `attacker` *(Player)*: The player dealing the attack.
  - `defender` *(Player)*: The player launched into the air.
  - `damage` *(number / int)*: Base raw damage before combo scaling.
  - `hitStunSeconds` *(number / float)*: Duration of hitstun in seconds.
  - `launchXPct` *(number / float)*: Horizontal launch velocity as a fraction of screen width/sec. Pushes away based on attacker facing direction.
  - `launchYPct` *(number / float)*: Vertical launch velocity as a fraction of screen height/sec. **Must be negative to launch upwards!**
- **Usage**:
  ```lua
  -- Launch upward and slightly outward:
  ApplyLaunchForce(attacker, defender, 24, 0.65, 0.12, -0.75)
  ```

---

### `CreateProjectile(owner, configTable)`
Spawns, registers with `PhysicsEngine`, and returns an active `Projectile` configured via a declarative Lua table.

- **Parameters**:
  - `owner` *(Player)*: The player spawning the projectile.
  - `configTable` *(table)*: Key-value configuration table (see [Projectile Table Config](#configuration-table-createprojectile)).
- **Returns**: `Projectile`
- **Usage**:
  ```lua
  local proj = CreateProjectile(player, {
      x_offset_pct = 0.05,
      y_offset_pct = -0.02,
      speed_x_pct  = 0.45,
      damage       = 15,
      hitStun      = 0.35
  })
  ```

---

### `create_projectile(owner, offsetPctVec2, velocityPctVec2, onHitFunc, trajectoryFunc)`
Shorthand procedural helper for spawning projectiles with direct vector parameters.

- **Parameters**:
  - `owner` *(Player)*: The owner casting the projectile.
  - `offsetPctVec2` *(Vector2)*: Spawn offset from owner as screen ratios (`NewVec2(xPct, yPct)`).
  - `velocityPctVec2` *(Vector2)*: Initial velocity as screen ratios (`NewVec2(vxPct, vyPct)`).
  - `onHitFunc` *(function or nil)*: Optional `function(owner, hit_player, proj)` callback.
  - `trajectoryFunc` *(function or nil)*: Optional `function(proj, dt)` movement callback.
- **Returns**: `Projectile`

---

## Input Triggers (`Keys`)

Input masks map hardware input checks to skill activations:

| Constant | Value | Bit | Description |
| :--- | :--- | :--- | :--- |
| `Keys.KeyTop` | `1` | `1 << 0` | Top action button (e.g., Light Attack / Special 1) |
| `Keys.KeyBottom` | `2` | `1 << 1` | Bottom action button (e.g., Heavy Attack / Special 2) |
| `Keys.KeyLeft` | `4` | `1 << 2` | Left action button (e.g., Skill 3 / Defensive) |
| `Keys.KeyRight` | `8` | `1 << 3` | Right action button (e.g., Skill 4 / Utility) |

### Combining Keys (Multi-Button Inputs)
The metatable allows using the `+` operator to combine buttons for simultaneous inputs:
```lua
input_trigger = Keys.KeyTop + Keys.KeyBottom  -- Requires both buttons to trigger
```

---

## Player Object API

The `player` reference is passed to callbacks (`attacker`, `defender`, `owner`, `hit_player`).

### Read-Only Coordinate Scalars
Because MoonSharp marshals C# structs by value, **do not read or mutate `player.Position.X` directly in Lua**. Use these scalar getters instead:
- `player.PosX` *(float)*: Current horizontal position (center $X$ in pixels).
- `player.PosY` *(float)*: Current vertical position (center $Y$ in pixels).

### Position Vector Assignment
To modify player position, always assign a complete `Vector2` via `NewVec2`:
```lua
-- Correct:
player.Position = NewVec2(player.PosX + 100, player.PosY)

-- INCORRECT (Does not update C# struct in MoonSharp):
player.Position.X = player.Position.X + 100
```

### State & Stats

| Property | Type | Description |
| :--- | :--- | :--- |
| `player.Health` | `int` | Current HP (ranges from `0` to `player.MAX_HEALTH` which is `350`). |
| `player.HitstunTimer` | `float` | Seconds remaining in hitstun (*note the lowercase 's'*). When `> 0`, player cannot act. |
| `player.InvulnerableTimer` | `float` | Seconds remaining of invulnerability (i-frames). While `> 0`, incoming attacks whiff. |
| `player.IsGrounded` | `bool` | `true` if standing firmly on the stage floor. |
| `player.IsCrouching` | `bool` | `true` if player is crouching down (shrinks hurtbox). |
| `player.IsAirStalled` | `bool` | `true` if suspended mid-air in hitstun with zeroed gravity. |
| `player.FacingRight` | `bool` | `true` if facing right; `false` if facing left. |
| `player.IsInHitstun` | `bool` | Helper indicating `player.HitstunTimer > 0`. |
| `player.IsNeutralBlocking`| `bool` | `true` if the player is in neutral/holding back and eligible to block incoming hits. |
| `player.ComboCount` | `int` | Number of consecutive hits landed in current combo. |
| `player.RoundsWon` | `int` | Number of rounds won in current match. |
| `player.TargetOpponent` | `Player` | Direct reference to the opponent `Player` instance. |
| `player.CurrentCharacter`| `Character`| Direct reference to the equipped `Character` instance. |

### Player Methods

Call player methods using colon syntax:

- `player:Translate(dxPx, dyPx)`:
  Instantly translates player position by `dxPx` and `dyPx` in pixels.
  ```lua
  -- Hop forward 80 pixels:
  local dir = player.FacingRight and 1 or -1
  player:Translate(dir * 80, 0)
  ```

- `player:SetVelocity(vxPxPerSec, vyPxPerSec)`:
  Overrides horizontal and vertical velocity in pixels per second.
  ```lua
  -- Stop horizontal movement and launch upward:
  player:SetVelocity(0, -600)
  ```

- `player:SavePreviousState()`:
  Forces `PreviousPosition = Position`. **Always call this immediately after instant teleports or position snaps** to eliminate interpolation ghosting and visual streaks.
  ```lua
  player.Position = NewVec2(targetX, targetY)
  player:SavePreviousState()
  ```

---

## Character Object API

Accessed via `character` parameter in skill callbacks or `player.CurrentCharacter`.

### Dimensions & Physics (Pixels)
Evaluated dynamically based on current screen resolution:
- `character.Width` *(float)*: Character width in pixels.
- `character.Height` *(float)*: Character height in pixels.
- `character.WalkSpeed` *(float)*: Walk speed in pixels/sec.
- `character.JumpForce` *(float)*: Jump impulse in pixels/sec (negative value).
- `character.Gravity` *(float)*: Downward gravity acceleration in pixels/sec².

### Viewport Ratios (0.0 to 1.0)
The underlying ratio values specified in the character Lua configuration:
- `character.WidthPct` *(float)*: Width as a fraction of screen width.
- `character.HeightPct` *(float)*: Height as a fraction of screen height.
- `character.WalkSpeedPct` *(float)*: Walk speed ratio.
- `character.JumpForcePct` *(float)*: Jump force ratio.
- `character.GravityPct` *(float)*: Gravity ratio.
- `character.Friction` *(float)*: Ground friction damping factor ($0.0$ to $1.0$).

### Resolution Calculation Trick
To compute the screen width and screen height dynamically without hardcoding:
```lua
local screen_w = character.Width / character.WidthPct
local screen_h = character.Height / character.HeightPct
```

### Combat State & Custom Variables
- `character.CurrentActionFrame` *(int)*:
  Current action tick counter (increments by 1 every frame while a skill is active; resets to 0 when starting a skill or returning to idle).
- `character.ActiveSkill` *(SkillData or nil)*:
  The currently executing skill object, or `nil` if idle/moving.
- `character.SkillCooldowns["skill_name"]` *(table)*:
  Dictionary mapping skill keys to remaining cooldown in seconds. Can be read or cleared directly in Lua:
  ```lua
  character.SkillCooldowns["fireball"] = 0.0 -- Instant cooldown reset
  ```
- `character.CustomData["var_name"]` *(table)*:
  Dictionary for storing persistent float variables per character instance (charges, stances, stacks):
  ```lua
  character.CustomData["charge_count"] = (character.CustomData["charge_count"] or 0) + 1
  ```

---

## Projectile System

### Configuration Table (`CreateProjectile`)

When calling `CreateProjectile(player, config)`, the config table accepts the following fields:

| Field | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `x_offset_pct` | `float` | `0.06` | Spawn horizontal offset from owner center (% of screen width). Automatically flipped when facing left. |
| `y_offset_pct` | `float` | `0.0` | Spawn vertical offset from owner center (% of screen height). |
| `speed_x_pct` | `float` | `0.45` | Horizontal velocity (% of screen width/sec). Automatically flipped when facing left. |
| `speed_y_pct` | `float` | `0.0` | Vertical velocity (% of screen height/sec). |
| `width_pct` | `float` | `0.035` | Hitbox width (% of screen width). |
| `height_pct` | `float` | `0.055` | Hitbox height (% of screen height). |
| `damage` | `int` | `12` | Base damage dealt on impact. |
| `hitStun` | `float` | `0.30` | Duration of defender hitstun on impact (seconds). |
| `knockback_pct` | `float` | `0.14` | Ground knockback ratio applied to defender on hit. |
| `lifetime` | `float` | `3.0` | Maximum lifetime in seconds before auto-destruction. |
| `attack_height` | `string`| `"mid"` | `"mid"`, `"low"`, or `"overhead"` (determines block requirements). |
| `is_blockbreak` | `bool` | `false` | If `true`, penetrates neutral blocks and bypasses block reductions. |
| `destroy_on_hit`| `bool` | `true` | If `false`, pierces through targets and remains active. |
| `color` | `table` | `{ 255, 140, 0 }` | RGBA fill color formatted as `{ R, G, B }`. |
| `trajectory` | `function` | Linear | Movement callback: `function(proj, dt)`. See [Trajectories](#built-in-trajectories-trajectories). |
| `on_hit` | `function` | `nil` | Custom callback on contact: `function(owner, hit_player, proj)`. Overrides default `ApplyHitForce`. |

---

### Built-In Trajectories (`Trajectories`)

The global `Trajectories` table provides pre-built movement algorithms:

1. **`Trajectories.Linear()`**
   Moves along constant velocity `(VelX, VelY)`.

2. **`Trajectories.SineWave(amplitudePx, frequency)`**
   Oscillates vertically in a sinusoidal wave while preserving horizontal speed.
   ```lua
   trajectory = Trajectories.SineWave(240, 12.0)
   ```

3. **`Trajectories.Gravity(gravityPxPerSec, bounceFactor)`**
   Applies downward gravity. If `bounceFactor > 0`, bounces off the stage floor; otherwise destroys upon landing.
   ```lua
   trajectory = Trajectories.Gravity(1400, 0.65)
   ```

4. **`Trajectories.Boomerang(turnAroundTime, accelPx)`**
   Decelerates horizontally, reverses direction after `turnAroundTime` seconds, and calls `proj:ResetHit()` to hit again on the return trip.
   ```lua
   trajectory = Trajectories.Boomerang(0.45, 1200)
   ```

5. **`Trajectories.Homing(turnRate)`**
   Adjusts vertical velocity toward `proj.Target.PosY` at `turnRate` rate.
   ```lua
   trajectory = Trajectories.Homing(7.5)
   ```

---

### Projectile Instance Properties & Methods

Inside custom `trajectory(proj, dt)` or `on_hit(owner, hit_player, proj)` callbacks:

#### Properties:
- `proj.PosX`, `proj.PosY` *(float, read-only)*: Center coordinates in pixels.
- `proj.VelX`, `proj.VelY` *(float, read-only)*: Current velocity in pixels/sec.
- `proj.GroundY` *(float, read-only)*: Stage floor $Y$ level in pixels.
- `proj.Width`, `proj.Height` *(float, read-only)*: Current width and height in pixels.
- `proj.Damage` *(int)*: Damage value.
- `proj.HitStun` *(float)*: Hitstun duration.
- `proj.KnockbackPct` *(float)*: Knockback ratio.
- `proj.ElapsedTime` *(float)*: Seconds elapsed since spawn.
- `proj.FacingRight` *(bool)*: Direction projectile was launched.
- `proj.Owner` *(Player)*: Player who spawned this projectile.
- `proj.Target` *(Player)*: Target opponent player.

#### Methods:
- `proj:SetVelocity(vx, vy)`: Sets new pixel velocity.
- `proj:Translate(dx, dy)`: Moves projectile by pixel delta.
- `proj:ResetHit()`: Clears `HasHit = false`, permitting multi-hit projectiles.
- `proj:Destroy()`: Immediately deactivates and removes projectile.
- `proj:SavePreviousState()`: Resets render interpolation to current position.

---

## Character Script Blueprint & Examples

### Standard Character Template

```lua
local Character = {
    name = "Brawler",

    physics = {
        width_pct      = 0.05,
        height_pct     = 0.20,
        walk_speed_pct = 0.25,
        jump_force_pct = -0.65,
        gravity_pct    = 1.40,
        friction       = 0.85
    },

    rendering = {
        body_color   = { 40, 110, 220 },
        sprite_sheet = "Assets/Characters/brawler_sheet.png",
        frame_width  = 128,
        frame_height = 128,
        draw_offset_x_pct = 0.0,
        draw_offset_y_pct = 0.0
    },

    skills = {
        -- Simple Melee Strike
        straight_punch = {
            input_trigger   = Keys.KeyTop,
            damage          = 14,
            startup_frames  = 4,
            active_frames   = 3,
            recovery_frames = 8,
            hitbox          = { x_offset_pct = 0.5, y_offset_pct = 0.2, width_pct = 0.4, height_pct = 0.2 },
            hitStun         = 0.25,
            cooldown_time   = 0.4,
            attack_height   = "mid"
        }
    }
}

return Character
```

---

### Advanced Skill Examples

#### 1. Projectile Spawner with Custom Trajectory & On-Hit
```lua
plasma_orb = {
    input_trigger   = Keys.KeyBottom,
    damage          = 0,
    startup_frames  = 6,
    active_frames   = 2,
    recovery_frames = 12,
    hitbox          = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 0.0, height_pct = 0.0 },
    hitStun         = 0.0,
    cooldown_time   = 1.2,
    is_customlogic  = true,
    on_customlogic  = function(player, character, dt)
        if character.CurrentActionFrame == 6 then
            CreateProjectile(player, {
                x_offset_pct = 0.05,
                y_offset_pct = -0.02,
                speed_x_pct  = 0.40,
                damage       = 18,
                hitStun      = 0.40,
                color        = { 0, 255, 200 },
                trajectory   = Trajectories.SineWave(180, 10.0),
                on_hit       = function(owner, hit_player, proj)
                    ApplyHitForce(owner, hit_player, proj.Damage, proj.HitStun, 0.20)
                    -- Custom: cancel air stall and pull downward slightly
                    hit_player.IsAirStalled = false
                end
            })
        end
    end
}
```

#### 2. Counter-Stance Skill (Parry / Teleport Counter)
```lua
shadow_counter = {
    input_trigger   = Keys.KeyLeft,
    damage          = 0,
    startup_frames  = 1,
    active_frames   = 14, -- Generous counter window
    recovery_frames = 16,
    hitbox          = { x_offset_pct = 0.0, y_offset_pct = 0.0, width_pct = 1.0, height_pct = 1.0 },
    hitStun         = 0.50,
    cooldown_time   = 3.0,
    is_counter      = true,
    on_counter      = function(defender, attacker)
        -- Teleport behind attacker and strike back
        local oppDir = attacker.FacingRight and 1 or -1
        defender.Position = NewVec2(attacker.PosX - (oppDir * 80), attacker.PosY)
        defender.FacingRight = attacker.FacingRight
        defender:SavePreviousState()

        -- Deliver counter-attack launch
        ApplyLaunchForce(defender, attacker, 26, 0.80, 0.15, -0.70)
    end
}
```

#### 3. Air-Crush / Overhead Strike with Hyper Armor
```lua
hyper_slam = {
    input_trigger     = Keys.KeyRight,
    damage            = 28,
    attack_height     = "overhead",
    startup_frames    = 12,
    active_frames     = 4,
    recovery_frames   = 18,
    hitbox            = { x_offset_pct = 0.4, y_offset_pct = 0.1, width_pct = 0.5, height_pct = 0.6 },
    hitStun           = 0.60,
    cooldown_time     = 2.5,
    is_hyperframes    = true,
    hyper_start_frame = 3,
    hyper_frames      = 11, -- Immune to hitstun during startup frames 3 through 14
    is_blockbreak     = true
}
```
