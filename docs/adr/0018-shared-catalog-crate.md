# ADR-0018: Ship blueprints and mothership templates live in a shared `catalog` crate

Both the `dockyard` and `admiral` binaries need ship blueprint definitions. Defining them independently in each binary's `catalog.rs` creates the same drift risk that deleted `Blueprint.cs` — two copies of the same truth that silently diverge. The solution is a single `catalog` library crate that both binaries import.

## Decision

A new `catalog` crate is added to the Cargo workspace. It owns:

- `Blueprint` struct + `blueprints()` — the full pool of purchasable ship blueprints (identical to what was in `dockyard/src/catalog.rs`)
- Weapon helper functions (`hitscan()`, `missile()`, `torpedo()`, `mine()`, `beam()`)
- `tonnage(hull_class)` — hull class to tonnage mapping
- One mothership template per faction (`mothership_template(faction_id)`) — factions have distinct motherships with different stats and drawing IDs

`dockyard` removes its own ship catalog code and imports from `catalog`. `admiral` imports from `catalog` to resolve starting blueprint IDs and faction mothership templates when building starting fleets. Admiral starting ships are referenced by blueprint ID in the admiral catalog (`admiral/src/catalog.rs`) and resolved to full `ShipDef` at offer-generation time; the binary panics at startup if any referenced ID is not found in the shared catalog.

What stays in `dockyard` and does not move: `ResearchItem`, `ResearchTrack`, `RESEARCH_ITEMS` — research is a dockyard-phase concept; the admiral binary has no use for it.

## Consequences

- Adding or rebalancing a ship blueprint is one change in one file. Both binaries pick it up automatically.
- Admiral starting ships and dockyard-offered ships are guaranteed to be the same ship — no hidden stat discrepancy between "the fighter_corvette you start with" and "the fighter_corvette you buy at jump 1."
- Faction motherships are defined once; adding a new faction requires one entry in `catalog` and one or more admiral entries in `admiral`.

## Trade-offs rejected

**`admiral` depends on `dockyard`:** rejected — `dockyard`'s session logic, protocol handling, and research catalog would become transitive dependencies of `admiral`. The dependency is in the wrong direction; only the ship data is shared, not the dockyard's business logic.

**Separate catalogs with a cross-binary test:** rejected — a test that asserts two Rust structs agree is noise compared to having one struct. "Nothing to drift" is always better than "test that nothing drifted."
