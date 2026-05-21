mod beam;
mod damage;
mod hitscan;
mod projectile;

pub use damage::apply_damage;

use fixed::types::{I16F16, I32F32};
use rand_core::RngCore;
use std::collections::BTreeMap;
use types::{HullClass, ShipId, TargetPriority};

use crate::{
    config::SimConfig,
    geometry::dist_sq,
    state::{Fleet, Pos2, Ship, SimState},
};

pub fn fire_weapons(state: &mut SimState, config: &SimConfig) {
    hitscan::fire_hitscan(state);
    projectile::spawn_projectiles(state, config);
    beam::start_beams(state);
}

pub fn advance_projectiles(state: &mut SimState, config: &SimConfig) {
    projectile::advance_projectiles(state, config);
}

pub fn resolve_beams(state: &mut SimState, config: &SimConfig) {
    beam::resolve_beams(state, config);
}

// ── Shared helpers ────────────────────────────────────────────────────────────

fn rng_frac(rng: &mut impl RngCore) -> I16F16 {
    I16F16::from_bits((rng.next_u64() >> 48) as i32)
}

fn hull_hit_radius(hull_class: HullClass, config: &SimConfig) -> I16F16 {
    let r = config.hull_hit_radii.get(&format!("{hull_class:?}")).copied().unwrap_or(8.0);
    I16F16::from_num(r)
}

fn nearest_enemy_in_range(
    ships: &BTreeMap<ShipId, Ship>,
    from: &Pos2,
    fleet: Fleet,
    range_sq: I32F32,
) -> Option<ShipId> {
    ships
        .iter()
        .filter(|(_, s)| s.fleet != fleet)
        .filter_map(|(id, s)| {
            let d = dist_sq(from, &s.pos);
            (d <= range_sq).then_some((d, *id))
        })
        .min_by(|(a, _), (b, _)| a.cmp(b))
        .map(|(_, id)| id)
}

/// Selects the primary target for a ship given its TargetPriority.
/// Returns None if no enemy is in range of any hardpoint.
/// For Spread: returns None — each hardpoint selects independently via `spread_target_for_hardpoint`.
pub(super) fn primary_target(
    ships: &BTreeMap<ShipId, Ship>,
    ship: &Ship,
    hardpoint_range_sq: I32F32,
) -> Option<ShipId> {
    match ship.target_priority {
        TargetPriority::Nearest | TargetPriority::Spread => {
            nearest_enemy_in_range(ships, &ship.pos, ship.fleet, hardpoint_range_sq)
        }
        TargetPriority::Heaviest => ships
            .iter()
            .filter(|(_, s)| s.fleet != ship.fleet)
            .filter_map(|(id, s)| {
                let d = dist_sq(&ship.pos, &s.pos);
                (d <= hardpoint_range_sq).then_some((s.hp, *id))
            })
            .max_by(|(a, _), (b, _)| a.cmp(b))
            .map(|(_, id)| id),
        TargetPriority::Weakest => ships
            .iter()
            .filter(|(_, s)| s.fleet != ship.fleet)
            .filter_map(|(id, s)| {
                let d = dist_sq(&ship.pos, &s.pos);
                (d <= hardpoint_range_sq).then_some((s.hp, *id))
            })
            .min_by(|(a, _), (b, _)| a.cmp(b))
            .map(|(_, id)| id),
        TargetPriority::MostThreatening => ships
            .iter()
            .filter(|(_, s)| s.fleet != ship.fleet)
            .filter_map(|(id, s)| {
                let d = dist_sq(&ship.pos, &s.pos);
                if d > hardpoint_range_sq {
                    return None;
                }
                let threat = threat_score(s);
                Some((threat, *id))
            })
            .max_by(|(a, _), (b, _)| a.cmp(b))
            .map(|(_, id)| id),
    }
}

/// Computed DPS estimate for a ship — sum of damage/cooldown_ticks across all hardpoints.
fn threat_score(ship: &Ship) -> I16F16 {
    ship.weapons.iter().fold(I16F16::ZERO, |acc, w| {
        if w.cooldown_ticks > 0 {
            acc + w.damage / I16F16::from_num(w.cooldown_ticks)
        } else {
            acc
        }
    })
}
