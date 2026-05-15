use fixed::types::I16F16;
use rand_core::RngCore;
use rand_xoshiro::Xoshiro256Plus;
use types::{FleetJson, HullClass, ShipDef, ShipId};

use crate::{
    config::SimConfig,
    state::{BoidWeights, Fleet, Pos2, Ship, SimState, Vec2, WeaponState},
};

pub fn spawn_fleet(state: &mut SimState, fleet: &FleetJson, side: Fleet, config: &SimConfig) {
    let spawn_x = match side {
        Fleet::A => config.fleet_a_spawn_x,
        Fleet::B => config.fleet_b_spawn_x,
    };

    let mut ships: Vec<&ShipDef> = fleet.ships.iter().collect();
    ships.sort_by_key(|s| hull_class_order(s.hull_class));

    for (i, def) in ships.iter().enumerate() {
        let pos = wedge_pos(spawn_x, i, ships.len(), side, config, &mut state.rng);
        let ship = build_ship(state.alloc_ship_id(), def, side, pos, false);
        state.ships.insert(ship.id, ship);
    }

    let ms_pos = Pos2::from_f64(spawn_x, 0.0);
    let ms = build_ship(state.alloc_ship_id(), &fleet.mothership, side, ms_pos, true);
    state.ships.insert(ms.id, ms);
}

fn hull_class_order(h: HullClass) -> u8 {
    match h {
        HullClass::Dreadnought => 0,
        HullClass::Corvette => 1,
        HullClass::Frigate => 2,
        HullClass::Destroyer => 3,
        HullClass::Cruiser => 4,
        HullClass::Battlecruiser => 5,
    }
}

fn wedge_pos(
    spawn_x: f64,
    index: usize,
    _total: usize,
    side: Fleet,
    config: &SimConfig,
    rng: &mut Xoshiro256Plus,
) -> Pos2 {
    let row = ((-1.0 + f64::sqrt(1.0 + 8.0 * index as f64)) / 2.0) as usize;
    let pos_in_row = index - row * (row + 1) / 2;
    let row_width = (row + 1) as f64;

    let row_spacing = 40.0;
    let ship_spacing = 35.0;

    let depth_sign = match side {
        Fleet::A => 1.0,
        Fleet::B => -1.0,
    };
    let depth = spawn_x + depth_sign * row as f64 * row_spacing;
    let lateral = (pos_in_row as f64 - (row_width - 1.0) / 2.0) * ship_spacing;

    let noise_x = noise_offset(rng, config.spawn_noise);
    let noise_y = noise_offset(rng, config.spawn_noise);

    Pos2::from_f64(depth + noise_x, lateral + noise_y)
}

fn noise_offset(rng: &mut Xoshiro256Plus, amplitude: f64) -> f64 {
    let raw = rng.next_u64() as f64 / u64::MAX as f64;
    (raw - 0.5) * amplitude
}

pub fn build_ship(id: ShipId, def: &ShipDef, fleet: Fleet, pos: Pos2, is_mothership: bool) -> Ship {
    let weapon = def.weapon.as_ref().map(|w| WeaponState {
        weapon_type: w.weapon_type,
        damage: I16F16::from_num(w.damage),
        range: I16F16::from_num(w.range),
        cooldown_ticks: w.cooldown_ticks,
        cooldown_remaining: 0,
        miss_chance: I16F16::from_num(w.miss_chance),
        crit_chance: I16F16::from_num(w.crit_chance),
        crit_damage: I16F16::from_num(w.crit_damage),
        ammo: w.ammo,
    });

    let preferred_range =
        def.weapon.as_ref().map(|w| I16F16::from_num(w.range)).unwrap_or(I16F16::ZERO);

    let heading = match fleet {
        Fleet::A => I16F16::ZERO,
        Fleet::B => I16F16::from_num(std::f64::consts::PI),
    };

    Ship {
        id,
        fleet,
        is_mothership,
        blueprint_drawing_id: def.blueprint_drawing_id.clone(),
        hull_class: def.hull_class,
        role: def.role,
        pos,
        vel: Vec2::ZERO,
        heading,
        hp: I16F16::from_num(def.hp),
        max_hp: I16F16::from_num(def.max_hp),
        shield_hp: I16F16::from_num(def.shield_hp),
        shield_max_hp: I16F16::from_num(def.shield_max_hp),
        shield_recharge_rate: I16F16::from_num(def.shield_recharge_rate),
        armor: I16F16::from_num(def.armor),
        max_speed: I16F16::from_num(def.speed),
        acceleration: I16F16::from_num(def.acceleration),
        turn_rate: I16F16::from_num(def.turn_rate),
        boid_weights: BoidWeights {
            separation: I16F16::from_num(def.boid_weights.separation),
            cohesion: I16F16::from_num(def.boid_weights.cohesion),
            alignment: I16F16::from_num(def.boid_weights.alignment),
            seek_enemy: I16F16::from_num(def.boid_weights.seek_enemy),
            maintain_range: I16F16::from_num(def.boid_weights.maintain_range),
        },
        weapon,
        preferred_range,
    }
}
