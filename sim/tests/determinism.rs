use sha2::{Digest, Sha256};
use sim::config::SimConfig;
use types::FleetJson;

fn hash_battle(seed: u64, fleet_a_json: &str, fleet_b_json: &str) -> String {
    let config = SimConfig::default_embedded();
    let fleet_a: FleetJson = serde_json::from_str(fleet_a_json).expect("fleet_a parse");
    let fleet_b: FleetJson = serde_json::from_str(fleet_b_json).expect("fleet_b parse");
    let output = sim::run_battle(seed, &fleet_a, &fleet_b, &config);
    hex::encode(Sha256::digest(&output.log_bytes))
}

const FLEET_A: &str = include_str!("../test_data/fleet_a.json");
const FLEET_B: &str = include_str!("../test_data/fleet_b.json");

// Golden hashes locked in at first passing run.
// If any of these fail, a determinism regression was introduced.
// Update ONLY after a deliberate, reviewed change to sim logic — treat
// a hash change the same way you'd treat a broken serialization test.
#[test]
fn determinism_seed_42() {
    assert_eq!(
        hash_battle(42, FLEET_A, FLEET_B),
        "981221468f9eaf1dbe63566db40868492c7c48a04c7c96571f00680458b46059",
    );
}

#[test]
fn determinism_seed_1337() {
    assert_eq!(
        hash_battle(1337, FLEET_A, FLEET_B),
        "ef7a6aa63d8cae1b9028ddc138763e99460fa5ba30b22a181a921f5b2cbd5712",
    );
}

#[test]
fn determinism_seed_99999() {
    assert_eq!(
        hash_battle(99999, FLEET_A, FLEET_B),
        "ee3507be70f2637723c91eb9a0d59a99af9759b9cf99262fce4a6755240d3a1b",
    );
}

// Sanity: two runs with the same seed in the same process must match.
#[test]
fn same_seed_same_hash() {
    let h1 = hash_battle(7, FLEET_A, FLEET_B);
    let h2 = hash_battle(7, FLEET_A, FLEET_B);
    assert_eq!(h1, h2);
}
