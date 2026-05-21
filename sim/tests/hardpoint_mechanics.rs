use fixed::types::I16F16;
use rand_core::SeedableRng;
use rand_xoshiro::Xoshiro256Plus;
use sim::combat::fire_weapons;
use sim::config::SimConfig;
use sim::state::{BoidWeights, Event, Fleet, Pos2, Ship, SimState, Vec2, WeaponKind, WeaponState};
use types::{CombatStance, HullClass, ProjectileSubtype, Role, ShipId, TargetPriority};

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

fn insert_hitscan_ship(
    state: &mut SimState,
    fleet: Fleet,
    pos: Pos2,
    range: f64,
    miss_near: f64,
    miss_far: f64,
    accurate_range: f64,
) -> ShipId {
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
            boid_weights: zero_boids(),
            weapons: vec![WeaponState {
                damage: I16F16::from_num(10),
                range: I16F16::from_num(range),
                cooldown_ticks: 10,
                cooldown_remaining: 0,
                crit_chance: I16F16::ZERO,
                crit_damage: I16F16::ONE,
                ammo: None,
                fire_forward: I16F16::ZERO,
                fire_lateral: I16F16::ZERO,
                salvo_count: 1,
                salvo_spread_angle: I16F16::ZERO,
                kind: WeaponKind::Hitscan {
                    miss_chance_near: I16F16::from_num(miss_near),
                    miss_chance_far: I16F16::from_num(miss_far),
                    accurate_range: I16F16::from_num(accurate_range),
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

fn insert_target(state: &mut SimState, fleet: Fleet, pos: Pos2) -> ShipId {
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
            hp: I16F16::from_num(200),
            max_hp: I16F16::from_num(200),
            shield_hp: I16F16::ZERO,
            shield_max_hp: I16F16::ZERO,
            shield_recharge_rate: I16F16::ZERO,
            armor: I16F16::ZERO,
            max_speed: I16F16::from_num(5),
            acceleration: I16F16::from_num(1),
            turn_rate: I16F16::from_num(0.5),
            boid_weights: zero_boids(),
            weapons: vec![],
            target_priority: TargetPriority::Nearest,
            combat_stance: CombatStance::Standoff,
            preferred_range: I16F16::ZERO,
            hit_radius: I16F16::from_num(5),
        },
    );
    id
}

// ── Hitscan miss scaling ──────────────────────────────────────────────────────

#[test]
fn hitscan_with_zero_miss_chance_always_hits() {
    // miss_near == miss_far == 0 → early-return path, miss_chance = 0, roll < 0 never true.
    let config = SimConfig::default_embedded();
    let mut state = make_state();
    insert_hitscan_ship(&mut state, Fleet::A, Pos2::ZERO, 100.0, 0.0, 0.0, 0.0);
    let target = insert_target(&mut state, Fleet::B, Pos2::from_f64(80.0, 0.0));

    fire_weapons(&mut state, &config);

    let hit = state.events.iter().any(|e| matches!(e, Event::HitscanFired { .. }));
    let missed = state.events.iter().any(|e| matches!(e, Event::HitscanMissed { .. }));
    assert!(hit, "weapon with 0% miss chance should produce a HitscanFired event");
    assert!(!missed, "weapon with 0% miss chance should never produce HitscanMissed");
    let hp = state.ships[&target].hp.to_num::<f64>();
    assert!((hp - 190.0).abs() < 0.01, "target should lose 10 HP, got {hp}");
}

#[test]
fn hitscan_with_full_miss_chance_always_misses() {
    // miss_near == miss_far == 1 → early-return path, miss_chance = 1.
    // rng_frac ∈ [0, 1), so roll < 1.0 is always true → always miss.
    let config = SimConfig::default_embedded();
    let mut state = make_state();
    insert_hitscan_ship(&mut state, Fleet::A, Pos2::ZERO, 100.0, 1.0, 1.0, 0.0);
    let target = insert_target(&mut state, Fleet::B, Pos2::from_f64(80.0, 0.0));

    fire_weapons(&mut state, &config);

    let missed = state.events.iter().any(|e| matches!(e, Event::HitscanMissed { .. }));
    let hit = state.events.iter().any(|e| matches!(e, Event::HitscanFired { .. }));
    assert!(missed, "weapon with 100% miss chance should produce HitscanMissed");
    assert!(!hit, "weapon with 100% miss chance should never produce HitscanFired");
    let hp = state.ships[&target].hp.to_num::<f64>();
    assert!((hp - 200.0).abs() < 0.01, "target HP unchanged when every shot misses, got {hp}");
}

#[test]
fn hitscan_target_within_accurate_range_clamps_to_miss_near() {
    // accurate_range=80, target at dist=20 → t = clamp((20−80)/..., 0, 1) = 0.
    // miss_chance = miss_near + (miss_far − miss_near) * 0 = miss_near = 0.0 → always hits.
    let config = SimConfig::default_embedded();
    let mut state = make_state();
    insert_hitscan_ship(&mut state, Fleet::A, Pos2::ZERO, 100.0, 0.0, 1.0, 80.0);
    let target = insert_target(&mut state, Fleet::B, Pos2::from_f64(20.0, 0.0));

    fire_weapons(&mut state, &config);

    let hit = state.events.iter().any(|e| matches!(e, Event::HitscanFired { .. }));
    assert!(hit, "target well within accurate_range should use miss_near=0.0, always hitting");
    let hp = state.ships[&target].hp.to_num::<f64>();
    assert!((hp - 190.0).abs() < 0.01, "target should take damage when within accurate_range");
}

// ── Multi-hardpoint independent fire ─────────────────────────────────────────

#[test]
fn two_hardpoints_produce_two_independent_fire_events() {
    let config = SimConfig::default_embedded();
    let mut state = make_state();

    let hitscan_weapon = |damage: f64| WeaponState {
        damage: I16F16::from_num(damage),
        range: I16F16::from_num(100),
        cooldown_ticks: 10,
        cooldown_remaining: 0,
        crit_chance: I16F16::ZERO,
        crit_damage: I16F16::ONE,
        ammo: None,
        fire_forward: I16F16::ZERO,
        fire_lateral: I16F16::ZERO,
        salvo_count: 1,
        salvo_spread_angle: I16F16::ZERO,
        kind: WeaponKind::Hitscan {
            miss_chance_near: I16F16::ZERO,
            miss_chance_far: I16F16::ZERO,
            accurate_range: I16F16::ZERO,
        },
    };

    let id = state.alloc_ship_id();
    state.ships.insert(
        id,
        Ship {
            id,
            fleet: Fleet::A,
            is_mothership: false,
            blueprint_drawing_id: String::new(),
            hull_class: HullClass::Cruiser,
            role: Role::Fighter,
            pos: Pos2::ZERO,
            vel: Vec2::ZERO,
            heading: I16F16::ZERO,
            hp: I16F16::from_num(300),
            max_hp: I16F16::from_num(300),
            shield_hp: I16F16::ZERO,
            shield_max_hp: I16F16::ZERO,
            shield_recharge_rate: I16F16::ZERO,
            armor: I16F16::ZERO,
            max_speed: I16F16::from_num(3),
            acceleration: I16F16::from_num(1),
            turn_rate: I16F16::from_num(0.5),
            boid_weights: zero_boids(),
            weapons: vec![hitscan_weapon(5.0), hitscan_weapon(8.0)],
            target_priority: TargetPriority::Nearest,
            combat_stance: CombatStance::Standoff,
            preferred_range: I16F16::from_num(100),
            hit_radius: I16F16::from_num(5),
        },
    );
    insert_target(&mut state, Fleet::B, Pos2::from_f64(50.0, 0.0));

    fire_weapons(&mut state, &config);

    let fire_count =
        state.events.iter().filter(|e| matches!(e, Event::HitscanFired { .. })).count();
    assert_eq!(
        fire_count, 2,
        "each of two hardpoints should independently produce a HitscanFired event"
    );
}

// ── Salvo count ───────────────────────────────────────────────────────────────

#[test]
fn salvo_count_three_spawns_three_projectiles() {
    let config = SimConfig::default_embedded();
    let mut state = make_state();

    let id = state.alloc_ship_id();
    state.ships.insert(
        id,
        Ship {
            id,
            fleet: Fleet::A,
            is_mothership: false,
            blueprint_drawing_id: String::new(),
            hull_class: HullClass::Frigate,
            role: Role::Torpedo,
            pos: Pos2::ZERO,
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
            boid_weights: zero_boids(),
            weapons: vec![WeaponState {
                damage: I16F16::from_num(30),
                range: I16F16::from_num(200),
                cooldown_ticks: 60,
                cooldown_remaining: 0,
                crit_chance: I16F16::ZERO,
                crit_damage: I16F16::ONE,
                ammo: None,
                fire_forward: I16F16::ZERO,
                fire_lateral: I16F16::ZERO,
                salvo_count: 3,
                salvo_spread_angle: I16F16::from_num(0.5),
                kind: WeaponKind::Projectile {
                    subtype: ProjectileSubtype::Torpedo,
                    speed: I16F16::from_num(4),
                    turn_rate: I16F16::ZERO,
                    fuse_ticks: 120,
                    explosion_radius: I16F16::ZERO,
                    explosion_damage: I16F16::ZERO,
                    hit_radius: I16F16::from_num(4),
                },
            }],
            target_priority: TargetPriority::Nearest,
            combat_stance: CombatStance::Standoff,
            preferred_range: I16F16::from_num(200),
            hit_radius: I16F16::from_num(5),
        },
    );
    insert_target(&mut state, Fleet::B, Pos2::from_f64(100.0, 0.0));

    fire_weapons(&mut state, &config);

    assert_eq!(state.projectiles.len(), 3, "salvo_count=3 should spawn exactly 3 projectiles");
}

// ── Fire point forward offset ─────────────────────────────────────────────────

#[test]
fn fire_forward_offset_shifts_projectile_spawn_along_heading() {
    // heading=0 → forward is the +x direction.
    // fire_forward=10 → projectile spawns at (ship_x + 10, ship_y).
    let config = SimConfig::default_embedded();
    let mut state = make_state();

    let id = state.alloc_ship_id();
    state.ships.insert(
        id,
        Ship {
            id,
            fleet: Fleet::A,
            is_mothership: false,
            blueprint_drawing_id: String::new(),
            hull_class: HullClass::Frigate,
            role: Role::Torpedo,
            pos: Pos2::ZERO,
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
            boid_weights: zero_boids(),
            weapons: vec![WeaponState {
                damage: I16F16::from_num(20),
                range: I16F16::from_num(200),
                cooldown_ticks: 60,
                cooldown_remaining: 0,
                crit_chance: I16F16::ZERO,
                crit_damage: I16F16::ONE,
                ammo: None,
                fire_forward: I16F16::from_num(10),
                fire_lateral: I16F16::ZERO,
                salvo_count: 1,
                salvo_spread_angle: I16F16::ZERO,
                kind: WeaponKind::Projectile {
                    subtype: ProjectileSubtype::Torpedo,
                    speed: I16F16::from_num(4),
                    turn_rate: I16F16::ZERO,
                    fuse_ticks: 120,
                    explosion_radius: I16F16::ZERO,
                    explosion_damage: I16F16::ZERO,
                    hit_radius: I16F16::from_num(4),
                },
            }],
            target_priority: TargetPriority::Nearest,
            combat_stance: CombatStance::Standoff,
            preferred_range: I16F16::from_num(200),
            hit_radius: I16F16::from_num(5),
        },
    );
    insert_target(&mut state, Fleet::B, Pos2::from_f64(100.0, 0.0));

    fire_weapons(&mut state, &config);

    assert_eq!(state.projectiles.len(), 1, "one projectile should spawn");
    let proj = state.projectiles.values().next().unwrap();
    let spawn_x = proj.prev_pos.x.to_num::<f64>();
    let spawn_y = proj.prev_pos.y.to_num::<f64>();
    assert!(
        (spawn_x - 10.0).abs() < 0.1,
        "projectile should spawn 10 units forward (x≈10), got x={spawn_x}"
    );
    assert!(
        spawn_y.abs() < 0.1,
        "projectile should spawn with no lateral offset (y≈0), got y={spawn_y}"
    );
}
