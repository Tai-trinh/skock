use fixed::types::I16F16;
use rand_core::SeedableRng;
use rand_xoshiro::Xoshiro256Plus;
use sim::config::SimConfig;
use sim::state::{BoidWeights, Fleet, Pos2, Ship, SimState, Vec2};
use sim::tick::run_tick;
use types::{CombatStance, HullClass, Role, ShipId, TargetPriority};

fn make_state() -> SimState {
    SimState::new(Xoshiro256Plus::seed_from_u64(0))
}

fn zero_boids() -> BoidWeights {
    BoidWeights {
        separation: I16F16::ZERO,
        cohesion: I16F16::ZERO,
        alignment: I16F16::ZERO,
        seek_nearest: I16F16::ZERO,
        seek_mass: I16F16::ZERO,
        seek_mothership: I16F16::ZERO,
        maintain_range: I16F16::ZERO,
    }
}

fn insert_ship(state: &mut SimState, fleet: Fleet, pos: Pos2, hit_radius: f64) -> ShipId {
    let id = state.alloc_ship_id();
    state.ships.insert(
        id,
        Ship {
            id,
            fleet,
            is_mothership: false,
            blueprint_drawing_id: String::new(),
            hull_class: HullClass::Corvette,
            role: Role::Fighter,
            pos,
            vel: Vec2::ZERO,
            heading: I16F16::ZERO,
            hp: I16F16::from_num(100),
            max_hp: I16F16::from_num(100),
            shield_hp: I16F16::ZERO,
            shield_max_hp: I16F16::ZERO,
            shield_recharge_rate: I16F16::ZERO,
            armor: I16F16::ZERO,
            max_speed: I16F16::from_num(20),
            acceleration: I16F16::from_num(2),
            turn_rate: I16F16::ZERO,
            boid_weights: zero_boids(),
            weapons: vec![],
            target_priority: TargetPriority::Nearest,
            combat_stance: CombatStance::Standoff,
            preferred_range: I16F16::ZERO,
            hit_radius: I16F16::from_num(hit_radius),
        },
    );
    id
}

// Ships should not reverse direction in a single tick — the turn rate cap
// limits heading change to boid_max_turn_rad radians per tick.
#[test]
fn turn_rate_cap_limits_heading_change() {
    let mut state = make_state();
    let config = SimConfig::default_embedded();

    let id = insert_ship(&mut state, Fleet::A, Pos2::from_f64(0.0, 0.0), 5.0);

    // Ship moving in +x; enemy directly behind in -x.
    // Without the cap, the seek force would reverse the heading in one tick.
    let ship = state.ships.get_mut(&id).unwrap();
    ship.vel = Vec2::from_f64(5.0, 0.0);
    ship.heading = I16F16::ZERO;
    ship.boid_weights.seek_nearest = I16F16::from_num(3.0);
    ship.acceleration = I16F16::from_num(5.0);

    // Enemy directly behind — seek force pulls hard in -x direction.
    let enemy_id = insert_ship(&mut state, Fleet::B, Pos2::from_f64(-100.0, 0.0), 5.0);
    state.ships.get_mut(&enemy_id).unwrap().boid_weights = zero_boids();

    run_tick(&mut state, &config);

    let heading: f64 = state.ships[&id].heading.to_num();
    // Allow a small fixed-point rounding margin above the 0.06 config value.
    assert!(
        heading.abs() <= 0.07,
        "heading changed by {heading:.4} rad — expected ≤ boid_max_turn_rad (0.06)"
    );
}

// A ship within enemy separation radius should be pushed away from the enemy,
// not toward it — even with zero seek weights.
#[test]
fn enemy_separation_pushes_ship_away() {
    let mut state = make_state();
    let config = SimConfig::default_embedded();

    // Ship A at origin with only separation force active.
    let id = insert_ship(&mut state, Fleet::A, Pos2::from_f64(0.0, 0.0), 5.0);
    state.ships.get_mut(&id).unwrap().boid_weights.separation = I16F16::from_num(2.0);

    // Enemy B at x=30 — within the 55-unit boid_enemy_separation_radius.
    let enemy_id = insert_ship(&mut state, Fleet::B, Pos2::from_f64(30.0, 0.0), 5.0);
    state.ships.get_mut(&enemy_id).unwrap().boid_weights = zero_boids();

    run_tick(&mut state, &config);

    let x_after: f64 = state.ships[&id].pos.x.to_num();
    assert!(
        x_after < 0.0,
        "ship moved toward enemy (x={x_after:.3}) — expected negative x (pushed away)"
    );
}

// Two friendly ships closer than own_radius + other_radius + margin should
// push each other apart, increasing their separation each tick.
#[test]
fn friendly_ships_push_apart_when_inside_hull_radii() {
    let mut state = make_state();
    let config = SimConfig::default_embedded();

    // Distance = 10, combined hull radii = 5+5 = 10, margin = 8 → min_dist = 18.
    // Ships are well inside the minimum — should receive full separation force.
    let id_a = insert_ship(&mut state, Fleet::A, Pos2::from_f64(0.0, 0.0), 5.0);
    let id_b = insert_ship(&mut state, Fleet::A, Pos2::from_f64(10.0, 0.0), 5.0);

    for id in [id_a, id_b] {
        state.ships.get_mut(&id).unwrap().boid_weights.separation = I16F16::from_num(2.0);
    }

    let dist_before: f64 = 10.0;

    run_tick(&mut state, &config);

    let xa: f64 = state.ships[&id_a].pos.x.to_num();
    let xb: f64 = state.ships[&id_b].pos.x.to_num();
    let dist_after = (xb - xa).abs();

    assert!(
        dist_after > dist_before,
        "ships did not separate: dist_before={dist_before:.2} dist_after={dist_after:.2}"
    );
}
