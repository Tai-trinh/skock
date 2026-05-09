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
2. Spend `Tech` across three tracks:
   - **Mothership upgrades** — hangar capacity, Mothership weapons/armor. Permanent; always available.
   - **Fleet doctrine** — fleet-wide passive bonuses per ship role (e.g. all Fighters +10% speed). Randomized table per jump, rerollable with `Tech`.
   - **Rare equipment** — one-off items attached to a specific ship. Randomized table, rerollable with `Tech`.

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

## Build order

1. Headless deterministic boids sim in Rust. No graphics. Two fleets fly at each other, shoot, one wins. Tick log to stdout. Test: run twice, diff output, must be byte-identical.
2. Determinism CI test. Lock in a corpus of (seed, fleetA, fleetB) battles with known result hashes. Runs on every commit forever.
3. Engine layer. Godot 4 (C#) scene that loads a battle log and renders it. Camera, ship sprites, projectile effects, victory screen.
4. Meta layer. Run map, shop, fleet builder, ship roster. All local first.
5. Local roguelite loop end-to-end. Single player, no server, full run start to finish. Playtest thoroughly — the game lives or dies here.
6. Server. Account system, fleet upload, opponent fetch. Async multiplayer dropped onto the existing single-player loop.
7. Verification. Server-side replay simulation as a background worker.
8. Polish, balance, content.
