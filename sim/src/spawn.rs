use fixed::types::{I16F16, I32F32};
use rand_core::RngCore;
use rand_xoshiro::Xoshiro256Plus;
use std::collections::BTreeMap;
use types::{FleetJson, HullClass, ShipDef, ShipId};

use crate::{
    config::SimConfig,
    state::{BoidWeights, Fleet, Pos2, Ship, SimState, Vec2, WeaponState},
};

// Returns a map from ShipId → original index in fleet.ships (not the mothership).
// The client uses this to assign post-battle HP back to the correct fleet slot.
pub fn spawn_fleet(
    state: &mut SimState,
    fleet: &FleetJson,
    side: Fleet,
    config: &SimConfig,
) -> BTreeMap<ShipId, usize> {
    let spawn_x = match side {
        Fleet::A => I32F32::from_num(config.fleet_a_spawn_x),
        Fleet::B => I32F32::from_num(config.fleet_b_spawn_x),
    };

    // Sort by hull class while keeping original indices so the client can map back.
    let mut indexed: Vec<(usize, &ShipDef)> = fleet.ships.iter().enumerate().collect();
    indexed.sort_by_key(|(_, s)| hull_class_order(s.hull_class));

    let mut id_to_fleet_index: BTreeMap<ShipId, usize> = BTreeMap::new();
    for (i, (original_idx, def)) in indexed.iter().enumerate() {
        let pos = wedge_pos(spawn_x, i, side, config, &mut state.rng);
        let ship = build_ship(state.alloc_ship_id(), def, side, pos, false);
        id_to_fleet_index.insert(ship.id, *original_idx);
        state.ships.insert(ship.id, ship);
    }

    let ms_pos = Pos2 { x: spawn_x, y: I32F32::ZERO };
    let ms = build_ship(state.alloc_ship_id(), &fleet.mothership, side, ms_pos, true);
    state.ships.insert(ms.id, ms);

    id_to_fleet_index
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
    spawn_x: I32F32,
    index: usize,
    side: Fleet,
    config: &SimConfig,
    rng: &mut Xoshiro256Plus,
) -> Pos2 {
    let discriminant = I32F32::ONE + I32F32::from_num(8 * index);
    let row = ((cordic::sqrt(discriminant) - I32F32::ONE) / I32F32::from_num(2)).to_num::<usize>();
    let pos_in_row = index - row * (row + 1) / 2;

    let row_spacing = I32F32::from_num(40);
    let ship_spacing = I32F32::from_num(35);

    let depth_sign = match side {
        Fleet::A => I32F32::ONE,
        Fleet::B => -I32F32::ONE,
    };
    let depth = spawn_x + depth_sign * I32F32::from_num(row) * row_spacing;
    let row_width = I32F32::from_num(row + 1);
    let lateral = (I32F32::from_num(pos_in_row) - (row_width - I32F32::ONE) / I32F32::from_num(2))
        * ship_spacing;

    let amplitude = I32F32::from_num(config.spawn_noise);
    let noise_x = noise_offset(rng, amplitude);
    let noise_y = noise_offset(rng, amplitude);

    Pos2 { x: depth + noise_x, y: lateral + noise_y }
}

fn noise_offset(rng: &mut Xoshiro256Plus, amplitude: I32F32) -> I32F32 {
    // Use the upper 32 bits so the result is in [0, 1) as an I32F32 fractional value.
    let raw = I32F32::from_bits((rng.next_u64() >> 32) as i64);
    (raw - I32F32::from_num(0.5)) * amplitude
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
