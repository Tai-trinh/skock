use fixed::types::{I16F16, I32F32};
use rand_core::RngCore;
use rand_xoshiro::Xoshiro256Plus;
use std::collections::BTreeMap;
use types::{
    CombatStance, FleetEffect, FleetJson, HardpointDef, HullClass, ProjectileSubtype, ShipDef,
    ShipId, WeaponDef,
};

use crate::{
    config::SimConfig,
    state::{BoidWeights, Fleet, Pos2, Ship, SimState, Vec2, WeaponKind, WeaponState},
};

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

    // Collect all AddHardpoint effects from every effect array.
    let all_effects: Vec<&FleetEffect> = fleet
        .doctrines
        .iter()
        .chain(fleet.role_equipment.iter())
        .chain(fleet.faction_effects.iter())
        .chain(fleet.admiral_effects.iter())
        .collect();

    let mut indexed: Vec<(usize, &ShipDef)> = fleet.ships.iter().enumerate().collect();
    indexed.sort_by_key(|(_, s)| hull_class_order(s.hull_class));

    let mut id_to_fleet_index: BTreeMap<ShipId, usize> = BTreeMap::new();
    for (i, (original_idx, def)) in indexed.iter().enumerate() {
        let pos = wedge_pos(spawn_x, i, side, config, &mut state.rng);
        let ship = build_ship(state.alloc_ship_id(), def, side, pos, false, config, &all_effects);
        id_to_fleet_index.insert(ship.id, *original_idx);
        state.ships.insert(ship.id, ship);
    }

    let ms_pos = Pos2 { x: spawn_x, y: I32F32::ZERO };
    let ms = build_ship(
        state.alloc_ship_id(),
        &fleet.mothership,
        side,
        ms_pos,
        true,
        config,
        &all_effects,
    );
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
    let raw = I32F32::from_bits((rng.next_u64() >> 32) as i64);
    (raw - I32F32::from_num(0.5)) * amplitude
}

fn scope_matches(scope: &Option<types::EffectScope>, def: &ShipDef) -> bool {
    let Some(s) = scope else { return true };
    if let Some(role) = s.role {
        if role != def.role {
            return false;
        }
    }
    if let Some(hull_class) = s.hull_class {
        if hull_class != def.hull_class {
            return false;
        }
    }
    if let Some(weight) = s.weight {
        if def.weight != Some(weight) {
            return false;
        }
    }
    true
}

pub fn build_ship(
    id: ShipId,
    def: &ShipDef,
    fleet: Fleet,
    pos: Pos2,
    is_mothership: bool,
    config: &SimConfig,
    fleet_effects: &[&FleetEffect],
) -> Ship {
    // Collect effective hardpoints: base + AddHardpoint effects that match this ship.
    let mut effective_hardpoints: Vec<&HardpointDef> = def.hardpoints.iter().collect();
    for effect in fleet_effects {
        if let FleetEffect::AddHardpoint { scope, hardpoint } = effect {
            if scope_matches(scope, def) {
                effective_hardpoints.push(hardpoint);
            }
        }
    }

    let weapons: Vec<WeaponState> =
        effective_hardpoints.iter().map(|hp| build_weapon_state(hp, config)).collect();

    // Compute preferred_range from CombatStance.
    let preferred_range = match def.combat_stance {
        CombatStance::Standoff => {
            weapons.iter().map(|w| w.range).max_by(|a, b| a.cmp(b)).unwrap_or(I16F16::ZERO)
        }
        CombatStance::Brawl => I16F16::ZERO,
        CombatStance::Broadside => {
            weapons.iter().map(|w| w.range).min_by(|a, b| a.cmp(b)).unwrap_or(I16F16::ZERO)
        }
    };

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
            seek_nearest: I16F16::from_num(def.boid_weights.seek_nearest),
            seek_mass: I16F16::from_num(def.boid_weights.seek_mass),
            seek_mothership: I16F16::from_num(def.boid_weights.seek_mothership),
            maintain_range: I16F16::from_num(def.boid_weights.maintain_range),
        },
        weapons,
        target_priority: def.target_priority,
        combat_stance: def.combat_stance,
        preferred_range,
        hit_radius: {
            let r =
                config.hull_hit_radii.get(&format!("{:?}", def.hull_class)).copied().unwrap_or(8.0);
            I16F16::from_num(r)
        },
        low_hp_flagged: false,
    }
}

fn build_weapon_state(hp: &HardpointDef, config: &SimConfig) -> WeaponState {
    let fire_forward = I16F16::from_num(hp.forward);
    let fire_lateral = I16F16::from_num(hp.lateral);
    let salvo_count = hp.salvo_count.max(1);
    let salvo_spread_angle = I16F16::from_num(hp.salvo_spread_angle);

    match &hp.weapon {
        WeaponDef::Hitscan {
            damage,
            range,
            cooldown_ticks,
            miss_chance_far,
            miss_chance_near,
            accurate_range,
            crit_chance,
            crit_damage,
            ammo,
        } => WeaponState {
            damage: I16F16::from_num(*damage),
            range: I16F16::from_num(*range),
            cooldown_ticks: *cooldown_ticks,
            cooldown_remaining: 0,
            crit_chance: I16F16::from_num(*crit_chance),
            crit_damage: I16F16::from_num(*crit_damage),
            ammo: *ammo,
            fire_forward,
            fire_lateral,
            salvo_count,
            salvo_spread_angle,
            kind: WeaponKind::Hitscan {
                miss_chance_far: I16F16::from_num(*miss_chance_far),
                miss_chance_near: I16F16::from_num(*miss_chance_near),
                accurate_range: I16F16::from_num(*accurate_range),
            },
        },

        WeaponDef::Projectile {
            subtype,
            damage,
            range,
            cooldown_ticks,
            projectile_speed,
            turn_rate,
            fuse_ticks,
            explosion_radius,
            explosion_damage,
            crit_chance,
            crit_damage,
            ammo,
        } => {
            let st = subtype.unwrap_or(ProjectileSubtype::SeekingMissile);
            let key = match st {
                ProjectileSubtype::SeekingMissile => "seeking_missile",
                ProjectileSubtype::Torpedo => "torpedo",
                ProjectileSubtype::Mine => "mine",
            };
            let hit_r = config.projectile_hit_radii.get(key).copied().unwrap_or(2.0);

            WeaponState {
                damage: I16F16::from_num(*damage),
                range: I16F16::from_num(*range),
                cooldown_ticks: *cooldown_ticks,
                cooldown_remaining: 0,
                crit_chance: I16F16::from_num(*crit_chance),
                crit_damage: I16F16::from_num(*crit_damage),
                ammo: *ammo,
                fire_forward,
                fire_lateral,
                salvo_count,
                salvo_spread_angle,
                kind: WeaponKind::Projectile {
                    subtype: st,
                    speed: I16F16::from_num(*projectile_speed),
                    turn_rate: I16F16::from_num(*turn_rate),
                    fuse_ticks: *fuse_ticks,
                    explosion_radius: I16F16::from_num(*explosion_radius),
                    explosion_damage: I16F16::from_num(*explosion_damage),
                    hit_radius: I16F16::from_num(hit_r),
                },
            }
        }

        WeaponDef::Beam {
            damage,
            range,
            cooldown_ticks,
            charge_ticks,
            duration_ticks,
            beam_width,
            slew_rate,
            track_rate,
            ramp_ticks,
            ramp_max,
            crit_chance,
            crit_damage,
            ammo,
        } => WeaponState {
            damage: I16F16::from_num(*damage),
            range: I16F16::from_num(*range),
            cooldown_ticks: *cooldown_ticks,
            cooldown_remaining: 0,
            crit_chance: I16F16::from_num(*crit_chance),
            crit_damage: I16F16::from_num(*crit_damage),
            ammo: *ammo,
            fire_forward,
            fire_lateral,
            salvo_count,
            salvo_spread_angle,
            kind: WeaponKind::Beam {
                charge_ticks: *charge_ticks,
                duration_ticks: *duration_ticks,
                beam_width: I16F16::from_num(*beam_width),
                slew_rate: I16F16::from_num(*slew_rate),
                track_rate: I16F16::from_num(*track_rate),
                ramp_ticks: *ramp_ticks,
                ramp_max: I16F16::from_num(*ramp_max),
            },
        },
    }
}
