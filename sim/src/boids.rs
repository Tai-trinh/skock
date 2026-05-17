use fixed::types::{I16F16, I32F32};
use std::collections::BTreeMap;
use types::ShipId;

use crate::state::{Pos2, Ship, Vec2};

fn dist_sq(a: &Pos2, b: &Pos2) -> I32F32 {
    let dx = a.x - b.x;
    let dy = a.y - b.y;
    dx * dx + dy * dy
}

// Returns a unit vector from `from` toward `to` in I16F16, or ZERO if coincident.
fn dir(from: &Pos2, to: &Pos2) -> Vec2 {
    let dx = to.x - from.x;
    let dy = to.y - from.y;
    let d_sq = dx * dx + dy * dy;
    if d_sq == I32F32::ZERO {
        return Vec2::ZERO;
    }
    let dist = cordic::sqrt(d_sq);
    Vec2 { x: I16F16::from_num(dx / dist), y: I16F16::from_num(dy / dist) }
}

pub fn compute_forces(
    ship: &Ship,
    ships: &BTreeMap<ShipId, Ship>,
    neighbor_radius_sq: I32F32,
) -> Vec2 {
    let mut sep = Vec2::ZERO;
    let mut coh_sum = Pos2::ZERO;
    let mut coh_count: i32 = 0;
    let mut ali = Vec2::ZERO;
    let mut ali_count: i32 = 0;
    let mut nearest_enemy: Option<(I32F32, &Ship)> = None;

    for other in ships.values() {
        if other.id == ship.id {
            continue;
        }

        let d_sq = dist_sq(&ship.pos, &other.pos);

        if other.fleet == ship.fleet {
            if d_sq < neighbor_radius_sq && d_sq > I32F32::ZERO {
                // Separation: push away from nearby friendlies
                let away = dir(&other.pos, &ship.pos);
                // Weight separation by inverse distance (closer = stronger push)
                let dist = cordic::sqrt(d_sq);
                let radius = cordic::sqrt(neighbor_radius_sq);
                let strength = (radius - dist) / radius;
                sep += away * I16F16::from_num(strength);

                // Cohesion: accumulate center of mass of neighbors
                coh_sum.x += I32F32::from_num(other.pos.x);
                coh_sum.y += I32F32::from_num(other.pos.y);
                coh_count += 1;

                // Alignment: match velocity of neighbors
                ali += other.vel;
                ali_count += 1;
            }
        } else {
            // Track nearest enemy for seek_enemy and maintain_range
            if nearest_enemy.is_none_or(|(d, _)| d_sq < d) {
                nearest_enemy = Some((d_sq, other));
            }
        }
    }

    let mut total = Vec2::ZERO;

    // Separation
    total += sep * ship.boid_weights.separation;

    // Cohesion: steer toward average position of neighbors
    if coh_count > 0 {
        let avg_x = coh_sum.x / I32F32::from_num(coh_count);
        let avg_y = coh_sum.y / I32F32::from_num(coh_count);
        let center = Pos2 { x: avg_x, y: avg_y };
        let to_center = dir(&ship.pos, &center);
        total += to_center * ship.boid_weights.cohesion;
    }

    // Alignment: steer toward average velocity of neighbors
    if ali_count > 0 {
        let avg =
            Vec2 { x: ali.x / I16F16::from_num(ali_count), y: ali.y / I16F16::from_num(ali_count) };
        total += avg * ship.boid_weights.alignment;
    }

    // Seek enemy + maintain range
    if let Some((d_sq, enemy)) = nearest_enemy {
        let to_enemy = dir(&ship.pos, &enemy.pos);
        total += to_enemy * ship.boid_weights.seek_enemy;

        // Maintain range: push away if too close, pull in if too far
        let dist = cordic::sqrt(d_sq);
        let preferred = I32F32::from_num(ship.preferred_range);
        if preferred > I32F32::ZERO {
            let diff = dist - preferred;
            // positive diff = too far, move toward; negative = too close, move away
            let dir_to_enemy = to_enemy;
            let range_force = if diff > I32F32::ZERO {
                dir_to_enemy
            } else {
                Vec2 { x: -dir_to_enemy.x, y: -dir_to_enemy.y }
            };
            total += range_force * ship.boid_weights.maintain_range;
        }
    }

    total
}
