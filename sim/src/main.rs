mod boids;
mod combat;
mod config;
mod log;
mod state;
mod tick;

use clap::Parser;
use fixed::types::I16F16;
use rand_core::{RngCore, SeedableRng};
use rand_xoshiro::Xoshiro256Plus;
use serde_json::json;
use std::io::{self, BufWriter, Write};
use types::{FleetJson, HullClass, ShipDef};

use crate::{
    config::SimConfig,
    state::{BoidWeights, Fleet, Pos2, Ship, SimState, Vec2, WeaponState},
    tick::{run_tick, TickResult},
};

#[derive(Parser)]
#[command(name = "skock-sim")]
struct Args {
    #[arg(long)]
    seed: u64,
    fleet_a: std::path::PathBuf,
    fleet_b: std::path::PathBuf,
    #[arg(long)]
    config: Option<std::path::PathBuf>,
    #[arg(long)]
    debug: bool,
}

fn main() {
    let args = Args::parse();

    let config = match load_config(&args) {
        Ok(c) => c,
        Err(e) => {
            emit_error("config_load_failed", &e.to_string());
            std::process::exit(1);
        }
    };

    let fleet_a: FleetJson = match load_fleet(&args.fleet_a) {
        Ok(f) => f,
        Err(e) => {
            emit_error("invalid_fleet_json", &format!("fleet_a: {e}"));
            std::process::exit(1);
        }
    };
    let fleet_b: FleetJson = match load_fleet(&args.fleet_b) {
        Ok(f) => f,
        Err(e) => {
            emit_error("invalid_fleet_json", &format!("fleet_b: {e}"));
            std::process::exit(1);
        }
    };

    let rng = Xoshiro256Plus::seed_from_u64(args.seed);
    let mut state = SimState::new(rng);

    spawn_fleet(&mut state, &fleet_a, Fleet::A, &config);
    spawn_fleet(&mut state, &fleet_b, Fleet::B, &config);

    let stdout = io::stdout();
    let mut out = BufWriter::new(stdout.lock());

    if let Err(e) = log::write_header(&mut out) {
        emit_error("io_error", &e.to_string());
        std::process::exit(1);
    }

    let result = loop {
        let result = run_tick(&mut state, &config);

        if let Err(e) = log::write_tick(&mut out, &state) {
            emit_error("io_error", &e.to_string());
            std::process::exit(1);
        }

        match result {
            TickResult::Continue => {}
            other => break other,
        }
    };

    let _ = out.flush();

    let (winner, ticks, reason) = match result {
        TickResult::Winner(Fleet::A) => ("fleet_a", state.tick, "mothership_destroyed"),
        TickResult::Winner(Fleet::B) => ("fleet_b", state.tick, "mothership_destroyed"),
        TickResult::Draw => ("draw", state.tick, "timeout_draw"),
        TickResult::Continue => unreachable!(),
    };

    // Survivors: ships still alive at battle end
    let survivors_a: Vec<_> = state
        .ships
        .values()
        .filter(|s| s.fleet == Fleet::A)
        .map(|s| {
            json!({
                "blueprint_drawing_id": s.blueprint_drawing_id,
                "hp": s.hp.to_num::<f32>()
            })
        })
        .collect();
    let survivors_b: Vec<_> = state
        .ships
        .values()
        .filter(|s| s.fleet == Fleet::B)
        .map(|s| {
            json!({
                "blueprint_drawing_id": s.blueprint_drawing_id,
                "hp": s.hp.to_num::<f32>()
            })
        })
        .collect();

    let result_json = json!({
        "winner": winner,
        "ticks": ticks,
        "reason": reason,
        "fleet_a_survivors": survivors_a,
        "fleet_b_survivors": survivors_b,
    });

    eprintln!("{}", result_json);
}

fn load_config(args: &Args) -> anyhow::Result<SimConfig> {
    if let Some(path) = &args.config {
        let text = std::fs::read_to_string(path)?;
        Ok(serde_json::from_str::<SimConfig>(&text)?)
    } else {
        Ok(SimConfig::default_embedded())
    }
}

fn load_fleet(path: &std::path::Path) -> anyhow::Result<FleetJson> {
    let text = std::fs::read_to_string(path)?;
    Ok(serde_json::from_str::<FleetJson>(&text)?)
}

fn emit_error(code: &str, message: &str) {
    let v = json!({ "error": code, "message": message });
    eprintln!("{v}");
}

fn spawn_fleet(state: &mut SimState, fleet: &FleetJson, side: Fleet, config: &SimConfig) {
    let spawn_x = match side {
        Fleet::A => config.fleet_a_spawn_x,
        Fleet::B => config.fleet_b_spawn_x,
    };

    // All non-mothership ships sorted by hull class (wedge front-to-back)
    let mut ships: Vec<&ShipDef> = fleet.ships.iter().collect();
    ships.sort_by_key(|s| hull_class_order(s.hull_class));

    for (i, def) in ships.iter().enumerate() {
        let pos = wedge_pos(spawn_x, i, ships.len(), side, config, &mut state.rng);
        let ship = build_ship(state.alloc_ship_id(), def, side, pos, false);
        state.ships.insert(ship.id, ship);
    }

    // Mothership always at back-center
    let ms_pos = Pos2::from_f64(spawn_x, 0.0);
    let ms = build_ship(
        state.alloc_ship_id(),
        &fleet.mothership,
        side,
        ms_pos,
        true,
    );
    state.ships.insert(ms.id, ms);
}

fn hull_class_order(h: HullClass) -> u8 {
    match h {
        HullClass::Dreadnought => 0,
        HullClass::Corvette => 1,
        HullClass::Frigate => 2,
        HullClass::Destroyer => 3,
        HullClass::Cruiser => 4,
        HullClass::Battlecruiser => 5,
    }
}

fn wedge_pos(
    spawn_x: f64,
    index: usize,
    _total: usize,
    side: Fleet,
    config: &SimConfig,
    rng: &mut Xoshiro256Plus,
) -> Pos2 {
    // Row depth: each row has (row+1) ships; row 0 is the tip
    let row = ((-1.0 + f64::sqrt(1.0 + 8.0 * index as f64)) / 2.0) as usize;
    let pos_in_row = index - row * (row + 1) / 2;
    let row_width = (row + 1) as f64;

    let row_spacing = 40.0;
    let ship_spacing = 35.0;

    // Depth offset: fleet A rows go toward positive x, fleet B toward negative x
    let depth_sign = match side {
        Fleet::A => 1.0,
        Fleet::B => -1.0,
    };
    let depth = spawn_x + depth_sign * row as f64 * row_spacing;
    let lateral = (pos_in_row as f64 - (row_width - 1.0) / 2.0) * ship_spacing;

    let noise_x = noise_offset(rng, config.spawn_noise);
    let noise_y = noise_offset(rng, config.spawn_noise);

    Pos2::from_f64(depth + noise_x, lateral + noise_y)
}

fn noise_offset(rng: &mut Xoshiro256Plus, amplitude: f64) -> f64 {
    // Returns value in [-amplitude/2, amplitude/2]
    let raw = rng.next_u64() as f64 / u64::MAX as f64; // [0, 1)
    (raw - 0.5) * amplitude
}

fn build_ship(
    id: types::ShipId,
    def: &ShipDef,
    fleet: Fleet,
    pos: Pos2,
    is_mothership: bool,
) -> Ship {
    let weapon = def.weapon.as_ref().map(|w| WeaponState {
        weapon_type: w.weapon_type,
        damage: I16F16::from_num(w.damage),
        range: I16F16::from_num(w.range),
        cooldown_ticks: w.cooldown_ticks,
        cooldown_remaining: 0,
        miss_chance: I16F16::from_num(w.miss_chance),
        crit_chance: I16F16::from_num(w.crit_chance),
        crit_damage: I16F16::from_num(w.crit_damage),
        ammo: w.ammo,
    });

    let preferred_range = def
        .weapon
        .as_ref()
        .map(|w| I16F16::from_num(w.range))
        .unwrap_or(I16F16::ZERO);

    // Initial heading: fleet A faces right (+x), fleet B faces left (-x)
    let heading = match fleet {
        Fleet::A => I16F16::ZERO,
        Fleet::B => I16F16::from_num(std::f64::consts::PI),
    };

    Ship {
        id,
        fleet,
        is_mothership,
        blueprint_drawing_id: def.blueprint_drawing_id.clone(),
        hull_class: def.hull_class,
        role: def.role,
        pos,
        vel: Vec2::ZERO,
        heading,
        hp: I16F16::from_num(def.hp),
        max_hp: I16F16::from_num(def.max_hp),
        shield_hp: I16F16::from_num(def.shield_hp),
        shield_max_hp: I16F16::from_num(def.shield_max_hp),
        shield_recharge_rate: I16F16::from_num(def.shield_recharge_rate),
        armor: I16F16::from_num(def.armor),
        max_speed: I16F16::from_num(def.speed),
        acceleration: I16F16::from_num(def.acceleration),
        turn_rate: I16F16::from_num(def.turn_rate),
        boid_weights: BoidWeights {
            separation: I16F16::from_num(def.boid_weights.separation),
            cohesion: I16F16::from_num(def.boid_weights.cohesion),
            alignment: I16F16::from_num(def.boid_weights.alignment),
            seek_enemy: I16F16::from_num(def.boid_weights.seek_enemy),
            maintain_range: I16F16::from_num(def.boid_weights.maintain_range),
        },
        weapon,
        preferred_range,
    }
}
