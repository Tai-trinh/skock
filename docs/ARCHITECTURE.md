System shape, component boundaries, and sim↔client interface for Skock.

## Components

- **`/sim`** (Rust) — headless CLI. Takes seed + two fleet JSONs, runs 30 Hz boids sim, writes battle log to stdout (MessagePack), result JSON to stderr. All game logic lives here.
- **`/types`** (Rust) — shared `FleetJson` and related structs. Neither `sim` nor server should redefine these.
- **`/client`** (Godot 4 + C#) — invokes sim as subprocess, parses battle log, renders battle, runs meta layer (shop, fleet builder, roguelite loop).
- **`/server`** (Rust + axum, not yet built) — stateless REST/JSON API; re-simulates sampled battles as anti-cheat background worker.

## Sim ↔ client interface

- Transport: sim writes MessagePack to stdout; Godot reads after process exits. Result/status go to stderr as `RESULT:{...json}`. `--debug` writes JSON to disk.
- At 256 entities: ~40 MB MessagePack / ~100 MB JSON — JSON too large for production.
- **State snapshots** (every tick) — full ship state (pos, vel, heading, hp) for all ships and active projectiles/beams. Allows renderer to scrub to any tick.
- **Event stream** (sparse) — one entry per meaningful occurrence (`projectile_fired`, `beam_hit`, `mine_detonated`, etc.). Drives visual effects.
- GDExtension (`gdext`) is a future option once the sim API stabilises.

## Client code structure

```
src/sim/       — subprocess wrapper, battle log parser, playback state
src/meta/      — RunState (autoload singleton), shop logic, admiral definitions, dockyard
src/ui/        — pure UI nodes; read from meta/ via signals, never own state
src/rendering/ — BattleRenderer, ShipNode, effect triggers
data/          — admirals.json, factions.json; loaded at startup, accessible as res://data/
```

- **UI data flow**: one-way. `RunState` (autoload singleton) owns all run state. UI nodes read from it; player actions call methods on it. No UI node owns state.
- **DI seam**: `LocalRunStore` depends on `IRunData` (not `RunState`) so C# tests can inject `FakeRunData` without touching Godot. Fakes live in `client.tests/Fakes/`. Anything with `using Godot` cannot be unit-tested outside the editor.
- **Store swapping** — each swaps with a one-line change in `RunState._Ready()`. Current seams:
  - `IRunStore` / `LocalRunStore` — run lifecycle only (Load, Save, StartRun, GetBattleSeed). Online: `ServerRunStore` validates lifecycle actions via REST before mutating RunState.
  - `IDockyard` / `LocalDockyardAdapter` — one instance per dockyard visit. Offline: spawns `skock-dockyard` subprocess. Online: `ServerDockyardAdapter` talks to the server which runs the same Rust binary. The binary is the single source of truth for offer generation and purchase validation (see ADR-0009).
  - `IStatsStore` / `LocalStatsStore` — per-run jump history and lifetime counters. Online: `ServerStatsStore` POSTs `BattleInputs` (seed + fleet JSONs) to the server for anti-cheat storage and retroactive re-simulation (honor system — see ADR-0003 and ADR-0004).
  - `IAdmiralStore` / `LocalAdmiralStore` — admiral and faction catalog. Reads `client/data/admirals.json` and `client/data/factions.json`. Online: `ServerAdmiralStore` fetches from REST API, enabling server-curated or rotating admiral pools.

## Client rendering and runtime

**Ship rendering:** layered 3D mesh composition. Ships are assembled from:
- **Base hull mesh** — keyed by `(faction, hull_class, weight)` — different factions have visually distinct hull designs for the same class
- **Weapon hardpoint mesh** — keyed by `role` — one per role type, attached to the hull
- **Equipment module meshes** — one per equipment item, attached to visible hardpoints

Meshes are looked up from a client-side dictionary at battle log load time. `blueprint_drawing_id` is an override for unique/named ships (e.g. the player's Mothership) that bypass the procedural assembly.

**Placeholder ship graphics (pre-art):** geometric shapes drawn in code until final 3D meshes are ready.

| Hull class | Shape |
|---|---|
| Corvette | Triangle (pointing forward) |
| Frigate | Diamond |
| Destroyer | Square |
| Cruiser | Rectangle (wider than tall) |
| Battlecruiser | Rectangle with a forward-pointing tip |
| Dreadnought | Rectangle with a forward-pointing tip and two triangular wing fins on the sides |
| Mothership | Hexagon |

Fleet A (player) and Fleet B (opponent) are distinguished by color, not shape. Placeholder colors TBD during renderer development.

**Run state (pre-server):** serialized to a local JSON save file via Godot's file API. Human-editable — save file tampering is the player's problem in single-player. Replaced by server run state once the server is built. No SQLite or local DB.

**Run state (online mode, planned post-offline):** the server owns a server-side Run ID assigned at run start. All run choices (fleet, Salvage, Tech, JumpNumber, LossCount) are stored in the server DB and fetched on login — local save is only a cache. If the local save and server record diverge, server wins. Matchmaking uses JumpNumber + LossCount as the progress dimensions. The opponent fleet DB is seeded with hand-authored curated fleets organised by these brackets. Future: curated fleets are benchmarked via the deterministic sim (seeded batch runs).

**Scenes:**
- `AdmiralSelect.tscn` — run start; player picks an admiral, sees starting fleet and passive bonus. Transitions to `Dockyard.tscn` on confirm.
- `Dockyard.tscn` — between every battle; randomized ship offers by tier (5/3/2/1), research/doctrine track, salvage and heal decisions. Transitions to `Battle.tscn` on launch.
- `Battle.tscn` — battle playback. Transitions to `Dockyard.tscn` on win (next jump) or loss (retry same jump); transitions to `RunEnd.tscn` on winning jump 8 or on 3rd loss.
- `RunEnd.tscn` — run result screen (win/loss stats, per-jump breakdown). Returns to `AdmiralSelect.tscn`.

The existing `FleetBuilder.tscn` is superseded by `Dockyard.tscn` — rename and extend rather than rewrite. `FleetBuilderUi.cs` buy/salvage logic carries over directly.

**Subprocess handling:** `System.Diagnostics.Process` (.NET standard API). Godot spawns the sim binary, waits for exit, reads stdout (MessagePack log) and stderr (battle result JSON).

**Camera:** fixed, auto-fits battlefield bounds. No player pan/zoom in initial build. TODO: add free camera if players request it.

## Build order

1. Headless deterministic boids sim in Rust. No graphics. Two fleets fly at each other, shoot, one wins. Tick log to stdout. Test: run twice, diff output, must be byte-identical. **First playable milestone:** two ship types (fighter + mothership), hitscan only, no status effects, no equipment, no doctrines. Watch the replay. Evaluate whether boids combat feels fun before building anything else.
2. Determinism CI test. Lock in a corpus of (seed, fleetA, fleetB) battles with known result hashes. Runs on every commit forever.
3. Engine layer. Godot 4 (C#) scene that loads a battle log and renders it. Camera, ship sprites, projectile effects, victory screen.
4. Meta layer. Shop, fleet builder, ship roster. All local first. No run map — encounters are linear, 1 through 8 in sequence.
5. Local roguelite loop end-to-end. Single player, no server, full run start to finish. Playtest thoroughly — the game lives or dies here. **Rule:** no new weapon types or combat mechanics added before this step is complete. Combat complexity is content, not foundation.
6. Server. Account system, fleet upload, opponent fetch. Async multiplayer dropped onto the existing single-player loop.
7. Verification. Server-side replay simulation as a background worker.
8. Polish, balance, content.

## Tech stack

| Concern | Choice |
|---|---|
| Game engine | Godot 4 (C#) |
| Sim language | Rust — engine-agnostic CLI tool |
| Server | Rust + axum — shares fleet/ship types with sim crate. Fallback: Node + Fastify. |
| Database | Postgres |
| Server protocol | REST/JSON, async, no real-time |
| Math | Fixed-point (`I32F32` positions, `I16F16` most else) — see ADR-0002 |
| RNG | xoshiro256+, explicit state (4× u64) — see ADR-0002 |
| Containers (game rule crates) | `BTreeMap`/`BTreeSet` in sim/dockyard/server game logic — see ADR-0001 |
| Replays | Seed + value-snapshot of both fleets at battle start |
| Anti-cheat | Honor system, sample-based server re-simulation — see ADR-0003 |

## Rust workspace structure

Four-crate workspace:

- **`types`** — shared fleet/ship structs, weapon definitions, effect types, fleet JSON deserialization. No game logic. Both `sim`, `dockyard`, and `server` depend on this.
- **`sim`** — boids engine, combat resolution, battle log serialization. CLI binary. Depends on `types`.
- **`dockyard`** — deterministic dockyard offer generation and session validation. CLI binary. Depends on `types`. See §Dockyard binary protocol below.
- **`server`** — axum HTTP API, fleet storage, opponent matching, anti-cheat worker. Depends on `types` only — never imports sim or dockyard logic directly.

The server re-runs the sim and dockyard binaries as subprocesses, same as the client. See ADR-0009.

## Player identity

PlayerID is passed to the dockyard binary on every session open. The binary uses it to gate metaprogression-unlocked content — the lookup mechanism for offline mode is deferred (TODO: a local metaprogression store, offline-capable Rust + C# interface). In online mode the server resolves the player's unlocks from the database before routing the request to the dockyard binary.

## Tick loop phase order

Each tick executes phases in this exact order — order is part of the determinism contract (see ADR-0010):

1. Increment tick counter
2. Apply continuous effects — shield recharge, burn/radiation damage, status effect countdowns
3. Rebuild spatial grid from current positions
4. Compute boid forces per ship (reads spatial grid)
5. Integrate positions and velocities (apply forces + inertia)
6. Resolve weapon firing — check range, cooldown, ammo; spawn projectiles
7. Advance projectiles — move, check hits, check fizzle
8. Resolve beam damage — all active beams deal damage to ships in path
9. Apply damage — shields absorb first, then armor reduction, then hull HP
10. Check end condition — either Mothership at 0 HP ends the battle. Winner = fleet whose Mothership is still standing. If both reach 0 HP in the same tick, result is `"draw"` — treated as a loss for the player in the run loop.
11. Apply attrition if tick >= 1800 (60s × 30 Hz) — 1% max HP damage per second, increasing 1% per second
12. Write state snapshot + events to battle log

## Sim code design

### Error handling

Panics for logic invariants inside the tick loop. Boundary errors (bad CLI args, invalid fleet JSON, stdout write failure) are caught in `main` and written to stderr with the `RESULT:` sentinel — same protocol as the battle result, with an `"error"` key instead of `"winner"`:

```
RESULT:{"error": "invalid_fleet_json", "message": "ship[2].hp must be > 0"}
```

Godot reads stderr after sim exit, finds the `RESULT:` line, and inspects it. If the JSON contains an `"error"` key, the battle failed. If no `RESULT:` line exists (e.g. a Rust panic), the sim crashed.

### Effect resolution

At battle start, all effects from `doctrines`, `role_equipment`, `faction_effects`, and `admiral_effects` are walked once and multiplied into each ship's stats. The tick loop reads plain resolved numbers — no effect lookup mid-battle. The sim owns all effect resolution.

**Current exception:** admiral effects are pre-applied by `BuildFleetForSim()` in C# — see ADR-0006.

Proc-based effects (on_hit, on_kill) will be event-driven and layered on top of pre-resolved stats when implemented. TODO: add conditional trigger effects that activate on sim state conditions — e.g. `on_mothership_below_50pct_hp: boost morale to nearby friendlies for 10s`. These are evaluated each tick against sim state, not pre-resolved.

### Fixed-point type assignments

Positions use `I32F32`; everything else `I16F16`. See ADR-0002.

### Point defense targeting

Point defense (`PointDefense` role) uses `target_priority: "projectile"`. Each tick it scans all live projectiles within weapon `range`, picks the one closest to any friendly ship (highest threat), and fires if cooldown allows. Interception resolves instantly (hitscan — no interceptor projectile spawned): if PD fires and the target projectile is in range, `projectile_intercepted` fires and the projectile is removed. A `miss_chance` on the PD weapon block is the tuning knob for PD reliability.

### Projectile hit detection

Swept segment test (see ADR-0013): each tick the projectile's movement is treated as a line segment `prev_pos → pos` (derived as `pos - velocity`). A hit occurs when the minimum distance from any circle center in the target ship's hull hit shape to the segment is ≤ `(circle.radius + projectile.hit_radius)`. `projectile.hit_radius` is a per-subtype default from sim config (e.g. `torpedo: 4`, `seeking_missile: 1`), overridable via `hit_radius` in the weapon block.

### Beam hit detection

Ray from source toward the nearest valid target. For each candidate ship, checks minimum distance from the ray to each circle center in the ship's hull hit shape; a hit occurs when that distance ≤ `(beam_width / 2 + circle.radius)`. Hits the first ship (sorted by distance from source) that satisfies the test. Ray stops at the first hit — client draws the beam terminating at the hit ship.

### Damage resolution *(see ADR-0011)*

```
shield_absorbed = min(raw_damage, shield_hp)
shield_hp      -= shield_absorbed
spillover       = raw_damage - shield_absorbed
hull_damage     = spillover * (1.0 - armor)
hp             -= hull_damage
```

### Entity IDs

Typed newtypes — `ShipId(u32)`, `ProjectileId(u32)`, `BeamId(u32)` — each with its own counter in sim state. Prevents accidental cross-type ID comparisons at compile time. IDs are assigned at entity creation and never reused within a battle. BTreeMap keys throughout the sim use these typed IDs.
