use fixed::types::{I16F16, I32F32};
use std::collections::BTreeMap;
use types::ShipId;

use crate::geometry::dist_sq;
use crate::state::{Pos2, Ship, Vec2};

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
) -> Vec2 {
    // ── Friendly neighbors: grid candidates → filter → N nearest ─────────────

    let neighbor_radius = cordic::sqrt(neighbor_radius_sq);

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

    let mut sep = Vec2::ZERO;
    let mut coh_sum = Pos2::ZERO;
    let mut coh_count: i32 = 0;
    let mut ali = Vec2::ZERO;
    let mut ali_count: i32 = 0;

    for &(d_sq, other_id) in &friendly_candidates {
        let other = &ships[&other_id];
        let dist = cordic::sqrt(d_sq);

        // Separation: push away, weighted by inverse distance
        let away = dir(&other.pos, &ship.pos);
        let strength = (neighbor_radius - dist) / neighbor_radius;
        sep += away * I16F16::from_num(strength);

        // Cohesion: accumulate center of mass
        coh_sum.x += other.pos.x;
        coh_sum.y += other.pos.y;
        coh_count += 1;

        // Alignment: match neighbor velocity
        ali += other.vel;
        ali_count += 1;
    }

    // ── Enemy scan: nearest, mass centroid, mothership ───────────────────────

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

    let mut total = Vec2::ZERO;

    total += sep * ship.boid_weights.separation;

    if coh_count > 0 {
        let avg = Pos2 {
            x: coh_sum.x / I32F32::from_num(coh_count),
            y: coh_sum.y / I32F32::from_num(coh_count),
        };
        total += dir(&ship.pos, &avg) * ship.boid_weights.cohesion;
    }

    if ali_count > 0 {
        let avg =
            Vec2 { x: ali.x / I16F16::from_num(ali_count), y: ali.y / I16F16::from_num(ali_count) };
        total += avg * ship.boid_weights.alignment;
    }

    if let Some((d_sq, nearest)) = nearest_enemy {
        let to_nearest = dir(&ship.pos, &nearest.pos);
        total += to_nearest * ship.boid_weights.seek_nearest;

        let dist = cordic::sqrt(d_sq);
        let preferred = I32F32::from_num(ship.preferred_range);
        if preferred > I32F32::ZERO {
            let diff = dist - preferred;
            let range_force = if diff > I32F32::ZERO {
                to_nearest
            } else {
                Vec2 { x: -to_nearest.x, y: -to_nearest.y }
            };
            total += range_force * ship.boid_weights.maintain_range;
        }
    }

    if enemy_mass_count > 0 && ship.boid_weights.seek_mass > I16F16::ZERO {
        let centroid = Pos2 {
            x: enemy_mass_sum.x / I32F32::from_num(enemy_mass_count),
            y: enemy_mass_sum.y / I32F32::from_num(enemy_mass_count),
        };
        total += dir(&ship.pos, &centroid) * ship.boid_weights.seek_mass;
    }

    if let Some(ms) = enemy_mothership {
        if ship.boid_weights.seek_mothership > I16F16::ZERO {
            total += dir(&ship.pos, &ms.pos) * ship.boid_weights.seek_mothership;
        }
    }

    total
}
