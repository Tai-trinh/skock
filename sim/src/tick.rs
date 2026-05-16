use fixed::types::{I16F16, I32F32};
use std::collections::BTreeMap;
use types::ShipId;

use crate::{
    boids::compute_forces,
    combat::resolve_hitscan,
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
        if let Some(ref mut w) = ship.weapon {
            if w.cooldown_remaining > 0 {
                w.cooldown_remaining -= 1;
            }
        }
    }

    // Phase 3: spatial grid skipped — O(N²) in boids

    // Phase 4 & 5: compute boid forces and integrate
    let neighbor_radius = I32F32::from_num(config.boid_neighbor_radius);
    let neighbor_radius_sq = neighbor_radius * neighbor_radius;

    let ship_ids: Vec<ShipId> = state.ships.keys().copied().collect();
    let forces: BTreeMap<ShipId, crate::state::Vec2> = ship_ids
        .iter()
        .map(|&id| (id, compute_forces(&state.ships[&id], &state.ships, neighbor_radius_sq)))
        .collect();

    for &id in &ship_ids {
        let ship = state.ships.get_mut(&id).expect("id from keys(), not yet removed");
        let force = forces[&id];

        // Apply force scaled by acceleration
        let ax = force.x * ship.acceleration;
        let ay = force.y * ship.acceleration;

        ship.vel.x += ax;
        ship.vel.y += ay;

        // Clamp to max speed using wider type to avoid overflow
        let vx = I32F32::from_num(ship.vel.x);
        let vy = I32F32::from_num(ship.vel.y);
        let speed_sq = vx * vx + vy * vy;
        let max_sq = I32F32::from_num(ship.max_speed) * I32F32::from_num(ship.max_speed);
        if speed_sq > max_sq && speed_sq > I32F32::ZERO {
            let speed = cordic::sqrt(speed_sq);
            let max = I32F32::from_num(ship.max_speed);
            ship.vel.x = I16F16::from_num(vx / speed * max);
            ship.vel.y = I16F16::from_num(vy / speed * max);
        }

        // Update position
        ship.pos.x += I32F32::from_num(ship.vel.x);
        ship.pos.y += I32F32::from_num(ship.vel.y);

        // Update heading from velocity direction
        let vx = ship.vel.x;
        let vy = ship.vel.y;
        if vx != I16F16::ZERO || vy != I16F16::ZERO {
            ship.heading = cordic::atan2(vy, vx);
        }
    }

    // Phase 6: resolve weapon firing (hitscan only for now)
    resolve_hitscan(state);

    // Phase 7 & 8: projectiles and beams — not yet implemented

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
