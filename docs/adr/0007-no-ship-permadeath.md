# ADR-0007: Ships have no permadeath — survive at 1 HP minimum

**Status:** Accepted

## Context

Most roguelites permanently destroy units that reach 0 HP during combat. Two models were considered:

**Option A — Permadeath:** ships destroyed in battle are gone. Player must replace them from Salvage. Creates high stakes but can force the player into unwinnable death spirals (no ships → no income → no ships).

**Option B — No permadeath (current):** ships that reach 0 HP during battle explode visually and leave the sim for that battle, but are restored to 1 HP in the fleet after battle. Auto-heal to full between jumps. The only way to permanently remove a ship is to manually salvage it in the dockyard.

## Decision

No permadeath (Option B).

Ships are the primary combo pieces. Losing them permanently to a single bad battle collapses the combo-hunting loop — the player loses the synergy they built, not just the battle. The roguelite tension comes from the 3-loss run limit (Mothership retreats), not from individual ship loss.

Visual drama is preserved: ships still explode and are absent from subsequent sim ticks. The distinction between "destroyed" and "survived at 1 HP" is only meaningful in the post-battle fleet restore step, invisible to the player during combat.

## Consequences

- Salvage yield is always calculated at full HP (ships are auto-healed before the dockyard phase), so it is always `Tonnage × 3` regardless of battle outcome.
- `ApplySurvivorHp` in `RunState` must handle ships absent from the survivor list by restoring them to 1 HP, not removing them.
- Ships fight with proportionally degraded stats as they take damage *within* a battle — the consequence is intra-battle performance loss, not carry-over to the next battle. Ships auto-heal to full between jumps; the slate is wiped clean.
- Save format must never write `hp: 0` for a player ship — minimum is 1 after post-battle restore.

## Trade-off rejected

Permadeath (Option A) creates higher per-ship tension but risks unrecoverable death spirals, particularly at later jump numbers where replacement costs are high relative to available Salvage. It also punishes the player for experimenting with compositions, which works against the combo-hunting design goal.
