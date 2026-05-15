use fixed::types::{I16F16, I32F32};
use rand_core::RngCore;
use types::{ShipId, WeaponType};

use crate::state::{Event, SimState};

fn dist_sq(ax: I32F32, ay: I32F32, bx: I32F32, by: I32F32) -> I32F32 {
    let dx = ax - bx;
    let dy = ay - by;
    dx * dx + dy * dy
}

fn rng_frac(rng: &mut impl RngCore) -> I16F16 {
    // Returns a value in [0, 1) from the RNG
    let raw = rng.next_u64();
    // Take top 32 bits, treat as 0..2^32, scale to [0,1)
    let top = (raw >> 32) as u32;
    I16F16::from_bits((top >> 16) as i32)
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
        let target_id = {
            let mut nearest: Option<(I32F32, ShipId)> = None;
            for (other_id, other) in &state.ships {
                if other.fleet == fleet {
                    continue;
                }
                let d = dist_sq(pos_x, pos_y, other.pos.x, other.pos.y);
                if d <= range_sq {
                    if nearest.is_none() || d < nearest.unwrap().0 {
                        nearest = Some((d, *other_id));
                    }
                }
            }
            match nearest {
                Some((_, tid)) => tid,
                None => continue,
            }
        };

        // Roll miss chance
        let (damage, miss_chance, crit_chance, crit_damage) = {
            let w = state.ships[&id].weapon.as_ref().unwrap();
            (w.damage, w.miss_chance, w.crit_chance, w.crit_damage)
        };

        let roll = rng_frac(&mut state.rng);
        if roll < miss_chance {
            state.events.push(Event::HitscanMissed {
                source_id: id,
                target_id,
            });
        } else {
            let crit_roll = rng_frac(&mut state.rng);
            let final_damage = if crit_roll < crit_chance {
                damage * crit_damage
            } else {
                damage
            };

            apply_damage(state, target_id, final_damage, id);

            state.events.push(Event::HitscanFired {
                source_id: id,
                target_id,
                damage: final_damage,
            });
        }

        // Update cooldown and ammo
        let ship = state.ships.get_mut(&id).unwrap();
        let w = ship.weapon.as_mut().unwrap();
        w.cooldown_remaining = w.cooldown_ticks;
        if let Some(ref mut ammo) = w.ammo {
            *ammo -= 1;
        }
    }
}

fn apply_damage(state: &mut SimState, target_id: ShipId, raw_damage: I16F16, _source_id: ShipId) {
    let ship = match state.ships.get_mut(&target_id) {
        Some(s) => s,
        None => return,
    };

    // Shields absorb first
    let shield_absorbed = raw_damage.min(ship.shield_hp);
    ship.shield_hp -= shield_absorbed;
    let spillover = raw_damage - shield_absorbed;

    // Armor reduces spillover
    let hull_damage = spillover * (I16F16::ONE - ship.armor);
    ship.hp -= hull_damage;

    // Check low HP threshold (25%)
    let low_hp_threshold = ship.max_hp / I16F16::from_num(4);
    if ship.hp <= low_hp_threshold && !state.low_hp_flagged.get(&target_id).copied().unwrap_or(false)
    {
        state.low_hp_flagged.insert(target_id, true);
        state.events.push(Event::ShipAtLowHp { id: target_id });
    }

    // Check destruction
    if ship.hp <= I16F16::ZERO {
        ship.hp = I16F16::ZERO;
        let fleet = ship.fleet;
        state.events.push(Event::ShipDestroyed {
            id: target_id,
            fleet,
        });
        state.ships.remove(&target_id);
    }
}
