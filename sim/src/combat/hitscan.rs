use fixed::types::I32F32;
use types::ShipId;

use crate::state::{Event, SimState, WeaponKind};

use super::{apply_damage, nearest_enemy_in_range, rng_frac};

pub(super) fn fire_hitscan(state: &mut SimState) {
    let ship_ids: Vec<ShipId> = state.ships.keys().copied().collect();
    for id in ship_ids {
        if !state.ships.contains_key(&id) {
            continue;
        }
        let (fleet, pos, range_sq, damage, miss_chance, crit_chance, crit_damage, cooldown_ticks) = {
            let ship = &state.ships[&id];
            let w = match &ship.weapon {
                Some(w)
                    if matches!(w.kind, WeaponKind::Hitscan { .. })
                        && w.cooldown_remaining == 0 =>
                {
                    w
                }
                _ => continue,
            };
            if w.ammo.is_some_and(|a| a == 0) {
                continue;
            }
            let WeaponKind::Hitscan { miss_chance } = w.kind else { unreachable!() };
            let r = I32F32::from_num(w.range);
            (
                ship.fleet,
                ship.pos,
                r * r,
                w.damage,
                miss_chance,
                w.crit_chance,
                w.crit_damage,
                w.cooldown_ticks,
            )
        };

        let Some(target_id) = nearest_enemy_in_range(&state.ships, &pos, fleet, range_sq) else {
            continue;
        };
        let target_pos = state.ships[&target_id].pos;

        let miss_roll = rng_frac(&mut state.rng);
        if miss_roll < miss_chance {
            state.events.push(Event::HitscanMissed {
                source_id: id,
                target_id,
                fleet,
                source_pos_x: pos.x,
                source_pos_y: pos.y,
                target_pos_x: target_pos.x,
                target_pos_y: target_pos.y,
            });
        } else {
            let crit_roll = rng_frac(&mut state.rng);
            let dmg = if crit_roll < crit_chance { damage * crit_damage } else { damage };
            apply_damage(state, target_id, dmg, fleet);
            state.events.push(Event::HitscanFired {
                source_id: id,
                target_id,
                damage: dmg,
                fleet,
                source_pos_x: pos.x,
                source_pos_y: pos.y,
                target_pos_x: target_pos.x,
                target_pos_y: target_pos.y,
            });
        }

        let w = state.ships.get_mut(&id).and_then(|s| s.weapon.as_mut());
        if let Some(w) = w {
            w.cooldown_remaining = cooldown_ticks;
            if let Some(ref mut ammo) = w.ammo {
                *ammo -= 1;
            }
        }
    }
}
