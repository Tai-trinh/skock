use fixed::types::I16F16;
use rand_core::SeedableRng;
use rand_xoshiro::Xoshiro256Plus;
use sim::combat::advance_projectiles;
use sim::config::SimConfig;
use sim::state::{BoidWeights, Fleet, Pos2, Projectile, Ship, SimState, Vec2};
use types::{
    CombatStance, HullClass, ProjectileId, ProjectileSubtype, Role, ShipId, TargetPriority,
};

fn make_state() -> SimState {
    SimState::new(Xoshiro256Plus::seed_from_u64(0))
}

fn insert_torpedo(state: &mut SimState, pos: Pos2, vel: Vec2, fuse_ticks: u32) -> ProjectileId {
    let id = state.alloc_projectile_id();
    state.projectiles.insert(
        id,
        Projectile {
            id,
            owner_id: ShipId(0),
            owner_fleet: Fleet::A,
            prev_pos: pos,
            pos,
            vel,
            heading: I16F16::ZERO,
            subtype: ProjectileSubtype::Torpedo,
            damage: I16F16::from_num(10),
            hit_radius: I16F16::from_num(2),
            explosion_radius: I16F16::ZERO,
            explosion_damage: I16F16::ZERO,
            fuse_ticks_remaining: fuse_ticks,
            turn_rate: I16F16::ZERO,
            crit_chance: I16F16::ZERO,
            crit_damage: I16F16::ONE,
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
            low_hp_flagged: false,
        },
    );
    id
}

#[test]
fn torpedo_advances_position_every_tick() {
    let config = SimConfig::default_embedded();
    let mut state = make_state();
    let speed = 7.0f64;
    let id = insert_torpedo(&mut state, Pos2::ZERO, Vec2::from_f64(speed, 0.0), 100);

    for tick in 1..=5 {
        advance_projectiles(&mut state, &config);
        let x = state.projectiles[&id].pos.x.to_num::<f64>();
        let expected = speed * tick as f64;
        assert!((x - expected).abs() < 0.02, "tick {tick}: expected x≈{expected}, got {x}");
    }
}

#[test]
fn torpedo_moves_in_velocity_direction_each_tick() {
    let config = SimConfig::default_embedded();
    let mut state = make_state();
    let speed = 7.0f64;
    let id = insert_torpedo(&mut state, Pos2::ZERO, Vec2::from_f64(speed, 0.0), 100);

    advance_projectiles(&mut state, &config);

    let proj = &state.projectiles[&id];
    let x = proj.pos.x.to_num::<f64>();
    let y = proj.pos.y.to_num::<f64>();
    assert!((x - speed).abs() < 0.01, "expected x≈{speed}, got {x}");
    assert!(y.abs() < 0.01, "expected y≈0, got {y}");
}

#[test]
fn seeking_missile_turns_toward_target() {
    let config = SimConfig::default_embedded();
    let mut state = make_state();

    // Missile at origin heading straight up (+y), enemy ship to the right (+x).
    // After one tick the missile should have turned right: vx should go from 0 to > 0.
    let speed = 9.0f64;
    let missile_id = state.alloc_projectile_id();
    state.projectiles.insert(
        missile_id,
        Projectile {
            id: missile_id,
            owner_id: ShipId(0),
            owner_fleet: Fleet::A,
            prev_pos: Pos2::ZERO,
            pos: Pos2::ZERO,
            vel: Vec2::from_f64(0.0, speed),
            heading: I16F16::from_num(std::f64::consts::FRAC_PI_2),
            subtype: ProjectileSubtype::SeekingMissile,
            damage: I16F16::from_num(10),
            hit_radius: I16F16::from_num(2),
            explosion_radius: I16F16::ZERO,
            explosion_damage: I16F16::ZERO,
            fuse_ticks_remaining: 100,
            turn_rate: I16F16::from_num(0.08),
            crit_chance: I16F16::ZERO,
            crit_damage: I16F16::ONE,
        },
    );

    insert_ship(&mut state, Fleet::B, Pos2::from_f64(200.0, 0.0));

    advance_projectiles(&mut state, &config);

    let vx = state.projectiles[&missile_id].vel.x.to_num::<f64>();
    assert!(
        vx > 0.0,
        "missile heading upward should gain +x velocity toward rightward target, got vx={vx}"
    );
}

#[test]
fn fuse_expiration_removes_projectile() {
    let config = SimConfig::default_embedded();
    let mut state = make_state();
    let id = insert_torpedo(&mut state, Pos2::ZERO, Vec2::from_f64(5.0, 0.0), 1);

    advance_projectiles(&mut state, &config);

    assert!(
        !state.projectiles.contains_key(&id),
        "torpedo with fuse=1 should be removed after one tick"
    );
}

#[test]
fn mine_detonates_when_enemy_enters_explosion_radius() {
    let config = SimConfig::default_embedded();
    let mut state = make_state();

    let mine_id = state.alloc_projectile_id();
    state.projectiles.insert(
        mine_id,
        Projectile {
            id: mine_id,
            owner_id: ShipId(0),
            owner_fleet: Fleet::A,
            prev_pos: Pos2::ZERO,
            pos: Pos2::ZERO,
            vel: Vec2::ZERO,
            heading: I16F16::ZERO,
            subtype: ProjectileSubtype::Mine,
            damage: I16F16::ZERO,
            hit_radius: I16F16::ZERO,
            explosion_radius: I16F16::from_num(20),
            explosion_damage: I16F16::from_num(50),
            fuse_ticks_remaining: 999,
            turn_rate: I16F16::ZERO,
            crit_chance: I16F16::ZERO,
            crit_damage: I16F16::ONE,
        },
    );

    // Enemy ship 10 units away — well within explosion_radius of 20
    insert_ship(&mut state, Fleet::B, Pos2::from_f64(10.0, 0.0));

    advance_projectiles(&mut state, &config);

    assert!(
        !state.projectiles.contains_key(&mine_id),
        "mine should be removed on proximity trigger"
    );
    let has_explosion =
        state.events.iter().any(|e| matches!(e, sim::state::Event::ProjectileExplosion { .. }));
    assert!(has_explosion, "mine detonation should emit a ProjectileExplosion event");
}
