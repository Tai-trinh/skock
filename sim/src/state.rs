use fixed::types::{I16F16, I32F32};
use rand_xoshiro::Xoshiro256Plus;
use std::collections::BTreeMap;
use types::{
    BeamId, CombatStance, HullClass, ProjectileId, ProjectileSubtype, Role, ShipId, TargetPriority,
};

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

#[derive(Debug, Clone, Copy)]
pub enum WeaponKind {
    Hitscan {
        /// Miss probability at max range.
        miss_chance_far: I16F16,
        /// Floor miss probability at or below `accurate_range`.
        miss_chance_near: I16F16,
        /// Distance at or below which miss chance equals `miss_chance_near`.
        accurate_range: I16F16,
    },
    Projectile {
        subtype: ProjectileSubtype,
        speed: I16F16,
        /// Turn rate for seeking missiles (rad/tick).
        turn_rate: I16F16,
        fuse_ticks: u32,
        explosion_radius: I16F16,
        explosion_damage: I16F16,
        hit_radius: I16F16,
    },
    Beam {
        charge_ticks: u32,
        duration_ticks: u32,
        beam_width: I16F16,
        /// Angular velocity (rad/tick) while not hitting any enemy.
        slew_rate: I16F16,
        /// Angular velocity (rad/tick) while firing on an enemy.
        track_rate: I16F16,
        ramp_ticks: u32,
        ramp_max: I16F16,
    },
}

#[derive(Debug, Clone)]
pub struct WeaponState {
    pub damage: I16F16,
    pub range: I16F16,
    pub cooldown_ticks: u32,
    pub cooldown_remaining: u32,
    pub crit_chance: I16F16,
    pub crit_damage: I16F16,
    pub ammo: Option<u32>,
    /// Offset from ship center along ship's forward axis (local frame).
    pub fire_forward: I16F16,
    /// Offset from ship center along ship's lateral axis (local frame). Positive = starboard.
    pub fire_lateral: I16F16,
    pub salvo_count: u32,
    pub salvo_spread_angle: I16F16,
    pub kind: WeaponKind,
}

#[derive(Debug, Clone)]
pub struct BoidWeights {
    pub separation: I16F16,
    pub cohesion: I16F16,
    pub alignment: I16F16,
    pub seek_nearest: I16F16,
    pub seek_mass: I16F16,
    pub seek_mothership: I16F16,
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
    pub weapons: Vec<WeaponState>,
    pub target_priority: TargetPriority,
    pub combat_stance: CombatStance,
    pub preferred_range: I16F16,
    pub hit_radius: I16F16,
}

// ── Projectile entity ─────────────────────────────────────────────────────────

#[derive(Debug, Clone)]
pub struct Projectile {
    pub id: ProjectileId,
    pub owner_id: ShipId,
    pub owner_fleet: Fleet,
    /// Position at the start of this tick (before movement) — used for swept-segment hit detection.
    pub prev_pos: Pos2,
    pub pos: Pos2,
    pub vel: Vec2,
    /// Direction of travel in radians, derived from vel.
    pub heading: I16F16,
    pub subtype: ProjectileSubtype,
    pub damage: I16F16,
    pub hit_radius: I16F16,
    pub explosion_radius: I16F16,
    pub explosion_damage: I16F16,
    pub fuse_ticks_remaining: u32,
    /// Missile homing turn rate (rad/tick). 0 = no homing.
    pub turn_rate: I16F16,
    pub crit_chance: I16F16,
    pub crit_damage: I16F16,
}

// ── Beam entity ───────────────────────────────────────────────────────────────

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BeamPhase {
    Charging,
    Firing,
}

#[derive(Debug, Clone)]
pub struct BeamEntity {
    pub id: BeamId,
    pub source_id: ShipId,
    pub hardpoint_index: usize,
    pub owner_fleet: Fleet,
    /// Cached position of the source ship, updated each tick. Avoids a ships-map lookup in log.rs.
    pub source_pos: Pos2,
    /// Current absolute angle (radians) of the beam ray, measured from +x axis.
    pub current_angle: I16F16,
    pub phase: BeamPhase,
    pub charge_ticks_remaining: u32,
    pub damage_ticks_remaining: u32,
    /// Consecutive ticks the beam has been hitting an enemy — drives damage ramp.
    pub on_target_ticks: u32,
    pub damage: I16F16,
    pub range: I16F16,
    pub beam_width: I16F16,
    pub slew_rate: I16F16,
    pub track_rate: I16F16,
    pub ramp_ticks: u32,
    pub ramp_max: I16F16,
    pub crit_chance: I16F16,
    pub crit_damage: I16F16,
}

// ── Events ────────────────────────────────────────────────────────────────────

#[derive(Debug, Clone)]
pub enum Event {
    HitscanFired {
        source_id: ShipId,
        target_id: ShipId,
        damage: I16F16,
        fleet: Fleet,
        source_pos_x: I32F32,
        source_pos_y: I32F32,
        target_pos_x: I32F32,
        target_pos_y: I32F32,
    },
    HitscanMissed {
        source_id: ShipId,
        target_id: ShipId,
        fleet: Fleet,
        source_pos_x: I32F32,
        source_pos_y: I32F32,
        target_pos_x: I32F32,
        target_pos_y: I32F32,
    },
    ProjectileExplosion {
        id: ProjectileId,
        fleet: Fleet,
        pos_x: I32F32,
        pos_y: I32F32,
        radius: I16F16,
    },
    ShipDestroyed {
        id: ShipId,
        fleet: Fleet,
    },
    ShipAtLowHp {
        id: ShipId,
    },
    AttritionStarted,
}

// ── Sim state ─────────────────────────────────────────────────────────────────

pub struct SimState {
    pub tick: u32,
    pub ships: BTreeMap<ShipId, Ship>,
    pub next_ship_id: u32,
    pub projectiles: BTreeMap<ProjectileId, Projectile>,
    pub next_projectile_id: u32,
    pub beams: BTreeMap<BeamId, BeamEntity>,
    pub next_beam_id: u32,
    /// Maps `(ShipId, hardpoint_index)` → BeamId for live beams.
    /// Written exclusively by combat::beam — insert on spawn, remove on expiry.
    pub active_beams: BTreeMap<(ShipId, usize), BeamId>,
    pub rng: Xoshiro256Plus,
    pub events: Vec<Event>,
    pub low_hp_flagged: BTreeMap<ShipId, bool>,
    pub attrition_started: bool,
    pub killed: Vec<(Fleet, HullClass, bool)>,
    pub damage_dealt: [I32F32; 2],
}

impl SimState {
    pub fn new(rng: Xoshiro256Plus) -> Self {
        Self {
            tick: 0,
            ships: BTreeMap::new(),
            next_ship_id: 0,
            projectiles: BTreeMap::new(),
            next_projectile_id: 0,
            beams: BTreeMap::new(),
            next_beam_id: 0,
            active_beams: BTreeMap::new(),
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

    pub fn alloc_projectile_id(&mut self) -> ProjectileId {
        let id = ProjectileId(self.next_projectile_id);
        self.next_projectile_id += 1;
        id
    }

    pub fn alloc_beam_id(&mut self) -> BeamId {
        let id = BeamId(self.next_beam_id);
        self.next_beam_id += 1;
        id
    }
}
