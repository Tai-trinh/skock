# Skock — Space Fleet Auto-Battler

Battles are fully deterministic: same seed + fleet snapshots → byte-identical output on any machine.

## Battlefield

1000 × 1000 unit coordinate space, origin at center. Fleet A spawns around x = -400, fleet B around x = +400. Ships arranged in their fleet's `formation` at spawn with small deterministic position noise (seeded from the battle seed via xoshiro256+). Mothership always anchors back-center with no noise. Weapon ranges meaningful in the 50–300 unit range.

## Core constraints

- Top-down 2D; 3D engine/textures used as sprites for easy rotation
- 30 ticks/sec logical simulation; renderer interpolates between ticks
- All battles end within 120 seconds
- Fully deterministic given the seed
- Single-threaded simulation

## The Mothership

The Mothership is a combat entity: it appears on the battlefield with artillery-class weapons and point defense.

**Win condition:** destroy the enemy Mothership.

**Forced retreat:** if the player's Mothership reaches 0 HP mid-battle, it immediately warps out — the battle ends, all surviving fleet ships are recalled with it. The Mothership is never destroyed; it escapes. This counts as a battle loss toward the 3-loss run limit.

Fleet size is limited by the Mothership's **hangar capacity** (a tonnage value). Each ship has a tonnage cost; heavier ships cost more. Upgrading hangar capacity (costs `Tech`) is a primary progression axis.

## Game loop

A run ends after **winning at jump 8** or accumulating **3 losses**.

The run visits **8 jump destinations** (jump 1–8). **JumpNumber only advances on a win.** A loss means the Mothership retreats, regroups at the same system's dockyard, and challenges a fresh rival fleet at that same jump — the player stays at jump N until they win it. Losing is a setback, not a skip. Pity salvage is awarded on loss (`JumpNumber × 10`) to prevent death spirals; no Tech is earned on a loss.

The retry opponent is drawn fresh from the same jump's pool — not the same fleet fought before, keeping the dockyard visit meaningful.

### Shopping phase

1. **Dockyard** — randomized selection of ships available to commission this jump, organized by hull class tier. Spend `Salvage` to add a ship to the fleet, or salvage an existing ship to recoup resources. Reroll any tier's offer by spending `Salvage`.
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
   - `Salvage` — flat payout every battle regardless of outcome: `JumpNumber × 10`. Victory adds a flat bonus: `JumpNumber × 15`. Scaling with jump number keeps the payout meaningful as costs grow; the base payout prevents death spirals — a losing player always recovers enough to rebuild.
   - `Tech` — victories only. Scales with JumpNumber: 1 Tech (jumps 1–3), 2 Tech (jumps 4–6), 3 Tech (jumps 7–8).

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

**Explosive payload:** torpedoes, bombs, and mines may carry an explosive payload defined by `explosion_radius` and `explosion_damage` on the weapon block. On detonation, an expanding explosion ring is emitted — every ship within `explosion_radius` takes `explosion_damage` exactly once, regardless of position in the ring. Damage is flat within the radius (no falloff). Hits both friendly and enemy ships. The explosion is a renderer event (`explosion_detonated`) with a position and radius for visual effect.

### Ship roles

Ships are identified by two fields plus an optional weight designation. Display name = `[weight] [role] [hull_class]` e.g. **"Heavy Missile Destroyer"**, **"Light Torpedo Corvette"**, **"Railgun Frigate"**.

**`hull_class`** — size and durability tier, determines base tonnage, HP, armor profile:
`Corvette`, `Frigate`, `Destroyer`, `Cruiser`, `Battlecruiser`, `Dreadnought`

**`role`** — weapon specialization and boid weight profile:
`Fighter`, `Missile`, `Torpedo`, `Mine`, `PointDefense`, `Artillery`, `Railgun`

`Fighter` — short-range hitscan (autocannon). Fast, low HP. Boid weights favour swarming and closing distance.

**`weight`** *(optional)* — `Light` or `Heavy`. Omitted = standard. Light = faster, less armor. Heavy = slower, more armor.

`Dreadnought` hull class — high HP, high armor/shields, high tonnage. Carries a short-range hitscan weapon — not toothless, but not a damage dealer. Boid weights favour pushing toward enemies and holding position. Purpose: soak hits and shield ships behind them.

**Targeting:** default is nearest enemy. Ships have an optional `target_priority` field that overrides targeting for specific roles (e.g. `PointDefense` targets incoming projectiles first, then nearest enemy). Defined per ship in the fleet JSON.

**Firing range:** weapon `range` field gates the fire condition — ship only fires when target distance ≤ `range`. The `maintain_range` boid force positions the ship at its preferred engagement distance. Both work together: boids handle positioning, range check handles firing permission.




## Client

**Factions:** each fleet belongs to a faction. Faction determines hull mesh set (visual identity) and confers passive fleet bonuses via `faction_effects` in the fleet JSON. Admirals belong to a faction.

**Admiral:** the first decision of every run. The player picks one admiral from a selection screen before jump 1. Each admiral comes with a small starting fleet (2–3 ships that match their archetype) and a permanent passive bonus via `admiral_effects`. `admiral_id` is opaque to the sim; the client uses it to look up the admiral's portrait and starting fleet definition. Locked for the full run once chosen.

All four effect sources (`doctrines`, `role_equipment`, `faction_effects`, `admiral_effects`) use the same data-driven effects vocabulary.

**Run reset:** every run starts fresh from the chosen admiral's starting fleet. No ships, doctrines, or equipment carry over between runs. The admiral selection screen is the only persistent choice a player makes at run start.





