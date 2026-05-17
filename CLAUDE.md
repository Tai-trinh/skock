# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Skock is a top-down 2D space fleet auto-battler roguelite. Fleets fight using boids-based movement. The core invariant is **full determinism**: given the same seed and fleet snapshots, every run of the sim produces byte-identical output. This enables local replays, Monte Carlo balance testing, and server-side anti-cheat re-simulation.

See `CONTEXT.md` for the full design document, `docs/adr/` for architectural decisions, and `docs/STANDARDS.md` for code style rules.

## Commands

This is a WSL2 environment targeting Windows binaries — use the `.exe` suffixed tools.

**Rust (sim):**
```
cargo.exe build -p sim --release      # build sim binary
cargo.exe test                         # all Rust tests
cargo.exe test --test determinism      # determinism golden-hash tests only
cargo.exe test -p sim <test_name>      # single test by name
cargo.exe fmt                          # format Rust
cargo.exe fmt --check                  # lint-only (CI)
```

**C# (client + tests):**
```
dotnet.exe build client/skock.csproj                    # type-check client (no Godot runtime)
dotnet.exe test client.tests/skock.tests.csproj         # all C# tests
dotnet.exe test client.tests/skock.tests.csproj --filter "FullyQualifiedName~<TestName>"
csharpier.exe format client/                            # format C#
csharpier.exe check client/                             # lint-only (CI)
```

**Convenience targets (`make`):**
```
make det            # cargo test --test determinism
make fmt            # cargo fmt + csharpier format
make fmt-check      # cargo fmt --check + csharpier check
make win-all        # fmt + build sim + build godot + test rust + test C#
```

**Before committing:** compiles with no warnings → determinism tests pass (`make det`) → no new bare `unwrap()` in sim code.

## Architecture

Three components built in this order:

**Headless Sim** (`/sim`, Rust) — CLI tool. Takes a seed + two fleet JSONs, runs a 30 Hz tick-based boids simulation, writes a battle log to stdout (MessagePack) and prints the result JSON to stderr. All game logic lives here.

**Shared types** (`/types`, Rust) — `FleetJson` and related structs shared between `sim` and the planned server. Neither component should redefine these types.

**Game Client** (`/client`, Godot 4 + C#) — Invokes the sim as a subprocess and reads the battle log it produces. Renders the battle, then handles the meta-layer: shop, fleet builder, run map, roguelite loop.

**Server** (`/server`, Rust + axum, not yet built) — Stateless REST/JSON API. Shares types with the `types` crate. Re-simulates sampled battles as a background worker for anti-cheat.

## Sim ↔ client interface

**Transport:** sim writes MessagePack bytes to stdout; Godot reads subprocess stdout after process exits. Battle result/status go to stderr as `RESULT:{...json}`. `--debug` flag writes JSON to disk instead. At 256 entities: ~40 MB MessagePack / ~100 MB JSON — JSON too large for production.

Two parallel streams:
- **State snapshots** (every tick) — full ship state (position, velocity, heading, health) for all ships and active projectiles/beams. Allows the renderer to scrub to any tick.
- **Event stream** (sparse) — one entry per meaningful occurrence (`projectile_fired`, `projectile_hit`, `beam_hit`, `mine_detonated`, etc.). Drives visual effects.

Godot calls the sim via subprocess. GDExtension (`gdext` crate) is a future option once the sim API stabilises.

## Client code structure

```
src/sim/    — subprocess wrapper, battle log parser, playback state
src/meta/   — RunState (autoload singleton), shop logic, admiral definitions, dockyard
src/ui/     — pure UI nodes; read from meta/ via signals, never own state
src/rendering/ — BattleRenderer, ShipNode, effect triggers
data/       — admirals.json, factions.json; loaded at startup, accessible as res://data/
```

**UI data flow:** one-way. `RunState` (autoload singleton in `src/meta/`) owns all run state. UI nodes read from it; player actions call methods on it. No UI node owns state.

**DI seam for testing:** `LocalRunStore` depends on `IRunData` (not `RunState`), so C# tests can use `FakeRunData` without touching the Godot runtime. Test fakes live in `client.tests/Fakes/`. Anything with `using Godot` cannot be unit-tested outside the editor.

**Store swapping:** `RunState._Ready()` constructs `LocalRunStore`, `LocalAdmiralStore`, `LocalStatsStore`. Each has a comment marking the one-line swap point for online adapters.

## Determinism rules

Breaking determinism is a silent, catastrophic bug. Every sim decision must follow these rules:

- **Fixed-point math only** — `fixed` crate: `I32F32` for positions, `I16F16` for most else. No floats in sim code.
- **Explicit RNG state** — xoshiro256+ (`rand_xoshiro`), 4× u64 state, no globals. State is serialized into fleet snapshots for replays.
- **Ordered containers only** — `BTreeMap`, `BTreeSet`, or arrays indexed by stable ID. Never `HashMap` or `HashSet` in sim code (non-deterministic iteration order). See ADR-0001.
- **Single-threaded sim** — no parallelism inside a battle tick.

`HashMap`/`HashSet` are fine in server and client code.

## Key domain decisions

- **Win condition** — destroy the enemy Mothership. The Mothership is a combat entity with beam weapons and point defense; losing yours ends the run immediately.
- **Fleet cap** — Mothership hangar capacity (tonnage). Upgrading it costs `Tech`.
- **Ship persistence** — no combat permadeath. Ships survive at 1 HP minimum. Damaged ships have proportionally degraded stats. Only the player can remove a ship by salvaging it. Salvage yield scales with current HP.
- **Healing** — ships auto-heal to full between jumps for free. Skipping a heal yields `Salvage` instead.
- **Currencies** — `Salvage` (from destroyed enemies and optional skipped heals; spent on building ships) and `Tech` (from victories; spent on Mothership upgrades, fleet doctrine, rare equipment).
- **Weapon archetypes** — hitscan (instant), projectile (sim entity: missiles, torpedoes, mines), beam/ray (duration-based, hits multiple targets in a line).
- **Boids forces** — weighted sum: `separation`, `cohesion`, `alignment`, `seek_enemy`, `maintain_range`. Ship roles are enum labels derived from weight profiles + primary weapon type, not hard-coded behavior.
