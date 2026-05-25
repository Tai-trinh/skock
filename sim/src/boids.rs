use fixed::types::{I16F16, I32F32};
use std::collections::BTreeMap;
use types::ShipId;

use crate::geometry::dist_sq;
use crate::state::{Pos2, Ship, Vec2};

// Returns a unit vector from `from` toward `to`, or ZERO if coincident.
fn dir(from: &Pos2, to: &Pos2) -> Vec2 {
    let dx = to.x - from.x;
    let dy = to.y - from.y;
    let d_sq = dx * dx + dy * dy;
    if d_sq == I32F32::ZERO {
        return Vec2::ZERO;
    }
    let dist = cordic::sqrt(d_sq);
    Vec2 { x: dx / dist, y: dy / dist }
}

fn cell_of(pos: &Pos2, cell_size: I32F32) -> (i32, i32) {
    ((pos.x / cell_size).to_num::<i32>(), (pos.y / cell_size).to_num::<i32>())
}

// Single flat grid sized to the perception radius.
// Cell size = neighbor_radius, so a 3×3 neighborhood covers every candidate within range.
// Build once per tick; each ship queries its own 3×3 neighborhood (~9 cells).
pub struct SpatialGrid {
    cell_size: I32F32,
    cells: BTreeMap<(i32, i32), Vec<ShipId>>,
}

impl SpatialGrid {
    pub fn build(ships: &BTreeMap<ShipId, Ship>, cell_size: I32F32) -> Self {
        let mut cells: BTreeMap<(i32, i32), Vec<ShipId>> = BTreeMap::new();
        // BTreeMap iteration is sorted by ShipId, so each Vec is in ascending ShipId order.
        for (&id, ship) in ships {
            cells.entry(cell_of(&ship.pos, cell_size)).or_default().push(id);
        }
        Self { cell_size, cells }
    }

    // ShipIds in the 3×3 cell neighborhood around `pos`.
    // Order: cells in BTreeMap (cx,cy) order; within each cell, ascending ShipId.
    fn candidates(&self, pos: &Pos2) -> Vec<ShipId> {
        let (cx, cy) = cell_of(pos, self.cell_size);
        let mut result = Vec::new();
        for dx in -1i32..=1 {
            for dy in -1i32..=1 {
                if let Some(ids) = self.cells.get(&(cx + dx, cy + dy)) {
                    result.extend_from_slice(ids);
                }
            }
        }
        result
    }
}

pub fn compute_forces(
    ship: &Ship,
    ships: &BTreeMap<ShipId, Ship>,
    grid: &SpatialGrid,
    neighbor_radius_sq: I32F32,
    max_neighbors: usize,
    separation_margin: I32F32,
) -> Vec2 {
    let neighbor_radius = cordic::sqrt(neighbor_radius_sq);
    let sep_margin = separation_margin;

    // ── Separation: all ships (friend + foe) within neighbor_radius ───────────
    // No neighbor cap — every hull within range contributes repulsion.
    let mut sep = Vec2::ZERO;
    for other_id in grid.candidates(&ship.pos) {
        if other_id == ship.id {
            continue;
        }
        let other = &ships[&other_id];
        let d_sq = dist_sq(&ship.pos, &other.pos);
        if d_sq >= neighbor_radius_sq || d_sq == I32F32::ZERO {
            continue;
        }
        let dist = cordic::sqrt(d_sq);
        let min_dist = I32F32::from_num(ship.hit_radius + other.hit_radius) + sep_margin;
        let away = dir(&other.pos, &ship.pos);
        let strength = if dist <= min_dist {
            I32F32::ONE
        } else {
            (neighbor_radius - dist) / (neighbor_radius - min_dist)
        };
        sep += away * strength;
    }

    // ── Friendly neighbors: cohesion + alignment (capped at max_neighbors) ────
    let mut friendly_candidates: Vec<(I32F32, ShipId)> = grid
        .candidates(&ship.pos)
        .into_iter()
        .filter(|&id| id != ship.id)
        .filter_map(|id| {
            let other = &ships[&id];
            if other.fleet != ship.fleet {
                return None;
            }
            let d_sq = dist_sq(&ship.pos, &other.pos);
            if d_sq < neighbor_radius_sq && d_sq > I32F32::ZERO {
                Some((d_sq, id))
            } else {
                None
            }
        })
        .collect();

    // Sort by distance; tie-break by ShipId for determinism.
    friendly_candidates.sort_by(|(a, aid), (b, bid)| a.cmp(b).then_with(|| aid.cmp(bid)));
    friendly_candidates.truncate(max_neighbors);

    let mut coh_sum = Pos2::ZERO;
    let mut coh_count: i32 = 0;
    let mut ali = Vec2::ZERO;
    let mut ali_count: i32 = 0;

    for &(_, other_id) in &friendly_candidates {
        let other = &ships[&other_id];

        coh_sum.x += other.pos.x;
        coh_sum.y += other.pos.y;
        coh_count += 1;

        // Alignment: normalize neighbor velocity to get heading only (not speed).
        let vel_sq = other.vel.x * other.vel.x + other.vel.y * other.vel.y;
        if vel_sq > I32F32::ZERO {
            let speed = cordic::sqrt(vel_sq);
            ali += Vec2 { x: other.vel.x / speed, y: other.vel.y / speed };
            ali_count += 1;
        }
    }

    // ── Enemy scan: nearest, mass centroid, mothership ────────────────────────
    let mut nearest_enemy: Option<(I32F32, &Ship)> = None;
    let mut enemy_mass_sum = Pos2::ZERO;
    let mut enemy_mass_count: i32 = 0;
    let mut enemy_mothership: Option<&Ship> = None;

    for other in ships.values() {
        if other.fleet == ship.fleet {
            continue;
        }
        let d_sq = dist_sq(&ship.pos, &other.pos);
        if nearest_enemy.is_none_or(|(d, _)| d_sq < d) {
            nearest_enemy = Some((d_sq, other));
        }
        enemy_mass_sum.x += other.pos.x;
        enemy_mass_sum.y += other.pos.y;
        enemy_mass_count += 1;
        if other.is_mothership {
            enemy_mothership = Some(other);
        }
    }

    // ── Accumulate total force ─────────────────────────────────────────────────
    let w = &ship.boid_weights;
    let mut total = Vec2::ZERO;

    total += sep * I32F32::from_num(w.separation);

    if coh_count > 0 {
        let avg = Pos2 {
            x: coh_sum.x / I32F32::from_num(coh_count),
            y: coh_sum.y / I32F32::from_num(coh_count),
        };
        total += dir(&ship.pos, &avg) * I32F32::from_num(w.cohesion);
    }

    if ali_count > 0 {
        let n = I32F32::from_num(ali_count);
        let avg = Vec2 { x: ali.x / n, y: ali.y / n };
        total += avg * I32F32::from_num(w.alignment);
    }

    if let Some((d_sq, nearest)) = nearest_enemy {
        let dist = cordic::sqrt(d_sq);
        let preferred = I32F32::from_num(ship.preferred_range);
        let within_preferred = preferred > I32F32::ZERO && dist < preferred;
        let to_nearest = dir(&ship.pos, &nearest.pos);

        // Suppress all seek forces while already inside preferred range —
        // the ship orbits on inertia rather than hunting an enemy it's already on top of.
        if !within_preferred {
            total += to_nearest * I32F32::from_num(w.seek_nearest);

            if enemy_mass_count > 0 && w.seek_mass > I16F16::ZERO {
                let centroid = Pos2 {
                    x: enemy_mass_sum.x / I32F32::from_num(enemy_mass_count),
                    y: enemy_mass_sum.y / I32F32::from_num(enemy_mass_count),
                };
                total += dir(&ship.pos, &centroid) * I32F32::from_num(w.seek_mass);
            }

            if let Some(ms) = enemy_mothership {
                if w.seek_mothership > I16F16::ZERO {
                    total += dir(&ship.pos, &ms.pos) * I32F32::from_num(w.seek_mothership);
                }
            }
        }

        // Proportional maintain_range: zero force at preferred_range, ±1 at 2× error.
        // Positive (toward) when too far, negative (away) when too close.
        // Zero crossing lets the ship carry through on inertia and orbit naturally.
        if preferred > I32F32::ZERO {
            let strength = ((dist - preferred) / preferred).clamp(-I32F32::ONE, I32F32::ONE);
            total += to_nearest * I32F32::from_num(w.maintain_range) * strength;
        }
    }

    total
}
