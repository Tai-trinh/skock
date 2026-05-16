use fixed::types::{I16F16, I32F32};
use rand_core::RngCore;
use types::{ShipId, WeaponType};

use crate::state::{Event, Fleet, SimState};

fn dist_sq(ax: I32F32, ay: I32F32, bx: I32F32, by: I32F32) -> I32F32 {
    let dx = ax - bx;
    let dy = ay - by;
    dx * dx + dy * dy
}

fn rng_frac(rng: &mut impl RngCore) -> I16F16 {
    I16F16::from_bits((rng.next_u64() >> 48) as i32)
}

pub fn resolve_hitscan(state: &mut SimState) {
    // Collect firing decisions first to avoid borrow conflicts
    let ship_ids: Vec<ShipId> = state.ships.keys().copied().collect();

    for id in ship_ids {
        if !state.ships.contains_key(&id) {
            continue; // destroyed earlier this tick
        }
        let (fleet, pos_x, pos_y, range_sq) = {
            let ship = &state.ships[&id];
            let w = match &ship.weapon {
                Some(w) if w.weapon_type == WeaponType::Hitscan && w.cooldown_remaining == 0 => w,
                _ => continue,
            };
            if !w.ammo.map_or(true, |a| a > 0) {
                continue;
            }
            let range = I32F32::from_num(w.range);
            (ship.fleet, ship.pos.x, ship.pos.y, range * range)
        };

        // Find nearest enemy in range
        let Some(target_id) = state
            .ships
            .iter()
            .filter(|(_, other)| other.fleet != fleet)
            .filter_map(|(other_id, other)| {
                let d = dist_sq(pos_x, pos_y, other.pos.x, other.pos.y);
                (d <= range_sq).then_some((d, *other_id))
            })
            .min_by(|(a, _), (b, _)| a.cmp(b))
            .map(|(_, id)| id)
        else {
            continue;
        };

        // Roll miss chance
        let (damage, miss_chance, crit_chance, crit_damage) = {
            let w = state.ships[&id]
                .weapon
                .as_ref()
                .expect("hitscan ship has weapon; guarded by weapon-type match above");
            (w.damage, w.miss_chance, w.crit_chance, w.crit_damage)
        };

        let roll = rng_frac(&mut state.rng);
        if roll < miss_chance {
            state.events.push(Event::HitscanMissed { source_id: id, target_id });
        } else {
            let crit_roll = rng_frac(&mut state.rng);
            let final_damage = if crit_roll < crit_chance { damage * crit_damage } else { damage };

            apply_damage(state, target_id, final_damage, fleet);

            state.events.push(Event::HitscanFired {
                source_id: id,
                target_id,
                damage: final_damage,
            });
        }

        // Update cooldown and ammo
        let ship = state.ships.get_mut(&id).expect("id present; retrieved same tick");
        let w = ship.weapon.as_mut().expect("hitscan ship has weapon; guarded above");
        w.cooldown_remaining = w.cooldown_ticks;
        if let Some(ref mut ammo) = w.ammo {
            *ammo -= 1;
        }
    }
}

fn apply_damage(
    state: &mut SimState,
    target_id: ShipId,
    raw_damage: I16F16,
    attacker_fleet: Fleet,
) {
    let Some(ship) = state.ships.get_mut(&target_id) else {
        return;
    };

    // Shields absorb first
    let shield_absorbed = raw_damage.min(ship.shield_hp);
    ship.shield_hp -= shield_absorbed;
    let spillover = raw_damage - shield_absorbed;

    // Armor reduces spillover
    let hull_damage = spillover * (I16F16::ONE - ship.armor);
    ship.hp -= hull_damage;

    // Accumulate damage dealt by the attacking fleet
    let attacker_idx = match attacker_fleet {
        Fleet::A => 0,
        Fleet::B => 1,
    };
    state.damage_dealt[attacker_idx] += fixed::types::I32F32::from_num(hull_damage);

    // Check low HP threshold (25%)
    let low_hp_threshold = ship.max_hp / I16F16::from_num(4);
    if ship.hp <= low_hp_threshold
        && !state.low_hp_flagged.get(&target_id).copied().unwrap_or(false)
    {
        state.low_hp_flagged.insert(target_id, true);
        state.events.push(Event::ShipAtLowHp { id: target_id });
    }

    // Check destruction
    if ship.hp <= I16F16::ZERO {
        ship.hp = I16F16::ZERO;
        let fleet = ship.fleet;
        let hull_class = ship.hull_class;
        let is_mothership = ship.is_mothership;
        state.killed.push((fleet, hull_class, is_mothership));
        state.events.push(Event::ShipDestroyed { id: target_id, fleet });
        state.ships.remove(&target_id);
    }
}
