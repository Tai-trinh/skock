pub mod boids;
pub mod combat;
pub mod config;
pub mod geometry;
pub mod log;
pub mod spawn;
pub mod state;
pub mod tick;

use rand_core::SeedableRng;
use rand_xoshiro::Xoshiro256Plus;
use types::FleetJson;

use crate::{
    config::SimConfig,
    state::{Fleet, SimState},
    tick::{run_tick, TickResult},
};

pub struct BattleOutput {
    pub log_bytes: Vec<u8>,
    pub winner: &'static str,
    pub ticks: u32,
    pub reason: &'static str,
}

pub fn run_battle(
    seed: u64,
    fleet_a: &FleetJson,
    fleet_b: &FleetJson,
    config: &SimConfig,
) -> BattleOutput {
    let rng = Xoshiro256Plus::seed_from_u64(seed);
    let mut state = SimState::new(rng);

    let _ = spawn::spawn_fleet(&mut state, fleet_a, Fleet::A, config);
    let _ = spawn::spawn_fleet(&mut state, fleet_b, Fleet::B, config);

    let mut log_bytes: Vec<u8> = Vec::new();
    log::write_header(&mut log_bytes).expect("write header");

    let result = loop {
        let result = run_tick(&mut state, config);
        log::write_tick(&mut log_bytes, &state).expect("write tick");
        match result {
            TickResult::Continue => {}
            other => break other,
        }
    };

    let (winner, reason) = match result {
        TickResult::Winner(Fleet::A) => ("fleet_a", "mothership_destroyed"),
        TickResult::Winner(Fleet::B) => ("fleet_b", "mothership_destroyed"),
        TickResult::Draw => ("draw", "timeout_draw"),
        TickResult::Continue => unreachable!(),
    };

    BattleOutput { log_bytes, winner, ticks: state.tick, reason }
}
