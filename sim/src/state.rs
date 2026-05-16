use fixed::types::{I16F16, I32F32};
use rand_xoshiro::Xoshiro256Plus;
use std::collections::BTreeMap;
use types::{HullClass, Role, ShipId, WeaponType};

#[derive(Debug, Clone, Copy)]
pub struct Pos2 {
    pub x: I32F32,
    pub y: I32F32,
}

impl Pos2 {
    pub const ZERO: Self = Self { x: I32F32::ZERO, y: I32F32::ZERO };

    pub fn from_f64(x: f64, y: f64) -> Self {
        Self { x: I32F32::from_num(x), y: I32F32::from_num(y) }
    }
}

#[derive(Debug, Clone, Copy)]
pub struct Vec2 {
    pub x: I16F16,
    pub y: I16F16,
}

impl Vec2 {
    pub const ZERO: Self = Self { x: I16F16::ZERO, y: I16F16::ZERO };

    pub fn from_f64(x: f64, y: f64) -> Self {
        Self { x: I16F16::from_num(x), y: I16F16::from_num(y) }
    }
}

impl std::ops::Add for Vec2 {
    type Output = Self;
    fn add(self, rhs: Self) -> Self {
        Self { x: self.x + rhs.x, y: self.y + rhs.y }
    }
}

impl std::ops::AddAssign for Vec2 {
    fn add_assign(&mut self, rhs: Self) {
        self.x += rhs.x;
        self.y += rhs.y;
    }
}

impl std::ops::Mul<I16F16> for Vec2 {
    type Output = Self;
    fn mul(self, rhs: I16F16) -> Self {
        Self { x: self.x * rhs, y: self.y * rhs }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Fleet {
    A,
    B,
}

impl Fleet {
    pub fn enemy(self) -> Self {
        match self {
            Fleet::A => Fleet::B,
            Fleet::B => Fleet::A,
        }
    }
}

#[derive(Debug, Clone)]
pub struct WeaponState {
    pub weapon_type: WeaponType,
    pub damage: I16F16,
    pub range: I16F16,
    pub cooldown_ticks: u32,
    pub cooldown_remaining: u32,
    pub miss_chance: I16F16,
    pub crit_chance: I16F16,
    pub crit_damage: I16F16,
    pub ammo: Option<u32>,
}

#[derive(Debug, Clone)]
pub struct BoidWeights {
    pub separation: I16F16,
    pub cohesion: I16F16,
    pub alignment: I16F16,
    pub seek_enemy: I16F16,
    pub maintain_range: I16F16,
}

#[derive(Debug, Clone)]
pub struct Ship {
    pub id: ShipId,
    pub fleet: Fleet,
    pub is_mothership: bool,
    pub blueprint_drawing_id: String,
    pub hull_class: HullClass,
    pub role: Role,
    pub pos: Pos2,
    pub vel: Vec2,
    pub heading: I16F16,
    pub hp: I16F16,
    pub max_hp: I16F16,
    pub shield_hp: I16F16,
    pub shield_max_hp: I16F16,
    pub shield_recharge_rate: I16F16,
    pub armor: I16F16,
    pub max_speed: I16F16,
    pub acceleration: I16F16,
    pub turn_rate: I16F16,
    pub boid_weights: BoidWeights,
    pub weapon: Option<WeaponState>,
    pub preferred_range: I16F16,
}

#[derive(Debug, Clone)]
pub enum Event {
    HitscanFired { source_id: ShipId, target_id: ShipId, damage: I16F16 },
    HitscanMissed { source_id: ShipId, target_id: ShipId },
    ShipDestroyed { id: ShipId, fleet: Fleet },
    ShipAtLowHp { id: ShipId },
    AttritionStarted,
}

pub struct SimState {
    pub tick: u32,
    pub ships: BTreeMap<ShipId, Ship>,
    pub next_ship_id: u32,
    pub rng: Xoshiro256Plus,
    pub events: Vec<Event>,
    pub low_hp_flagged: BTreeMap<ShipId, bool>,
    pub attrition_started: bool,
    // Statistics — accumulated during the battle, emitted in RESULT JSON.
    pub killed: Vec<(Fleet, HullClass, bool)>, // (fleet of dead ship, hull_class, is_mothership)
    pub damage_dealt: [I32F32; 2],             // [fleet_a_dealt, fleet_b_dealt]
}

impl SimState {
    pub fn new(rng: Xoshiro256Plus) -> Self {
        Self {
            tick: 0,
            ships: BTreeMap::new(),
            next_ship_id: 0,
            rng,
            events: Vec::new(),
            low_hp_flagged: BTreeMap::new(),
            attrition_started: false,
            killed: Vec::new(),
            damage_dealt: [I32F32::ZERO, I32F32::ZERO],
        }
    }

    pub fn alloc_ship_id(&mut self) -> ShipId {
        let id = ShipId(self.next_ship_id);
        self.next_ship_id += 1;
        id
    }
}
