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
- **Store swapping**: `RunState._Ready()` constructs `LocalRunStore`, `LocalAdmiralStore`, `LocalStatsStore`. Each has a one-line swap comment for online adapters.
