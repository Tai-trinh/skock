pub use catalog::{find_blueprint, mothership_template};
use types::{EffectScope, FleetEffect, ModifierType, Role};

pub struct FactionDef {
    pub id: &'static str,
    pub display_name: &'static str,
    pub effects: fn() -> Vec<FleetEffect>,
}

pub struct AdmiralDef {
    pub id: &'static str,
    pub name: &'static str,
    pub faction_id: &'static str,
    pub bonus_text: &'static str,
    pub starting_salvage: i32,
    pub starting_tech: i32,
    pub starting_hangar_capacity: i32,
    pub starting_blueprint_ids: &'static [&'static str],
    pub effects: fn() -> Vec<FleetEffect>,
}

// ── Factions ──────────────────────────────────────────────────────────────────

pub const FACTIONS: &[FactionDef] = &[
    FactionDef { id: "gallforce", display_name: "Gallforce", effects: gallforce_effects },
    FactionDef { id: "iron_legion", display_name: "Iron Legion", effects: iron_legion_effects },
    FactionDef { id: "freetraders", display_name: "Freetraders", effects: freetraders_effects },
];

fn gallforce_effects() -> Vec<FleetEffect> {
    vec![FleetEffect::StatModifier {
        scope: Some(EffectScope { role: Some(Role::Fighter), hull_class: None, weight: None }),
        stat: "speed".into(),
        modifier_type: ModifierType::Increased,
        modifier: 0.10,
    }]
}

fn iron_legion_effects() -> Vec<FleetEffect> {
    vec![FleetEffect::StatModifier {
        scope: Some(EffectScope { role: Some(Role::Artillery), hull_class: None, weight: None }),
        stat: "range".into(),
        modifier_type: ModifierType::Increased,
        modifier: 0.10,
    }]
}

fn freetraders_effects() -> Vec<FleetEffect> {
    vec![FleetEffect::DamageReduction { value: 0.05 }]
}

// ── Admirals ──────────────────────────────────────────────────────────────────

pub const ADMIRALS: &[AdmiralDef] = &[
    AdmiralDef {
        id: "kira",
        name: "Admiral Kira",
        faction_id: "gallforce",
        bonus_text: "All Fighters +15% speed. Gallforce: Fighters +10% speed.",
        starting_salvage: 60,
        starting_tech: 0,
        starting_hangar_capacity: 12,
        starting_blueprint_ids: &["fighter_corvette", "fighter_corvette"],
        effects: kira_effects,
    },
    AdmiralDef {
        id: "voss",
        name: "Admiral Voss",
        faction_id: "iron_legion",
        bonus_text: "Artillery weapons +10% damage. Iron Legion: Artillery +10% range.",
        starting_salvage: 30,
        starting_tech: 1,
        starting_hangar_capacity: 12,
        starting_blueprint_ids: &["artillery_destroyer"],
        effects: voss_effects,
    },
    AdmiralDef {
        id: "shen",
        name: "Admiral Shen",
        faction_id: "freetraders",
        bonus_text: "Hangar capacity +4T. Freetraders: Fleet-wide 5% damage reduction.",
        starting_salvage: 40,
        starting_tech: 0,
        starting_hangar_capacity: 16,
        starting_blueprint_ids: &["fighter_frigate"],
        effects: shen_effects,
    },
];

fn kira_effects() -> Vec<FleetEffect> {
    vec![FleetEffect::StatModifier {
        scope: Some(EffectScope { role: Some(Role::Fighter), hull_class: None, weight: None }),
        stat: "speed".into(),
        modifier_type: ModifierType::Increased,
        modifier: 0.15,
    }]
}

fn voss_effects() -> Vec<FleetEffect> {
    vec![FleetEffect::StatModifier {
        scope: Some(EffectScope { role: Some(Role::Artillery), hull_class: None, weight: None }),
        stat: "damage".into(),
        modifier_type: ModifierType::Increased,
        modifier: 0.10,
    }]
}

fn shen_effects() -> Vec<FleetEffect> {
    vec![]
}
