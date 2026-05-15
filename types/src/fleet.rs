use serde::{Deserialize, Serialize};
use serde_json::Value;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FleetJson {
    pub faction: String,
    pub admiral_id: String,
    pub formation: String,
    pub mothership: ShipDef,
    pub ships: Vec<ShipDef>,
    #[serde(default)]
    pub doctrines: Vec<Value>,
    #[serde(default)]
    pub role_equipment: Vec<Value>,
    #[serde(default)]
    pub faction_effects: Vec<Value>,
    #[serde(default)]
    pub admiral_effects: Vec<Value>,
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
    pub equipment: Vec<Value>,
}

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

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct WeaponDef {
    #[serde(rename = "type")]
    pub weapon_type: WeaponType,
    pub damage: f64,
    pub range: f64,
    pub cooldown_ticks: u32,
    #[serde(default)]
    pub miss_chance: f64,
    #[serde(default)]
    pub crit_chance: f64,
    #[serde(default = "default_one")]
    pub crit_damage: f64,
    #[serde(default)]
    pub ammo: Option<u32>,
    // Projectile fields
    #[serde(default)]
    pub subtype: Option<ProjectileSubtype>,
    #[serde(default)]
    pub projectile_speed: f64,
    #[serde(default)]
    pub turn_rate: f64,
    #[serde(default)]
    pub fuse_ticks: u32,
    #[serde(default)]
    pub explosion_radius: f64,
    #[serde(default)]
    pub explosion_damage: f64,
    // Beam fields
    #[serde(default)]
    pub charge_ticks: u32,
    #[serde(default)]
    pub duration_ticks: u32,
    #[serde(default)]
    pub beam_width: f64,
    // Status effect fields
    #[serde(default)]
    pub stun_ticks: u32,
    #[serde(default)]
    pub burn_damage: f64,
    #[serde(default)]
    pub burn_ticks: u32,
    #[serde(default)]
    pub radiation_damage: f64,
    #[serde(default)]
    pub radiation_ticks: u32,
}

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
    Plasma,
    Railgun,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum Weight {
    Light,
    Heavy,
}
