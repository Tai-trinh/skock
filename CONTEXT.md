# Skock — Space Fleet Auto-Battler

Skock is a roguelite where you command a fleet hyperspace-jumping from location to location to scavenge and survive — a homage to Gallforce and Homeworld. Each run consists of 8 encounters against opponent fleets. Battles are fully deterministic: replaying a battle with the same seed and fleet snapshots produces byte-identical results on any machine, enabling local replays and server-side anti-cheat verification. The multiplayer layer is simple: retrieve an opponent's fleet from the server and auto-battle it locally.

## Battlefield

1000 × 1000 unit coordinate space, origin at center. Fleet A spawns around x = -400, fleet B around x = +400. Ships arranged in their fleet's `formation` at spawn with small deterministic position noise (seeded from the battle seed via xoshiro256+) — ships look organic rather than perfectly geometric. Mothership always anchors back-center with no noise. Weapon ranges meaningful in the 50–300 unit range. TODO: revisit battlefield size once boid movement and fleet sizes are playtested.

## Core constraints

- Top-down 2D; 3D engine/textures used as sprites for easy rotation
- 30 ticks/sec logical simulation; renderer interpolates between ticks
- All battles end within 120 seconds
- Fully deterministic given the seed
- Single-threaded simulation
- Anime art style (Macross, Gallforce)
- Target session length: 15–30 minutes

## The Mothership

The Mothership is the player's one permanent ship and the heart of the fleet. It is a combat entity — it appears on the battlefield with artillery-class weapons and point defense. Destroying the enemy Mothership wins the battle; losing your own ends the run immediately regardless of remaining fleet.

Fleet size is limited by the Mothership's **hangar capacity** (a tonnage value). Each ship has a tonnage cost; heavier ships cost more. Lore: the entire fleet must physically dock inside the Mothership to survive hyperspace jumps. Upgrading hangar capacity (costs `Tech`) is a primary progression axis.

## Game loop

A run consists of 8 jumps. Lose 3 battles and the run ends. After the 8th jump the player may continue into endless mode, earning minimal resources and ending the run on the first loss.

### Shopping phase

1. Spend `Salvage` to build new ships or scrap unwanted ones.
2. Spend `Tech` across four tracks:
   - **Mothership upgrades** — hangar capacity, Mothership weapons/armor. Permanent; always available.
   - **Doctrines** — role/fleet-scoped passive bonuses (e.g. all Fighters +10% speed). Randomized table per jump, rerollable with `Tech`.
   - **Role equipment** — rarer, higher-magnitude role-scoped items (e.g. all `Missile`-role ships gain shield regen). Randomized table, rerollable with `Tech`.
   - **Ship equipment** — unique items slotted into a specific Mothership or Capital ship. Randomized table, rerollable with `Tech`.

### Battle phase

3. Face a curated opponent fleet matched to your current jump number and win/loss ratio. Opponent fleets are hand-authored fleet JSON files shipped with the game, organized by difficulty tier. When the server exists, real player fleets replace them — the fleet JSON format is the bridge.
4. Your fleet warps in from the left; the opponent warps in from the right.
5. The fleets battle. If combat exceeds 60 seconds, attrition kicks in: ships take 1% of max HP per second, increasing by 1% each additional second. At 120 seconds both fleets warp out — draw.
6. Earn resources based on outcome:
   - `Salvage` — earned from enemy ships destroyed during the battle
   - `Tech` — earned from victories (rarer)

### Between jumps

All ships auto-heal to full HP for free. The player may optionally skip healing a ship to receive `Salvage` instead — a trade of combat effectiveness for resources. Ships that reach 0 HP during battle survive at 1 HP minimum; no ship is permanently destroyed by combat. Ships below full HP fight with proportionally deteriorated stats (speed, damage, turn rate, etc.).

The only way to permanently remove a ship is to manually salvage it in the shop. Salvage yield is proportional to current HP — a damaged ship returns less, preventing a skip-heal-then-salvage farming loop.

Ships that reach 0 HP during battle explode and are removed from the sim that tick (`ship_destroyed` event). They do not appear in subsequent state snapshots. After the battle, the fleet roster restores all destroyed ships to 1 HP — the player does not lose them permanently. This keeps battles visually dramatic without punishing the player with forced repurchases.

## Combat

### Weapon archetypes

- **Hitscan** — damage resolves instantly at the tick fired. No sim entity created. Optional `miss_chance` (0.0–1.0): rolled via battle RNG each shot; on miss, a `hitscan_missed` event fires and no damage is applied.
- **Projectile** — a first-class sim entity with position, velocity, target, and `ticks_remaining`. On expiry without a hit it fizzles (`projectile_fizzled`).
- **Beam / ray** — continuous damage over a fixed tick duration. The beam entity persists in state snapshots while active (source, target, `ticks_remaining`). Damages everything in its path each tick — can hit multiple targets in a line. `Artillery`-role and `Battlecruiser`/`Dreadnought` hull class ships typically carry beams: slow charge time, high sustained damage, long cooldown.

### Projectile subtypes

- **Seeking missile** — homes toward a target each tick within a turn-rate limit. Fizzles after N ticks if no hit. Interceptable by point defense.
- **Torpedo** — straight-line, no homing (or very low turn rate). High damage, long lifetime. Interceptable by point defense. May carry an explosive payload (see below).
- **Drifting bomb / mine** — launched with an initial velocity then drifts unpowered. Detonates on proximity (radius trigger). No target tracking. Can be shot down by point defense. Always explosive.

**Explosive payload:** torpedoes, bombs, and mines may carry an explosive payload defined by `explosion_radius` and `explosion_damage` on the weapon block. On detonation, an expanding explosion ring is emitted — every ship within `explosion_radius` takes `explosion_damage` exactly once, regardless of position in the ring. Damage is flat within the radius (no falloff). Hits both friendly and enemy ships. The explosion is a renderer event (`explosion_detonated`) with a position and radius for visual effect. TODO: consider damage falloff by distance once basic explosions are playtested.

### Status effects *(low priority — implement after core sim is working)*

Weapons can inflict temporary status effects on ships in addition to or instead of direct damage. Status effects are tracked per ship in state snapshots.

- **EMP / Stun** — disables the ship for N ticks: weapons cannot fire, boid forces are zeroed (ship drifts on inertia), shields are instantly drained to 0 and cannot recharge for the duration. Applied via `"stun_ticks": 20` on the weapon block. Any weapon archetype (hitscan, projectile, beam) can carry an EMP. Short disable — intended as a tactical window opener, not a death sentence.

- **Burning** — damage over time: deals X damage per tick for N ticks. Applied via `"burn_damage"` and `"burn_ticks"` on the weapon block. Refreshes on repeat hits — duration resets, damage per tick unchanged. Does not stack.
- **Radiation** — slow, persistent damage over time. Lower damage per tick than burning but longer duration. Also suppresses shield recharge while active. Applied via `"radiation_damage"` and `"radiation_ticks"`.

TODO: revisit all status effects once core sim (movement, combat, hitscan) is working — status effects are not needed for the initial playable build.

### Events

`projectile_fired`, `projectile_hit`, `projectile_fizzled`, `projectile_intercepted`, `mine_detonated`, `explosion_detonated`, `beam_fired`, `beam_hit`, `beam_ended`, `hitscan_fired`, `hitscan_missed`, `ship_destroyed`, `ship_at_low_hp` (hull ≤ 25% max HP, fires once per ship), `attrition_started` (fires at tick 1800)

### Ship roles

Ships are identified by two fields plus an optional weight designation. Display name = `[weight] [role] [hull_class]` e.g. **"Heavy Missile Destroyer"**, **"Light Torpedo Corvette"**, **"Railgun Frigate"**.

**`hull_class`** — size and durability tier, determines base tonnage, HP, armor profile:
`Corvette`, `Frigate`, `Destroyer`, `Cruiser`, `Battlecruiser`, `Dreadnought`

**`role`** — weapon specialization and boid weight profile:
`Fighter`, `Missile`, `Torpedo`, `Mine`, `PointDefense`, `Artillery`, `Plasma`, `Railgun`

**`weight`** *(optional)* — `Light` or `Heavy`. Omitted = standard. Light = faster, less armor. Heavy = slower, more armor.

`Dreadnought` hull class — high HP, high armor/shields, high tonnage. Carries a short-range hitscan weapon — not toothless, but not a damage dealer. Boid weights favour pushing toward enemies and holding position. Purpose: soak hits and shield ships behind them. TODO: revisit Dreadnought weapon stats and role balance once playtested.

TODO: revisit ship roles — add Shield projector (support ship that extends shields to nearby friendlies) once core roles are playtested.

**Targeting:** default is nearest enemy. Ships have an optional `target_priority` field that overrides targeting for specific roles (e.g. `PointDefense` targets incoming projectiles first, then nearest enemy). Defined per ship in the fleet JSON. TODO: revisit targeting logic after playtesting — nearest enemy may produce boring behaviour at scale.

**Firing range:** weapon `range` field gates the fire condition — ship only fires when target distance ≤ `range`. The `maintain_range` boid force positions the ship at its preferred engagement distance. Both work together: boids handle positioning, range check handles firing permission.

TODO: define weapon stats (damage, range, cooldown, missile turn rate, beam charge time, point-defense priority rules).
TODO: revisit ship roles — add more archetypes and flesh out Capital ship stats once combat feel is playtested.
TODO: shop economy needs playtesting — reroll costs, Tech drop rates, doctrine bonus magnitudes, rare equipment pool.

## Boids

Ships use boids-based movement with inertia and acceleration. Neighbor search is O(N²) brute-force for now — the sim runs offline so raw throughput is not the first concern. TODO: replace with a uniform spatial grid or k-d tree if sim speed becomes a bottleneck during playtesting.

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

## Tick loop phase order

Each tick executes phases in this exact order — order is part of the determinism contract:

1. Increment tick counter
2. Apply continuous effects — shield recharge, burn/radiation damage, status effect countdowns
3. Rebuild spatial grid from current positions
4. Compute boid forces per ship (reads spatial grid)
5. Integrate positions and velocities (apply forces + inertia)
6. Resolve weapon firing — check range, cooldown, ammo; spawn projectiles
7. Advance projectiles — move, check hits, check fizzle
8. Resolve beam damage — all active beams deal damage to ships in path
9. Apply damage — shields absorb first, then armor reduction, then hull HP
10. Check victory condition — Mothership at 0 HP ends battle
11. Apply attrition if tick > 1800 (60s × 30 Hz) — 1% max HP damage per second, increasing 1% per second
12. Write state snapshot + events to battle log

## Sim code design

### Error handling

Panics for logic invariants inside the tick loop. Boundary errors (bad CLI args, invalid fleet JSON, stdout write failure) are caught in `main` and written to stderr as structured JSON — same channel as the battle result, but with an `"error"` key instead of `"winner"`:

```json
{"error": "invalid_fleet_json", "message": "ship[2].hp must be > 0"}
```

Godot reads stderr after sim exit; a non-zero exit code plus an `"error"` key means the battle failed to run.

### Effect resolution

At battle start, all effects from `doctrines`, `role_equipment`, `faction_effects`, and `admiral_effects` are walked once and multiplied into each ship's stats. The tick loop reads plain resolved numbers — no effect lookup mid-battle. Proc-based effects (on_hit, on_kill) will be event-driven and layered on top of pre-resolved stats when implemented. TODO: add conditional trigger effects that activate on sim state conditions — e.g. `on_mothership_below_50pct_hp: boost morale to nearby friendlies for 10s`. These are evaluated each tick against sim state, not pre-resolved.

### Fixed-point type assignments

| Field category | Type | Bytes |
|---|---|---|
| Position (x, y) | `I32F32` | 8 |
| Velocity, acceleration | `I16F16` | 4 |
| Heading (radians) | `I16F16` | 4 |
| HP, shield HP, damage, regen | `I16F16` | 4 |
| Armor fraction, crit chance | `I16F16` | 4 |
| Boid weights | `I16F16` | 4 |

`I32F32` only for positions — needed for future battlefield scaling. Everything else `I16F16` (±32 767 range, 4 bytes). Halves per-field cost in state snapshots.

### Point defense targeting

Point defense (`PointDefense` role) uses `target_priority: "projectile"`. Each tick it scans all live projectiles within weapon `range`, picks the one closest to any friendly ship (highest threat), and fires if cooldown allows. On hit: `projectile_intercepted` event, projectile removed. No special code path — point defense is a standard weapon that targets `ProjectileId` instead of `ShipId`.

### Projectile hit detection

Point-in-radius per tick: projectile hits if `distance(projectile, target) <= hit_radius`. No swept segment test. Tunneling is prevented by capping projectile speed so it cannot travel more than one ship-radius per tick. TODO: add swept segment test if tunneling is observed in playtesting.

### Beam hit detection

Ray from source toward the nearest valid target. Hits the first ship whose center is within `beam_width` of the ray (sorted by distance). Ray stops at the first hit — client draws the beam terminating at the hit ship. TODO: add `beam_pierce` weapon field to punch through multiple hulls once basic beams are playtested.

### Damage resolution

```
shield_absorbed = min(raw_damage, shield_hp)
shield_hp      -= shield_absorbed
spillover       = raw_damage - shield_absorbed
hull_damage     = spillover * (1.0 - armor)
hp             -= hull_damage
```

Shields absorb first. Armor only reduces spillover damage to hull HP. A ship with full shields is tankier against burst; armor matters most when shields are down.

### Entity IDs

Typed newtypes — `ShipId(u32)`, `ProjectileId(u32)`, `BeamId(u32)` — each with its own counter in sim state. Prevents accidental cross-type ID comparisons at compile time. IDs are assigned at entity creation and never reused within a battle. BTreeMap keys throughout the sim use these typed IDs.

## Rust workspace structure

Three-crate workspace:

- **`types`** — shared fleet/ship structs, weapon definitions, effect types, fleet JSON deserialization. No game logic. Both `sim` and `server` depend on this.
- **`sim`** — boids engine, combat resolution, battle log serialization. CLI binary. Depends on `types`.
- **`server`** — axum HTTP API, fleet storage, opponent matching, anti-cheat worker. Depends on `types` only — never imports sim logic directly.

The server re-runs the sim binary as a subprocess for anti-cheat verification, same as the client.

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

The Rust sim is a standalone CLI tool. Godot invokes it as a subprocess. Sim runs to completion, then Godot reads the battle log.

**Transport:** sim writes MessagePack log bytes to stdout; Godot reads subprocess stdout after process exits. Debug flag (`--debug`) writes JSON to disk instead for desync investigation.

**Battle result:** single-line JSON written to stderr on completion:
```json
{"winner": "fleet_a", "ticks": 2134, "reason": "mothership_destroyed", "fleet_a_survivors": [{"blueprint_drawing_id": "...", "hp": 45}], "fleet_b_survivors": [...]}
```
Reasons: `mothership_destroyed`, `timeout_draw`. `fleet_a_survivors` and `fleet_b_survivors` list final HP for every ship that was alive at battle end — ships that reached 0 HP mid-battle are absent (treated as 1 HP by the client when restoring the fleet). Always written regardless of `--debug` flag.

**Format:** MessagePack (`rmp-serde` in Rust, `MessagePack-CSharp` in Godot) in production; JSON on disk in debug. At up to 256 total entities a full 120s battle log is ~100 MB JSON / ~40 MB MessagePack. Same data model — switching is a flag not a rewrite.

**Schema:** the battle log opens with a header record containing a `schema_version` integer. Client rejects logs where the version doesn't match what it understands. Followed by one MessagePack record per tick, interleaved:
```
{ tick, ships: [...], projectiles: [...], beams: [...], events: [...] }
```
State snapshot and events for the same tick are one record. Godot builds the scrub index and event list in a single pass. No framing needed beyond MessagePack's own length encoding.

**Render loop:** interpolated. Renderer runs at display framerate. Each frame computes `t ∈ [0,1]` between the two nearest sim ticks and lerps ship positions and headings. Events fire when `t` crosses a tick boundary. State snapshots allow seeking to any tick directly without replaying from tick 0.

**Playback controls:** play + speed control (1×, 2×, 4×). No scrubbing in initial build. TODO: add scrubbing if players request it.

**Debug overlay** *(toggled by key during battle playback)*: displays raw tick state for a selected ship — position, velocity, HP, shields, active status effects, boid weights, current target, weapon cooldown. Reads directly from the in-memory battle log at the current playback tick. Built first — validates log correctness during sim and renderer development. TODO: add player-facing fleet stats panel (damage dealt/received, ships destroyed, weapons fired) once debug overlay confirms log data is correct.

TODO: validate schema against renderer needs once the engine layer is being built.
TODO: evaluate GDExtension (`gdext` crate) to embed the sim directly in Godot once the sim API stabilises.

## Fleet JSON specification

The sim CLI takes a seed as a CLI argument, two fleet JSON files, and an optional sim config: `skock-sim --seed <u64> fleet_a.json fleet_b.json [--config sim_config.json]`.

`sim_config.json` holds global tuning knobs — attrition start tick, attrition rate, boid force caps, tick rate, battlefield bounds. In development, loaded from disk via `--config`. In production, embedded at compile time via `include_str!` — the `--config` flag overrides the embedded config for local testing. No runtime file dependency in the shipped binary.

Fleet JSON top-level structure:
```json
{
  "faction": "gallforce",
  "admiral_id": "commander_yuki",
  "formation": "wedge",
  "mothership": { ...ship... },
  "ships": [ ...ship... ],
  "doctrines": [ ... ],
  "role_equipment": [ ... ],
  "faction_effects": [ ... ],
  "admiral_effects": [ ... ]
}
```

`formation` controls spawn layout. Mothership always anchors back-center regardless of formation type. Formations are inspired by historical naval tactics. Initial formation: `wedge` only. Candidates for future additions: `line_of_battle` (broadside line), `echelon` (diagonal stagger), `encirclement` (flanking wings), `defensive_circle` (ships orbiting the Mothership). TODO: add formations once wedge is playtested and the formation system is proven.

Wedge ordering front to back by hull class: `Dreadnought` → `Corvette` → `Frigate` → `Destroyer` → `Cruiser` → `Battlecruiser` → `Mothership`. Dreadnoughts absorb first contact at the tip; Battlecruisers anchor near the Mothership; Mothership at the rear.

All stats are fully resolved — the sim performs no blueprint lookups. Each ship object:
```json
{
  "blueprint_drawing_id": "missile_destroyer_mk1",
  "hull_class": "Destroyer",
  "role": "Missile",
  "weight": "Heavy",
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
- **Ship equipment** (`equipment` on ship) — unique per-ship items. Only Mothership and `Battlecruiser` or `Dreadnought` hull class ships have slots; all others have `"equipment": []` and cannot be equipped.

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
  "ammo": 12,
  "crit_chance": 0.10,
  "crit_damage": 2.0,
  "explosion_radius": 80,
  "explosion_damage": 60
}
```

`crit_chance` (0.0–1.0) and `crit_damage` (multiplier, e.g. 2.0 = double damage) are optional — omitted means no crits. Crit roll uses the battle RNG (xoshiro256+) so results are deterministic. `ammo` is optional — omitted means unlimited. When present, the weapon can only fire that many times before it is exhausted. `turn_rate` and `fuse_ticks` apply to missiles only; beams add `charge_ticks` and `duration_ticks`; fields irrelevant to the weapon type are omitted.

Doctrines and equipment use a shared data-driven effects vocabulary. The sim understands each effect type by name and applies it — no per-item hardcoded logic. New items that use existing effect types require no sim code changes.

Doctrine entry (supports upsides and downsides via multiple effects):
```json
{
  "id": "glass_cannon",
  "effects": [
    { "scope": { "role": "Fighter" },         "type": "stat_modifier", "stat": "damage", "modifier_type": "more",      "modifier": 1.30 },
    { "scope": { "hull_class": "Destroyer" }, "type": "stat_modifier", "stat": "hp",     "modifier_type": "increased", "modifier": -0.30 },
    { "scope": null,                          "type": "stat_modifier", "stat": "speed",  "modifier_type": "increased", "modifier": 0.05 }
  ]
}
```
`scope` targets which ships the effect applies to — by `role`, `hull_class`, `weight`, or any combination. `null` scope = fleet-wide. Both `role` and `hull_class` may be specified together to target e.g. only `Missile Destroyers`.

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
- `stat_modifier` — modifies a stat. Two stacking modes via `modifier_type`:
  - `"increased"` / `"decreased"` — additive: all bonuses on the same stat sum together, then applied as `base * (1 + total)`. Negative `modifier` values are decreases (e.g. `-0.30` = 30% decreased).
  - `"more"` / `"less"` — multiplicative: each bonus multiplies the running total independently. Sub-1.0 values are less (e.g. `0.70` = 30% less).
  - Final formula: `base * (1 + Σ increased/decreased) * Π more/less`
  - Example: `{ "type": "stat_modifier", "stat": "speed", "modifier_type": "more", "modifier": 1.2 }`
- `hp_regen` — restores HP per tick: `{ "type": "hp_regen", "value": 2 }`
- `damage_reduction` — reduces all incoming damage by a fraction: `{ "type": "damage_reduction", "value": 0.15 }`

Both `armor` and shield fields are optional — omitted means no armor or shields. `armor` is a damage reduction fraction (0.10 = 10% of incoming damage absorbed). Shields are a separate HP pool depleted before hull HP. `shield_recharge_rate` is HP restored to shields per tick (optional, omitted means shields do not recharge). Hull HP does not regenerate by default — revisit if playtesting reveals a need.

TODO: revisit ship stat block — more fields likely needed once boid tuning and combat balancing begins (e.g. weapon range, point-defense radius, tonnage, shield regen rate).
TODO: revisit weapon block — more fields may be needed (e.g. splash radius for mines/torpedoes, beam width, projectile speed).
TODO: revisit proc-based effects (`on_hit_received`, `on_kill`, `on_low_hp`, etc.) once sim code has enough flesh to reason about the trigger/effect pipeline concretely.

## Client

**Ship rendering:** layered 3D mesh composition. Ships are assembled from:
- **Base hull mesh** — keyed by `(faction, hull_class, weight)` — different factions have visually distinct hull designs for the same class
- **Weapon hardpoint mesh** — keyed by `role` — one per role type, attached to the hull
- **Equipment module meshes** — one per equipment item, attached to visible hardpoints

Meshes are looked up from a client-side dictionary at battle log load time. `blueprint_drawing_id` is an override for unique/named ships (e.g. the player's Mothership) that bypass the procedural assembly. Normal ships are fully assembled from components. This gives visual readability — you can see what a ship is armed with at a glance.

**Factions:** each fleet belongs to a faction. Faction determines hull mesh set (visual identity) and confers passive fleet bonuses via `faction_effects` in the fleet JSON. Start with one faction; add more as art and balance allows.

**Admiral:** chosen once at run start, locked for the full run. Confers passive bonuses only via `admiral_effects` in the fleet JSON — same effects vocabulary as doctrines. `admiral_id` is opaque to the sim; the client uses it to look up the admiral's portrait (2D anime front-facing art) shown in the shopping sequence. Multiple admirals available to choose from at run start, each with a distinct bonus profile and personality. TODO: revisit active admiral abilities (battle triggers, once-per-run effects) once proc-based effects are implemented.

All four effect sources (`doctrines`, `role_equipment`, `faction_effects`, `admiral_effects`) use the same data-driven effects vocabulary. The sim concatenates all four into a single flat list and applies them identically — the source array is invisible at the sim layer. Keeping them as separate arrays in the fleet JSON makes the source of each bonus explicit for client-side debugging and UI display ("this bonus comes from your admiral").

TODO: define art style guide for hull meshes per class (Corvette = slim/fast silhouette, Dreadnought = wide/blocky, etc.).
TODO: define faction names and visual identity once more than one faction is needed.

**Run state (pre-server):** serialized to a local JSON save file via Godot's file API. Human-editable — save file tampering is the player's problem in single-player. Replaced by server run state once the server is built: the server becomes the source of truth for fleet JSON and all player choices between fights, making local save tampering irrelevant. No SQLite or local DB.

**UI data flow:** one-way. A single `RunState` C# class owns all run state. UI nodes read from it and subscribe to signals for updates; player actions call methods on it. No UI node owns state. Prevents split-brain bugs where multiple nodes disagree on currency counts or fleet composition.

**Godot project structure:**
- `src/sim/` — subprocess wrapper, battle log parser, playback state
- `src/meta/` — `RunState`, shop logic, fleet builder logic
- `src/ui/` — pure UI nodes, display only; call into `meta/` or `sim/` via signals, never the reverse
- `src/rendering/` — battle renderer, ship sprite assembly, effect triggers

No game logic in `_Ready()` or `_Process()` — those delegate to the appropriate layer.

**Subprocess handling:** `System.Diagnostics.Process` (.NET standard API). Godot spawns the sim binary, waits for exit, reads stdout (MessagePack log) and stderr (battle result JSON).

**Camera:** fixed, auto-fits battlefield bounds. Zoom and position calculated once from battle area so all ships are always visible. No player pan/zoom in initial build. TODO: add free camera if players request it.

## Build order

1. Headless deterministic boids sim in Rust. No graphics. Two fleets fly at each other, shoot, one wins. Tick log to stdout. Test: run twice, diff output, must be byte-identical. **First playable milestone:** two ship types (fighter + mothership), hitscan only, no status effects, no equipment, no doctrines. Watch the replay. Evaluate whether boids combat feels fun before building anything else.
2. Determinism CI test. Lock in a corpus of (seed, fleetA, fleetB) battles with known result hashes. Runs on every commit forever.
3. Engine layer. Godot 4 (C#) scene that loads a battle log and renders it. Camera, ship sprites, projectile effects, victory screen.
4. Meta layer. Run map, shop, fleet builder, ship roster. All local first.
5. Local roguelite loop end-to-end. Single player, no server, full run start to finish. Playtest thoroughly — the game lives or dies here. **Rule:** no new weapon types or combat mechanics added before this step is complete. Combat complexity is content, not foundation.
6. Server. Account system, fleet upload, opponent fetch. Async multiplayer dropped onto the existing single-player loop.
7. Verification. Server-side replay simulation as a background worker.
8. Polish, balance, content.
