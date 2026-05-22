# ADR-0019: Encounter fleet data moves to catalog; served by skock-encounter binary

**Status:** Accepted

Opponent fleet JSON files (`jump_1.json`–`jump_8.json`) previously lived in `client/data/opponents/` and were read directly from disk by `BattleRenderer.InitBattleAsync()`. This approach cannot extend to the online case, where the encounter fleet is a real player fleet selected by the server based on win/loss ratio.

## Decision

A new `skock-encounter` binary serves encounter fleets. It is a **one-shot** binary (same pattern as `skock-admiral`): C# spawns it, writes one JSON line to stdin, reads one JSON line (the fleet JSON) from stdout, and the binary exits.

**Protocol:**

```
C# → stdin:   { "player_id": "...", "run_number": 3, "losses": 1, "wins": 2 }
binary → stdout: <FleetJsonData JSON>
```

The binary is a new `encounter/` crate in the workspace. Fleet definitions are embedded at compile time via `include_str!()` from `catalog/data/opponents/jump_N.json`. Local selection is keyed solely on `run_number`; `player_id`, `losses`, and `wins` are accepted now and will drive server-side matchmaking when the online backend exists.

A new `IEncounterSource` / `LocalEncounterAdapter` seam (same pattern as `IAdmiralSelection` / `LocalAdmiralAdapter`) wraps the binary call. `RunState` owns the `IEncounterSource` instance (injected in `_Ready()`). `BeginBattle()` becomes `BeginBattleAsync()` and fetches the encounter fleet internally before starting the battle. `BattleRenderer` no longer touches opponent fleet files.

<!-- TODO: switch wire format from JSON to MessagePack in production (all three binaries: skock-admiral, skock-dockyard, skock-encounter). Use a runtime --format flag defaulting to json; production build passes --format msgpack. C# adapters read a shared config value. Requires rmp-serde (Rust) and MessagePack-CSharp (C#). -->

## Consequences

- `client/data/opponents/jump_1.json`–`jump_8.json` are deleted from the client.
- `RunState.GetOpponentFleetPath()` is deleted.
- `BattleRenderer.InitBattleAsync()` no longer reads any fleet file; the opponent fleet is already set on `RunState.CurrentOpponentFleet` when the renderer starts.
- `ServerEncounterAdapter` (future) hits the same endpoint server-side; `IEncounterSource` is the seam.

## Trade-offs rejected

**Keep JSON files in client/data/opponents/:** simple but a dead end — file paths cannot be replaced by a server response without a seam. Adding the seam later requires the same migration cost.

**Fold into skock-admiral:** rejected because admiral selection (run start, one call per run) and encounter selection (pre-battle, one call per jump) are distinct operations with different inputs and different call sites. Merging them complicates the protocol for no gain.

**Embed fleet data as inline Rust structs (like blueprints()):** rejected because the fleet JSON is the authoring format and already stable. `include_str!()` keeps the files editable as JSON without Rust recompilation and avoids a verbose hand-transcription of complex nested structs.
