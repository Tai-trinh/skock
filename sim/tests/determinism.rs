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
        "5f07cfcb53445d5a9c89d48067255c5306a94af6bc8a83bf832233427401bf56",
    );
}

#[test]
fn determinism_seed_1337() {
    assert_eq!(
        hash_battle(1337, FLEET_A, FLEET_B),
        "8a5930a7f43f39bf750e326177ee09f48dcdd40a46d35afe34ba6b1ff9fe2b86",
    );
}

#[test]
fn determinism_seed_99999() {
    assert_eq!(
        hash_battle(99999, FLEET_A, FLEET_B),
        "93dc700fdf56ad9ccf2a926d96e3a84bc28e29b1fcc3f08b7ba0dd25cf38ae5c",
    );
}

// Sanity: two runs with the same seed in the same process must match.
#[test]
fn same_seed_same_hash() {
    let h1 = hash_battle(7, FLEET_A, FLEET_B);
    let h2 = hash_battle(7, FLEET_A, FLEET_B);
    assert_eq!(h1, h2);
}
