# ADR-0017: Admiral selection is a dedicated Rust binary; run seed originates there

**Status:** Accepted
**Supersedes:** ADR-0006

Admiral selection was previously C#-side: `admirals.json` and `factions.json` lived in `client/data/`, `LocalAdmiralStore` read them, and `AdmiralEffectsRegistry` held C# lambdas for stat bonuses. ADR-0006 deferred the migration of admiral effects to the `admiral_effects` fleet JSON field until the sim's data-driven effect interpreter existed. That interpreter now exists; this ADR completes the migration and moves admiral data fully into Rust.

## Decision

A new `skock-admiral` binary handles admiral selection. It is a **one-shot** binary (not a session): C# spawns it, writes one JSON line to stdin, reads one JSON line from stdout, and the binary exits.

**Protocol:**

```
C# → stdin:   { "player_id": "offline:..." }
binary → stdout: { "run_seed": <u64>, "offers": [ <AdmiralOffer>, <AdmiralOffer>, <AdmiralOffer> ] }
```

The binary generates `run_seed` from OS entropy, seeds xoshiro256+ from it, and uses that RNG to select the 3 admiral offers deterministically. Given the same seed the binary always produces the same offers — `run_seed` is the handle for run reproducibility.

Each offer is one admiral drawn from a distinct faction. The catalog is hardcoded in Rust (`admiral/src/catalog.rs`). The binary errors at startup if the catalog cannot fill 3 distinct-faction slots.

Each `AdmiralOffer` contains the full `starting_fleet` as a resolved `FleetJsonData` with `admiral_effects` and `faction_effects` already populated from the admiral's and faction's effect definitions — the sim applies them at battle start via the shared effect vocabulary. Stat bonuses are not baked into ship stats.

C# stores `run_seed` when the player selects an admiral and passes it to all subsequent binaries (dockyard, sim). The run seed is the first and only use of OS entropy per run; everything downstream is deterministic from it.

## Consequences

- `client/data/admirals.json`, `client/data/factions.json`, `IAdmiralStore`, `LocalAdmiralStore`, `AdmiralEffectsRegistry`, and the dead `ShipEffects` field on `Admiral` are all deleted.
- A new `IAdmiralSelection` / `LocalAdmiralAdapter` seam (same pattern as `IDockyard` / `LocalDockyardAdapter`) wraps the binary call. `ServerAdmiralAdapter` (future) hits the same binary server-side.
- `Random.Shared.NextInt64()` in `LocalRunStore.StartRun()` is deleted — C# no longer generates any game-meaningful random state.
- The display-only `BonusText` field on admiral offers is populated by the binary; the C# UI renders it directly.

## Trade-offs rejected

**Session-based protocol (like dockyard):** rejected because admiral selection has no ongoing state — no buy/sell/reroll actions. One-shot is sufficient and simpler.

**C#-side catalog with a sync test:** rejected per ADR-0009 (Rust is the single source of truth). Eliminated the same way as Blueprint.cs — the binary returns the data C# needs; no C#-side catalog to drift.

**Per-admiral seed (each offer has its own run seed):** rejected. The run seed is the fate of the run, independent of which admiral the player picks. One seed drives offer selection AND all downstream randomness.
