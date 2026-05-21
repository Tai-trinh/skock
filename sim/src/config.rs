use std::collections::BTreeMap;

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SimConfig {
    pub tick_rate: u32,
    pub battlefield_half_width: f64,
    pub battlefield_half_height: f64,
    pub fleet_a_spawn_x: f64,
    pub fleet_b_spawn_x: f64,
    pub spawn_noise: f64,
    pub boid_neighbor_radius: f64,
    pub boid_max_neighbors: u32,
    pub boid_separation_margin: f64,
    pub boid_velocity_damping: f64,
    pub boid_min_speed_fraction: f64,
    pub attrition_start_tick: u32,
    pub attrition_base_damage_per_second: f64,
    pub attrition_ramp_per_second: f64,
    pub max_ticks: u32,
    /// Single-circle hit radius per hull class name (e.g. "Corvette" → 5.0).
    #[serde(default)]
    pub hull_hit_radii: BTreeMap<String, f64>,
    /// Default collision radius per projectile subtype name (e.g. "seeking_missile" → 1.0).
    #[serde(default)]
    pub projectile_hit_radii: BTreeMap<String, f64>,
}

impl SimConfig {
    pub fn default_embedded() -> Self {
        serde_json::from_str(include_str!("../../sim_config.json"))
            .expect("embedded sim_config.json is invalid")
    }
}
