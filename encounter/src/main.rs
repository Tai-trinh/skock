use serde::Deserialize;
use serde_json::json;
use std::io::{self, BufRead, Write};

// TODO: switch to MessagePack in production (--format msgpack flag, rmp-serde).
// Apply consistently to skock-admiral and skock-dockyard at the same time.

static FLEETS: [&str; 8] = [
    include_str!("../../catalog/data/opponents/jump_1.json"),
    include_str!("../../catalog/data/opponents/jump_2.json"),
    include_str!("../../catalog/data/opponents/jump_3.json"),
    include_str!("../../catalog/data/opponents/jump_4.json"),
    include_str!("../../catalog/data/opponents/jump_5.json"),
    include_str!("../../catalog/data/opponents/jump_6.json"),
    include_str!("../../catalog/data/opponents/jump_7.json"),
    include_str!("../../catalog/data/opponents/jump_8.json"),
];

#[derive(Deserialize)]
struct Input {
    #[allow(dead_code)]
    player_id: String,
    run_number: usize,
    #[allow(dead_code)]
    losses: u32,
    #[allow(dead_code)]
    wins: u32,
}

fn main() {
    let line = match io::stdin().lock().lines().next() {
        Some(Ok(l)) => l,
        _ => {
            emit_error("io_error", "failed to read input line");
            std::process::exit(1);
        }
    };

    let input: Input = match serde_json::from_str(&line) {
        Ok(v) => v,
        Err(e) => {
            emit_error("invalid_input_json", &e.to_string());
            std::process::exit(1);
        }
    };

    if input.run_number < 1 || input.run_number > FLEETS.len() {
        emit_error(
            "invalid_run_number",
            &format!("run_number must be 1–{}; got {}", FLEETS.len(), input.run_number),
        );
        std::process::exit(1);
    }

    let fleet_json = FLEETS[input.run_number - 1];
    if let Err(e) = io::stdout().write_all(fleet_json.as_bytes()) {
        emit_error("io_error", &e.to_string());
        std::process::exit(1);
    }
    let _ = io::stdout().write_all(b"\n");
}

fn emit_error(code: &str, message: &str) {
    eprintln!("RESULT:{}", json!({ "error": code, "message": message }));
}
