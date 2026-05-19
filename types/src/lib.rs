pub mod fleet;
pub mod ids;

pub use fleet::{
    BoidWeightsDef, EffectScope, FleetEffect, FleetJson, HullClass, ModifierType,
    ProjectileSubtype, Role, ShipDef, WeaponDef, WeaponType, Weight,
};
pub use ids::{BeamId, ProjectileId, ShipId};
