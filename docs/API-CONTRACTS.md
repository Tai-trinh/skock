Wire formats and binary protocols for Skock's inter-process interfaces.

## Dockyard binary protocol

The `skock-dockyard` binary runs for the duration of one dockyard visit. The pipe stays open; C# and the binary exchange newline-delimited JSON messages (one JSON object per line).

**Session open** — C# sends `{ "action": "get_offers", "player_id": "...", "run_seed": u64, "jump_number": u32, "tier_rerolls": [u32;4], "research_rerolls": [u32;4], "salvage": i32, "tech": i32, "hangar_used": i32, "hangar_cap": i32, "fleet": [ { "index": u, "tonnage": i32 } ], "upgrade_purchases": { "id": count } }`. Binary responds with the full offer set and current resource state.

**Actions** (each line → one response line):
- `{ "action": "commission", "blueprint_id": "..." }` — buy a ship from the current offer. Binary validates offer membership and affordability. Response includes the full `ship_def` for the client to add to its fleet.
- `{ "action": "salvage_fleet_ship", "index": u }` — salvage a ship at the given position in the session fleet. Binary returns yield (`tonnage × 3`) and updated resources.
- `{ "action": "reroll_tier", "tier_index": u }` — reroll one ship tier, costs Salvage. Response includes updated tier offer.
- `{ "action": "reroll_research", "track_index": u }` — reroll one research track, costs Tech. Response includes updated track.
- `{ "action": "buy_research", "upgrade_id": "..." }` — purchase a research item. Binary validates offer, affordability, and max-purchase cap.

**Session close** — `{ "action": "shopping_done" }`. Binary returns `"delta"`: `{ "salvage_final", "tech_final", "hangar_used_final", "tier_rerolls_final", "research_rerolls_final", "ships_commissioned": ["id"...], "ships_salvaged": [original_index...], "upgrades_purchased": ["id"...] }`. C# applies the delta to `RunState` and saves.

**Errors** — non-fatal action errors return `{ "ok": false, "error": "not_in_offer" | "cannot_afford" | "maxed" | "invalid_index" | ... }`. Fatal errors (invalid JSON, session not started) go to stderr as `RESULT:{"error":"...", "message":"..."}` and the binary exits.

**Determinism** — offer generation is a pure function of `(run_seed, jump_number, tier_rerolls, research_rerolls, player_unlocks)`. Given identical inputs, any machine produces identical offers. C# seam: `IDockyard` / `LocalDockyardAdapter` (offline subprocess) / `ServerDockyardAdapter` (online, TODO).

## Fleet JSON specification

The sim CLI takes a seed as a CLI argument, two fleet JSON files, and an optional sim config: `skock-sim --seed <u64> fleet_a.json fleet_b.json [--config sim_config.json]`.

`sim_config.json` holds global tuning knobs — attrition start tick, attrition rate, boid force caps, tick rate, battlefield bounds. In development, loaded from disk via `--config`. In production, embedded at compile time via `include_str!` — the `--config` flag overrides the embedded config for local testing. No runtime file dependency in the shipped binary.

**Hull hit shapes** — compound circle hitboxes per hull class. Offsets are in ship-local space (x = forward axis, y = lateral) and rotate with `ship.heading`. See ADR-0014.
```json
"hull_hit_shapes": {
  "Corvette":      [{ "ox": 0,   "oy": 0, "r": 5  }],
  "Frigate":       [{ "ox": 0,   "oy": 0, "r": 7  }],
  "Destroyer":     [{ "ox": 0,   "oy": 0, "r": 8  }],
  "Cruiser":       [{ "ox": 0,   "oy": 0, "r": 9  }],
  "Battlecruiser": [{ "ox": -10, "oy": 0, "r": 8  }, { "ox": 10, "oy": 0, "r": 8  }],
  "Dreadnought":   [{ "ox": -8,  "oy": 0, "r": 12 }, { "ox": 8,  "oy": 0, "r": 12 }],
  "Mothership":    [{ "ox": 0,   "oy": 0, "r": 18 }]
}
```

**Projectile hit radii** — default collision radius per projectile subtype. Overridable per weapon block via `"hit_radius": N`. See ADR-0013.
```json
"projectile_hit_radii": {
  "seeking_missile": 1,
  "torpedo":         4,
  "drifting_bomb":   6
}
```

Mine/bomb proximity trigger and explosion radius damage both use ship center only (not hull hit shapes) — explosion radii are large enough that the approximation is negligible.

Fleet JSON top-level structure:
```json
{
  "faction": "gallforce",
  "admiral_id": "commander_yuki",
  "formation": "wedge",
  "mothership": { ...ship... },
  "ships": [ ...ship... ],
  "doctrines": [ ... ],
  "role_equipment": [ ... ],
  "faction_effects": [ ... ],
  "admiral_effects": [ ... ]
}
```

`formation` controls spawn layout. Mothership always anchors back-center regardless of formation type. Formations are inspired by historical naval tactics. Initial formation: `wedge` only. Candidates for future additions: `line_of_battle` (broadside line), `echelon` (diagonal stagger), `encirclement` (flanking wings), `defensive_circle` (ships orbiting the Mothership). TODO: add formations once wedge is playtested and the formation system is proven.

Wedge ordering front to back by hull class: `Dreadnought` → `Corvette` → `Frigate` → `Destroyer` → `Cruiser` → `Battlecruiser` → `Mothership`. Dreadnoughts absorb first contact at the tip; Battlecruisers anchor near the Mothership; Mothership at the rear.

All stats are fully resolved — the sim performs no blueprint lookups. Each ship object:
```json
{
  "blueprint_drawing_id": "missile_destroyer_mk1",
  "hull_class": "Destroyer",
  "role": "Missile",
  "weight": "Heavy",
  "hp": 120,
  "max_hp": 120,
  "speed": 5,
  "acceleration": 2,
  "turn_rate": 0.8,
  "boid_weights": {
    "separation": 1.5,
    "cohesion": 0.8,
    "alignment": 0.6,
    "seek_enemy": 2.0,
    "maintain_range": 1.2
  },
  "armor": 0.10,
  "shield_hp": 80,
  "shield_max_hp": 80,
  "shield_recharge_rate": 2,
  "weapon": { ... },
  "equipment": [ ... ]
}
```

`blueprint_drawing_id` is opaque to the sim — passed through into battle log state snapshots so the renderer knows which sprite to use. The Mothership follows the same structure but is always present and unique.

Three tiers of fleet bonuses — all use the same data-driven effects vocabulary, distinction is a shop/acquisition concept only:

- **Doctrines** (`doctrines` array) — role/fleet-scoped stat bonuses, randomized table per jump, rerollable with `Tech`.
- **Role equipment** (`role_equipment` array) — rarer, higher-magnitude role-scoped items. Same JSON shape as doctrines. Separate shop track.
- **Ship equipment** (`equipment` on ship) — unique per-ship items. Only Mothership and `Battlecruiser` or `Dreadnought` hull class ships have slots; all others have `"equipment": []` and cannot be equipped.

To the sim all three are just lists of effects to apply — the tiers are invisible at the sim layer.

The `weapon` block inside a ship:
```json
"weapon": {
  "type": "projectile",
  "subtype": "seeking_missile",
  "damage": 40,
  "range": 200,
  "cooldown_ticks": 60,
  "turn_rate": 0.4,
  "fuse_ticks": 90,
  "ammo": 12,
  "crit_chance": 0.10,
  "crit_damage": 2.0,
  "explosion_radius": 80,
  "explosion_damage": 60
}
```

`crit_chance` (0.0–1.0) and `crit_damage` (multiplier, e.g. 2.0 = double damage) are optional — omitted means no crits. Crit roll uses the battle RNG (xoshiro256+) so results are deterministic. `ammo` is optional — omitted means unlimited. When present, the weapon can only fire that many times before it is exhausted. `turn_rate` and `fuse_ticks` apply to missiles only; beams add `charge_ticks` and `duration_ticks`; fields irrelevant to the weapon type are omitted.

Doctrines and equipment use a shared data-driven effects vocabulary. The sim understands each effect type by name and applies it — no per-item hardcoded logic. New items that use existing effect types require no sim code changes.

Doctrine entry (supports upsides and downsides via multiple effects):
```json
{
  "id": "glass_cannon",
  "effects": [
    { "scope": { "role": "Fighter" },         "type": "stat_modifier", "stat": "damage", "modifier_type": "more",      "modifier": 1.30 },
    { "scope": { "hull_class": "Destroyer" }, "type": "stat_modifier", "stat": "hp",     "modifier_type": "increased", "modifier": -0.30 },
    { "scope": null,                          "type": "stat_modifier", "stat": "speed",  "modifier_type": "increased", "modifier": 0.05 }
  ]
}
```
`scope` targets which ships the effect applies to — by `role`, `hull_class`, `weight`, or any combination. `null` scope = fleet-wide. Both `role` and `hull_class` may be specified together to target e.g. only `Missile Destroyers`.

Equipment entry (ship-level, supports passive and active effects):
```json
{
  "id": "repair_nanobots",
  "effects": [
    { "type": "hp_regen", "value": 2 }
  ]
}
```

Known effect types:
- `stat_modifier` — modifies a stat. Two stacking modes via `modifier_type`:
  - `"increased"` / `"decreased"` — additive: all bonuses on the same stat sum together, then applied as `base * (1 + total)`. Negative `modifier` values are decreases (e.g. `-0.30` = 30% decreased).
  - `"more"` / `"less"` — multiplicative: each bonus multiplies the running total independently. Sub-1.0 values are less (e.g. `0.70` = 30% less).
  - Final formula: `base * (1 + Σ increased/decreased) * Π more/less`
  - Example: `{ "type": "stat_modifier", "stat": "speed", "modifier_type": "more", "modifier": 1.2 }`
- `hp_regen` — restores HP per tick: `{ "type": "hp_regen", "value": 2 }`
- `damage_reduction` — reduces all incoming damage by a fraction: `{ "type": "damage_reduction", "value": 0.15 }`

Both `armor` and shield fields are optional — omitted means no armor or shields. `armor` is a damage reduction fraction (0.10 = 10% of incoming damage absorbed). Shields are a separate HP pool depleted before hull HP. `shield_recharge_rate` is HP restored to shields per tick (optional, omitted means shields do not recharge). Hull HP does not regenerate by default — revisit if playtesting reveals a need.

TODO: revisit ship stat block — more fields likely needed once boid tuning and combat balancing begins (e.g. weapon range, point-defense radius, tonnage, shield regen rate).
TODO: revisit weapon block — more fields may be needed (e.g. splash radius for mines/torpedoes, beam width, projectile speed).
TODO: revisit proc-based effects (`on_hit_received`, `on_kill`, `on_low_hp`, etc.) once sim code has enough flesh to reason about the trigger/effect pipeline concretely.

## Sim ↔ client interface

**Transport:** sim writes MessagePack log bytes to stdout; Godot reads subprocess stdout after process exits. Debug flag (`--debug`) writes JSON to disk instead for desync investigation.

**Battle result:** single line on stderr, prefixed with `RESULT:` sentinel, on completion:
```
RESULT:{"winner": "fleet_a", "ticks": 2134, "reason": "mothership_destroyed", "fleet_a_survivors": [{"blueprint_drawing_id": "...", "hp": 45}], "fleet_b_survivors": [...]}
```
Reasons: `mothership_destroyed`, `timeout_draw`. `fleet_a_survivors` and `fleet_b_survivors` list final HP for every ship that was alive at battle end — ships that reached 0 HP mid-battle are absent (treated as 1 HP by the client when restoring the fleet). Always written regardless of `--debug` flag.

**Format:** MessagePack (`rmp-serde` in Rust, `MessagePack-CSharp` in Godot) in production; JSON on disk in debug. Same data model — switching is a flag not a rewrite.

**Schema:** the battle log opens with a header record containing a `schema_version` integer. Client rejects logs where the version doesn't match what it understands. Followed by one MessagePack record per tick, interleaved:
```
{ tick, ships: [...], projectiles: [...], beams: [...], events: [...] }
```
State snapshot and events for the same tick are one record. Godot builds the scrub index and event list in a single pass. No framing needed beyond MessagePack's own length encoding.

**Render loop:** interpolated. Renderer runs at display framerate. Each frame computes `t ∈ [0,1]` between the two nearest sim ticks and lerps ship positions and headings. Events fire when `t` crosses a tick boundary. State snapshots allow seeking to any tick directly without replaying from tick 0.

**Playback controls:** play + speed control (1×, 2×, 4×). No scrubbing in initial build. TODO: add scrubbing if players request it.

**Debug overlay** *(toggled by key during battle playback)*: displays raw tick state for a selected ship — position, velocity, HP, shields, active status effects, boid weights, current target, weapon cooldown. Reads directly from the in-memory battle log at the current playback tick. Built first — validates log correctness during sim and renderer development. TODO: add player-facing fleet stats panel (damage dealt/received, ships destroyed, weapons fired) once debug overlay confirms log data is correct.

TODO: validate schema against renderer needs once the engine layer is being built.
