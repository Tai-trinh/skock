use rand_core::{OsRng, RngCore, SeedableRng};
use rand_xoshiro::Xoshiro256Plus;
use serde::{Deserialize, Serialize};
use serde_json::json;
use std::io::{self, BufRead, Write};
use types::FleetJson;

mod catalog;

#[derive(Deserialize)]
struct Input {
    player_id: String,
}

#[derive(Serialize)]
struct Output {
    run_seed: u64,
    offers: Vec<AdmiralOffer>,
}

#[derive(Serialize)]
struct AdmiralOffer {
    admiral_id: &'static str,
    admiral_name: &'static str,
    faction_id: &'static str,
    faction_name: &'static str,
    bonus_text: &'static str,
    starting_salvage: i32,
    starting_tech: i32,
    starting_hangar_capacity: i32,
    starting_fleet: FleetJson,
}

fn main() {
    validate_catalog();

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

    // Suppress unused warning — player_id will gate unlocked admirals in future.
    let _ = &input.player_id;

    let run_seed = OsRng.next_u64();
    let mut rng = Xoshiro256Plus::seed_from_u64(run_seed);

    let offers = build_offers(&mut rng);
    let output = Output { run_seed, offers };

    let mut bytes = serde_json::to_vec(&output).unwrap();
    bytes.push(b'\n');
    if let Err(e) = io::stdout().write_all(&bytes) {
        emit_error("io_error", &e.to_string());
        std::process::exit(1);
    }
}

fn validate_catalog() {
    assert!(
        catalog::FACTIONS.len() >= 3,
        "catalog must have at least 3 factions; found {}",
        catalog::FACTIONS.len()
    );
    for faction in catalog::FACTIONS {
        let has_admiral = catalog::ADMIRALS.iter().any(|a| a.faction_id == faction.id);
        assert!(has_admiral, "faction '{}' has no admirals in the catalog", faction.id);
    }
    for admiral in catalog::ADMIRALS {
        for bp_id in admiral.starting_blueprint_ids {
            assert!(
                catalog::find_blueprint(bp_id).is_some(),
                "admiral '{}' references unknown blueprint '{}'",
                admiral.id,
                bp_id
            );
        }
    }
}

fn build_offers(rng: &mut Xoshiro256Plus) -> Vec<AdmiralOffer> {
    catalog::FACTIONS
        .iter()
        .map(|faction| {
            let candidates: Vec<&catalog::AdmiralDef> =
                catalog::ADMIRALS.iter().filter(|a| a.faction_id == faction.id).collect();

            let idx = (rng.next_u64() as usize) % candidates.len();
            let admiral = candidates[idx];

            AdmiralOffer {
                admiral_id: admiral.id,
                admiral_name: admiral.name,
                faction_id: faction.id,
                faction_name: faction.display_name,
                bonus_text: admiral.bonus_text,
                starting_salvage: admiral.starting_salvage,
                starting_tech: admiral.starting_tech,
                starting_hangar_capacity: admiral.starting_hangar_capacity,
                starting_fleet: build_fleet(admiral, faction),
            }
        })
        .collect()
}

fn build_fleet(admiral: &catalog::AdmiralDef, faction: &catalog::FactionDef) -> FleetJson {
    let ships = admiral
        .starting_blueprint_ids
        .iter()
        .map(|id| {
            catalog::find_blueprint(id)
                .unwrap_or_else(|| panic!("blueprint '{}' not found", id))
                .ship_def
                .clone()
        })
        .collect();

    FleetJson {
        faction: faction.id.to_string(),
        admiral_id: admiral.id.to_string(),
        formation: "wedge".to_string(),
        mothership: catalog::mothership_template(faction.id),
        ships,
        doctrines: vec![],
        role_equipment: vec![],
        faction_effects: (faction.effects)(),
        admiral_effects: (admiral.effects)(),
    }
}

fn emit_error(code: &str, message: &str) {
    eprintln!("RESULT:{}", json!({ "error": code, "message": message }));
}
