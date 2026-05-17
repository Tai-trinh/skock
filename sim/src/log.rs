use crate::state::{Event, Fleet, SimState};
use serde::Serialize;
use std::io::{self, Write};

const SCHEMA_VERSION: u32 = 1;

#[derive(Serialize)]
struct LogHeader {
    schema_version: u32,
}

#[derive(Serialize)]
struct ShipSnapshot {
    id: u32,
    fleet: u8,
    blueprint_drawing_id: String,
    is_mothership: bool,
    // I32F32 raw bits
    pos_x: i64,
    pos_y: i64,
    // I16F16 raw bits
    vel_x: i32,
    vel_y: i32,
    heading: i32,
    hp: i32,
    max_hp: i32,
    shield_hp: i32,
    shield_max_hp: i32,
}

#[derive(Serialize)]
#[serde(tag = "type", rename_all = "snake_case")]
enum LogEvent {
    HitscanFired { source_id: u32, target_id: u32, damage: i32 },
    HitscanMissed { source_id: u32, target_id: u32 },
    ShipDestroyed { id: u32, fleet: u8 },
    ShipAtLowHp { id: u32 },
    AttritionStarted,
}

#[derive(Serialize)]
struct TickRecord {
    tick: u32,
    ships: Vec<ShipSnapshot>,
    projectiles: Vec<()>,
    beams: Vec<()>,
    events: Vec<LogEvent>,
}

fn fleet_byte(f: Fleet) -> u8 {
    match f {
        Fleet::A => 0,
        Fleet::B => 1,
    }
}

fn map_event(e: &Event) -> LogEvent {
    match e {
        Event::HitscanFired { source_id, target_id, damage } => LogEvent::HitscanFired {
            source_id: source_id.0,
            target_id: target_id.0,
            damage: damage.to_bits(),
        },
        Event::HitscanMissed { source_id, target_id } => {
            LogEvent::HitscanMissed { source_id: source_id.0, target_id: target_id.0 }
        }
        Event::ShipDestroyed { id, fleet } => {
            LogEvent::ShipDestroyed { id: id.0, fleet: fleet_byte(*fleet) }
        }
        Event::ShipAtLowHp { id } => LogEvent::ShipAtLowHp { id: id.0 },
        Event::AttritionStarted => LogEvent::AttritionStarted,
    }
}

pub fn write_header(out: &mut impl Write) -> io::Result<()> {
    let header = LogHeader { schema_version: SCHEMA_VERSION };
    let bytes = rmp_serde::to_vec_named(&header).map_err(io::Error::other)?;
    out.write_all(&bytes)
}

pub fn write_tick(out: &mut impl Write, state: &SimState) -> io::Result<()> {
    let ships: Vec<ShipSnapshot> = state
        .ships
        .values()
        .map(|s| ShipSnapshot {
            id: s.id.0,
            fleet: fleet_byte(s.fleet),
            blueprint_drawing_id: s.blueprint_drawing_id.clone(),
            is_mothership: s.is_mothership,
            pos_x: s.pos.x.to_bits(),
            pos_y: s.pos.y.to_bits(),
            vel_x: s.vel.x.to_bits(),
            vel_y: s.vel.y.to_bits(),
            heading: s.heading.to_bits(),
            hp: s.hp.to_bits(),
            max_hp: s.max_hp.to_bits(),
            shield_hp: s.shield_hp.to_bits(),
            shield_max_hp: s.shield_max_hp.to_bits(),
        })
        .collect();

    let events: Vec<LogEvent> = state.events.iter().map(map_event).collect();

    let record = TickRecord { tick: state.tick, ships, projectiles: vec![], beams: vec![], events };

    let bytes = rmp_serde::to_vec_named(&record).map_err(io::Error::other)?;
    out.write_all(&bytes)
}
