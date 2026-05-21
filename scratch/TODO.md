# Deferred TODOs

Low-priority design notes and balance questions. Not coding context — move here to keep instruction files lean.

## Game loop

- Consider a "rematch same opponent / draw fresh" player choice if hard-walls on a specific jump prove frustrating in playtesting.

## Combat / weapons

- Define weapon stats (damage, range, cooldown, missile turn rate, beam charge time, point-defense priority rules) once combat feel is playtested.
- Revisit ship roles — add more archetypes and flesh out Capital ship stats once combat feel is playtested.
- Revisit Dreadnought weapon stats and role balance once playtested.
- Revisit ship roles — add Shield projector (support ship that extends shields to nearby friendlies) once core roles are playtested.
- Revisit targeting logic after playtesting — nearest enemy may produce boring behaviour at scale.
- Consider damage falloff by distance for explosion radius once basic explosions are playtested.

## Boids

- Define weight profiles per ship role.
- Revisit boid forces — more forces likely needed once combat feel is playtested.

## Playback / debug UI

- Add scrubbing to battle playback if players request it (currently play + speed control only).
- Add player-facing fleet stats panel (damage dealt/received, ships destroyed, weapons fired) once debug overlay confirms log data is correct.
- Validate battle log schema against renderer needs once the engine layer is being built.

## VFX / rendering polish

- Add free camera if players request it (currently fixed, auto-fits battlefield bounds).
- Tune ship trail position count and width per hull class (currently 30 positions, uniform width).
- Add bright white leading edge to hitscan line for visual pop.
- Add glow/bloom and charge-up particle effect to beams.
- Wire projectile and beam rendering to `tick.Projectiles[]` / `tick.Beams[]` in the client renderer (sim already outputs these).

## API / contracts

- Revisit ship stat block — more fields likely needed once boid tuning and combat balancing begins (e.g. weapon range, point-defense radius, tonnage, shield regen rate).
- Revisit weapon block — more fields may be needed (e.g. splash radius for mines/torpedoes, beam width, projectile speed).
- Revisit proc-based effects (`on_hit_received`, `on_kill`, `on_low_hp`, etc.) once sim code has enough flesh to reason about the trigger/effect pipeline.
- Add formations once wedge is playtested and the formation system is proven. Candidates: `line_of_battle` (broadside line), `echelon` (diagonal stagger), `encirclement` (flanking wings), `defensive_circle` (ships orbiting the Mothership).

## Economy / shop

- Shop economy needs playtesting — reroll costs, Tech drop rates, doctrine bonus magnitudes, rare equipment pool.
- Tune Salvage payout multipliers (`JumpNumber × 10` base, `× 15` victory bonus) via playtesting.
- Tune Tech drop scaling (1/2/3 Tech at jumps 1–3/4–6/7–8) via playtesting.

## Client / meta

**JumpRecord** spec (implement when RunEnd screen is built): canonical per-jump statistics stored on `RunState` as a list. Contains: jump number, win/loss outcome, battle duration in ticks, enemy kills by hull class, own ships lost by hull class, total damage dealt, total damage taken, player fleet snapshot, opponent fleet snapshot. Totals on RunEnd screen derived from the full list; per-jump breakdown rows link to each record. In online mode the opponent fleet snapshot is the only persistent record of the opponent — server does not re-expose it.

**Fleet Inspector** spec (implement when battle result overlay is built): UI panel showing fleet composition at battle time — admiral portrait, admiral name, faction, ship roster (hull class, role, HP, weapon type and damage), active equipment, doctrines, upgrades. Available on battle result overlay (auto-shown, player fleet left / opponent fleet right) and on RunEnd screen via per-jump breakdown accordion. Admiral portraits are placeholder colored rectangles with initial letter until real assets are supplied.


- Design meta-progression system — achievements that unlock new admirals, new factions, additional starting difficulties, and cosmetic goodies. Implement after the core loop is playtested.
- Define full admiral roster, starting fleets, and bonus magnitudes once the loop is playtested. Placeholder examples: Admiral Kira (2 Fighter Corvettes, all Fighters +15% speed), Admiral Voss (1 Artillery Frigate + 1 Destroyer, beam weapons +10% damage), Admiral Shen (1 Cruiser, Mothership hangar +4T).
- Revisit active admiral abilities (battle triggers, once-per-run effects) once proc-based effects are implemented.
- Define faction names and visual identity once more than one faction is needed.

## Art (from ARCHITECTURE.md)

- Replace placeholder ship graphics (geometric shapes) with final 3D meshes.
- Replace all placeholder VFX (ship trails, hitscan lines, projectile shapes, beams) with final art once core loop is playtested.

## Server / online (from ARCHITECTURE.md)

Build order steps 6–8:
- Server: account system, fleet upload, opponent fetch. Async multiplayer dropped onto the single-player loop.
- Verification: server-side replay simulation as background worker.
- Polish, balance, content.

Online run state: server owns a run ID assigned at run start. All run choices (fleet, Salvage, Tech, JumpNumber, LossCount) stored in server DB, fetched on login — local save is only a cache; server wins on diverge. Matchmaking uses JumpNumber + LossCount. Opponent fleet DB seeded with hand-authored curated fleets organised by these brackets. Future: curated fleets benchmarked via deterministic sim (seeded batch runs).

## Dockyard / meta (from ARCHITECTURE.md and API-CONTRACTS.md)

- `ServerDockyardAdapter`: implement online dockyard seam (currently `LocalDockyardAdapter` only).
- PlayerID metaprogression: local metaprogression store, offline-capable Rust + C# interface. Dockyard binary uses PlayerID to gate metaprogression-unlocked content — offline lookup not yet built.
- Conditional trigger effects: e.g. `on_mothership_below_50pct_hp: boost morale to nearby friendlies for 10s`. Evaluated per tick against sim state, not pre-resolved (distinct from proc-based on_hit/on_kill effects).
