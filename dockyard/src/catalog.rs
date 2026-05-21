// Re-export shared ship catalog so session.rs can continue to use `crate::catalog::*`.
pub use catalog::{blueprints, Blueprint};

use serde::Serialize;

// ── Research upgrade effects ──────────────────────────────────────────────────

/// Structured effect carried by a ResearchItemOffer; interpreted by the client
/// instead of hard-coded C# lambdas (single source of truth).
#[derive(Debug, Clone, Serialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum UpgradeEffect {
    HangarCap {
        delta: i32,
    },
    /// Adds delta to both current Hp and MaxHp.
    MothershipHp {
        delta: f64,
    },
    MothershipWeaponDamage {
        delta: f64,
    },
    MothershipWeaponRange {
        delta: f64,
    },
    /// Adds delta to both current Hp and MaxHp for every fleet ship.
    FleetHp {
        delta: f64,
    },
    FleetSpeed {
        delta: f64,
    },
    FleetWeaponDamage {
        delta: f64,
    },
    FleetWeaponRange {
        delta: f64,
    },
    FleetWeaponCooldown {
        delta: i32,
        min: i32,
    },
    FleetArmor {
        delta: f64,
        max: f64,
    },
    Salvage {
        delta: i32,
    },
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
    pub effects: &'static [UpgradeEffect],
}

pub const RESEARCH_ITEMS: &[ResearchItem] = &[
    // ── Mothership Upgrades ───────────────────────────────────────────────────
    ResearchItem {
        id: "hangar_expansion",
        display_name: "Hangar Expansion",
        description: "+4T hangar capacity",
        tech_cost: 1,
        max_purchases: 5,
        track: ResearchTrack::MothershipUpgrades,
        effects: &[UpgradeEffect::HangarCap { delta: 4 }],
    },
    ResearchItem {
        id: "expanded_hangar",
        display_name: "Expanded Hangar",
        description: "+8T hangar capacity",
        tech_cost: 2,
        max_purchases: 3,
        track: ResearchTrack::MothershipUpgrades,
        effects: &[UpgradeEffect::HangarCap { delta: 8 }],
    },
    ResearchItem {
        id: "reinforced_hull",
        display_name: "Reinforced Hull",
        description: "Mothership +100 max HP",
        tech_cost: 2,
        max_purchases: 3,
        track: ResearchTrack::MothershipUpgrades,
        effects: &[UpgradeEffect::MothershipHp { delta: 100.0 }],
    },
    ResearchItem {
        id: "weapons_overcharge",
        display_name: "Weapons Overcharge",
        description: "Mothership weapon +5 damage",
        tech_cost: 2,
        max_purchases: 3,
        track: ResearchTrack::MothershipUpgrades,
        effects: &[UpgradeEffect::MothershipWeaponDamage { delta: 5.0 }],
    },
    ResearchItem {
        id: "mothership_range_boost",
        display_name: "Long-Range Sensors",
        description: "Mothership weapon range +40",
        tech_cost: 2,
        max_purchases: 2,
        track: ResearchTrack::MothershipUpgrades,
        effects: &[UpgradeEffect::MothershipWeaponRange { delta: 40.0 }],
    },
    // ── Doctrines ─────────────────────────────────────────────────────────────
    ResearchItem {
        id: "fleet_armor_plating",
        display_name: "Fleet Armor Plating",
        description: "All fleet ships +20 max HP",
        tech_cost: 2,
        max_purchases: 3,
        track: ResearchTrack::Doctrines,
        effects: &[UpgradeEffect::FleetHp { delta: 20.0 }],
    },
    ResearchItem {
        id: "advanced_propulsion",
        display_name: "Advanced Propulsion",
        description: "All fleet ships +1 speed",
        tech_cost: 2,
        max_purchases: 2,
        track: ResearchTrack::Doctrines,
        effects: &[UpgradeEffect::FleetSpeed { delta: 1.0 }],
    },
    ResearchItem {
        id: "targeting_array",
        display_name: "Targeting Array",
        description: "All fleet ship weapons +20 range",
        tech_cost: 2,
        max_purchases: 3,
        track: ResearchTrack::Doctrines,
        effects: &[UpgradeEffect::FleetWeaponRange { delta: 20.0 }],
    },
    ResearchItem {
        id: "rapid_fire_capacitors",
        display_name: "Rapid-Fire Capacitors",
        description: "All fleet ship weapon cooldown -4 ticks (min 5)",
        tech_cost: 3,
        max_purchases: 2,
        track: ResearchTrack::Doctrines,
        effects: &[UpgradeEffect::FleetWeaponCooldown { delta: -4, min: 5 }],
    },
    // ── Role Equipment ────────────────────────────────────────────────────────
    ResearchItem {
        id: "salvage_cache",
        display_name: "Salvage Cache",
        description: "+30 salvage immediately",
        tech_cost: 1,
        max_purchases: 3,
        track: ResearchTrack::RoleEquipment,
        effects: &[UpgradeEffect::Salvage { delta: 30 }],
    },
    ResearchItem {
        id: "fleet_damage_boost",
        display_name: "Weapons Upgrade",
        description: "All fleet ship weapons +5 damage",
        tech_cost: 3,
        max_purchases: 2,
        track: ResearchTrack::RoleEquipment,
        effects: &[UpgradeEffect::FleetWeaponDamage { delta: 5.0 }],
    },
    ResearchItem {
        id: "fleet_armor",
        display_name: "Composite Armor",
        description: "All fleet ships gain 10% armor (damage reduction)",
        tech_cost: 3,
        max_purchases: 1,
        track: ResearchTrack::RoleEquipment,
        effects: &[UpgradeEffect::FleetArmor { delta: 0.10, max: 0.9 }],
    },
];

// ── Tier definitions ──────────────────────────────────────────────────────────

pub struct TierDef {
    pub label: &'static str,
    pub hull_classes: &'static [types::HullClass],
    pub slots: usize,
    pub reroll_cost: i32,
}

pub const TIERS: &[TierDef] = &[
    TierDef {
        label: "Corvette / Fighter",
        hull_classes: &[types::HullClass::Corvette],
        slots: 5,
        reroll_cost: 5,
    },
    TierDef {
        label: "Frigate",
        hull_classes: &[types::HullClass::Frigate],
        slots: 3,
        reroll_cost: 5,
    },
    TierDef {
        label: "Destroyer / Cruiser",
        hull_classes: &[types::HullClass::Destroyer, types::HullClass::Cruiser],
        slots: 2,
        reroll_cost: 5,
    },
    TierDef {
        label: "Capital",
        hull_classes: &[types::HullClass::Battlecruiser, types::HullClass::Dreadnought],
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
