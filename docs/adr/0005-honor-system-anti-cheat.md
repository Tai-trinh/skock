# ADR-0005: Honor-system anti-cheat with retroactive re-simulation

**Status:** Accepted

## Context

The sim is fully deterministic: given an identical seed and two fleet JSONs, any machine produces byte-identical output. This makes server-side verification mechanically cheap — no replay state to reconstruct, just run the sim binary again with the same inputs.

Two models were considered:

**Option A — Server-authoritative:** server validates every player action before applying it (commission, salvage, upgrade, reroll). Battle seeds are server-issued. The client can never act on stale or fabricated data.

**Option B — Honor system + retroactive verification:** client reports battle results and actions; server trusts by default. If the client misreports a result, the server can detect it retroactively by re-simulating the battle from stored inputs. Re-simulation triggers: anomaly detection and leaderboard threshold review. During development, all battles are re-simulated.

## Decision

Honor system (Option B).

The server stores `BattleInputs` (seed + fleet A JSON + fleet B JSON) alongside every `JumpRecord`. Retroactive re-simulation is triggered by anomaly detection heuristics and a leaderboard review pass. In development and during debugging, the server re-simulates every submitted battle.

The `IStatsStore` interface is the seam. `LocalStatsStore` (offline) captures `BattleInputs` in memory but does not transmit them. The future `ServerStatsStore` (online) POSTs `BattleInputs` to the server at battle completion for storage and eventual verification.

## Consequences

- Leaderboard manipulation is detectable after the fact, not prevented in real time.
- Server DB requires a `battle_inputs` table: `(run_id, jump_number, seed, fleet_a_json, fleet_b_json)`.
- No round-trip latency during dockyard actions or battle — fully offline gameplay experience.
- `ServerStatsStore.RecordBattle` must transmit `BattleInputs` alongside the `JumpRecord`.
- Anti-cheat coverage is probabilistic in production; complete during development.

## Trade-off rejected

Option A (server-authoritative) prevents fraud in real time but adds a network round-trip to every dockyard action (commission, salvage, reroll, upgrade, research), requires the server to be reachable during gameplay, and significantly complicates the client flow. For an async single-player roguelite the latency cost outweighs the fraud prevention benefit — the main attack surface is leaderboard fraud, which retroactive detection handles adequately.
