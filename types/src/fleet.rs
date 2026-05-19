use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FleetJson {
    pub faction: String,
    pub admiral_id: String,
    pub formation: String,
    pub mothership: ShipDef,
    pub ships: Vec<ShipDef>,
    #[serde(default)]
    pub doctrines: Vec<FleetEffect>,
    #[serde(default)]
    pub role_equipment: Vec<FleetEffect>,
    #[serde(default)]
    pub faction_effects: Vec<FleetEffect>,
    #[serde(default)]
    pub admiral_effects: Vec<FleetEffect>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ShipDef {
    pub blueprint_drawing_id: String,
    pub hull_class: HullClass,
    pub role: Role,
    #[serde(default)]
    pub weight: Option<Weight>,
    pub hp: f64,
    pub max_hp: f64,
    pub speed: f64,
    pub acceleration: f64,
    pub turn_rate: f64,
    pub boid_weights: BoidWeightsDef,
    #[serde(default)]
    pub armor: f64,
    #[serde(default)]
    pub shield_hp: f64,
    #[serde(default)]
    pub shield_max_hp: f64,
    #[serde(default)]
    pub shield_recharge_rate: f64,
    #[serde(default)]
    pub weapon: Option<WeaponDef>,
    #[serde(default)]
    pub equipment: Vec<FleetEffect>,
}

// ── Fleet effects ─────────────────────────────────────────────────────────────

/// Scope filter for a fleet effect. Any omitted field is unconstrained.
/// A `null` scope in JSON (represented as `Option<EffectScope>` = None) means fleet-wide.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct EffectScope {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub role: Option<Role>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub hull_class: Option<HullClass>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub weight: Option<Weight>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ModifierType {
    /// Additive: bonuses sum, then applied as `base * (1 + total)`.
    Increased,
    /// Additive decrease: negative modifier, same stacking as Increased.
    Decreased,
    /// Multiplicative: each bonus multiplies independently.
    More,
    /// Multiplicative decrease: sub-1.0 multiplier.
    Less,
}

/// A single typed effect entry carried in `doctrines`, `role_equipment`,
/// `faction_effects`, `admiral_effects`, or per-ship `equipment` arrays.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum FleetEffect {
    StatModifier {
        /// `null` = fleet-wide. Combine `role`, `hull_class`, `weight` to narrow.
        scope: Option<EffectScope>,
        stat: String,
        modifier_type: ModifierType,
        modifier: f64,
    },
    HpRegen {
        value: f64,
    },
    DamageReduction {
        value: f64,
    },
}

// ── Weapon definitions ────────────────────────────────────────────────────────

/// Typed weapon definition — discriminated by the `"type"` JSON field.
/// Each variant only carries fields relevant to that weapon archetype.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum WeaponDef {
    Hitscan {
        damage: f64,
        range: f64,
        cooldown_ticks: u32,
        #[serde(default)]
        miss_chance: f64,
        #[serde(default)]
        crit_chance: f64,
        #[serde(default = "default_one")]
        crit_damage: f64,
        #[serde(default)]
        ammo: Option<u32>,
    },
    Projectile {
        #[serde(default)]
        subtype: Option<ProjectileSubtype>,
        damage: f64,
        range: f64,
        cooldown_ticks: u32,
        #[serde(default)]
        projectile_speed: f64,
        #[serde(default)]
        turn_rate: f64,
        #[serde(default)]
        fuse_ticks: u32,
        #[serde(default)]
        explosion_radius: f64,
        #[serde(default)]
        explosion_damage: f64,
        #[serde(default)]
        crit_chance: f64,
        #[serde(default = "default_one")]
        crit_damage: f64,
        #[serde(default)]
        ammo: Option<u32>,
    },
    Beam {
        damage: f64,
        range: f64,
        cooldown_ticks: u32,
        #[serde(default)]
        charge_ticks: u32,
        #[serde(default)]
        duration_ticks: u32,
        #[serde(default)]
        beam_width: f64,
        /// Angular velocity (rad/tick) while beam is not hitting any enemy — fast re-acquisition.
        #[serde(default)]
        slew_rate: f64,
        /// Angular velocity (rad/tick) while beam is firing on an enemy — slow tracking.
        #[serde(default)]
        track_rate: f64,
        /// Ticks to ramp from base damage to ramp_max × damage. 0 = no ramp.
        #[serde(default)]
        ramp_ticks: u32,
        /// Maximum damage multiplier at full ramp. 1.0 = no ramp.
        #[serde(default = "default_one")]
        ramp_max: f64,
        #[serde(default)]
        crit_chance: f64,
        #[serde(default = "default_one")]
        crit_damage: f64,
        #[serde(default)]
        ammo: Option<u32>,
    },
}

impl WeaponDef {
    pub fn range(&self) -> f64 {
        match self {
            Self::Hitscan { range, .. } => *range,
            Self::Projectile { range, .. } => *range,
            Self::Beam { range, .. } => *range,
        }
    }
}

/// Runtime weapon type tag — used by `WeaponState` to identify archetype at sim time.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WeaponType {
    Hitscan,
    Projectile,
    Beam,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ProjectileSubtype {
    SeekingMissile,
    Torpedo,
    Mine,
}

// ── Other types ───────────────────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct BoidWeightsDef {
    #[serde(default = "default_one")]
    pub separation: f64,
    #[serde(default = "default_one")]
    pub cohesion: f64,
    #[serde(default = "default_one")]
    pub alignment: f64,
    #[serde(default = "default_one")]
    pub seek_enemy: f64,
    #[serde(default = "default_one")]
    pub maintain_range: f64,
}

fn default_one() -> f64 {
    1.0
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum HullClass {
    Corvette,
    Frigate,
    Destroyer,
    Cruiser,
    Battlecruiser,
    Dreadnought,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum Role {
    Fighter,
    Missile,
    Torpedo,
    Mine,
    PointDefense,
    Artillery,
    Railgun,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum Weight {
    Light,
    Heavy,
}
