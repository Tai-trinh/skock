use std::collections::BTreeMap;

use fixed::types::{I16F16, I32F32};
use rand_core::RngCore;
use types::{BeamId, HullClass, ProjectileId, ProjectileSubtype, ShipId, WeaponType};

use crate::{
    config::SimConfig,
    geometry::{dist_sq, min_dist_sq_to_segment},
    state::{BeamEntity, BeamPhase, Event, Fleet, Pos2, Projectile, SimState, Vec2},
};

// ── RNG ───────────────────────────────────────────────────────────────────────

fn rng_frac(rng: &mut impl RngCore) -> I16F16 {
    I16F16::from_bits((rng.next_u64() >> 48) as i32)
}

// ── Hull hit radius ───────────────────────────────────────────────────────────

fn hull_hit_radius(hull_class: HullClass, config: &SimConfig) -> I16F16 {
    let r = config.hull_hit_radii.get(&format!("{hull_class:?}")).copied().unwrap_or(8.0);
    I16F16::from_num(r)
}

// ── Nearest enemy ─────────────────────────────────────────────────────────────

fn nearest_enemy_in_range(
    ships: &BTreeMap<ShipId, crate::state::Ship>,
    from: &Pos2,
    fleet: Fleet,
    range_sq: I32F32,
) -> Option<ShipId> {
    ships
        .iter()
        .filter(|(_, s)| s.fleet != fleet)
        .filter_map(|(id, s)| {
            let d = dist_sq(from, &s.pos);
            (d <= range_sq).then_some((d, *id))
        })
        .min_by(|(a, _), (b, _)| a.cmp(b))
        .map(|(_, id)| id)
}

// ── Phase 6: weapon firing ────────────────────────────────────────────────────

pub fn fire_weapons(state: &mut SimState, config: &SimConfig) {
    fire_hitscan(state);
    spawn_projectiles(state, config);
    start_beams(state);
}

fn fire_hitscan(state: &mut SimState) {
    let ship_ids: Vec<ShipId> = state.ships.keys().copied().collect();
    for id in ship_ids {
        if !state.ships.contains_key(&id) {
            continue;
        }
        let (fleet, pos, range_sq, damage, miss_chance, crit_chance, crit_damage, cooldown_ticks) = {
            let ship = &state.ships[&id];
            let w = match &ship.weapon {
                Some(w) if w.weapon_type == WeaponType::Hitscan && w.cooldown_remaining == 0 => w,
                _ => continue,
            };
            if w.ammo.is_some_and(|a| a == 0) {
                continue;
            }
            let r = I32F32::from_num(w.range);
            (
                ship.fleet,
                ship.pos,
                r * r,
                w.damage,
                w.miss_chance,
                w.crit_chance,
                w.crit_damage,
                w.cooldown_ticks,
            )
        };

        let Some(target_id) = nearest_enemy_in_range(&state.ships, &pos, fleet, range_sq) else {
            continue;
        };
        let target_pos = state.ships[&target_id].pos;

        let miss_roll = rng_frac(&mut state.rng);
        if miss_roll < miss_chance {
            state.events.push(Event::HitscanMissed {
                source_id: id,
                target_id,
                fleet,
                source_pos_x: pos.x,
                source_pos_y: pos.y,
                target_pos_x: target_pos.x,
                target_pos_y: target_pos.y,
            });
        } else {
            let crit_roll = rng_frac(&mut state.rng);
            let dmg = if crit_roll < crit_chance { damage * crit_damage } else { damage };
            apply_damage(state, target_id, dmg, fleet);
            state.events.push(Event::HitscanFired {
                source_id: id,
                target_id,
                damage: dmg,
                fleet,
                source_pos_x: pos.x,
                source_pos_y: pos.y,
                target_pos_x: target_pos.x,
                target_pos_y: target_pos.y,
            });
        }

        let w = state.ships.get_mut(&id).and_then(|s| s.weapon.as_mut());
        if let Some(w) = w {
            w.cooldown_remaining = cooldown_ticks;
            if let Some(ref mut ammo) = w.ammo {
                *ammo -= 1;
            }
        }
    }
}

fn spawn_projectiles(state: &mut SimState, config: &SimConfig) {
    // Snapshot ship data needed for target selection before touching projectiles
    let ship_snapshot: Vec<(ShipId, Fleet, Pos2)> =
        state.ships.iter().map(|(id, s)| (*id, s.fleet, s.pos)).collect();

    let ship_ids: Vec<ShipId> = state.ships.keys().copied().collect();
    let mut to_spawn: Vec<Projectile> = Vec::new();

    for id in ship_ids {
        let Some(ship) = state.ships.get(&id) else { continue };
        let w = match &ship.weapon {
            Some(w) if w.weapon_type == WeaponType::Projectile && w.cooldown_remaining == 0 => w,
            _ => continue,
        };
        if w.ammo.is_some_and(|a| a == 0) {
            continue;
        }

        let fleet = ship.fleet;
        let pos = ship.pos;
        let heading = ship.heading;
        let subtype = w.subtype.unwrap_or(ProjectileSubtype::SeekingMissile);
        let speed = w.projectile_speed;
        let turn_rate = w.proj_turn_rate;
        let damage = w.damage;
        let fuse_ticks = w.fuse_ticks;
        let expl_radius = w.explosion_radius;
        let expl_damage = w.explosion_damage;
        let hit_radius = w.proj_hit_radius;
        let crit_chance = w.crit_chance;
        let crit_damage = w.crit_damage;
        let cooldown_ticks = w.cooldown_ticks;

        let (vel, init_heading) = match subtype {
            ProjectileSubtype::Mine => {
                let vx = cordic::cos(heading) * I16F16::from_num(0.3f32);
                let vy = cordic::sin(heading) * I16F16::from_num(0.3f32);
                (Vec2 { x: vx, y: vy }, heading)
            }
            ProjectileSubtype::Torpedo => {
                let vx = cordic::cos(heading) * speed;
                let vy = cordic::sin(heading) * speed;
                (Vec2 { x: vx, y: vy }, heading)
            }
            ProjectileSubtype::SeekingMissile => {
                let ext_r = I32F32::from_num(4) * I32F32::from_num(w.range);
                let target = ship_snapshot
                    .iter()
                    .filter(|(_, f, _)| *f != fleet)
                    .filter_map(|(_tid, _, tpos)| {
                        let d = dist_sq(&pos, tpos);
                        (d <= ext_r * ext_r).then_some((d, tpos))
                    })
                    .min_by(|(a, _), (b, _)| a.cmp(b))
                    .map(|(_, tpos)| *tpos);

                match target {
                    Some(tpos) => {
                        let dx = I32F32::from_num(tpos.x - pos.x);
                        let dy = I32F32::from_num(tpos.y - pos.y);
                        let dist = cordic::sqrt(dx * dx + dy * dy);
                        if dist > I32F32::ZERO {
                            let vx = I16F16::from_num(dx / dist) * speed;
                            let vy = I16F16::from_num(dy / dist) * speed;
                            let h = cordic::atan2(vy, vx);
                            (Vec2 { x: vx, y: vy }, h)
                        } else {
                            let vx = cordic::cos(heading) * speed;
                            let vy = cordic::sin(heading) * speed;
                            (Vec2 { x: vx, y: vy }, heading)
                        }
                    }
                    None => {
                        let vx = cordic::cos(heading) * speed;
                        let vy = cordic::sin(heading) * speed;
                        (Vec2 { x: vx, y: vy }, heading)
                    }
                }
            }
        };

        let proj_hit_radius = if hit_radius > I16F16::ZERO {
            hit_radius
        } else {
            let key = match subtype {
                ProjectileSubtype::SeekingMissile => "seeking_missile",
                ProjectileSubtype::Torpedo => "torpedo",
                ProjectileSubtype::Mine => "mine",
            };
            I16F16::from_num(config.projectile_hit_radii.get(key).copied().unwrap_or(2.0))
        };

        let fuse = if fuse_ticks > 0 { fuse_ticks } else { 180 };
        let proj_id = state.alloc_projectile_id();

        to_spawn.push(Projectile {
            id: proj_id,
            owner_id: id,
            owner_fleet: fleet,
            prev_pos: pos,
            pos,
            vel,
            heading: init_heading,
            subtype,
            damage,
            hit_radius: proj_hit_radius,
            explosion_radius: expl_radius,
            explosion_damage: expl_damage,
            fuse_ticks_remaining: fuse,
            turn_rate,
            crit_chance,
            crit_damage,
        });

        // Update weapon cooldown and ammo
        if let Some(ship) = state.ships.get_mut(&id) {
            if let Some(w) = ship.weapon.as_mut() {
                w.cooldown_remaining = cooldown_ticks;
                if let Some(ref mut ammo) = w.ammo {
                    *ammo -= 1;
                }
            }
        }
    }

    for proj in to_spawn {
        state.projectiles.insert(proj.id, proj);
    }
}

fn start_beams(state: &mut SimState) {
    let ship_ids: Vec<ShipId> = state.ships.keys().copied().collect();

    for id in ship_ids {
        let Some(ship) = state.ships.get(&id) else { continue };
        let w = match &ship.weapon {
            Some(w)
                if w.weapon_type == WeaponType::Beam
                    && w.cooldown_remaining == 0
                    && w.active_beam_id.is_none() =>
            {
                w
            }
            _ => continue,
        };
        if w.ammo.is_some_and(|a| a == 0) {
            continue;
        }

        let fleet = ship.fleet;
        let pos = ship.pos;
        let range = w.range;
        let damage = w.damage;
        let beam_width = w.beam_width;
        let charge_ticks = w.charge_ticks;
        let duration_ticks = w.duration_ticks;
        let slew_rate = w.slew_rate;
        let track_rate = w.track_rate;
        let ramp_ticks = w.ramp_ticks;
        let ramp_max = w.ramp_max;
        let crit_chance = w.crit_chance;
        let crit_damage = w.crit_damage;
        let cooldown_ticks = w.cooldown_ticks;

        let range_sq = I32F32::from_num(range) * I32F32::from_num(range);
        let Some(nearest_id) = nearest_enemy_in_range(&state.ships, &pos, fleet, range_sq) else {
            continue;
        };

        let initial_angle = {
            let t = &state.ships[&nearest_id];
            angle_to(pos, t.pos)
        };

        let beam_id = state.alloc_beam_id();
        state.beams.insert(
            beam_id,
            BeamEntity {
                id: beam_id,
                source_id: id,
                owner_fleet: fleet,
                current_angle: initial_angle,
                phase: BeamPhase::Charging,
                charge_ticks_remaining: charge_ticks.max(1),
                damage_ticks_remaining: duration_ticks.max(1),
                on_target_ticks: 0,
                damage,
                range,
                beam_width,
                slew_rate,
                track_rate,
                ramp_ticks,
                ramp_max,
                crit_chance,
                crit_damage,
            },
        );

        if let Some(ship) = state.ships.get_mut(&id) {
            if let Some(w) = ship.weapon.as_mut() {
                w.active_beam_id = Some(beam_id);
                w.cooldown_remaining = cooldown_ticks;
            }
        }
    }
}

// ── Phase 7: advance projectiles ──────────────────────────────────────────────

// All data needed to resolve a hit or explosion after the main projectile loop.
enum ProjOutcome {
    DirectHit {
        target: ShipId,
        base_damage: I16F16,
        crit_chance: I16F16,
        crit_damage: I16F16,
        attacker: Fleet,
    },
    Explosion {
        proj_id: ProjectileId,
        attacker: Fleet,
        pos: Pos2,
        radius: I16F16,
        damage: I16F16,
        crit_chance: I16F16,
        crit_damage: I16F16,
    },
    SplashPlusDirect {
        direct_target: ShipId,
        direct_damage: I16F16,
        crit_chance: I16F16,
        crit_damage: I16F16,
        attacker: Fleet,
        proj_id: ProjectileId,
        pos: Pos2,
        radius: I16F16,
        expl_damage: I16F16,
    },
}

pub fn advance_projectiles(state: &mut SimState, config: &SimConfig) {
    // Snapshot ship data so we can work on projectiles mutably without borrow conflicts.
    let ship_snapshot: Vec<(ShipId, Fleet, Pos2, HullClass)> =
        state.ships.iter().map(|(id, s)| (*id, s.fleet, s.pos, s.hull_class)).collect();

    let proj_ids: Vec<ProjectileId> = state.projectiles.keys().copied().collect();
    let mut to_remove: Vec<ProjectileId> = Vec::new();
    let mut outcomes: Vec<ProjOutcome> = Vec::new();

    for pid in proj_ids {
        let Some(proj) = state.projectiles.get_mut(&pid) else { continue };

        // Save prev_pos before moving
        proj.prev_pos = proj.pos;

        // Seek: missiles turn toward nearest enemy
        if proj.subtype == ProjectileSubtype::SeekingMissile && proj.turn_rate > I16F16::ZERO {
            steer_missile(proj, &ship_snapshot);
        }

        // Move
        proj.pos.x += I32F32::from_num(proj.vel.x);
        proj.pos.y += I32F32::from_num(proj.vel.y);
        if proj.vel.x != I16F16::ZERO || proj.vel.y != I16F16::ZERO {
            proj.heading = cordic::atan2(proj.vel.y, proj.vel.x);
        }

        if proj.fuse_ticks_remaining > 0 {
            proj.fuse_ticks_remaining -= 1;
        }

        let pid = proj.id;
        let owner_fleet = proj.owner_fleet;
        let pos = proj.pos;
        let prev_pos = proj.prev_pos;
        let hit_radius = proj.hit_radius;
        let expl_radius = proj.explosion_radius;
        let expl_damage = proj.explosion_damage;
        let damage = proj.damage;
        let subtype = proj.subtype;
        let crit_chance = proj.crit_chance;
        let crit_damage = proj.crit_damage;
        let fuse_done = proj.fuse_ticks_remaining == 0;

        // ── Mine: proximity trigger ───────────────────────────────────────────
        if subtype == ProjectileSubtype::Mine {
            let expl_r_sq = I32F32::from_num(expl_radius) * I32F32::from_num(expl_radius);
            let triggered = ship_snapshot
                .iter()
                .any(|(_, f, spos, _)| *f != owner_fleet && dist_sq(&pos, spos) <= expl_r_sq);
            if triggered || fuse_done {
                outcomes.push(ProjOutcome::Explosion {
                    proj_id: pid,
                    attacker: owner_fleet,
                    pos,
                    radius: expl_radius,
                    damage: expl_damage,
                    crit_chance,
                    crit_damage,
                });
                to_remove.push(pid);
            }
            continue;
        }

        // ── Fuse expired (torpedo/missile with no hit) ─────────────────────────
        if fuse_done {
            if expl_radius > I16F16::ZERO {
                outcomes.push(ProjOutcome::Explosion {
                    proj_id: pid,
                    attacker: owner_fleet,
                    pos,
                    radius: expl_radius,
                    damage: expl_damage,
                    crit_chance,
                    crit_damage,
                });
            }
            to_remove.push(pid);
            continue;
        }

        // ── Swept-segment hit detection ───────────────────────────────────────
        let mut best_hit: Option<(I32F32, ShipId)> = None;

        for (ship_id, fleet, spos, hull_class) in &ship_snapshot {
            if *fleet == owner_fleet {
                continue;
            }
            let hull_r = hull_hit_radius(*hull_class, config);
            let combined_r = hull_r + hit_radius;
            let combined_r_sq = I32F32::from_num(combined_r) * I32F32::from_num(combined_r);
            let min_dsq = min_dist_sq_to_segment(spos, &prev_pos, &pos);

            if min_dsq <= combined_r_sq {
                let center_dsq = dist_sq(&pos, spos);
                let is_closer = best_hit.map_or(true, |(best, _)| center_dsq < best);
                if is_closer {
                    best_hit = Some((center_dsq, *ship_id));
                }
            }
        }

        if let Some((_, target_id)) = best_hit {
            if expl_radius > I16F16::ZERO {
                outcomes.push(ProjOutcome::SplashPlusDirect {
                    direct_target: target_id,
                    direct_damage: damage,
                    crit_chance,
                    crit_damage,
                    attacker: owner_fleet,
                    proj_id: pid,
                    pos,
                    radius: expl_radius,
                    expl_damage,
                });
            } else {
                outcomes.push(ProjOutcome::DirectHit {
                    target: target_id,
                    base_damage: damage,
                    crit_chance,
                    crit_damage,
                    attacker: owner_fleet,
                });
            }
            to_remove.push(pid);
        }
    }

    // Remove spent projectiles
    for pid in &to_remove {
        state.projectiles.remove(pid);
    }

    // Apply outcomes (rolling crits + applying damage)
    for outcome in outcomes {
        match outcome {
            ProjOutcome::DirectHit {
                target,
                base_damage,
                crit_chance,
                crit_damage: crit_mul,
                attacker,
            } => {
                let roll = rng_frac(&mut state.rng);
                let dmg = if roll < crit_chance { base_damage * crit_mul } else { base_damage };
                apply_damage(state, target, dmg, attacker);
            }
            ProjOutcome::Explosion {
                proj_id,
                attacker,
                pos,
                radius,
                damage,
                crit_chance,
                crit_damage: crit_mul,
            } => {
                apply_explosion(
                    state,
                    proj_id,
                    attacker,
                    pos,
                    radius,
                    damage,
                    crit_chance,
                    crit_mul,
                );
            }
            ProjOutcome::SplashPlusDirect {
                direct_target,
                direct_damage,
                crit_chance,
                crit_damage: crit_mul,
                attacker,
                proj_id,
                pos,
                radius,
                expl_damage,
            } => {
                let roll = rng_frac(&mut state.rng);
                let dmg = if roll < crit_chance { direct_damage * crit_mul } else { direct_damage };
                apply_damage(state, direct_target, dmg, attacker);
                apply_explosion(
                    state,
                    proj_id,
                    attacker,
                    pos,
                    radius,
                    expl_damage,
                    crit_chance,
                    crit_mul,
                );
            }
        }
    }
}

fn steer_missile(proj: &mut Projectile, ships: &[(ShipId, Fleet, Pos2, HullClass)]) {
    let fleet = proj.owner_fleet;
    let pos = proj.pos;

    let best = ships
        .iter()
        .filter(|(_, f, _, _)| *f != fleet)
        .min_by(|(_, _, a, _), (_, _, b, _)| dist_sq(&pos, a).cmp(&dist_sq(&pos, b)));

    let Some((_, _, tpos, _)) = best else { return };

    let dx = I16F16::from_num(tpos.x - pos.x);
    let dy = I16F16::from_num(tpos.y - pos.y);
    let desired = cordic::atan2(dy, dx);

    let diff = normalize_angle(desired - proj.heading);
    let turn = diff.clamp(-proj.turn_rate, proj.turn_rate);
    let new_heading = proj.heading + turn;

    let vx = I32F32::from_num(proj.vel.x);
    let vy = I32F32::from_num(proj.vel.y);
    let speed_sq = vx * vx + vy * vy;
    if speed_sq > I32F32::ZERO {
        let speed = I16F16::from_num(cordic::sqrt(speed_sq));
        proj.vel.x = cordic::cos(new_heading) * speed;
        proj.vel.y = cordic::sin(new_heading) * speed;
    }
    proj.heading = new_heading;
}

fn apply_explosion(
    state: &mut SimState,
    proj_id: ProjectileId,
    attacker: Fleet,
    pos: Pos2,
    radius: I16F16,
    damage: I16F16,
    crit_chance: I16F16,
    crit_mul: I16F16,
) {
    if radius <= I16F16::ZERO {
        return;
    }
    let radius_sq = I32F32::from_num(radius) * I32F32::from_num(radius);
    let targets: Vec<ShipId> = state
        .ships
        .iter()
        .filter(|(_, s)| s.fleet != attacker && dist_sq(&pos, &s.pos) <= radius_sq)
        .map(|(id, _)| *id)
        .collect();

    for target_id in targets {
        let roll = rng_frac(&mut state.rng);
        let dmg = if roll < crit_chance { damage * crit_mul } else { damage };
        apply_damage(state, target_id, dmg, attacker);
    }

    state.events.push(Event::ProjectileExplosion {
        id: proj_id,
        fleet: attacker,
        pos_x: pos.x,
        pos_y: pos.y,
        radius,
    });
}

// ── Phase 8: resolve beams ────────────────────────────────────────────────────

pub fn resolve_beams(state: &mut SimState, config: &SimConfig) {
    let beam_ids: Vec<BeamId> = state.beams.keys().copied().collect();
    let mut beams_to_remove: Vec<BeamId> = Vec::new();

    // Snapshot ship data for queries during beam processing
    let ship_snapshot: Vec<(ShipId, Fleet, Pos2, HullClass)> =
        state.ships.iter().map(|(id, s)| (*id, s.fleet, s.pos, s.hull_class)).collect();

    for bid in beam_ids {
        let Some(beam) = state.beams.get(&bid) else { continue };
        let source_id = beam.source_id;

        if !state.ships.contains_key(&source_id) {
            beams_to_remove.push(bid);
            continue;
        }

        let source_pos = state.ships[&source_id].pos;

        match beam.phase {
            BeamPhase::Charging => {
                let beam = state.beams.get_mut(&bid).unwrap();
                if beam.charge_ticks_remaining > 0 {
                    beam.charge_ticks_remaining -= 1;
                }
                if beam.charge_ticks_remaining == 0 {
                    beam.phase = BeamPhase::Firing;
                }
            }
            BeamPhase::Firing => {
                // Snapshot beam params
                let (
                    angle,
                    range,
                    beam_half_w,
                    damage,
                    ramp_ticks,
                    ramp_max,
                    slew_rate,
                    track_rate,
                    crit_chance,
                    crit_mul,
                    on_target_ticks,
                    owner_fleet,
                ) = {
                    let b = &state.beams[&bid];
                    (
                        b.current_angle,
                        b.range,
                        b.beam_width / I16F16::from_num(2),
                        b.damage,
                        b.ramp_ticks,
                        b.ramp_max,
                        b.slew_rate,
                        b.track_rate,
                        b.crit_chance,
                        b.crit_damage,
                        b.on_target_ticks,
                        b.owner_fleet,
                    )
                };

                // Beam hit detection using snapshot
                let hit_ship = beam_ray_first_hit(
                    &source_pos,
                    angle,
                    range,
                    beam_half_w,
                    &ship_snapshot,
                    owner_fleet,
                    config,
                );

                // Ramp damage
                let ramp_factor = if ramp_ticks > 0 {
                    let frac = I16F16::from_num(on_target_ticks.min(ramp_ticks))
                        / I16F16::from_num(ramp_ticks);
                    I16F16::ONE + (ramp_max - I16F16::ONE) * frac
                } else {
                    I16F16::ONE
                };

                if let Some(target_id) = hit_ship {
                    let roll = rng_frac(&mut state.rng);
                    let final_dmg = {
                        let base = damage * ramp_factor;
                        if roll < crit_chance {
                            base * crit_mul
                        } else {
                            base
                        }
                    };
                    apply_damage(state, target_id, final_dmg, owner_fleet);

                    // Update beam: increment on_target_ticks, track at track_rate
                    if let Some(beam) = state.beams.get_mut(&bid) {
                        beam.on_target_ticks += 1;
                        // Slew toward hit target at track_rate
                        if let Some((_, _, tpos, _)) =
                            ship_snapshot.iter().find(|(id, _, _, _)| *id == target_id)
                        {
                            let desired = angle_to(source_pos, *tpos);
                            let diff = normalize_angle(desired - beam.current_angle);
                            beam.current_angle += diff.clamp(-track_rate, track_rate);
                        }
                    }
                } else {
                    // Not hitting — slew toward nearest enemy at slew_rate
                    if let Some(beam) = state.beams.get_mut(&bid) {
                        beam.on_target_ticks = 0;
                    }

                    let nearest_pos = ship_snapshot
                        .iter()
                        .filter(|(_, f, _, _)| *f != owner_fleet)
                        .map(|(_, _, pos, _)| pos)
                        .min_by(|a, b| dist_sq(&source_pos, a).cmp(&dist_sq(&source_pos, b)))
                        .copied();

                    if let (Some(beam), Some(np)) = (state.beams.get_mut(&bid), nearest_pos) {
                        let desired = angle_to(source_pos, np);
                        let diff = normalize_angle(desired - beam.current_angle);
                        beam.current_angle += diff.clamp(-slew_rate, slew_rate);
                    }
                }

                // Decrement duration
                let expired = {
                    let beam = state.beams.get_mut(&bid).unwrap();
                    if beam.damage_ticks_remaining > 0 {
                        beam.damage_ticks_remaining -= 1;
                    }
                    beam.damage_ticks_remaining == 0
                };
                if expired {
                    beams_to_remove.push(bid);
                }
            }
        }
    }

    // Expire beams and clear active_beam_id on the source ship
    for bid in &beams_to_remove {
        if let Some(beam) = state.beams.remove(bid) {
            if let Some(ship) = state.ships.get_mut(&beam.source_id) {
                if let Some(w) = ship.weapon.as_mut() {
                    w.active_beam_id = None;
                }
            }
        }
    }
}

fn beam_ray_first_hit(
    source: &Pos2,
    angle: I16F16,
    range: I16F16,
    beam_half_w: I16F16,
    ships: &[(ShipId, Fleet, Pos2, HullClass)],
    owner_fleet: Fleet,
    config: &SimConfig,
) -> Option<ShipId> {
    let dir_x = I32F32::from_num(cordic::cos(angle));
    let dir_y = I32F32::from_num(cordic::sin(angle));
    let range_f = I32F32::from_num(range);
    let half_w = I32F32::from_num(beam_half_w);

    let mut hits: Vec<(I32F32, ShipId)> = ships
        .iter()
        .filter(|(_, f, _, _)| *f != owner_fleet)
        .filter_map(|(id, _, spos, hull_class)| {
            let hull_r = I32F32::from_num(hull_hit_radius(*hull_class, config));
            let combined_r = hull_r + half_w;

            let cx = spos.x - source.x;
            let cy = spos.y - source.y;
            let t = cx * dir_x + cy * dir_y;
            if t < I32F32::ZERO || t > range_f {
                return None;
            }
            let perp_x = cx - dir_x * t;
            let perp_y = cy - dir_y * t;
            let perp_sq = perp_x * perp_x + perp_y * perp_y;
            (perp_sq <= combined_r * combined_r).then_some((t, *id))
        })
        .collect();

    hits.sort_by(|(a, _), (b, _)| a.cmp(b));
    hits.into_iter().next().map(|(_, id)| id)
}

// ── Damage application ────────────────────────────────────────────────────────

pub fn apply_damage(
    state: &mut SimState,
    target_id: ShipId,
    raw_damage: I16F16,
    attacker_fleet: Fleet,
) {
    let Some(ship) = state.ships.get_mut(&target_id) else { return };

    let shield_absorbed = raw_damage.min(ship.shield_hp);
    ship.shield_hp -= shield_absorbed;
    let spillover = raw_damage - shield_absorbed;
    let hull_damage = spillover * (I16F16::ONE - ship.armor);
    ship.hp -= hull_damage;

    let idx = match attacker_fleet {
        Fleet::A => 0,
        Fleet::B => 1,
    };
    state.damage_dealt[idx] += I32F32::from_num(hull_damage);

    let low_hp = ship.max_hp / I16F16::from_num(4);
    if ship.hp <= low_hp && !state.low_hp_flagged.get(&target_id).copied().unwrap_or(false) {
        state.low_hp_flagged.insert(target_id, true);
        state.events.push(Event::ShipAtLowHp { id: target_id });
    }

    if ship.hp <= I16F16::ZERO {
        ship.hp = I16F16::ZERO;
        let fleet = ship.fleet;
        let hull_class = ship.hull_class;
        let is_mothership = ship.is_mothership;
        state.killed.push((fleet, hull_class, is_mothership));
        state.events.push(Event::ShipDestroyed { id: target_id, fleet });
        state.ships.remove(&target_id);
    }
}

// ── Angle helpers ─────────────────────────────────────────────────────────────

fn normalize_angle(a: I16F16) -> I16F16 {
    let pi = I16F16::from_num(std::f64::consts::PI);
    let two_pi = pi + pi;
    let mut r = a;
    while r > pi {
        r -= two_pi;
    }
    while r < -pi {
        r += two_pi;
    }
    r
}

fn angle_to(from: Pos2, to: Pos2) -> I16F16 {
    let dx = I16F16::from_num(to.x - from.x);
    let dy = I16F16::from_num(to.y - from.y);
    cordic::atan2(dy, dx)
}
