# Skock — Game Design Document

**Genre:** Top-down 2D space fleet auto-battler roguelite
**Platform:** PC (Windows, initial)
**Version:** WIP — pre-alpha

---

## 1. Concept & Vision

### Elevator Pitch

Build a fleet, find the doctrine that makes it pop, and auto-battle rival colonies across 8 jump destinations. Each run is a fresh draft — pick an admiral, commission ships, spend Tech on upgrades, and push to jump 8 without 3 losses.

### Design Pillars

1. **Find the synergy.** Ships are combo pieces; doctrines are synergizers. The core pleasure loop is discovering which doctrine makes your fleet composition click.
2. **Winning earns choices.** Tech is victory-gated. A player on a winning streak gains faster access to upgrades, creating a positive-feedback loop that rewards smart drafting.
3. **Losing is a setback, not a reset.** Pity salvage prevents death spirals. A losing player can always rebuild — the 3-loss limit creates tension without making individual losses catastrophic.
4. **Determinism as a feature.** Battles replay identically given the same seed and fleets. This enables replays, anti-cheat, and eventual async multiplayer without real-time infrastructure.

### Unique Selling Points

- Auto-battler where the strategy is entirely in fleet construction and doctrine selection — no micro during battle.
- Boids-based movement gives battles an organic, emergent feel without manual unit control.
- Deterministic simulation means a replay is a first-class feature, not an afterthought.

---

## 2. Core Game Loop

```
Admiral Selection
    ↓
Dockyard (commission ships, research upgrades, salvage)
    ↓
Battle (auto-resolved, fully deterministic)
    ↓
  Win → advance to next jump → Dockyard
  Loss → pity salvage → Dockyard (same jump, fresh opponent)
    ↓
Jump 8 win → Victory screen
3rd loss → Defeat screen
```

A run spans **8 jumps**. JumpNumber only advances on a win. A loss means the Mothership retreats to the same system's dockyard and challenges a fresh rival fleet — the player retries jump N until they win it.

---

## 3. Progression Systems

### Resources

| Resource | Earned by | Spent on |
|---|---|---|
| **Salvage** | Every battle (win + loss); salvaging ships | Commissioning ships; tier rerolls |
| **Tech** | Victories only | Research upgrades; research rerolls |

**Salvage payout:** `JumpNumber × 10` (base, every battle) + `JumpNumber × 15` (victory bonus). Scaling keeps payouts meaningful as costs grow; the base prevents death spirals.

**Tech payout:** 1 Tech (jumps 1–3), 2 Tech (jumps 4–6), 3 Tech (jumps 7–8). Victories only — losses halt research entirely.

### Run Structure

- Run ends on **jump 8 victory** or **3 losses** (either order).
- Each loss adds 1 to the loss count. Draws count as losses.
- No ship permadeath — all ships heal to full HP for free between jumps (see §Fleet Management).

### Progression Axes

1. **Hangar capacity** (Tech) — unlocks larger fleets.
2. **Fleet stats** (Tech) — HP, speed, weapon damage, armor, etc. via doctrine/research.
3. **Fleet composition** (Salvage) — commission and salvage ships to hit doctrine thresholds.

A player is expected to go deep on 1–2 research tracks per run. Each track should feel meaningfully progressed within 3–4 purchases.

---

## 4. Dockyard Phase

Visited before every battle (including retries). Two distinct shop sections:

### Ship Commission (Salvage)

Randomized ship offers organized by hull class tier:
- **Tier distribution:** 5 / 3 / 2 / 1 slots (common → rare).
- Reroll any tier's offer by spending Salvage.
- Buy (commission) a ship to add it to the fleet; salvage an existing fleet ship to recoup `Tonnage × 3` Salvage.

### Research (Tech)

Four tracks, each offering a randomized set of upgrades per jump:
- **Mothership upgrades** — hangar capacity, Mothership weapons/armor. Permanent; always available.
- **Doctrines** — role/fleet-scoped passive bonuses (e.g. all Fighters +10% speed). Randomized, rerollable with Tech.
- **Role equipment** — rarer, higher-magnitude role-scoped items (e.g. all Missile ships gain shield regen). Separate track.
- **Ship equipment** — unique items for Mothership and Capital-class ships only.

Offers refresh every jump, drawn from a weighted pool — the same doctrine can appear across multiple jumps. Scarcity comes from randomness and budget, not depletion.

### Fleet Management

- **Heal:** all ships auto-heal to full HP before the dockyard phase. No player decision.
- **Salvage:** yield = `Tonnage × 3` (always at full HP, since healing precedes dockyard).
- **Tonnage cap:** fleet size limited by Mothership hangar capacity. Heavier ships cost more tonnage.
- **Mothership:** cannot be salvaged; losing it ends the battle (forced retreat).

---

## 5. Battle Phase

Battles are fully automatic — no player control during combat. The player watches the replay.

### Setup

- Battlefield: 1000 × 1000 unit coordinate space, origin at center.
- Fleet A spawns around x = −400, Fleet B around x = +400.
- Ships arranged per fleet's `formation` with small deterministic position noise — ships look organic rather than perfectly geometric.
- Mothership always anchors back-center with no noise.

### Win Condition

Destroy the enemy Mothership. If the player's Mothership reaches 0 HP, it immediately warps out — the battle ends and all surviving ships are recalled. This is a loss; the Mothership is never permanently destroyed.

### Attrition

If combat exceeds 60 seconds (tick 1800), attrition kicks in: 1% of max HP damage per second, increasing 1% each additional second. Escalating damage ensures a victor is crowned before 120 seconds in all realistic cases.

**Draws:** if both Motherships reach 0 HP in the same tick, the result is a draw. Draws count as losses (toward the 3-loss limit) and earn no Tech.

### Opponent Fleets

Rival fleets are hand-authored fleet JSON files organized by difficulty tier, matched to jump number and win/loss ratio. When the server exists, real player fleets replace the hand-authored ones — the fleet JSON format is the bridge. The retry opponent is drawn fresh from the same jump's pool (not the same fleet fought before), keeping the dockyard visit meaningful.

---

## 6. Combat Systems

### Ship Movement (Boids)

Ships use boids-based movement with inertia and acceleration. Five weighted forces per ship:

| Force | Effect |
|---|---|
| `separation` | Avoid crowding neighbors (doubles as flee-from-enemy) |
| `cohesion` | Move toward center of neighbors |
| `alignment` | Match heading of neighbors |
| `seek_enemy` | Move toward nearest enemy (doubles as follow-leader) |
| `maintain_range` | Hold preferred engagement distance |

Behavior changes by tuning weights per role, not per ship. Neighbor search uses a uniform spatial hash grid; only the `boid_max_neighbors` nearest friendly neighbors are considered.

### Weapon Archetypes

**Hitscan** — instant damage at the tick fired. No sim entity created. Optional `miss_chance`.

**Projectile** — sim entity with position, velocity, target, and lifetime. Three subtypes:
- **Seeking missile** — homes toward target each tick within a turn-rate limit. Interceptable by point defense.
- **Torpedo** — straight-line, high damage, long lifetime. Interceptable. May carry explosive payload.
- **Drifting bomb / mine** — launches with initial velocity then drifts. Detonates on proximity. Always explosive.

Explosive payloads deal flat damage within `explosion_radius` — no falloff. Hits friendly and enemy ships.

**Beam** — two-phase weapon:
- *Charge phase:* turret slews toward target; beam entity visible so renderer shows aiming. Charge cancels on stun.
- *Firing phase:* continuous damage each tick to the first enemy in the ray direction within range. Optional damage ramp from base to `damage × ramp_max` over `ramp_ticks` of continuous contact.

Two tracking rates: fast (`slew_rate`) when not hitting any enemy; slow (`track_rate`) when firing on one.

### Ship Roles

| Role | Primary Weapon | Behavior |
|---|---|---|
| `Fighter` | Short-range hitscan (autocannon) | Fast, low HP; swarms and closes distance |
| `Missile` | Seeking missiles | Mid-range; kites at engage distance |
| `Torpedo` | Torpedoes | Slow, high damage; pushes to close range |
| `Mine` | Drifting bombs | Deploys mines; area denial |
| `PointDefense` | Hitscan vs. projectiles | Intercepts incoming missiles and torpedoes |
| `Artillery` | Beams | Slow charge, high sustained damage; long range |
| `Railgun` | High-velocity hitscan | Long range, high alpha |

### Hull Classes

| Hull | Tonnage tier | Notes |
|---|---|---|
| Corvette | Lightest | Fast, fragile |
| Frigate | Light | Balanced |
| Destroyer | Medium | Workhorse |
| Cruiser | Heavy | Durable |
| Battlecruiser | Heavier | Capital-adjacent; carries beam weapons |
| Dreadnought | Heaviest | Soak hits and shield ships behind them |

**Weight modifier** (optional per ship): `Light` = faster, less armor. `Heavy` = slower, more armor.

**Display name:** `[Weight] [Role] [Hull class]` — e.g. **"Heavy Missile Destroyer"**, **"Light Torpedo Corvette"**.

### Damage Resolution

```
shield_absorbed = min(raw_damage, shield_hp)
shield_hp      -= shield_absorbed
spillover       = raw_damage - shield_absorbed
hull_damage     = spillover × (1.0 − armor)
hp             -= hull_damage
```

Ships hit 0 HP survive at 1 HP minimum (no in-battle permadeath). Shields and armor are optional per ship.

---

## 7. Admirals & Factions

### Admiral Selection

The first and only persistent choice of each run. At run start, the player picks one admiral from 3 offers (one per distinct faction). The admiral is locked for the full run.

Each admiral provides:
- A **small starting fleet** (2–3 ships matching their archetype).
- A **permanent passive bonus** via `admiral_effects` — applied to fleet stats at battle start.
- A **faction affiliation** — determines hull mesh set (visual identity) and `faction_effects`.

`admiral_effects` and `faction_effects` use the same data-driven effects vocabulary as doctrines and equipment. The sim applies all four sources identically.

### Factions

Each faction has a distinct hull mesh set and passive fleet bonus. Admirals belong to a faction; running a faction enough unlocks new admirals (meta-progression, post-launch).

*Placeholder examples:*
- **Gallforce** — Admiral Kira — 2 Fighter Corvettes — all Fighters +15% speed.
- *(More factions TBD once visual identity is established.)*

---

## 8. Story & Setting

Skock is set in a future where rival colonial fleets contest habitable systems across 8 jump destinations. The player commands a Mothership — a mobile base that houses and launches a fleet. The Mothership is never destroyed; if overwhelmed, it warps out and regroups.

**Lore framing (shopping phase):** at each jump destination the player finds a dockyard offering ships for commission. Separately, the Mothership's research team surfaces doctrines and tech discoveries. Offers refresh every jump — scarcity comes from randomness and budget, not depletion.

**Lore framing (battle):** the player encounters a rival colony fleet contesting the same system. Rival fleets are hand-authored (eventually real player fleets in multiplayer).

*Full narrative, faction backstories, and character bios TBD.*

---

## 9. Art Direction

### Visual Style

Top-down 2D. The game engine uses 3D meshes rendered as sprites for easy rotation — ships are layered 3D mesh compositions.

**Ship assembly (production):**
- Base hull mesh — keyed by `(faction, hull_class, weight)`. Different factions have visually distinct designs for the same class.
- Weapon hardpoint mesh — keyed by role.
- Equipment module meshes — attached to visible hardpoints.
- `blueprint_drawing_id` overrides procedural assembly for unique/named ships (e.g. player's Mothership).

**Fleet distinction:** Fleet A (player) and Fleet B (opponent) distinguished by color, not shape.

**Spawn feel:** ships look organic rather than perfectly geometric — small deterministic position noise at spawn.

### Placeholder Ship Graphics

Geometric shapes in code until final 3D meshes are ready:

| Hull class | Placeholder shape |
|---|---|
| Corvette | Triangle (pointing forward) |
| Frigate | Diamond |
| Destroyer | Square |
| Cruiser | Rectangle (wider than tall) |
| Battlecruiser | Rectangle with forward-pointing tip |
| Dreadnought | Rectangle with forward tip + two triangular wing fins |
| Mothership | Hexagon |

### Placeholder VFX

All temporary — goal is something working to evaluate combat feel:

- **Ship trails** — `Line2D`, 30 historical positions, tapers from ship width to 0, fleet color fading to transparent.
- **Hitscan line** — `Line2D` from source to target, width 2px, fleet color; alpha-fades over 12 display frames.
- **Seeking missile** — small arrowhead `Polygon2D` + 10-position trail.
- **Torpedo** — `Line2D` arc of 6 points (nose forward) + 10-position trail.
- **Drifting bomb / mine** — 6 radiating lines via `DrawLine()`, slowly rotating. No trail.
- **Beam** — `Line2D` along `current_angle`. Width = `beam_width`. 30% alpha during charge, 100% during firing. Fleet color.
- **Explosion** — expanding ring visual on `explosion_detonated` event.

*Replace all placeholder graphics with final art once core loop is playtested.*

---

## 10. Audio

*TBD — to be defined once visual direction is established. Expected: ambient space soundtrack, per-weapon SFX (hitscan crack, missile launch/impact, beam charge/fire), mothership warp-out sting, victory/defeat music stings.*

---

## 11. UI & Scenes

### Scene Flow

```
AdmiralSelect.tscn
    → Dockyard.tscn (between every battle)
        → Battle.tscn
            → Dockyard.tscn (on win, next jump)
            → Dockyard.tscn (on loss, same jump)
            → RunEnd.tscn (on 3rd loss or jump 8 win)
                → AdmiralSelect.tscn
```

### Scene Descriptions

**AdmiralSelect** — run start. Player picks one admiral from 3 offers; sees starting fleet and passive bonus. Portraits are placeholder colored rectangles with initial letter until real assets are supplied.

**Dockyard** — between every battle. Ship offer tiers (5/3/2/1), research tracks, fleet list with salvage buttons. Resource counters: Salvage, Tech, Tonnage used/cap. Fleet composition and all button states driven from authoritative state returned by the dockyard binary.

**Battle** — battle playback. Fixed camera auto-fits battlefield bounds. Debug overlay (toggled by key): raw tick state for a selected ship — position, velocity, HP, shields, boid weights, current target, weapon cooldown.

**RunEnd** — run result screen. Win/loss outcome, per-jump breakdown (jump number, outcome, battle duration, kills, losses, damage dealt/received). Returns to AdmiralSelect.

### HUD / Overlays

- **Battle playback controls:** play + speed (1×, 2×, 4×). No scrubbing in initial build.
- **Abandon confirm:** ESC during playback prompts confirmation before returning to menu.
- **Status messages:** inline feedback for shop actions (commissioned, salvaged, cannot afford, etc.).

---

## 12. Controls

Skock is a point-and-click / keyboard game — no real-time input during battle.

| Context | Input | Action |
|---|---|---|
| All menus | Mouse | Navigate, click buttons |
| Battle playback | Space | Play / Pause |
| Battle playback | 1 / 2 / 4 | Set speed multiplier |
| Battle playback | ESC | Open abandon confirm |
| Battle playback | (key TBD) | Toggle debug overlay |

*Full keybinding spec TBD. No gamepad support planned for initial build.*

---

## 13. Open Design Questions

*(Move to `scratch/TODO.md` when answered.)*

- Revisit targeting logic after playtesting — nearest enemy may produce boring behaviour at scale.
- Define weight profiles per ship role (boid force tuning).
- Economy balance: reroll costs, Tech drop rates, doctrine bonus magnitudes, rare equipment pool.
- Active admiral abilities (battle triggers, once-per-run effects) — pending proc-based effects implementation.
- Additional formations beyond `wedge`: `line_of_battle`, `echelon`, `encirclement`, `defensive_circle`.
- Damage falloff by distance for explosion radius — revisit once basic explosions are playtested.
- Hull HP regeneration — revisit if playtesting reveals a need.
- Free camera — add if players request it (currently fixed, auto-fits battlefield).
