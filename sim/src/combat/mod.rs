mod beam;
mod damage;
mod hitscan;
mod projectile;

pub use damage::apply_damage;

use fixed::types::{I16F16, I32F32};
use rand_core::RngCore;
use std::collections::BTreeMap;
use types::{HullClass, ShipId};

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
