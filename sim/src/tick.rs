use fixed::types::{I16F16, I32F32};
use std::collections::BTreeMap;
use types::ShipId;

use crate::{
    boids::{compute_forces, SpatialGrid},
    combat::fire_weapons as resolve_weapons,
    config::SimConfig,
    state::{Event, Fleet, SimState},
};

pub enum TickResult {
    Continue,
    Winner(Fleet),
    Draw,
}

pub fn run_tick(state: &mut SimState, config: &SimConfig) -> TickResult {
    // Phase 1: increment tick counter
    state.tick += 1;
    state.events.clear();

    // Phase 2: apply continuous effects (shield recharge)
    for ship in state.ships.values_mut() {
        if ship.shield_recharge_rate > I16F16::ZERO && ship.shield_hp < ship.shield_max_hp {
            ship.shield_hp = (ship.shield_hp + ship.shield_recharge_rate).min(ship.shield_max_hp);
        }
        // Tick down weapon cooldowns
        for w in &mut ship.weapons {
            if w.cooldown_remaining > 0 {
                w.cooldown_remaining -= 1;
            }
        }
    }

    // Phase 3: build spatial grid for boid neighbor queries
    let neighbor_radius = I32F32::from_num(config.boid_neighbor_radius);
    let neighbor_radius_sq = neighbor_radius * neighbor_radius;
    let separation_margin = I32F32::from_num(config.boid_separation_margin);
    let damping = I32F32::from_num(config.boid_velocity_damping);
    let min_speed_fraction = I32F32::from_num(config.boid_min_speed_fraction);
    let grid = SpatialGrid::build(&state.ships, neighbor_radius);

    // Phase 4 & 5: compute boid forces and integrate
    let ship_ids: Vec<ShipId> = state.ships.keys().copied().collect();
    let forces: BTreeMap<ShipId, crate::state::Vec2> = ship_ids
        .iter()
        .map(|&id| {
            let force = compute_forces(
                &state.ships[&id],
                &state.ships,
                &grid,
                neighbor_radius_sq,
                config.boid_max_neighbors as usize,
                separation_margin,
            );
            (id, force)
        })
        .collect();

    for &id in &ship_ids {
        let ship = state.ships.get_mut(&id).expect("id from keys(), not yet removed");
        let force = forces[&id];

        // Save current velocity for turn-rate clamping below.
        let old_vx = ship.vel.x;
        let old_vy = ship.vel.y;

        // Apply force scaled by acceleration
        let accel = I32F32::from_num(ship.acceleration);
        ship.vel.x += force.x * accel;
        ship.vel.y += force.y * accel;

        // Clamp to max speed.
        {
            let speed_sq = ship.vel.x * ship.vel.x + ship.vel.y * ship.vel.y;
            let max = I32F32::from_num(ship.max_speed);
            let max_sq = max * max;
            if speed_sq > max_sq && speed_sq > I32F32::ZERO {
                let speed = cordic::sqrt(speed_sq);
                ship.vel.x = ship.vel.x / speed * max;
                ship.vel.y = ship.vel.y / speed * max;
            }
        }

        // Turn rate cap: limit angular change per tick so ships curve gracefully.
        // Only active when the ship already has a heading (non-zero velocity).
        let old_speed_sq = old_vx * old_vx + old_vy * old_vy;
        if old_speed_sq > I32F32::ZERO {
            let new_speed_sq = ship.vel.x * ship.vel.x + ship.vel.y * ship.vel.y;
            if new_speed_sq > I32F32::ZERO {
                let old_speed = cordic::sqrt(old_speed_sq);
                let new_speed = cordic::sqrt(new_speed_sq);
                let hx = I16F16::from_num(old_vx / old_speed);
                let hy = I16F16::from_num(old_vy / old_speed);
                let dx = I16F16::from_num(ship.vel.x / new_speed);
                let dy = I16F16::from_num(ship.vel.y / new_speed);
                let dot = hx * dx + hy * dy;
                let max_turn = ship.turn_rate;
                let cos_max_turn = cordic::cos(max_turn);
                if dot < cos_max_turn {
                    let cross = hx * dy - hy * dx;
                    let turn = if cross >= I16F16::ZERO { max_turn } else { -max_turn };
                    let (sin_t, cos_t) = cordic::sin_cos(turn);
                    let new_hx = hx * cos_t - hy * sin_t;
                    let new_hy = hx * sin_t + hy * cos_t;
                    ship.vel.x = I32F32::from_num(new_hx) * new_speed;
                    ship.vel.y = I32F32::from_num(new_hy) * new_speed;
                }
            }
        }

        // Velocity damping.
        ship.vel.x *= damping;
        ship.vel.y *= damping;

        // Minimum speed: ships with non-zero velocity never fall below
        // min_speed_fraction of max_speed so balanced forces cause a swerve
        // rather than a full stall.
        {
            let speed_sq = ship.vel.x * ship.vel.x + ship.vel.y * ship.vel.y;
            let min_speed = I32F32::from_num(ship.max_speed) * min_speed_fraction;
            let min_sq = min_speed * min_speed;
            if speed_sq > I32F32::ZERO && speed_sq < min_sq {
                let speed = cordic::sqrt(speed_sq);
                if speed > I32F32::ZERO {
                    ship.vel.x = ship.vel.x / speed * min_speed;
                    ship.vel.y = ship.vel.y / speed * min_speed;
                }
            }
        }

        // Update position
        ship.pos.x += ship.vel.x;
        ship.pos.y += ship.vel.y;

        // Update heading from velocity direction
        if ship.vel.x != I32F32::ZERO || ship.vel.y != I32F32::ZERO {
            ship.heading = I16F16::from_num(cordic::atan2(ship.vel.y, ship.vel.x));
        }
    }

    // Phase 6: resolve weapon firing — hitscan instant, spawn projectiles, start beams
    resolve_weapons(state, config);

    // Phase 7: advance projectiles — move, seek, hit detection, fuse/explosion
    crate::combat::advance_projectiles(state, config);

    // Phase 8: resolve beam damage — charge countdown, angle tracking, ramp damage
    crate::combat::resolve_beams(state, config);

    // Phase 9: damage already applied inline in combat.rs

    // Phase 10: check victory condition
    let mothership_a = state.ships.values().any(|s| s.fleet == Fleet::A && s.is_mothership);
    let mothership_b = state.ships.values().any(|s| s.fleet == Fleet::B && s.is_mothership);

    match (mothership_a, mothership_b) {
        (false, _) => return TickResult::Winner(Fleet::B),
        (_, false) => return TickResult::Winner(Fleet::A),
        _ => {}
    }

    // Phase 11: attrition
    if state.tick == config.attrition_start_tick {
        state.attrition_started = true;
        state.events.push(Event::AttritionStarted);
    }

    if state.attrition_started {
        let ticks_since = state.tick - config.attrition_start_tick;
        let tick_rate = I32F32::from_num(config.tick_rate);
        let base = I32F32::from_num(config.attrition_base_damage_per_second);
        let ramp = I32F32::from_num(config.attrition_ramp_per_second);

        // All arithmetic in fixed-point — no floats in sim code.
        // seconds_elapsed = ticks_since / tick_rate
        let seconds_elapsed = I32F32::from_num(ticks_since) / tick_rate;
        // damage_fraction = fraction of max_hp dealt to each ship this tick
        let damage_fraction = (base + ramp * seconds_elapsed) / tick_rate;

        let ids: Vec<ShipId> = state.ships.keys().copied().collect();
        for id in ids {
            let (damage, hull_class, fleet, is_mothership) = {
                let ship = &state.ships[&id];
                let dmg = I16F16::from_num(I32F32::from_num(ship.max_hp) * damage_fraction);
                (dmg, ship.hull_class, ship.fleet, ship.is_mothership)
            };
            // Apply directly to hull HP — attrition bypasses shields
            let ship = state.ships.get_mut(&id).expect("id from keys(), not yet removed");
            ship.hp -= damage;
            if ship.hp <= I16F16::ZERO {
                ship.hp = I16F16::ZERO;
                state.killed.push((fleet, hull_class, is_mothership));
                state.events.push(Event::ShipDestroyed { id, fleet });
                state.ships.remove(&id);
            }
        }
    }

    if state.tick >= config.max_ticks {
        return TickResult::Draw;
    }

    TickResult::Continue
}
