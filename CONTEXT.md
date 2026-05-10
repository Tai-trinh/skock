# Skock — Space Fleet Auto-Battler

Skock is a roguelite where you command a fleet hyperspace-jumping from location to location to scavenge and survive — a homage to Gallforce and Homeworld. Each run consists of 8 encounters against opponent fleets. Battles are fully deterministic: replaying a battle with the same seed and fleet snapshots produces byte-identical results on any machine, enabling local replays and server-side anti-cheat verification. The multiplayer layer is simple: retrieve an opponent's fleet from the server and auto-battle it locally.

## Core constraints

- Top-down 2D; 3D engine/textures used as sprites for easy rotation
- 30 ticks/sec logical simulation; renderer interpolates between ticks
- All battles end within 120 seconds
- Fully deterministic given the seed
- Single-threaded simulation
- Anime art style (Macross, Gallforce)
- Target session length: 15–30 minutes

## The Mothership

The Mothership is the player's one permanent ship and the heart of the fleet. It is a combat entity — it appears on the battlefield with beam weapons and point defense. Destroying the enemy Mothership wins the battle; losing your own ends the run immediately regardless of remaining fleet.

Fleet size is limited by the Mothership's **hangar capacity** (a tonnage value). Each ship has a tonnage cost; heavier ships cost more. Lore: the entire fleet must physically dock inside the Mothership to survive hyperspace jumps. Upgrading hangar capacity (costs `Tech`) is a primary progression axis.

## Game loop

A run consists of 8 jumps. Lose 3 battles and the run ends. After the 8th jump the player may continue into endless mode, earning minimal resources and ending the run on the first loss.

### Shopping phase

1. Spend `Salvage` to build new ships or scrap unwanted ones.
2. Spend `Tech` across four tracks:
   - **Mothership upgrades** — hangar capacity, Mothership weapons/armor. Permanent; always available.
   - **Doctrines** — role/fleet-scoped passive bonuses (e.g. all Fighters +10% speed). Randomized table per jump, rerollable with `Tech`.
   - **Role equipment** — rarer, higher-magnitude role-scoped items (e.g. all MissileBoats gain shield regen). Randomized table, rerollable with `Tech`.
   - **Ship equipment** — unique items slotted into a specific Mothership or Capital ship. Randomized table, rerollable with `Tech`.

### Battle phase

3. Face a curated opponent fleet matched to your current jump number and win/loss ratio.
4. Your fleet warps in from the left; the opponent warps in from the right.
5. The fleets battle. If combat exceeds 60 seconds, attrition kicks in: ships take 1% of max HP per second, increasing by 1% each additional second. At 120 seconds both fleets warp out — draw.
6. Earn resources based on outcome:
   - `Salvage` — earned from enemy ships destroyed during the battle
   - `Tech` — earned from victories (rarer)

### Between jumps

All ships auto-heal to full HP for free. The player may optionally skip healing a ship to receive `Salvage` instead — a trade of combat effectiveness for resources. Ships that reach 0 HP during battle survive at 1 HP minimum; no ship is permanently destroyed by combat. Ships below full HP fight with proportionally deteriorated stats (speed, damage, turn rate, etc.).

The only way to permanently remove a ship is to manually salvage it in the shop. Salvage yield is proportional to current HP — a damaged ship returns less, preventing a skip-heal-then-salvage farming loop.

## Combat

### Weapon archetypes

- **Hitscan** — damage resolves instantly at the tick fired. No sim entity created.
- **Projectile** — a first-class sim entity with position, velocity, target, and `ticks_remaining`. On expiry without a hit it fizzles (`projectile_fizzled`).
- **Beam / ray** — continuous damage over a fixed tick duration. The beam entity persists in state snapshots while active (source, target, `ticks_remaining`). Damages everything in its path each tick — can hit multiple targets in a line. Capital ships carry beams: slow charge time, high sustained damage, long cooldown.

### Projectile subtypes

- **Seeking missile** — homes toward a target each tick within a turn-rate limit. Fizzles after N ticks if no hit. Interceptable by point defense.
- **Torpedo** — straight-line, no homing (or very low turn rate). High damage, long lifetime. Interceptable by point defense.
- **Drifting bomb / mine** — launched with an initial velocity then drifts unpowered. Detonates on proximity (radius trigger). No target tracking. Can be shot down by point defense.

### Status effects

Weapons can inflict temporary status effects on ships in addition to or instead of direct damage. Status effects are tracked per ship in state snapshots.

- **Stun** — disables the ship for N ticks: weapons cannot fire, boid forces are zeroed (ship drifts on inertia). Applied via a `stun` field on the weapon block: `"stun_ticks": 20`. Any weapon archetype (hitscan, projectile, beam) can carry a stun.

- **Burning** — damage over time: deals X damage per tick for N ticks. Applied via `"burn_damage"` and `"burn_ticks"` on the weapon block. Stacks or refreshes duration (TODO: decide on stack behaviour).
- **Radiation** — slow, persistent damage over time. Lower damage per tick than burning but longer duration. Also suppresses shield recharge while active. Applied via `"radiation_damage"` and `"radiation_ticks"`.

TODO: define other status effects (e.g. slow, weapons jam, shield disruption) once these are implemented and the pattern is established.
TODO: decide whether burning stacks (multiple hits add duration or damage) or just refreshes.

### Events

`projectile_fired`, `projectile_hit`, `projectile_fizzled`, `projectile_intercepted`, `mine_detonated`, `beam_fired`, `beam_hit`, `beam_ended`

### Ship roles

Ship role is an enum label (`Fighter`, `MissileBoat`, `TorpedoBomber`, `MineLayer`, `PointDefense`, `Capital`) derived from a ship's boid weight profile and primary weapon type — not hard-coded behavior. Two ships with the same role share the same defaults but can diverge via stat tuning.

TODO: define weapon stats (damage, range, cooldown, missile turn rate, beam charge time, point-defense priority rules).
TODO: revisit ship roles — add more archetypes and flesh out Capital ship stats once combat feel is playtested.
TODO: shop economy needs playtesting — reroll costs, Tech drop rates, doctrine bonus magnitudes, rare equipment pool.

## Boids

Ships use boids-based movement with inertia and acceleration. Layout: SoA (struct of arrays) with a uniform spatial grid; each ship considers ≤16 neighbors per tick.

Forces are a weighted sum per ship: `separation`, `cohesion`, `alignment`, `seek_enemy`, `maintain_range`. Each ship role has a distinct weight profile — behavior changes by tuning weights, not code. Note: `seek_enemy` doubles as follow-leader (point it at an ally); `separation` doubles as flee-from-enemy (point it at a threat).

TODO: define weight profiles per ship role.
TODO: revisit boid forces — more forces likely needed once combat feel is playtested.

## Morale *(low priority — defer until sim is further along)*

Each ship has a `morale` value (0–100). Morale is part of the sim state and included in state snapshots every tick.

**Morale decreases when:**
- The ship takes HP damage
- The ship is surrounded (more enemies than friendlies within boid neighbor radius)
- A nearby friendly ship is destroyed

**Morale recovers passively** — ticks back toward 100 at a fixed rate every tick regardless of circumstances.

**Morale increases faster when:**
- Close to other friendly ships (cohesion bonus)
- Close to the Mothership (stronger boost; overrides passive recovery rate)

**Effect on behavior:** morale is expressed through boid weights. Low morale shifts weights toward fleeing — `separation` from enemies increases, a `seek_mothership` force activates pulling the ship back toward the Mothership. High morale allows full aggressive weight profiles. The transition is continuous, not a binary flip.

TODO: define morale thresholds, recovery rate, damage/destruction penalty magnitudes, and Mothership proximity radius.

## Tech stack

| Concern | Choice |
|---|---|
| Game engine | Godot 4 (C#) |
| Sim language | Rust — engine-agnostic CLI tool |
| Server | Rust + axum — shares fleet/ship types with sim crate. Fallback: Node + Fastify. |
| Database | Postgres |
| Server protocol | REST/JSON, async, no real-time |
| Math | Fixed-point via `fixed` crate — `I32F32` for positions, `I16F16` for most else |
| RNG | xoshiro256+ (`rand_xoshiro` crate), explicit state (4× u64), no globals |
| Containers (sim) | `BTreeMap` / `BTreeSet` / arrays-by-ID only — see ADR-0001 |
| Replays | Seed + value-snapshot of both fleets at battle start |
| Anti-cheat | Server re-simulates sampled battles using the same sim binary |

## Sim ↔ client interface

The Rust sim is a standalone CLI tool. Godot invokes it as a subprocess and reads the battle log written to disk.

**Format:** JSON (human-readable; invaluable for debugging desyncs). Upgrade path to MessagePack (`rmp-serde` / `MessagePack-CSharp`) when log size or parse time becomes a problem — no schema compiler required.

**Schema:** two parallel streams —
- **State snapshots** (every tick): tick number + full ship state (position, velocity, heading, health) for all ships. Allows the renderer to scrub to any point in the battle.
- **Event stream** (sparse): one entry per meaningful occurrence (shot fired, hit, ship at 0 HP, etc.). Renderer uses this to trigger visual effects at the correct tick.

TODO: validate schema against renderer needs once the engine layer is being built.
TODO: evaluate GDExtension (`gdext` crate) to embed the sim directly in Godot once the sim API stabilises.

## Fleet JSON specification

The sim CLI takes a seed as a CLI argument and two fleet JSON files: `skock-sim --seed <u64> fleet_a.json fleet_b.json`.

Fleet JSON top-level structure:
```json
{
  "mothership": { ...ship... },
  "ships": [ ...ship... ],
  "doctrines": [ ... ],
  "role_equipment": [ ... ]
}
```

All stats are fully resolved — the sim performs no blueprint lookups. Each ship object:
```json
{
  "blueprint_drawing_id": "fighter_mk1",
  "role": "Fighter",
  "hp": 120,
  "max_hp": 120,
  "speed": 5,
  "acceleration": 2,
  "turn_rate": 0.8,
  "boid_weights": {
    "separation": 1.5,
    "cohesion": 0.8,
    "alignment": 0.6,
    "seek_enemy": 2.0,
    "maintain_range": 1.2
  },
  "armor": 0.10,
  "shield_hp": 80,
  "shield_max_hp": 80,
  "shield_recharge_rate": 2,
  "weapon": { ... },
  "equipment": [ ... ]
}
```

`blueprint_drawing_id` is opaque to the sim — passed through into battle log state snapshots so the renderer knows which sprite to use. The Mothership follows the same structure but is always present and unique.

Three tiers of fleet bonuses — all use the same data-driven effects vocabulary, distinction is a shop/acquisition concept only:

- **Doctrines** (`doctrines` array) — role/fleet-scoped stat bonuses, randomized table per jump, rerollable with `Tech`.
- **Role equipment** (`role_equipment` array) — rarer, higher-magnitude role-scoped items. Same JSON shape as doctrines. Separate shop track.
- **Ship equipment** (`equipment` on ship) — unique per-ship items. Only Mothership and Capital ships have slots; all other ship types have `"equipment": []` and cannot be equipped.

To the sim all three are just lists of effects to apply — the tiers are invisible at the sim layer.

The `weapon` block inside a ship:
```json
"weapon": {
  "type": "projectile",
  "subtype": "seeking_missile",
  "damage": 40,
  "range": 200,
  "cooldown_ticks": 60,
  "turn_rate": 0.4,
  "fuse_ticks": 90,
  "ammo": 12
}
```

`ammo` is optional — omitted means unlimited. When present, the weapon can only fire that many times before it is exhausted. `turn_rate` and `fuse_ticks` apply to missiles only; beams add `charge_ticks` and `duration_ticks`; fields irrelevant to the weapon type are omitted.

Doctrines and equipment use a shared data-driven effects vocabulary. The sim understands each effect type by name and applies it — no per-item hardcoded logic. New items that use existing effect types require no sim code changes.

Doctrine entry (supports upsides and downsides via multiple effects):
```json
{
  "id": "glass_cannon",
  "effects": [
    { "role": "Fighter", "stat": "damage", "modifier": 1.30 },
    { "role": "Fighter", "stat": "hp",     "modifier": 0.70 }
  ]
}
```
`role` scopes the effect to a ship type; `null` role means fleet-wide.

Equipment entry (ship-level, supports passive and active effects):
```json
{
  "id": "repair_nanobots",
  "effects": [
    { "type": "hp_regen", "value": 2 }
  ]
}
```

Known effect types:
- `stat_modifier` — multiplies a stat: `{ "type": "stat_modifier", "stat": "speed", "modifier": 1.2 }`
- `hp_regen` — restores HP per tick: `{ "type": "hp_regen", "value": 2 }`
- `damage_reduction` — reduces all incoming damage by a fraction: `{ "type": "damage_reduction", "value": 0.15 }`

Both `armor` and shield fields are optional — omitted means no armor or shields. `armor` is a damage reduction fraction (0.10 = 10% of incoming damage absorbed). Shields are a separate HP pool depleted before hull HP. `shield_recharge_rate` is HP restored to shields per tick (optional, omitted means shields do not recharge). Hull HP does not regenerate by default — revisit if playtesting reveals a need.

TODO: revisit ship stat block — more fields likely needed once boid tuning and combat balancing begins (e.g. weapon range, point-defense radius, tonnage, shield regen rate).
TODO: revisit weapon block — more fields may be needed (e.g. splash radius for mines/torpedoes, beam width, projectile speed).
TODO: revisit proc-based effects (`on_hit_received`, `on_kill`, `on_low_hp`, etc.) once sim code has enough flesh to reason about the trigger/effect pipeline concretely.

## Build order

1. Headless deterministic boids sim in Rust. No graphics. Two fleets fly at each other, shoot, one wins. Tick log to stdout. Test: run twice, diff output, must be byte-identical.
2. Determinism CI test. Lock in a corpus of (seed, fleetA, fleetB) battles with known result hashes. Runs on every commit forever.
3. Engine layer. Godot 4 (C#) scene that loads a battle log and renders it. Camera, ship sprites, projectile effects, victory screen.
4. Meta layer. Run map, shop, fleet builder, ship roster. All local first.
5. Local roguelite loop end-to-end. Single player, no server, full run start to finish. Playtest thoroughly — the game lives or dies here.
6. Server. Account system, fleet upload, opponent fetch. Async multiplayer dropped onto the existing single-player loop.
7. Verification. Server-side replay simulation as a background worker.
8. Polish, balance, content.
