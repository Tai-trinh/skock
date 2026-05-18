# ADR-0004: IStatsStore is separate from IRunStore

**Status:** Accepted

## Context

Three seams handle server interaction in online mode: `IRunStore` (run lifecycle), `IDockyard` (dockyard session actions), and `IStatsStore` (battle history and lifetime counters). A single unified store was considered.

## Decision

Keep them separate. Each interface has a distinct online contract:

- **`IRunStore`** — run lifecycle only: `Load`, `Save`, `DeleteSave`, `StartRun`, `GetBattleSeed`. Server-authoritative for lifecycle events; local state is a cache of the server record.
- **`IDockyard`** — dockyard session: `GetOffersAsync`, `CommissionAsync`, `SalvageFleetShipAsync`, `RerollTierAsync`, `RerollResearchAsync`, `BuyResearchAsync`, `ShoppingDoneAsync`. Server-authoritative per action — the Rust binary (or server-side equivalent) validates every purchase before applying it. See ADR-0009.
- **`IStatsStore`** — honor-system with retroactive verification (see ADR-0003). The client reports results; the server trusts by default and re-simulates later. No round-trip required per battle.

Merging any two of these would conflate interfaces with different validation models, forcing every future implementor to mix authoritative validation logic with fire-and-forget reporting logic in the same class.

## Consequences

- Three swap points in `RunState._Ready()` instead of one.
- `ServerRunStore`, `ServerDockyardAdapter`, and `ServerStatsStore` will be different classes with different HTTP verbs and error-handling contracts.
- Adding a new dockyard action requires touching only `IDockyard`; adding a new lifecycle event touches only `IRunStore`; adding a new stat touches only `IStatsStore`.

## Trade-off rejected

A unified `IServerStore` would have one swap point but would force a single online model onto three concerns with different validation requirements. The seam would leak — either all stats go through server-authoritative round-trips (unnecessary latency) or all dockyard actions go through honor-system reporting (no server validation).
