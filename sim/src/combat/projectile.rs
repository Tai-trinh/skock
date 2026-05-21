use fixed::types::{I16F16, I32F32};
use types::{HullClass, ProjectileId, ProjectileSubtype, ShipId, TargetPriority};

use crate::{
    geometry::{dist_sq, min_dist_sq_to_segment},
    state::{Event, Fleet, Pos2, Projectile, SimState, Vec2, WeaponKind},
};

use super::{apply_damage, hull_hit_radius, nearest_enemy_in_range, rng_frac};

pub(super) fn spawn_projectiles(state: &mut SimState, _config: &crate::config::SimConfig) {
    let ship_snapshot: Vec<(ShipId, Fleet, Pos2)> =
        state.ships.iter().map(|(id, s)| (*id, s.fleet, s.pos)).collect();

    let ship_ids: Vec<ShipId> = state.ships.keys().copied().collect();
    let mut to_spawn: Vec<Projectile> = Vec::new();

    for ship_id in ship_ids {
        let Some(ship) = state.ships.get(&ship_id) else { continue };
        let weapon_count = ship.weapons.len();
        let fleet = ship.fleet;
        let ship_pos = ship.pos;
        let ship_heading = ship.heading;
        let target_priority = ship.target_priority;

        for hp_idx in 0..weapon_count {
            let Some(ship) = state.ships.get(&ship_id) else { break };
            let w = match ship.weapons.get(hp_idx) {
                Some(w)
                    if matches!(w.kind, WeaponKind::Projectile { .. })
                        && w.cooldown_remaining == 0 =>
                {
                    w
                }
                _ => continue,
            };
            if w.ammo.is_some_and(|a| a == 0) {
                continue;
            }

            let WeaponKind::Projectile {
                subtype,
                speed,
                turn_rate,
                fuse_ticks,
                explosion_radius: expl_radius,
                explosion_damage: expl_damage,
                hit_radius,
            } = w.kind
            else {
                unreachable!()
            };

            let damage = w.damage;
            let crit_chance = w.crit_chance;
            let crit_damage = w.crit_damage;
            let cooldown_ticks = w.cooldown_ticks;
            let salvo_count = w.salvo_count;
            let salvo_spread_angle = w.salvo_spread_angle;
            let fire_forward = w.fire_forward;
            let fire_lateral = w.fire_lateral;
            let range = w.range;

            // Compute fire point in world space from local-frame offsets.
            let fire_pos = local_to_world(ship_pos, ship_heading, fire_forward, fire_lateral);

            // Determine target direction for initial velocity.
            let target_pos_opt = match target_priority {
                TargetPriority::Spread => {
                    let r_sq = I32F32::from_num(range) * I32F32::from_num(range);
                    nearest_enemy_in_range(&state.ships, &ship_pos, fleet, r_sq)
                        .map(|tid| state.ships[&tid].pos)
                }
                _ => {
                    let r_sq = I32F32::from_num(range) * I32F32::from_num(range);
                    super::primary_target(&state.ships, &state.ships[&ship_id], r_sq)
                        .map(|tid| state.ships[&tid].pos)
                }
            };

            let base_angle = match target_pos_opt {
                Some(tpos) => {
                    let dx = I16F16::from_num(tpos.x - fire_pos.x);
                    let dy = I16F16::from_num(tpos.y - fire_pos.y);
                    let d_sq = I32F32::from_num(dx * dx + dy * dy);
                    if d_sq > I32F32::ZERO {
                        cordic::atan2(dy, dx)
                    } else {
                        ship_heading
                    }
                }
                None => ship_heading,
            };

            // Check if there's a target within range for non-mine subtypes.
            if subtype != ProjectileSubtype::Mine && target_pos_opt.is_none() {
                // Check extended range for missiles.
                if subtype == ProjectileSubtype::SeekingMissile {
                    let ext_r = I32F32::from_num(4) * I32F32::from_num(range);
                    let has_target = ship_snapshot.iter().any(|(_, f, tpos)| {
                        *f != fleet && dist_sq(&ship_pos, tpos) <= ext_r * ext_r
                    });
                    if !has_target {
                        continue;
                    }
                } else {
                    continue;
                }
            }

            // Spawn salvo projectiles spread across salvo_spread_angle.
            let fuse = if fuse_ticks > 0 { fuse_ticks } else { 180 };

            for s in 0..salvo_count {
                let angle = if salvo_count > 1 {
                    let step = salvo_spread_angle / I16F16::from_num(salvo_count - 1);
                    let offset =
                        step * I16F16::from_num(s) - salvo_spread_angle / I16F16::from_num(2);
                    base_angle + offset
                } else {
                    base_angle
                };

                let (vel, init_heading) = match subtype {
                    ProjectileSubtype::Mine => {
                        let vx = cordic::cos(ship_heading) * I16F16::from_num(0.3f32);
                        let vy = cordic::sin(ship_heading) * I16F16::from_num(0.3f32);
                        (Vec2 { x: vx, y: vy }, ship_heading)
                    }
                    ProjectileSubtype::Torpedo | ProjectileSubtype::SeekingMissile => {
                        let vx = cordic::cos(angle) * speed;
                        let vy = cordic::sin(angle) * speed;
                        (Vec2 { x: vx, y: vy }, angle)
                    }
                };

                let proj_id = state.alloc_projectile_id();
                to_spawn.push(Projectile {
                    id: proj_id,
                    owner_id: ship_id,
                    owner_fleet: fleet,
                    prev_pos: fire_pos,
                    pos: fire_pos,
                    vel,
                    heading: init_heading,
                    subtype,
                    damage,
                    hit_radius,
                    explosion_radius: expl_radius,
                    explosion_damage: expl_damage,
                    fuse_ticks_remaining: fuse,
                    turn_rate,
                    crit_chance,
                    crit_damage,
                });
            }

            if let Some(ship) = state.ships.get_mut(&ship_id) {
                if let Some(w) = ship.weapons.get_mut(hp_idx) {
                    w.cooldown_remaining = cooldown_ticks;
                    if let Some(ref mut ammo) = w.ammo {
                        *ammo -= 1;
                    }
                }
            }
        }
    }

    for proj in to_spawn {
        state.projectiles.insert(proj.id, proj);
    }
}

/// Converts local-frame offsets to world position.
fn local_to_world(ship_pos: Pos2, heading: I16F16, forward: I16F16, lateral: I16F16) -> Pos2 {
    let cos_h = cordic::cos(heading);
    let sin_h = cordic::sin(heading);
    let wx = cos_h * forward - sin_h * lateral;
    let wy = sin_h * forward + cos_h * lateral;
    Pos2 { x: ship_pos.x + I32F32::from_num(wx), y: ship_pos.y + I32F32::from_num(wy) }
}

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

pub(super) fn advance_projectiles(state: &mut SimState, config: &crate::config::SimConfig) {
    let ship_snapshot: Vec<(ShipId, Fleet, Pos2, HullClass)> =
        state.ships.iter().map(|(id, s)| (*id, s.fleet, s.pos, s.hull_class)).collect();

    let proj_ids: Vec<ProjectileId> = state.projectiles.keys().copied().collect();
    let mut to_remove: Vec<ProjectileId> = Vec::new();
    let mut outcomes: Vec<ProjOutcome> = Vec::new();

    for pid in proj_ids {
        let Some(proj) = state.projectiles.get_mut(&pid) else { continue };

        proj.prev_pos = proj.pos;

        if proj.subtype == ProjectileSubtype::SeekingMissile && proj.turn_rate > I16F16::ZERO {
            steer_missile(proj, &ship_snapshot);
        }

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
                let is_closer = best_hit.is_none_or(|(best, _)| center_dsq < best);
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

    for pid in &to_remove {
        state.projectiles.remove(pid);
    }

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

#[allow(clippy::too_many_arguments)]
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
