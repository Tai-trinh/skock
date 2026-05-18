# ADR-0009: Rust is the single source of truth for game rules; C#/Godot caches

**Status:** Accepted

## Context

The original dockyard implementation split game rules across two layers: offer generation lived in `DockUi.cs` (UI layer, wrong abstraction level), purchase validation lived in `LocalRunStore`, and the randomisation used `System.Random` — not platform-guaranteed deterministic across machines. The research track showed every upgrade every time rather than a randomised table. As the server is built, the same rules would need to be re-implemented in server-side C# or Rust — a duplication that creates divergence risk and a second surface for bugs.

The sim already demonstrated the right model: all battle rules live in the Rust binary; C# is a display and state-application layer only.

## Decision

Rust is the single source of truth for all game rules. Every rule that governs what is valid — what can be offered, whether a purchase is legal, what it costs, what the yield is — lives in a Rust binary or library. C# invokes that binary, caches the results for display, and applies the final delta to local state.

A new `skock-dockyard` binary implements this for the dockyard phase:

- Deterministic offer generation (same seed + jump + reroll counts + player unlocks → identical offers on any machine).
- Session-based stdin/stdout JSON protocol. The pipe stays open for the full dockyard visit. The binary holds session state (current offer set, running resource balances). Actions: `get_offers`, `commission`, `salvage_fleet_ship`, `reroll_tier`, `reroll_research`, `buy_research`, `shopping_done`.
- `get_offers` receives the full session context: player ID, run seed, jump number, tier and research reroll counts, current Salvage, Tech, hangar state, and fleet roster.
- `shopping_done` returns the session delta: ships commissioned and salvaged, resources spent and gained, final balances. C# applies the delta to `RunState`.
- Errors follow the same `RESULT:{"error": ...}` sentinel protocol as the sim, written to stderr.

The `IDockyard` C# interface is the seam. `LocalDockyardAdapter` (offline) spawns the Rust binary as a subprocess. `ServerDockyardAdapter` (online, future) talks to the server, which runs the same Rust logic server-side — the interface is identical to the client.

`IRunStore` retains run lifecycle only: `Load`, `Save`, `DeleteSave`, `StartRun`, `GetBattleSeed`. All dockyard-phase actions move to `IDockyard`.

## Consequences

- Dockyard phase is fully deterministic. A player can replay any run's dockyard offers from the run seed alone.
- No rule duplication between client and server. The server runs the same `skock-dockyard` binary (or links the same crate) — one implementation.
- C# is display and state-application only for the dockyard phase. It cannot drift from the Rust rules.
- Player identity (PlayerID) is introduced as a first-class input to the binary. In offline mode a stable local UUID is generated and stored. In online mode the platform ID (Steam, Apple, etc.) is used. The binary uses the PlayerID to gate metaprogression-unlocked content — the lookup mechanism for offline mode is deferred.
- `SalvageShip` moves from `IRunStore` to `IDockyard`: salvage yield (`tonnage × 3`) is a game rule and must be enforced by Rust.
- `System.Random`-based offer generation in `DockUi.cs` is deleted. `TierRerolls` save-state field is retained; `ResearchRerolls[4]` is added to track rerolls per research track.

## Trade-off rejected

Keeping game rules in C# with a future server re-implementation. This would require writing and maintaining the same logic twice (C# client + Rust server), with no compile-time guarantee they agree. Every rule change becomes two PRs. The dockyard would remain non-deterministic across platforms. The approach was viable for the first prototype but does not scale to the online design.
