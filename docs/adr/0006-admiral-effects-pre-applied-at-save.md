# ADR-0006: Admiral ship effects are pre-applied when writing fleet JSON for the sim

**Status:** Accepted

## Context

Admiral bonuses (e.g. "all Fighters +15% speed") must reach the sim. Two application points were considered:

**Option A — Pre-apply at save time:** `LocalRunStore.BuildFleetForSim()` clones each ship, walks the admiral's `ShipEffects`, and writes the modified stats into the fleet JSON passed to the sim. The sim sees final resolved numbers.

**Option B — Sim-side apply:** admiral effects are included in the fleet JSON as an `admiral_effects` array (same data-driven vocabulary as doctrines). The sim applies them at battle start, same as doctrines.

## Decision

Pre-apply at save time (Option A) for now, because the data-driven effect interpreter does not exist yet. `AdmiralEffectsRegistry` holds the effects as C# lambdas. Applying them in `BuildFleetForSim()` before writing the JSON keeps the sim free of any C# logic dependency.

**Planned migration:** once the shared effect vocabulary interpreter is implemented (CONTEXT.md — `stat_modifier`, `hp_regen`, etc.), admiral effects should move to the `admiral_effects` array in the fleet JSON (Option B). The registry lambdas and `BuildFleetForSim()` effect-application loop are then deleted.

## Consequences

- The fleet JSON on disk contains modified (admiral-boosted) stats. The shop/inspector displays base stats from `RunState.Fleet` (unmodified). A ship shown at 8 speed in the shop may fight at 9.2 speed — this discrepancy is intentional and noted in `FleetInspector` comments.
- Admiral effects are invisible to the Fleet Inspector's ship roster rows; only `BonusText` is shown as a label.
- The sim binary does not need to know about admiral IDs or the effect registry — it operates on resolved numbers only.
- `AdmiralEffectsRegistry` is the migration target: when replaced with data-driven effects, delete this file and update `BuildFleetForSim()`.

## Trade-off rejected

Option B (sim-side apply) is the correct long-term design — it makes admiral effects visible to the sim's event stream and keeps the JSON truthful. Rejected now because building the effect interpreter before the core sim is playtested violates the build order (CONTEXT.md § Build order: no new mechanics before the loop is end-to-end).
