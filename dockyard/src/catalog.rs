use std::sync::OnceLock;

use types::{BoidWeightsDef, HullClass, Role, ShipDef, WeaponDef, WeaponType};

// ── Hull tonnage ──────────────────────────────────────────────────────────────

pub fn tonnage(hull_class: HullClass) -> i32 {
    match hull_class {
        HullClass::Corvette => 2,
        HullClass::Frigate => 4,
        HullClass::Destroyer => 6,
        HullClass::Cruiser => 10,
        HullClass::Battlecruiser => 16,
        HullClass::Dreadnought => 24,
    }
}

// ── Blueprint catalog ─────────────────────────────────────────────────────────

pub struct Blueprint {
    pub id: &'static str,
    pub display_name: &'static str,
    pub salvage_cost: i32,
    pub hull_class: HullClass,
    pub ship_def: ShipDef,
}

impl Blueprint {
    pub fn tonnage(&self) -> i32 {
        tonnage(self.hull_class)
    }
}

static BLUEPRINTS: OnceLock<Vec<Blueprint>> = OnceLock::new();

pub fn blueprints() -> &'static [Blueprint] {
    BLUEPRINTS.get_or_init(build_blueprints).as_slice()
}

fn hitscan(damage: f64, range: f64, cooldown_ticks: u32) -> WeaponDef {
    WeaponDef {
        weapon_type: WeaponType::Hitscan,
        damage,
        range,
        cooldown_ticks,
        miss_chance: 0.0,
        crit_chance: 0.0,
        crit_damage: 1.0,
        ammo: None,
        subtype: None,
        projectile_speed: 0.0,
        turn_rate: 0.0,
        fuse_ticks: 0,
        explosion_radius: 0.0,
        explosion_damage: 0.0,
        charge_ticks: 0,
        duration_ticks: 0,
        beam_width: 0.0,
        stun_ticks: 0,
        burn_damage: 0.0,
        burn_ticks: 0,
        radiation_damage: 0.0,
        radiation_ticks: 0,
    }
}

fn build_blueprints() -> Vec<Blueprint> {
    vec![
        // ── Corvettes (2T) ────────────────────────────────────────────────────
        Blueprint {
            id: "fighter_corvette",
            display_name: "Fighter Corvette",
            salvage_cost: 10,
            hull_class: HullClass::Corvette,
            ship_def: ShipDef {
                blueprint_drawing_id: "corvette_a".into(),
                hull_class: HullClass::Corvette,
                role: Role::Fighter,
                weight: None,
                hp: 60.0,
                max_hp: 60.0,
                speed: 8.0,
                acceleration: 2.0,
                turn_rate: 1.2,
                boid_weights: BoidWeightsDef {
                    separation: 1.5,
                    cohesion: 0.4,
                    alignment: 0.3,
                    seek_enemy: 2.0,
                    maintain_range: 1.0,
                },
                armor: 0.0,
                shield_hp: 0.0,
                shield_max_hp: 0.0,
                shield_recharge_rate: 0.0,
                weapon: Some(hitscan(10.0, 80.0, 15)),
                equipment: vec![],
            },
        },
        Blueprint {
            id: "missile_corvette",
            display_name: "Missile Corvette",
            salvage_cost: 15,
            hull_class: HullClass::Corvette,
            ship_def: ShipDef {
                blueprint_drawing_id: "corvette_a".into(),
                hull_class: HullClass::Corvette,
                role: Role::Missile,
                weight: None,
                hp: 50.0,
                max_hp: 50.0,
                speed: 9.0,
                acceleration: 2.5,
                turn_rate: 1.5,
                boid_weights: BoidWeightsDef {
                    separation: 2.0,
                    cohesion: 0.3,
                    alignment: 0.2,
                    seek_enemy: 1.8,
                    maintain_range: 1.5,
                },
                armor: 0.0,
                shield_hp: 0.0,
                shield_max_hp: 0.0,
                shield_recharge_rate: 0.0,
                weapon: Some(hitscan(8.0, 120.0, 10)),
                equipment: vec![],
            },
        },
        // ── Frigates (4T) ────────────────────────────────────────────────────
        Blueprint {
            id: "fighter_frigate",
            display_name: "Fighter Frigate",
            salvage_cost: 25,
            hull_class: HullClass::Frigate,
            ship_def: ShipDef {
                blueprint_drawing_id: "frigate_a".into(),
                hull_class: HullClass::Frigate,
                role: Role::Fighter,
                weight: None,
                hp: 130.0,
                max_hp: 130.0,
                speed: 5.0,
                acceleration: 1.2,
                turn_rate: 0.8,
                boid_weights: BoidWeightsDef {
                    separation: 1.2,
                    cohesion: 0.5,
                    alignment: 0.4,
                    seek_enemy: 1.5,
                    maintain_range: 1.2,
                },
                armor: 0.0,
                shield_hp: 0.0,
                shield_max_hp: 0.0,
                shield_recharge_rate: 0.0,
                weapon: Some(hitscan(20.0, 100.0, 20)),
                equipment: vec![],
            },
        },
        Blueprint {
            id: "point_defense_frigate",
            display_name: "Point Defense Frigate",
            salvage_cost: 30,
            hull_class: HullClass::Frigate,
            ship_def: ShipDef {
                blueprint_drawing_id: "frigate_a".into(),
                hull_class: HullClass::Frigate,
                role: Role::PointDefense,
                weight: None,
                hp: 110.0,
                max_hp: 110.0,
                speed: 4.5,
                acceleration: 1.0,
                turn_rate: 0.7,
                boid_weights: BoidWeightsDef {
                    separation: 1.8,
                    cohesion: 0.6,
                    alignment: 0.5,
                    seek_enemy: 1.0,
                    maintain_range: 2.0,
                },
                armor: 0.0,
                shield_hp: 0.0,
                shield_max_hp: 0.0,
                shield_recharge_rate: 0.0,
                weapon: Some(hitscan(6.0, 65.0, 8)),
                equipment: vec![],
            },
        },
        Blueprint {
            id: "missile_frigate",
            display_name: "Missile Frigate",
            salvage_cost: 35,
            hull_class: HullClass::Frigate,
            ship_def: ShipDef {
                blueprint_drawing_id: "frigate_a".into(),
                hull_class: HullClass::Frigate,
                role: Role::Missile,
                weight: None,
                hp: 120.0,
                max_hp: 120.0,
                speed: 4.0,
                acceleration: 1.0,
                turn_rate: 0.6,
                boid_weights: BoidWeightsDef {
                    separation: 1.3,
                    cohesion: 0.4,
                    alignment: 0.3,
                    seek_enemy: 1.2,
                    maintain_range: 2.0,
                },
                armor: 0.0,
                shield_hp: 0.0,
                shield_max_hp: 0.0,
                shield_recharge_rate: 0.0,
                weapon: Some(hitscan(15.0, 140.0, 18)),
                equipment: vec![],
            },
        },
        // ── Destroyers (6T) ──────────────────────────────────────────────────
        Blueprint {
            id: "artillery_destroyer",
            display_name: "Artillery Destroyer",
            salvage_cost: 45,
            hull_class: HullClass::Destroyer,
            ship_def: ShipDef {
                blueprint_drawing_id: "destroyer_a".into(),
                hull_class: HullClass::Destroyer,
                role: Role::Artillery,
                weight: None,
                hp: 200.0,
                max_hp: 200.0,
                speed: 3.0,
                acceleration: 0.8,
                turn_rate: 0.5,
                boid_weights: BoidWeightsDef {
                    separation: 1.0,
                    cohesion: 0.3,
                    alignment: 0.3,
                    seek_enemy: 0.8,
                    maintain_range: 1.5,
                },
                armor: 0.0,
                shield_hp: 0.0,
                shield_max_hp: 0.0,
                shield_recharge_rate: 0.0,
                weapon: Some(hitscan(40.0, 150.0, 45)),
                equipment: vec![],
            },
        },
        Blueprint {
            id: "torpedo_destroyer",
            display_name: "Torpedo Destroyer",
            salvage_cost: 55,
            hull_class: HullClass::Destroyer,
            ship_def: ShipDef {
                blueprint_drawing_id: "destroyer_a".into(),
                hull_class: HullClass::Destroyer,
                role: Role::Torpedo,
                weight: None,
                hp: 250.0,
                max_hp: 250.0,
                speed: 2.5,
                acceleration: 0.6,
                turn_rate: 0.4,
                boid_weights: BoidWeightsDef {
                    separation: 1.0,
                    cohesion: 0.2,
                    alignment: 0.2,
                    seek_enemy: 0.6,
                    maintain_range: 2.5,
                },
                armor: 0.0,
                shield_hp: 0.0,
                shield_max_hp: 0.0,
                shield_recharge_rate: 0.0,
                weapon: Some(hitscan(90.0, 180.0, 90)),
                equipment: vec![],
            },
        },
        Blueprint {
            id: "railgun_destroyer",
            display_name: "Railgun Destroyer",
            salvage_cost: 60,
            hull_class: HullClass::Destroyer,
            ship_def: ShipDef {
                blueprint_drawing_id: "destroyer_a".into(),
                hull_class: HullClass::Destroyer,
                role: Role::Railgun,
                weight: None,
                hp: 150.0,
                max_hp: 150.0,
                speed: 2.0,
                acceleration: 0.5,
                turn_rate: 0.3,
                boid_weights: BoidWeightsDef {
                    separation: 1.2,
                    cohesion: 0.2,
                    alignment: 0.2,
                    seek_enemy: 0.5,
                    maintain_range: 3.0,
                },
                armor: 0.0,
                shield_hp: 0.0,
                shield_max_hp: 0.0,
                shield_recharge_rate: 0.0,
                weapon: Some(hitscan(65.0, 260.0, 60)),
                equipment: vec![],
            },
        },
        // ── Cruisers (10T) ───────────────────────────────────────────────────
        Blueprint {
            id: "fighter_cruiser",
            display_name: "Fighter Cruiser",
            salvage_cost: 85,
            hull_class: HullClass::Cruiser,
            ship_def: ShipDef {
                blueprint_drawing_id: "cruiser_a".into(),
                hull_class: HullClass::Cruiser,
                role: Role::Fighter,
                weight: None,
                hp: 350.0,
                max_hp: 350.0,
                speed: 5.0,
                acceleration: 1.0,
                turn_rate: 0.7,
                boid_weights: BoidWeightsDef {
                    separation: 1.0,
                    cohesion: 0.6,
                    alignment: 0.4,
                    seek_enemy: 1.8,
                    maintain_range: 1.0,
                },
                armor: 0.0,
                shield_hp: 0.0,
                shield_max_hp: 0.0,
                shield_recharge_rate: 0.0,
                weapon: Some(hitscan(28.0, 95.0, 18)),
                equipment: vec![],
            },
        },
        Blueprint {
            id: "artillery_cruiser",
            display_name: "Artillery Cruiser",
            salvage_cost: 95,
            hull_class: HullClass::Cruiser,
            ship_def: ShipDef {
                blueprint_drawing_id: "cruiser_a".into(),
                hull_class: HullClass::Cruiser,
                role: Role::Artillery,
                weight: None,
                hp: 280.0,
                max_hp: 280.0,
                speed: 2.5,
                acceleration: 0.6,
                turn_rate: 0.4,
                boid_weights: BoidWeightsDef {
                    separation: 1.0,
                    cohesion: 0.3,
                    alignment: 0.3,
                    seek_enemy: 0.7,
                    maintain_range: 2.0,
                },
                armor: 0.0,
                shield_hp: 0.0,
                shield_max_hp: 0.0,
                shield_recharge_rate: 0.0,
                weapon: Some(hitscan(70.0, 210.0, 55)),
                equipment: vec![],
            },
        },
        // ── Battlecruisers (16T) ─────────────────────────────────────────────
        Blueprint {
            id: "assault_battlecruiser",
            display_name: "Assault Battlecruiser",
            salvage_cost: 160,
            hull_class: HullClass::Battlecruiser,
            ship_def: ShipDef {
                blueprint_drawing_id: "battlecruiser_a".into(),
                hull_class: HullClass::Battlecruiser,
                role: Role::Fighter,
                weight: None,
                hp: 600.0,
                max_hp: 600.0,
                speed: 3.5,
                acceleration: 0.7,
                turn_rate: 0.4,
                boid_weights: BoidWeightsDef {
                    separation: 0.8,
                    cohesion: 0.5,
                    alignment: 0.4,
                    seek_enemy: 1.5,
                    maintain_range: 1.0,
                },
                armor: 0.0,
                shield_hp: 0.0,
                shield_max_hp: 0.0,
                shield_recharge_rate: 0.0,
                weapon: Some(hitscan(55.0, 120.0, 30)),
                equipment: vec![],
            },
        },
        Blueprint {
            id: "siege_battlecruiser",
            display_name: "Siege Battlecruiser",
            salvage_cost: 180,
            hull_class: HullClass::Battlecruiser,
            ship_def: ShipDef {
                blueprint_drawing_id: "battlecruiser_a".into(),
                hull_class: HullClass::Battlecruiser,
                role: Role::Artillery,
                weight: None,
                hp: 450.0,
                max_hp: 450.0,
                speed: 2.0,
                acceleration: 0.5,
                turn_rate: 0.3,
                boid_weights: BoidWeightsDef {
                    separation: 1.0,
                    cohesion: 0.2,
                    alignment: 0.2,
                    seek_enemy: 0.5,
                    maintain_range: 2.5,
                },
                armor: 0.0,
                shield_hp: 0.0,
                shield_max_hp: 0.0,
                shield_recharge_rate: 0.0,
                weapon: Some(hitscan(120.0, 250.0, 75)),
                equipment: vec![],
            },
        },
    ]
}

// ── Research catalog ──────────────────────────────────────────────────────────

#[derive(Clone, Copy, PartialEq, Eq)]
pub enum ResearchTrack {
    MothershipUpgrades,
    Doctrines,
    RoleEquipment,
    ShipEquipment,
}

pub struct ResearchItem {
    pub id: &'static str,
    pub display_name: &'static str,
    pub description: &'static str,
    pub tech_cost: i32,
    pub max_purchases: i32,
    pub track: ResearchTrack,
    /// Applied to session hangar_cap immediately on purchase (0 = no effect).
    pub hangar_cap_delta: i32,
}

pub const RESEARCH_ITEMS: &[ResearchItem] = &[
    // ── Mothership Upgrades ───────────────────────────────────────────────────
    ResearchItem {
        id: "hangar_expansion",
        display_name: "Hangar Expansion",
        description: "+2T hangar capacity",
        tech_cost: 1,
        max_purchases: 5,
        track: ResearchTrack::MothershipUpgrades,
        hangar_cap_delta: 2,
    },
    ResearchItem {
        id: "expanded_hangar",
        display_name: "Expanded Hangar",
        description: "+4T hangar capacity",
        tech_cost: 2,
        max_purchases: 3,
        track: ResearchTrack::MothershipUpgrades,
        hangar_cap_delta: 4,
    },
    ResearchItem {
        id: "reinforced_hull",
        display_name: "Reinforced Hull",
        description: "Mothership +100 max HP",
        tech_cost: 2,
        max_purchases: 3,
        track: ResearchTrack::MothershipUpgrades,
        hangar_cap_delta: 0,
    },
    ResearchItem {
        id: "weapons_overcharge",
        display_name: "Weapons Overcharge",
        description: "Mothership weapon +5 damage",
        tech_cost: 2,
        max_purchases: 3,
        track: ResearchTrack::MothershipUpgrades,
        hangar_cap_delta: 0,
    },
    ResearchItem {
        id: "mothership_range_boost",
        display_name: "Long-Range Sensors",
        description: "Mothership weapon range +40",
        tech_cost: 2,
        max_purchases: 2,
        track: ResearchTrack::MothershipUpgrades,
        hangar_cap_delta: 0,
    },
    // ── Doctrines ─────────────────────────────────────────────────────────────
    ResearchItem {
        id: "fleet_armor_plating",
        display_name: "Fleet Armor Plating",
        description: "All fleet ships +20 max HP",
        tech_cost: 2,
        max_purchases: 3,
        track: ResearchTrack::Doctrines,
        hangar_cap_delta: 0,
    },
    ResearchItem {
        id: "advanced_propulsion",
        display_name: "Advanced Propulsion",
        description: "All fleet ships +1 speed",
        tech_cost: 2,
        max_purchases: 2,
        track: ResearchTrack::Doctrines,
        hangar_cap_delta: 0,
    },
    ResearchItem {
        id: "targeting_array",
        display_name: "Targeting Array",
        description: "All fleet ship weapons +20 range",
        tech_cost: 2,
        max_purchases: 3,
        track: ResearchTrack::Doctrines,
        hangar_cap_delta: 0,
    },
    ResearchItem {
        id: "rapid_fire_capacitors",
        display_name: "Rapid-Fire Capacitors",
        description: "All fleet ship weapon cooldown -4 ticks (min 5)",
        tech_cost: 3,
        max_purchases: 2,
        track: ResearchTrack::Doctrines,
        hangar_cap_delta: 0,
    },
    // ── Role Equipment ────────────────────────────────────────────────────────
    ResearchItem {
        id: "salvage_cache",
        display_name: "Salvage Cache",
        description: "+30 salvage immediately",
        tech_cost: 1,
        max_purchases: 3,
        track: ResearchTrack::RoleEquipment,
        hangar_cap_delta: 0,
    },
    ResearchItem {
        id: "fleet_damage_boost",
        display_name: "Weapons Upgrade",
        description: "All fleet ship weapons +5 damage",
        tech_cost: 3,
        max_purchases: 2,
        track: ResearchTrack::RoleEquipment,
        hangar_cap_delta: 0,
    },
    ResearchItem {
        id: "fleet_armor",
        display_name: "Composite Armor",
        description: "All fleet ships gain 10% armor (damage reduction)",
        tech_cost: 3,
        max_purchases: 1,
        track: ResearchTrack::RoleEquipment,
        hangar_cap_delta: 0,
    },
];

// ── Tier definitions ──────────────────────────────────────────────────────────

pub struct TierDef {
    pub label: &'static str,
    pub hull_classes: &'static [HullClass],
    pub slots: usize,
    pub reroll_cost: i32,
}

pub const TIERS: &[TierDef] = &[
    TierDef {
        label: "Corvette / Fighter",
        hull_classes: &[HullClass::Corvette],
        slots: 5,
        reroll_cost: 5,
    },
    TierDef { label: "Frigate", hull_classes: &[HullClass::Frigate], slots: 3, reroll_cost: 5 },
    TierDef {
        label: "Destroyer / Cruiser",
        hull_classes: &[HullClass::Destroyer, HullClass::Cruiser],
        slots: 2,
        reroll_cost: 5,
    },
    TierDef {
        label: "Capital",
        hull_classes: &[HullClass::Battlecruiser, HullClass::Dreadnought],
        slots: 1,
        reroll_cost: 5,
    },
];

// ── Research track definitions ────────────────────────────────────────────────

pub struct ResearchTrackDef {
    pub label: &'static str,
    pub track: ResearchTrack,
    /// None = always-available track (Mothership Upgrades), no reroll.
    pub reroll_cost: Option<i32>,
    /// Max items shown per jump for randomised tracks.
    pub offer_slots: usize,
}

pub const RESEARCH_TRACKS: &[ResearchTrackDef] = &[
    ResearchTrackDef {
        label: "Mothership Upgrades",
        track: ResearchTrack::MothershipUpgrades,
        reroll_cost: None,
        offer_slots: 0, // unused — always-available shows all non-maxed items
    },
    ResearchTrackDef {
        label: "Doctrines",
        track: ResearchTrack::Doctrines,
        reroll_cost: Some(1),
        offer_slots: 3,
    },
    ResearchTrackDef {
        label: "Role Equipment",
        track: ResearchTrack::RoleEquipment,
        reroll_cost: Some(1),
        offer_slots: 3,
    },
    ResearchTrackDef {
        label: "Ship Equipment",
        track: ResearchTrack::ShipEquipment,
        reroll_cost: Some(1),
        offer_slots: 2,
    },
];
