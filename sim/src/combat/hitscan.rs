use fixed::types::I16F16;
use types::ShipId;

use crate::{
    geometry::dist_sq,
    state::{Event, SimState, WeaponKind},
};

use super::{apply_damage, consume_hardpoint, range_sq, rng_frac, roll_damage};

pub(super) fn fire_hitscan(state: &mut SimState) {
    let ship_ids: Vec<ShipId> = state.ships.keys().copied().collect();

    for ship_id in ship_ids {
        if !state.ships.contains_key(&ship_id) {
            continue;
        }

        let weapon_count = state.ships[&ship_id].weapons.len();
        for hp_idx in 0..weapon_count {
            fire_hardpoint_hitscan(state, ship_id, hp_idx);
        }
    }
}

fn fire_hardpoint_hitscan(state: &mut SimState, ship_id: ShipId, hp_idx: usize) {
    let (
        fleet,
        ship_pos,
        range,
        range_sq,
        damage,
        miss_far,
        miss_near,
        accurate_range_val,
        crit_chance,
        crit_damage,
        cooldown_ticks,
    ) = {
        let ship = match state.ships.get(&ship_id) {
            Some(s) => s,
            None => return,
        };
        let w = match ship.weapons.get(hp_idx) {
            Some(w)
                if matches!(w.kind, WeaponKind::Hitscan { .. }) && w.cooldown_remaining == 0 =>
            {
                w
            }
            _ => return,
        };
        if w.ammo.is_some_and(|a| a == 0) {
            return;
        }
        let WeaponKind::Hitscan { miss_chance_far, miss_chance_near, accurate_range } = w.kind
        else {
            unreachable!()
        };
        (
            ship.fleet,
            ship.pos,
            w.range,
            range_sq(w.range),
            w.damage,
            miss_chance_far,
            miss_chance_near,
            accurate_range,
            w.crit_chance,
            w.crit_damage,
            w.cooldown_ticks,
        )
    };

    let target_id = super::primary_target(&state.ships, &state.ships[&ship_id], range_sq);

    let Some(target_id) = target_id else { return };

    let target_pos = state.ships[&target_id].pos;

    // Distance-dependent miss chance.
    let miss_chance =
        compute_miss_chance(&ship_pos, &target_pos, range, accurate_range_val, miss_near, miss_far);

    let miss_roll = rng_frac(&mut state.rng);
    if miss_roll < miss_chance {
        state.events.push(Event::HitscanMissed {
            source_id: ship_id,
            target_id,
            fleet,
            source_pos_x: ship_pos.x,
            source_pos_y: ship_pos.y,
            target_pos_x: target_pos.x,
            target_pos_y: target_pos.y,
        });
    } else {
        let dmg = roll_damage(damage, crit_chance, crit_damage, &mut state.rng);
        apply_damage(state, target_id, dmg, fleet);
        state.events.push(Event::HitscanFired {
            source_id: ship_id,
            target_id,
            damage: dmg,
            fleet,
            source_pos_x: ship_pos.x,
            source_pos_y: ship_pos.y,
            target_pos_x: target_pos.x,
            target_pos_y: target_pos.y,
        });
    }

    consume_hardpoint(state, ship_id, hp_idx, cooldown_ticks);
}

/// Linearly interpolates miss chance from `miss_near` at `accurate_range` to `miss_far` at `range`.
fn compute_miss_chance(
    source: &crate::state::Pos2,
    target: &crate::state::Pos2,
    range: I16F16,
    accurate_range: I16F16,
    miss_near: I16F16,
    miss_far: I16F16,
) -> I16F16 {
    if miss_near == miss_far {
        return miss_near;
    }
    let effective_range = range - accurate_range;
    if effective_range <= I16F16::ZERO {
        return miss_near;
    }
    let d = I16F16::from_num(cordic::sqrt(dist_sq(source, target)));
    let t = ((d - accurate_range) / effective_range).clamp(I16F16::ZERO, I16F16::ONE);
    miss_near + (miss_far - miss_near) * t
}
