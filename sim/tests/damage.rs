use fixed::types::I16F16;
use rand_core::SeedableRng;
use rand_xoshiro::Xoshiro256Plus;
use sim::combat::apply_damage;
use sim::state::{BoidWeights, Fleet, Pos2, Ship, SimState, Vec2};
use types::{HullClass, Role, ShipId};

fn make_state() -> SimState {
    SimState::new(Xoshiro256Plus::seed_from_u64(0))
}

fn insert_ship(state: &mut SimState, fleet: Fleet, hp: f64, shield_hp: f64, armor: f64) -> ShipId {
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
            pos: Pos2::ZERO,
            vel: Vec2::ZERO,
            heading: I16F16::ZERO,
            hp: I16F16::from_num(hp),
            max_hp: I16F16::from_num(hp),
            shield_hp: I16F16::from_num(shield_hp),
            shield_max_hp: I16F16::from_num(shield_hp),
            shield_recharge_rate: I16F16::ZERO,
            armor: I16F16::from_num(armor),
            max_speed: I16F16::from_num(5),
            acceleration: I16F16::from_num(1),
            turn_rate: I16F16::from_num(0.5),
            boid_weights: BoidWeights {
                separation: I16F16::ZERO,
                cohesion: I16F16::ZERO,
                alignment: I16F16::ZERO,
                seek_enemy: I16F16::ZERO,
                maintain_range: I16F16::ZERO,
            },
            weapon: None,
            preferred_range: I16F16::ZERO,
        },
    );
    id
}

#[test]
fn shields_absorb_damage_before_hull() {
    let mut state = make_state();
    let id = insert_ship(&mut state, Fleet::A, 100.0, 50.0, 0.0);

    apply_damage(&mut state, id, I16F16::from_num(30), Fleet::B);

    let ship = &state.ships[&id];
    let shield = ship.shield_hp.to_num::<f64>();
    let hp = ship.hp.to_num::<f64>();
    assert!((shield - 20.0).abs() < 0.01, "shield should absorb 30 leaving 20, got {shield}");
    assert!(
        (hp - 100.0).abs() < 0.01,
        "hull HP unchanged when shields absorb all damage, got {hp}"
    );
}

#[test]
fn armor_reduces_hull_damage_after_shields_depleted() {
    let mut state = make_state();
    // 50% armor, no shields: raw=40, spillover=40, hull_damage=40*(1-0.5)=20
    let id = insert_ship(&mut state, Fleet::A, 100.0, 0.0, 0.5);

    apply_damage(&mut state, id, I16F16::from_num(40), Fleet::B);

    let hp = state.ships[&id].hp.to_num::<f64>();
    assert!((hp - 80.0).abs() < 0.1, "50% armor should halve hull damage: expected 80, got {hp}");
}

#[test]
fn ship_removed_when_hp_reaches_zero() {
    let mut state = make_state();
    let id = insert_ship(&mut state, Fleet::A, 50.0, 0.0, 0.0);

    apply_damage(&mut state, id, I16F16::from_num(100), Fleet::B);

    assert!(!state.ships.contains_key(&id), "ship should be removed from state when HP reaches 0");
    let destroyed =
        state.events.iter().any(|e| matches!(e, sim::state::Event::ShipDestroyed { .. }));
    assert!(destroyed, "ShipDestroyed event should fire when ship is killed");
}
