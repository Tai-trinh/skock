# ADR-0006: IStatsStore is separate from IRunStore

**Status:** Accepted

## Context

Two seams handle server interaction in online mode: `IRunStore` (run lifecycle and dockyard actions) and `IStatsStore` (battle history and lifetime counters). A single unified store was considered.

## Decision

Keep them separate. They have fundamentally different online semantics:

- **`IRunStore`** is server-authoritative. Every action (commission, salvage, reroll, upgrade) must be validated by the server before being applied to RunState. The server can reject the action outright. A single source of truth; local state is a cache of the server record.
- **`IStatsStore`** is honor-system with retroactive verification (see ADR-0005). The client reports results; the server trusts by default and re-simulates later. No round-trip required per battle. The server stores `BattleInputs` for retroactive checking, not as a prerequisite to accepting the report.

Merging them into one interface would conflate these two models, forcing every future implementor to mix authoritative validation logic with fire-and-forget reporting logic in the same class.

## Consequences

- Two swap points in `RunState._Ready()` instead of one.
- `ServerRunStore` and `ServerStatsStore` will be different classes with different HTTP verbs and error-handling contracts.
- Adding a new dockyard action requires touching only `IRunStore`; adding a new stat requires touching only `IStatsStore`.

## Trade-off rejected

A unified `IServerStore` would have one swap point but would force a single online model onto two concerns with different validation requirements. The seam would leak — either all stats go through server-authoritative round-trips (unnecessary latency) or all dockyard actions go through honor-system reporting (no server validation).
