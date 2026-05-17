# ADR-0002: Fixed-point arithmetic for all sim values

**Status:** Accepted

## Context

The sim must produce byte-identical output for the same seed on any machine. Floating-point arithmetic (IEEE 754) is not guaranteed to be deterministic across platforms, compiler versions, or optimization levels — FPU rounding modes, SSE vs. x87, and auto-vectorization can all produce subtly different results for the same computation.

Two models were considered:

**Option A — Fixed-point arithmetic:** use the `fixed` crate (`I32F32`, `I16F16`). All arithmetic is integer arithmetic under the hood — results are identical on any platform that implements two's complement integers (every modern target).

**Option B — Floats with strict controls:** use `f32`/`f64` but compile with `-C target-feature=+sse2`, disable fast-math (`-ffast-math` equivalent), and assert consistent FPU state at sim entry. This is the approach taken by some game engines (e.g. lockstep RTS games on known hardware targets).

## Decision

Fixed-point (Option A).

`I32F32` for positions — 32-bit integer part needed for the 1000 × 1000 battlefield with room to grow. `I16F16` for everything else (velocity, acceleration, heading, HP, damage, armor, boid weights) — ±32 767 range covers all plausible game values, and 4 bytes per field halves the per-field cost in state snapshots vs. `I32F32`.

## Consequences

- Sim code contains no `f32` or `f64`. Any float that appears is a bug.
- Range ceiling is a hard constraint: `I16F16` overflows above ±32 767. Weapon damage, HP totals, and velocity values must stay within this range by design. Positions use `I32F32` to avoid this constraint on the battlefield.
- Fixed-point is less ergonomic than floats — no standard math library, transcendental functions (sin, cos for heading) must use fixed-point approximations.
- Client and server code may use floats freely — the constraint applies only inside the sim tick loop.

## Trade-off rejected

Option B (controlled floats) requires trusting that the compiler, platform, and runtime never introduce a divergence — a hard guarantee to maintain across Rust toolchain upgrades, CI runners, and future server anti-cheat re-simulation environments. A single `-C opt-level` change could silently break determinism. Fixed-point makes the guarantee structural: integer arithmetic is deterministic by definition.
