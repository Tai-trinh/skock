use serde::Deserialize;
use sim::config::SimConfig;
use types::FleetJson;

#[derive(Deserialize, Debug)]
struct Header {
    schema_version: u32,
}

#[derive(Deserialize, Debug)]
struct ProjSnap {
    id: u32,
    pos_x: i64,
    pos_y: i64,
}

#[derive(Deserialize, Debug)]
#[allow(dead_code)]
struct TickRec {
    tick: u32,
    projectiles: Vec<ProjSnap>,
    #[serde(default)]
    ships: Vec<serde_json::Value>,
    #[serde(default)]
    beams: Vec<serde_json::Value>,
    #[serde(default)]
    events: Vec<serde_json::Value>,
}

const MISSILE_FLEET: &str = r#"{
    "faction":"test","admiral_id":"none","formation":"wedge",
    "mothership":{
        "blueprint_drawing_id":"ms_a","hull_class":"Dreadnought","role":"Artillery",
        "hp":500,"max_hp":500,"speed":1,"acceleration":0.3,"turn_rate":0.1,
        "boid_weights":{"separation":2,"cohesion":0,"alignment":0,"seek_nearest":0.2,"maintain_range":1},
        "hardpoints":[{"type":"hitscan","damage":5,"range":150,"cooldown_ticks":60}]
    },
    "ships":[{
        "blueprint_drawing_id":"corvette_m","hull_class":"Corvette","role":"Fighter",
        "hp":60,"max_hp":60,"speed":8,"acceleration":2,"turn_rate":1.2,
        "boid_weights":{"separation":1.5,"cohesion":0.4,"alignment":0.3,"seek_nearest":2,"maintain_range":1},
        "hardpoints":[{
            "type":"projectile","subtype":"seeking_missile",
            "damage":35,"range":200,"cooldown_ticks":50,
            "projectile_speed":9,"turn_rate":0.08,
            "fuse_ticks":63,"explosion_radius":0,"explosion_damage":0
        }]
    }],
    "doctrines":[],"role_equipment":[],"faction_effects":[],"admiral_effects":[]
}"#;

const FLEET_B: &str = include_str!("../test_data/fleet_b.json");

#[test]
fn missiles_move_each_tick() {
    let config = SimConfig::default_embedded();
    let fleet_a: FleetJson = serde_json::from_str(MISSILE_FLEET).unwrap();
    let fleet_b: FleetJson = serde_json::from_str(FLEET_B).unwrap();

    let output = sim::run_battle(42, &fleet_a, &fleet_b, &config);

    // Decode log: first record is header, rest are ticks
    let mut bytes = output.log_bytes.as_slice();
    let header: Header = rmp_serde::from_slice(bytes).unwrap();
    assert_eq!(header.schema_version, 3, "unexpected schema version");

    // Skip past header bytes
    let header_len =
        rmp_serde::to_vec_named(&serde_json::json!({"schema_version": 3})).unwrap().len();
    bytes = &output.log_bytes[header_len..];

    // Collect ticks that have projectiles
    let mut ticks_with_proj: Vec<(u32, u32, i64, i64)> = Vec::new(); // (tick, id, px, py)
    let mut cursor = bytes;
    while !cursor.is_empty() {
        let tick: TickRec = match rmp_serde::from_slice(cursor) {
            Ok(t) => t,
            Err(_) => break,
        };
        for p in &tick.projectiles {
            ticks_with_proj.push((tick.tick, p.id, p.pos_x, p.pos_y));
        }
        if ticks_with_proj.len() >= 20 {
            break;
        }
        // Advance cursor — use raw read_from
        use std::io::Cursor;
        let mut c = Cursor::new(cursor);
        let _: TickRec = rmp_serde::from_read(&mut c).unwrap();
        cursor = &cursor[c.position() as usize..];
    }

    // Find any projectile that appears in 2+ consecutive ticks and check it moved
    let mut by_id: std::collections::BTreeMap<u32, Vec<(u32, i64, i64)>> =
        std::collections::BTreeMap::new();
    for (tick, id, px, py) in &ticks_with_proj {
        by_id.entry(*id).or_default().push((*tick, *px, *py));
    }

    println!("Projectile tick data ({} entries):", ticks_with_proj.len());
    for (id, entries) in &by_id {
        println!("  proj {id}:");
        for (tick, px, py) in entries {
            let x = *px as f64 / 4_294_967_296.0;
            let y = *py as f64 / 4_294_967_296.0;
            println!("    tick {tick}: x={x:.2}, y={y:.2}");
        }
    }

    let mut any_moved = false;
    for entries in by_id.values() {
        if entries.len() >= 2 {
            let (_, x0, y0) = entries[0];
            let (_, x1, y1) = entries[1];
            if x0 != x1 || y0 != y1 {
                any_moved = true;
            }
        }
    }

    assert!(
        any_moved,
        "No missile moved between ticks! Positions are stuck. Data: {ticks_with_proj:?}"
    );
}
