# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Skock is a top-down 2D space fleet auto-battler roguelite. Fleets fight using boids-based movement. The core invariant is **full determinism**: given the same seed and fleet snapshots, every run of the sim produces byte-identical output. This enables local replays, Monte Carlo balance testing, and server-side anti-cheat re-simulation.

The repo is in early planning phase — no source code has been committed yet. See `CONTEXT.md` for the full design document and `docs/adr/` for architectural decisions.

## Architecture

Three components built in this order:

**Headless Sim** (`/sim`, Rust) — CLI tool. Takes a seed + two fleet JSONs, runs a 30 Hz tick-based boids simulation, writes a battle log to disk and prints the result. All game logic lives here. The determinism CI test hashes a fixed corpus of battles and asserts the hash matches on every commit.

**Game Client** (`/client`, Godot 4 + C#) — Invokes the sim as a subprocess and reads the battle log it produces. Renders the battle, then handles the meta-layer: shop, fleet builder, run map, roguelite loop.

**Server** (`/server`, Rust + axum) — Stateless REST/JSON API. Shares fleet/ship types with the sim crate. Tables: `users`, `fleets`, `battle_results`, `runs`, `leaderboards`. Re-simulates sampled battles as a background worker for anti-cheat.

## Sim ↔ client interface

The sim writes a JSON battle log. Two parallel streams:
- **State snapshots** (every tick) — full ship state (position, velocity, heading, health) for all ships and active projectiles/beams. Allows the renderer to scrub to any tick.
- **Event stream** (sparse) — one entry per meaningful occurrence (`projectile_fired`, `projectile_hit`, `beam_hit`, `mine_detonated`, etc.). Drives visual effects.

Godot calls the sim via subprocess. GDExtension (`gdext` crate) is a future option once the sim API stabilises.

## Determinism rules

Breaking determinism is a silent, catastrophic bug. Every sim decision must follow these rules:

- **Fixed-point math only** — `fixed` crate: `I32F32` for positions, `I16F16` for most else. No floats in sim code.
- **Explicit RNG state** — xoshiro256+ (`rand_xoshiro`), 4× u64 state, no globals. State is serialized into fleet snapshots for replays.
- **Ordered containers only** — `BTreeMap`, `BTreeSet`, or arrays indexed by stable ID. Never `HashMap` or `HashSet` in sim code (non-deterministic iteration order). See ADR-0001.
- **Single-threaded sim** — no parallelism inside a battle tick.

`HashMap`/`HashSet` are fine in server and client code where determinism is not required.

## Key domain decisions

- **Win condition** — destroy the enemy Mothership. The Mothership is a combat entity with beam weapons and point defense; losing yours ends the run immediately.
- **Fleet cap** — Mothership hangar capacity (tonnage). Upgrading it costs `Tech`.
- **Ship persistence** — no combat permadeath. Ships survive at 1 HP minimum. Damaged ships have proportionally degraded stats. Only the player can remove a ship by salvaging it. Salvage yield scales with current HP.
- **Healing** — ships auto-heal to full between jumps for free. Skipping a heal yields `Salvage` instead.
- **Currencies** — `Salvage` (from destroyed enemies and optional skipped heals; spent on building ships) and `Tech` (from victories; spent on Mothership upgrades, fleet doctrine, rare equipment).
- **Weapon archetypes** — hitscan (instant), projectile (sim entity: missiles, torpedoes, mines), beam/ray (duration-based, hits multiple targets in a line).
- **Boids forces** — weighted sum: `separation`, `cohesion`, `alignment`, `seek_enemy`, `maintain_range`. Ship roles are enum labels derived from weight profiles + primary weapon type, not hard-coded behavior.
