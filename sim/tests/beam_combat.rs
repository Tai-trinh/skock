use fixed::types::I16F16;
use rand_core::SeedableRng;
use rand_xoshiro::Xoshiro256Plus;
use sim::combat::{apply_damage, fire_weapons, resolve_beams};
use sim::config::SimConfig;
use sim::state::{BoidWeights, Fleet, Pos2, Ship, SimState, Vec2, WeaponKind, WeaponState};
use types::{CombatStance, HullClass, Role, ShipId, TargetPriority};

fn make_state() -> SimState {
    SimState::new(Xoshiro256Plus::seed_from_u64(0))
}

fn insert_beam_ship(state: &mut SimState, fleet: Fleet, pos: Pos2, range: f64) -> ShipId {
    let id = state.alloc_ship_id();
    state.ships.insert(
        id,
        Ship {
            id,
            fleet,
            is_mothership: false,
            blueprint_drawing_id: String::new(),
            hull_class: HullClass::Battlecruiser,
            role: Role::Artillery,
            pos,
            vel: Vec2::ZERO,
            heading: I16F16::ZERO,
            hp: I16F16::from_num(200),
            max_hp: I16F16::from_num(200),
            shield_hp: I16F16::ZERO,
            shield_max_hp: I16F16::ZERO,
            shield_recharge_rate: I16F16::ZERO,
            armor: I16F16::ZERO,
            max_speed: I16F16::from_num(3),
            acceleration: I16F16::from_num(1),
            turn_rate: I16F16::from_num(0.5),
            boid_weights: BoidWeights {
                separation: I16F16::ZERO,
                cohesion: I16F16::ZERO,
                alignment: I16F16::ZERO,
                seek_nearest: I16F16::ZERO,
                seek_mass: I16F16::ZERO,
                seek_mothership: I16F16::ZERO,
                maintain_range: I16F16::ZERO,
            },
            weapons: vec![WeaponState {
                damage: I16F16::from_num(10),
                range: I16F16::from_num(range),
                cooldown_ticks: 0,
                cooldown_remaining: 0,
                crit_chance: I16F16::ZERO,
                crit_damage: I16F16::ONE,
                ammo: None,
                fire_forward: I16F16::ZERO,
                fire_lateral: I16F16::ZERO,
                salvo_count: 1,
                salvo_spread_angle: I16F16::ZERO,
                kind: WeaponKind::Beam {
                    charge_ticks: 0, // start_beams clamps to max(1), so 1 charge tick
                    duration_ticks: 1,
                    beam_width: I16F16::from_num(5),
                    slew_rate: I16F16::from_num(0.5),
                    track_rate: I16F16::from_num(0.1),
                    ramp_ticks: 0,
                    ramp_max: I16F16::ONE,
                },
            }],
            target_priority: TargetPriority::Nearest,
            combat_stance: CombatStance::Standoff,
            preferred_range: I16F16::from_num(range),
            hit_radius: I16F16::from_num(5),
        },
    );
    id
}

fn insert_ship(state: &mut SimState, fleet: Fleet, pos: Pos2) -> ShipId {
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
            max_speed: I16F16::from_num(5),
            acceleration: I16F16::from_num(1),
            turn_rate: I16F16::from_num(0.5),
            boid_weights: BoidWeights {
                separation: I16F16::ZERO,
                cohesion: I16F16::ZERO,
                alignment: I16F16::ZERO,
                seek_nearest: I16F16::ZERO,
                seek_mass: I16F16::ZERO,
                seek_mothership: I16F16::ZERO,
                maintain_range: I16F16::ZERO,
            },
            weapons: vec![],
            target_priority: TargetPriority::Nearest,
            combat_stance: CombatStance::Standoff,
            preferred_range: I16F16::ZERO,
            hit_radius: I16F16::from_num(5),
        },
    );
    id
}

#[test]
fn beam_spawns_beam_entity_and_registers_in_active_beams() {
    let config = SimConfig::default_embedded();
    let mut state = make_state();
    let beam_ship = insert_beam_ship(&mut state, Fleet::A, Pos2::ZERO, 200.0);
    insert_ship(&mut state, Fleet::B, Pos2::from_f64(50.0, 0.0));

    fire_weapons(&mut state, &config);

    assert_eq!(state.beams.len(), 1, "one beam entity should be created");
    assert!(
        state.active_beams.contains_key(&(beam_ship, 0)),
        "firing ship should be registered in active_beams"
    );
    let beam_id = state.active_beams[&(beam_ship, 0)];
    assert!(state.beams.contains_key(&beam_id), "active_beams entry should point to a live beam");
}

#[test]
fn beam_ship_cannot_spawn_second_beam_while_first_active() {
    let config = SimConfig::default_embedded();
    let mut state = make_state();
    insert_beam_ship(&mut state, Fleet::A, Pos2::ZERO, 200.0);
    insert_ship(&mut state, Fleet::B, Pos2::from_f64(50.0, 0.0));

    fire_weapons(&mut state, &config);
    assert_eq!(state.beams.len(), 1);

    // Call again without resolve_beams — cooldown is 0, so only active_beams blocks re-fire.
    fire_weapons(&mut state, &config);
    assert_eq!(state.beams.len(), 1, "second fire_weapons should not spawn a second beam");
    assert_eq!(state.active_beams.len(), 1, "active_beams should still have exactly one entry");
}

#[test]
fn active_beams_cleared_when_beam_expires_and_ship_can_fire_again() {
    let config = SimConfig::default_embedded();
    let mut state = make_state();
    let beam_ship = insert_beam_ship(&mut state, Fleet::A, Pos2::ZERO, 200.0);
    insert_ship(&mut state, Fleet::B, Pos2::from_f64(50.0, 0.0));

    fire_weapons(&mut state, &config);
    assert_eq!(state.beams.len(), 1, "beam should spawn");

    // Tick 1: Charging → Firing transition (charge_ticks clamped to 1).
    resolve_beams(&mut state, &config);
    assert_eq!(state.beams.len(), 1, "beam still alive after charge completes");

    // Tick 2: Firing for 1 tick then expires (duration_ticks = 1).
    resolve_beams(&mut state, &config);
    assert!(state.beams.is_empty(), "beam entity should be removed on expiry");
    assert!(
        !state.active_beams.contains_key(&(beam_ship, 0)),
        "active_beams should be cleared when beam expires"
    );

    // With active_beams cleared and cooldown_ticks = 0, ship can immediately fire again.
    fire_weapons(&mut state, &config);
    assert_eq!(state.beams.len(), 1, "ship should be able to spawn a new beam after expiry");
}

#[test]
fn active_beams_cleared_when_source_ship_destroyed_mid_beam() {
    let config = SimConfig::default_embedded();
    let mut state = make_state();
    let beam_ship = insert_beam_ship(&mut state, Fleet::A, Pos2::ZERO, 200.0);
    insert_ship(&mut state, Fleet::B, Pos2::from_f64(50.0, 0.0));

    fire_weapons(&mut state, &config);
    assert!(state.active_beams.contains_key(&(beam_ship, 0)), "beam should be active");

    // Destroy the source ship. active_beams still holds the stale entry.
    apply_damage(&mut state, beam_ship, I16F16::from_num(9999), Fleet::B);
    assert!(!state.ships.contains_key(&beam_ship), "source ship should be destroyed");
    assert!(
        state.active_beams.contains_key(&(beam_ship, 0)),
        "active_beams still stale before resolve_beams"
    );

    // resolve_beams detects dead source and cleans up both the beam entity and active_beams.
    resolve_beams(&mut state, &config);
    assert!(state.beams.is_empty(), "orphaned beam entity should be removed");
    assert!(
        !state.active_beams.contains_key(&(beam_ship, 0)),
        "active_beams should be cleared when source ship dies"
    );
}
