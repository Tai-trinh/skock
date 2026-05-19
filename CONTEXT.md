# Skock — Space Fleet Auto-Battler

Skock is a roguelite where you command a fleet hyperspace-jumping from location to location to scavenge and survive — a homage to Gallforce and Homeworld. A run spans 8 jump destinations; winning each one advances the Mothership toward the frontier. Battles are fully deterministic: replaying a battle with the same seed and fleet snapshots produces byte-identical results on any machine, enabling local replays and server-side anti-cheat verification. The multiplayer layer is simple: retrieve an opponent's fleet from the server and auto-battle it locally.

## Battlefield

1000 × 1000 unit coordinate space, origin at center. Fleet A spawns around x = -400, fleet B around x = +400. Ships arranged in their fleet's `formation` at spawn with small deterministic position noise (seeded from the battle seed via xoshiro256+) — ships look organic rather than perfectly geometric. Mothership always anchors back-center with no noise. Weapon ranges meaningful in the 50–300 unit range. TODO: revisit battlefield size once boid movement and fleet sizes are playtested.

## Core constraints

- Top-down 2D; 3D engine/textures used as sprites for easy rotation
- 30 ticks/sec logical simulation; renderer interpolates between ticks
- All battles end within 120 seconds
- Fully deterministic given the seed
- Single-threaded simulation
- Anime art style (Macross, Gallforce)
- Target session length: 15–30 minutes

## The Mothership

The Mothership is a colony ship — the last refuge of a surviving population fleeing across the stars. It carries both the fleet and the people. It is a combat entity: it appears on the battlefield with artillery-class weapons and point defense.

**Win condition:** destroy the enemy Mothership.

**Forced retreat:** if the player's Mothership reaches 0 HP mid-battle, it immediately warps out — the battle ends, all surviving fleet ships are recalled with it. The Mothership is never destroyed; it escapes. This counts as a battle loss toward the 3-loss run limit.

Fleet size is limited by the Mothership's **hangar capacity** (a tonnage value). Each ship has a tonnage cost; heavier ships cost more. Upgrading hangar capacity (costs `Tech`) is a primary progression axis.

## Game loop

A run ends after **winning at jump 8** or accumulating **3 losses**. Every fleet encountered is a rival colony ship competing for the same fertile frontier systems — not enemies, but competitors.

The run visits **8 jump destinations** (jump 1–8). **JumpNumber only advances on a win.** A loss means the Mothership retreats, regroups at the same system's dockyard, and challenges a fresh rival fleet at that same jump — the player stays at jump N until they win it. Losing is a setback, not a skip. Pity salvage is awarded on loss (`JumpNumber × 10`) to prevent death spirals; no Tech is earned on a loss.

The retry opponent is drawn fresh from the same jump's pool — not the same fleet fought before, keeping the dockyard visit meaningful. TODO: consider a "rematch same opponent / draw fresh" player choice if hard-walls on a specific jump prove frustrating in playtesting.

**Hidden final encounter:** winning jump 8 without a single loss across the entire run AND achieving a top-10% run score unlocks a secret jump 9 — the strongest rival fleet, which reached the prime homeworld first. Beating them claims the best world (true victory ending). Skipping or losing still earns the standard victory (settling for a second-rate system). TODO: define the scoring formula once the loop is playtested.

### Shopping phase

Ships are the primary combo pieces. Doctrines are the synergizers — they activate multiplicatively when you have enough ships of the right role/type to qualify. The combo-hunting loop: build a fleet composition, then find the doctrines that make it pop, or find a doctrine first and draft ships toward it.

**Lore framing:** at each jump destination the player finds a dockyard offering a random selection of ships for commission (spend `Salvage`). Separately, the Mothership's research team surfaces a randomized set of doctrines and tech discoveries for the player to adopt (spend `Tech`). Offers refresh every jump, drawn from a weighted pool — the same ship type or doctrine can appear on multiple jumps. Scarcity comes from randomness and budget, not depletion.

1. **Dockyard** — randomized selection of ships available to commission this jump, organized by hull class tier. Spend `Salvage` to add a ship to the fleet, or salvage an existing ship to recoup resources. Reroll any tier's offer by spending `Salvage`.

   Slot counts per tier reflect rarity and tonnage cost — smaller ships appear more often and are easier to stack:
   - **Capital** (Battlecruiser, Dreadnought): 1 offer
   - **Destroyer / Cruiser**: 2 offers
   - **Frigate**: 3 offers
   - **Corvette / Fighter**: 5 offers

   TODO: playtest and tune dockyard offer counts, ship tonnage values, and Salvage costs per hull class once the loop is end-to-end. Numbers above are first-pass guesses.
2. **Research** — spend `Tech` across four tracks. Tech is earned from victories only — losses decimate the population, halting research. A player is expected to go deep on 1–2 tracks per run, not touch all four. Each track should feel meaningfully progressed within 3–4 Tech purchases. Winning more battles gives more Tech and therefore more choices — specialization depth is the reward for winning.
   - **Mothership upgrades** — hangar capacity, Mothership weapons/armor. Permanent; always available.
   - **Doctrines** — role/fleet-scoped passive bonuses (e.g. all Fighters +10% speed). Randomized table per jump, rerollable with `Tech`.
   - **Role equipment** — rarer, higher-magnitude role-scoped items (e.g. all `Missile`-role ships gain shield regen). Randomized table, rerollable with `Tech`.
   - **Ship equipment** — unique items slotted into a specific Mothership or Capital ship. Randomized table, rerollable with `Tech`.

### Battle phase

3. Encounter a rival colony fleet contesting the same system. Rival fleets are hand-authored fleet JSON files shipped with the game, organized by difficulty tier and matched to the current jump number and win/loss ratio. When the server exists, real player fleets replace the hand-authored ones — the fleet JSON format is the bridge.
4. Your fleet warps in from the left; the opponent warps in from the right.
5. The fleets battle. If combat exceeds 60 seconds, attrition kicks in: ships take 1% of max HP per second, increasing by 1% each additional second. The attrition design goal is to always crown a victor — escalating damage should eliminate at least one Mothership before 120 seconds in all realistic cases. If both Motherships somehow reach 0 HP in the same tick, the result is a draw; draws count as losses (toward the 3-loss limit and the flawless-run check) and earn no Tech.
6. Earn resources based on outcome:
   - `Salvage` — flat payout every battle regardless of outcome: `JumpNumber × 10`. Victory adds a flat bonus: `JumpNumber × 15`. Scaling with jump number keeps the payout meaningful as costs grow; the base payout prevents death spirals — a losing player always recovers enough to rebuild. TODO: tune multipliers via playtesting.
   - `Tech` — victories only. Scales with JumpNumber: 1 Tech (jumps 1–3), 2 Tech (jumps 4–6), 3 Tech (jumps 7–8). TODO: tune via playtesting.

### Between jumps

All ships auto-heal to full HP for free between jumps — no player choice, no UI. Ships that reach 0 HP during battle survive at 1 HP minimum; no ship is permanently destroyed by combat. Ships below full HP fight with proportionally deteriorated stats (speed, damage, turn rate, etc.) during the battle, but the slate is wiped clean before the next jump.

The only way to permanently remove a ship is to manually salvage it in the dockyard. Salvage yield = `Tonnage × 3`, always at full HP (since ships are always healed before the dockyard phase). The Mothership cannot be salvaged — it is not part of the fleet roster and losing it ends the run immediately.

## Combat

### Weapon archetypes

- **Hitscan** — damage resolves instantly at the tick fired. No sim entity created. Optional `miss_chance` (0.0–1.0): rolled via battle RNG each shot; on miss, a `hitscan_missed` event fires and no damage is applied.
- **Projectile** — a first-class sim entity with position, velocity, target, and `ticks_remaining`. On expiry without a hit it fizzles (`projectile_fizzled`).
- **Beam / ray** — two-phase weapon. **Charge phase** (`charge_ticks`): the turret slews toward the target; beam entity exists in state snapshots so the renderer shows the turret aiming. Charge cancels immediately if the firing ship is stunned. **Firing phase** (`duration_ticks`): continuous damage each tick to the first enemy in the current ray direction within `range`. Beam ends immediately if the intended target dies.

  Tracking uses two angular rates (see ADR-0016): `slew_rate` (fast) when the ray is not hitting any enemy; `track_rate` (slow) when firing on an enemy. Both are active during charge and firing.

  Damage is per tick with optional linear ramp (see ADR-0015): `damage` scales from base to `damage × ramp_max` over `ramp_ticks` ticks of continuous on-target contact. Ramp resets to zero on any tick the beam is not hitting an enemy.

  `Artillery`-role and `Battlecruiser` hull class ships typically carry beams: slow charge, high sustained damage, long cooldown.

### Projectile subtypes

- **Seeking missile** — homes toward a target each tick within a turn-rate limit. Fizzles after N ticks if no hit. Interceptable by point defense.
- **Torpedo** — straight-line, no homing (or very low turn rate). High damage, long lifetime. Interceptable by point defense. May carry an explosive payload (see below).
- **Drifting bomb / mine** — launched with an initial velocity then drifts unpowered. Detonates on proximity (radius trigger). No target tracking. Can be shot down by point defense. Always explosive.

**Explosive payload:** torpedoes, bombs, and mines may carry an explosive payload defined by `explosion_radius` and `explosion_damage` on the weapon block. On detonation, an expanding explosion ring is emitted — every ship within `explosion_radius` takes `explosion_damage` exactly once, regardless of position in the ring. Damage is flat within the radius (no falloff). Hits both friendly and enemy ships. The explosion is a renderer event (`explosion_detonated`) with a position and radius for visual effect. TODO: consider damage falloff by distance once basic explosions are playtested.

### Status effects *(low priority — implement after core sim is working)*

Weapons can inflict temporary status effects on ships in addition to or instead of direct damage. Status effects are tracked per ship in state snapshots.

- **EMP / Stun** — disables the ship for N ticks: weapons cannot fire, boid forces are zeroed (ship drifts on inertia), shields are instantly drained to 0 and cannot recharge for the duration. Applied via `"stun_ticks": 20` on the weapon block. Any weapon archetype (hitscan, projectile, beam) can carry an EMP. Short disable — intended as a tactical window opener, not a death sentence.

- **Burning** — damage over time: deals X damage per tick for N ticks. Applied via `"burn_damage"` and `"burn_ticks"` on the weapon block. Refreshes on repeat hits — duration resets, damage per tick unchanged. Does not stack.
- **Radiation** — slow, persistent damage over time. Lower damage per tick than burning but longer duration. Also suppresses shield recharge while active. Applied via `"radiation_damage"` and `"radiation_ticks"`.

TODO: revisit all status effects once core sim (movement, combat, hitscan) is working — status effects are not needed for the initial playable build.

### Events

`projectile_fired`, `projectile_hit`, `projectile_fizzled`, `projectile_intercepted`, `mine_detonated`, `explosion_detonated`, `beam_fired`, `beam_hit`, `beam_ended`, `hitscan_fired`, `hitscan_missed`, `ship_destroyed`, `ship_at_low_hp` (hull ≤ 25% max HP, fires once per ship), `attrition_started` (fires at tick 1800)

### Ship roles

Ships are identified by two fields plus an optional weight designation. Display name = `[weight] [role] [hull_class]` e.g. **"Heavy Missile Destroyer"**, **"Light Torpedo Corvette"**, **"Railgun Frigate"**.

**`hull_class`** — size and durability tier, determines base tonnage, HP, armor profile:
`Corvette`, `Frigate`, `Destroyer`, `Cruiser`, `Battlecruiser`, `Dreadnought`

**`role`** — weapon specialization and boid weight profile:
`Fighter`, `Missile`, `Torpedo`, `Mine`, `PointDefense`, `Artillery`, `Railgun`

`Fighter` — short-range hitscan (autocannon). Fast, low HP. Boid weights favour swarming and closing distance.

**`weight`** *(optional)* — `Light` or `Heavy`. Omitted = standard. Light = faster, less armor. Heavy = slower, more armor.

`Dreadnought` hull class — high HP, high armor/shields, high tonnage. Carries a short-range hitscan weapon — not toothless, but not a damage dealer. Boid weights favour pushing toward enemies and holding position. Purpose: soak hits and shield ships behind them. TODO: revisit Dreadnought weapon stats and role balance once playtested.

TODO: revisit ship roles — add Shield projector (support ship that extends shields to nearby friendlies) once core roles are playtested.

**Hull hit shape** — a ship's collision geometry: a list of circles in ship-local space, each defined by a forward/lateral offset and a radius. Circles rotate with the ship's heading. Most ships are a single circle; elongated hulls (Battlecruiser, Dreadnought) use two circles staggered along the forward axis. Per hull class, tuned in sim config. Used for projectile and beam hit detection; mine proximity and explosion damage use ship center only.

**Targeting:** default is nearest enemy. Ships have an optional `target_priority` field that overrides targeting for specific roles (e.g. `PointDefense` targets incoming projectiles first, then nearest enemy). Defined per ship in the fleet JSON. TODO: revisit targeting logic after playtesting — nearest enemy may produce boring behaviour at scale.

**Firing range:** weapon `range` field gates the fire condition — ship only fires when target distance ≤ `range`. The `maintain_range` boid force positions the ship at its preferred engagement distance. Both work together: boids handle positioning, range check handles firing permission.

TODO: define weapon stats (damage, range, cooldown, missile turn rate, beam charge time, point-defense priority rules).
TODO: revisit ship roles — add more archetypes and flesh out Capital ship stats once combat feel is playtested.
TODO: shop economy needs playtesting — reroll costs, Tech drop rates, doctrine bonus magnitudes, rare equipment pool.

## Boids

Ships use boids-based movement with inertia and acceleration. Neighbor search uses a uniform spatial hash grid (`BTreeMap<(i32,i32), Vec<ShipId>>`), cell size = perception radius. Only the `boid_max_neighbors` nearest friendly neighbors are considered per ship.

Forces are a weighted sum per ship: `separation`, `cohesion`, `alignment`, `seek_enemy`, `maintain_range`. Each ship role has a distinct weight profile — behavior changes by tuning weights, not code. Note: `seek_enemy` doubles as follow-leader (point it at an ally); `separation` doubles as flee-from-enemy (point it at a threat).

TODO: define weight profiles per ship role.
TODO: revisit boid forces — more forces likely needed once combat feel is playtested.

## Morale *(low priority — defer until sim is further along)*

Each ship has a `morale` value (0–100). Morale is part of the sim state and included in state snapshots every tick.

**Morale decreases when:**
- The ship takes HP damage
- The ship is surrounded (more enemies than friendlies within boid neighbor radius)
- A nearby friendly ship is destroyed

**Morale recovers passively** — ticks back toward 100 at a fixed rate every tick regardless of circumstances.

**Morale increases faster when:**
- Close to other friendly ships (cohesion bonus)
- Close to the Mothership (stronger boost; overrides passive recovery rate)

**Effect on behavior:** morale is expressed through boid weights. Low morale shifts weights toward fleeing — `separation` from enemies increases, a `seek_mothership` force activates pulling the ship back toward the Mothership. High morale allows full aggressive weight profiles. The transition is continuous, not a binary flip.

TODO: define morale thresholds, recovery rate, damage/destruction penalty magnitudes, and Mothership proximity radius.

## Player identity

Every player has a stable **PlayerID** used to key metaprogression data. In offline single-player mode a UUID is generated on first run and stored in the save file: `"offline:<uuid>"`. In online mode the platform identity is used: `"steam:<steamid64>"`, `"apple:<apple_id>"`, etc. The client normalises the format; the server treats it as an opaque string.

## Client

**Factions:** each fleet belongs to a faction. Faction determines hull mesh set (visual identity) and confers passive fleet bonuses via `faction_effects` in the fleet JSON. Start with one faction; add more as art and balance allows. Admirals belong to a faction.

TODO: define faction names and visual identity once more than one faction is needed.

**Admiral:** the first decision of every run. The player picks one admiral from a selection screen before jump 1. Each admiral comes with a small starting fleet (2–3 ships that match their archetype) and a permanent passive bonus via `admiral_effects`. The starting fleet and bonus telegraph a build direction — the player knows their initial angle and hunts the dockyard and research for cards that complete the combo. `admiral_id` is opaque to the sim; the client uses it to look up the admiral's portrait (2D anime front-facing art) and starting fleet definition. Locked for the full run once chosen.

Example admirals (placeholder — tune during playtesting):
- **Admiral Kira** — starts with 2 Fighter Corvettes; all Fighters +15% speed.
- **Admiral Voss** — starts with 1 Artillery Frigate + 1 Destroyer; beam weapons +10% damage.
- **Admiral Shen** — starts with 1 Cruiser; Mothership hangar capacity +4T at run start.

TODO: define full admiral roster, starting fleets, and bonus magnitudes once the loop is playtested. TODO: revisit active admiral abilities (battle triggers, once-per-run effects) once proc-based effects are implemented.

All four effect sources (`doctrines`, `role_equipment`, `faction_effects`, `admiral_effects`) use the same data-driven effects vocabulary. The sim concatenates all four into a single flat list and applies them identically — the source array is invisible at the sim layer. Keeping them as separate arrays in the fleet JSON makes the source of each bonus explicit for client-side debugging and UI display ("this bonus comes from your admiral").

**JumpRecord:** the canonical per-jump statistics record stored on `RunState` as a list (one entry per completed jump). Populated by `RecordBattleResult` after each battle. Contains: jump number, win/loss outcome, battle duration in ticks, enemy kills by hull class, own ships lost by hull class, total damage dealt, total damage taken, a snapshot of the player's fleet at the time of the battle, and a snapshot of the opponent's fleet. Totals on the RunEnd screen are derived from the full list; the per-jump breakdown table rows link directly to each record. In online mode, the opponent fleet snapshot is the only persistent record of who was faced — the server does not re-expose it.

**Fleet Inspector:** a UI panel showing the composition of a fleet at battle time: admiral portrait, admiral name, faction, ship roster (hull class, role, HP, weapon type and damage), and any active equipment, doctrines, and upgrades. Available on the battle result overlay (post-replay, auto-shown, player fleet left / opponent fleet right) and on the RunEnd screen via the per-jump breakdown accordion. When both fleets are shown, the current jump's opponent is visually emphasised; past opponents are shown at reduced prominence. Admiral portraits are placeholder colored rectangles with the admiral's initial letter until real assets are supplied.

**Run reset:** every run starts fresh from the chosen admiral's starting fleet. No ships, doctrines, or equipment carry over between runs. The combo-hunting feel depends on a blank slate — a persistent fleet collapses into one optimal build. The admiral selection screen is the only persistent choice a player makes at run start.

**Meta-progression (out-of-run):** separate from run state. Tracks achievements and unlocks across all runs. TODO: design meta-progression system — achievements that unlock new admirals, new factions, additional starting difficulties (e.g. smaller hangar cap, fewer starting ships), and cosmetic or quality-of-life goodies. Implement after the core loop is playtested.

