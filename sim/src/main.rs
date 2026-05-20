use clap::Parser;
use rand_core::SeedableRng;
use rand_xoshiro::Xoshiro256Plus;
use serde_json::json;
use std::io::{self, BufWriter, Write};
use types::FleetJson;

use sim::{
    config::SimConfig,
    log,
    spawn::spawn_fleet,
    state::{Fleet, SimState},
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

    let fleet_a_idx = spawn_fleet(&mut state, &fleet_a, Fleet::A, &config);
    let fleet_b_idx = spawn_fleet(&mut state, &fleet_b, Fleet::B, &config);

    let stdout = io::stdout();
    let mut out = BufWriter::new(stdout.lock());

    let write_header_fn: fn(&mut BufWriter<_>) -> io::Result<()> =
        if args.debug { log::write_header_json } else { log::write_header };
    let write_tick_fn: fn(&mut BufWriter<_>, &SimState) -> io::Result<()> =
        if args.debug { log::write_tick_json } else { log::write_tick };

    if let Err(e) = write_header_fn(&mut out) {
        emit_error("io_error", &e.to_string());
        std::process::exit(1);
    }

    let result = loop {
        let result = run_tick(&mut state, &config);

        if let Err(e) = write_tick_fn(&mut out, &state) {
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

    let survivors_a: Vec<_> = state
        .ships
        .values()
        .filter(|s| s.fleet == Fleet::A)
        .map(|s| {
            json!({
                "blueprint_drawing_id": s.blueprint_drawing_id,
                "hp": s.hp.to_num::<f32>(),
                "is_mothership": s.is_mothership,
                "fleet_index": fleet_a_idx.get(&s.id).copied(),
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
                "hp": s.hp.to_num::<f32>(),
                "is_mothership": s.is_mothership,
                "fleet_index": fleet_b_idx.get(&s.id).copied(),
            })
        })
        .collect();

    let killed_a: Vec<_> = state
        .killed
        .iter()
        .filter(|(f, _, _)| *f == Fleet::A)
        .map(|(_, hc, ms)| json!({ "hull_class": hc, "is_mothership": ms }))
        .collect();
    let killed_b: Vec<_> = state
        .killed
        .iter()
        .filter(|(f, _, _)| *f == Fleet::B)
        .map(|(_, hc, ms)| json!({ "hull_class": hc, "is_mothership": ms }))
        .collect();

    eprintln!(
        "RESULT:{}",
        json!({
            "winner": winner,
            "ticks": ticks,
            "reason": reason,
            "fleet_a_survivors": survivors_a,
            "fleet_b_survivors": survivors_b,
            "fleet_a_killed": killed_a,
            "fleet_b_killed": killed_b,
            "fleet_a_damage_dealt": state.damage_dealt[0].to_num::<f32>(),
            "fleet_b_damage_dealt": state.damage_dealt[1].to_num::<f32>(),
        })
    );
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
    eprintln!("RESULT:{}", json!({ "error": code, "message": message }));
}
