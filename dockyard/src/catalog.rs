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
    ResearchItem {
        id: "hangar_expansion",
        display_name: "Hangar Expansion",
        description: "+2T hangar capacity",
        tech_cost: 1,
        max_purchases: 3,
        track: ResearchTrack::MothershipUpgrades,
        hangar_cap_delta: 2,
    },
    ResearchItem {
        id: "reinforced_hull",
        display_name: "Reinforced Hull",
        description: "Mothership +100 max HP",
        tech_cost: 2,
        max_purchases: 2,
        track: ResearchTrack::MothershipUpgrades,
        hangar_cap_delta: 0,
    },
    ResearchItem {
        id: "weapons_overcharge",
        display_name: "Weapons Overcharge",
        description: "Mothership weapon +5 damage",
        tech_cost: 2,
        max_purchases: 2,
        track: ResearchTrack::MothershipUpgrades,
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
