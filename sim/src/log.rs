use crate::state::{BeamPhase, Event, Fleet, SimState};
use serde::Serialize;
use std::io::{self, Write};
use types::ProjectileSubtype;

const SCHEMA_VERSION: u32 = 3;

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
    pos_x: i64,
    pos_y: i64,
    vel_x: i64,
    vel_y: i64,
    heading: i32,
    hp: i32,
    max_hp: i32,
    shield_hp: i32,
    shield_max_hp: i32,
}

#[derive(Serialize)]
struct ProjectileSnapshot {
    id: u32,
    fleet: u8,
    pos_x: i64,
    pos_y: i64,
    heading: i32,
    subtype: u8,
}

#[derive(Serialize)]
struct BeamSnapshot {
    id: u32,
    fleet: u8,
    source_pos_x: i64,
    source_pos_y: i64,
    current_angle: i32,
    phase: u8,
    beam_width: i32,
    range: i32,
}

#[derive(Serialize)]
#[serde(tag = "type", rename_all = "snake_case")]
enum LogEvent {
    HitscanFired {
        source_id: u32,
        target_id: u32,
        damage: i32,
        fleet: u8,
        source_pos_x: i64,
        source_pos_y: i64,
        target_pos_x: i64,
        target_pos_y: i64,
    },
    HitscanMissed {
        source_id: u32,
        target_id: u32,
        fleet: u8,
        source_pos_x: i64,
        source_pos_y: i64,
        target_pos_x: i64,
        target_pos_y: i64,
    },
    ProjectileExplosion {
        id: u32,
        fleet: u8,
        pos_x: i64,
        pos_y: i64,
        radius: i32,
    },
    ShipDestroyed {
        id: u32,
        fleet: u8,
    },
    ShipAtLowHp {
        id: u32,
    },
    AttritionStarted,
}

#[derive(Serialize)]
struct TickRecord {
    tick: u32,
    ships: Vec<ShipSnapshot>,
    projectiles: Vec<ProjectileSnapshot>,
    beams: Vec<BeamSnapshot>,
    events: Vec<LogEvent>,
}

fn fleet_byte(f: Fleet) -> u8 {
    match f {
        Fleet::A => 0,
        Fleet::B => 1,
    }
}

fn subtype_byte(s: ProjectileSubtype) -> u8 {
    match s {
        ProjectileSubtype::SeekingMissile => 0,
        ProjectileSubtype::Torpedo => 1,
        ProjectileSubtype::Mine => 2,
    }
}

fn phase_byte(p: BeamPhase) -> u8 {
    match p {
        BeamPhase::Charging => 0,
        BeamPhase::Firing => 1,
    }
}

fn map_event(e: &Event) -> LogEvent {
    match e {
        Event::HitscanFired {
            source_id,
            target_id,
            damage,
            fleet,
            source_pos_x,
            source_pos_y,
            target_pos_x,
            target_pos_y,
        } => LogEvent::HitscanFired {
            source_id: source_id.0,
            target_id: target_id.0,
            damage: damage.to_bits(),
            fleet: fleet_byte(*fleet),
            source_pos_x: source_pos_x.to_bits(),
            source_pos_y: source_pos_y.to_bits(),
            target_pos_x: target_pos_x.to_bits(),
            target_pos_y: target_pos_y.to_bits(),
        },
        Event::HitscanMissed {
            source_id,
            target_id,
            fleet,
            source_pos_x,
            source_pos_y,
            target_pos_x,
            target_pos_y,
        } => LogEvent::HitscanMissed {
            source_id: source_id.0,
            target_id: target_id.0,
            fleet: fleet_byte(*fleet),
            source_pos_x: source_pos_x.to_bits(),
            source_pos_y: source_pos_y.to_bits(),
            target_pos_x: target_pos_x.to_bits(),
            target_pos_y: target_pos_y.to_bits(),
        },
        Event::ProjectileExplosion { id, fleet, pos_x, pos_y, radius } => {
            LogEvent::ProjectileExplosion {
                id: id.0,
                fleet: fleet_byte(*fleet),
                pos_x: pos_x.to_bits(),
                pos_y: pos_y.to_bits(),
                radius: radius.to_bits(),
            }
        }
        Event::ShipDestroyed { id, fleet } => {
            LogEvent::ShipDestroyed { id: id.0, fleet: fleet_byte(*fleet) }
        }
        Event::ShipAtLowHp { id } => LogEvent::ShipAtLowHp { id: id.0 },
        Event::AttritionStarted => LogEvent::AttritionStarted,
    }
}

// ── Debug JSON structs (human-readable f32 values) ───────────────────────────

#[derive(Serialize)]
struct ShipSnapshotDebug {
    id: u32,
    fleet: u8,
    blueprint_drawing_id: String,
    is_mothership: bool,
    pos_x: f32,
    pos_y: f32,
    vel_x: f32,
    vel_y: f32,
    heading: f32,
    hp: f32,
    max_hp: f32,
    shield_hp: f32,
    shield_max_hp: f32,
}

#[derive(Serialize)]
struct ProjectileSnapshotDebug {
    id: u32,
    fleet: u8,
    pos_x: f32,
    pos_y: f32,
    vel_x: f32,
    vel_y: f32,
    heading: f32,
    subtype: u8,
}

#[derive(Serialize)]
struct BeamSnapshotDebug {
    id: u32,
    fleet: u8,
    source_pos_x: f32,
    source_pos_y: f32,
    current_angle: f32,
    phase: u8,
    beam_width: f32,
    range: f32,
}

#[derive(Serialize)]
struct TickRecordDebug {
    tick: u32,
    ships: Vec<ShipSnapshotDebug>,
    projectiles: Vec<ProjectileSnapshotDebug>,
    beams: Vec<BeamSnapshotDebug>,
    events: Vec<LogEvent>,
}

pub fn write_header_json(out: &mut impl Write) -> io::Result<()> {
    let header = LogHeader { schema_version: SCHEMA_VERSION };
    let mut bytes = serde_json::to_vec(&header).map_err(io::Error::other)?;
    bytes.push(b'\n');
    out.write_all(&bytes)
}

pub fn write_tick_json(out: &mut impl Write, state: &SimState) -> io::Result<()> {
    let ships: Vec<ShipSnapshotDebug> = state
        .ships
        .values()
        .map(|s| ShipSnapshotDebug {
            id: s.id.0,
            fleet: fleet_byte(s.fleet),
            blueprint_drawing_id: s.blueprint_drawing_id.clone(),
            is_mothership: s.is_mothership,
            pos_x: s.pos.x.to_num::<f32>(),
            pos_y: s.pos.y.to_num::<f32>(),
            vel_x: s.vel.x.to_num::<f32>(),
            vel_y: s.vel.y.to_num::<f32>(),
            heading: s.heading.to_num::<f32>(),
            hp: s.hp.to_num::<f32>(),
            max_hp: s.max_hp.to_num::<f32>(),
            shield_hp: s.shield_hp.to_num::<f32>(),
            shield_max_hp: s.shield_max_hp.to_num::<f32>(),
        })
        .collect();

    let projectiles: Vec<ProjectileSnapshotDebug> = state
        .projectiles
        .values()
        .map(|p| ProjectileSnapshotDebug {
            id: p.id.0,
            fleet: fleet_byte(p.owner_fleet),
            pos_x: p.pos.x.to_num::<f32>(),
            pos_y: p.pos.y.to_num::<f32>(),
            vel_x: p.vel.x.to_num::<f32>(),
            vel_y: p.vel.y.to_num::<f32>(),
            heading: p.heading.to_num::<f32>(),
            subtype: subtype_byte(p.subtype),
        })
        .collect();

    let beams: Vec<BeamSnapshotDebug> = state
        .beams
        .values()
        .map(|b| BeamSnapshotDebug {
            id: b.id.0,
            fleet: fleet_byte(b.owner_fleet),
            source_pos_x: b.source_pos.x.to_num::<f32>(),
            source_pos_y: b.source_pos.y.to_num::<f32>(),
            current_angle: b.current_angle.to_num::<f32>(),
            phase: phase_byte(b.phase),
            beam_width: b.beam_width.to_num::<f32>(),
            range: b.range.to_num::<f32>(),
        })
        .collect();

    let events: Vec<LogEvent> = state.events.iter().map(map_event).collect();
    let record = TickRecordDebug { tick: state.tick, ships, projectiles, beams, events };
    let mut bytes = serde_json::to_vec(&record).map_err(io::Error::other)?;
    bytes.push(b'\n');
    out.write_all(&bytes)
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

    let projectiles: Vec<ProjectileSnapshot> = state
        .projectiles
        .values()
        .map(|p| ProjectileSnapshot {
            id: p.id.0,
            fleet: fleet_byte(p.owner_fleet),
            pos_x: p.pos.x.to_bits(),
            pos_y: p.pos.y.to_bits(),
            heading: p.heading.to_bits(),
            subtype: subtype_byte(p.subtype),
        })
        .collect();

    let beams: Vec<BeamSnapshot> = state
        .beams
        .values()
        .map(|b| BeamSnapshot {
            id: b.id.0,
            fleet: fleet_byte(b.owner_fleet),
            source_pos_x: b.source_pos.x.to_bits(),
            source_pos_y: b.source_pos.y.to_bits(),
            current_angle: b.current_angle.to_bits(),
            phase: phase_byte(b.phase),
            beam_width: b.beam_width.to_bits(),
            range: b.range.to_bits(),
        })
        .collect();

    let events: Vec<LogEvent> = state.events.iter().map(map_event).collect();

    let record = TickRecord { tick: state.tick, ships, projectiles, beams, events };
    let bytes = rmp_serde::to_vec_named(&record).map_err(io::Error::other)?;
    out.write_all(&bytes)
}
