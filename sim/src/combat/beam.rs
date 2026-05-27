use fixed::types::{I16F16, I32F32};
use types::{BeamId, HullClass, ShipId};

use crate::{
    config::SimConfig,
    geometry::dist_sq,
    state::{BeamEntity, BeamPhase, Fleet, Pos2, SimState, WeaponKind},
};

use super::{
    apply_damage, consume_hardpoint, hull_hit_radius, normalize_angle, range_sq, roll_damage,
};

pub(super) fn start_beams(state: &mut SimState) {
    let ship_ids: Vec<ShipId> = state.ships.keys().copied().collect();

    for ship_id in ship_ids {
        let Some(ship) = state.ships.get(&ship_id) else { continue };
        let weapon_count = ship.weapons.len();

        for hp_idx in 0..weapon_count {
            // Skip if this hardpoint already has a live beam.
            if state.active_beams.contains_key(&(ship_id, hp_idx)) {
                continue;
            }

            let Some(ship) = state.ships.get(&ship_id) else { break };
            let w = match ship.weapons.get(hp_idx) {
                Some(w)
                    if matches!(w.kind, WeaponKind::Beam { .. }) && w.cooldown_remaining == 0 =>
                {
                    w
                }
                _ => continue,
            };
            if w.ammo.is_some_and(|a| a == 0) {
                continue;
            }

            let WeaponKind::Beam {
                charge_ticks,
                duration_ticks,
                beam_width,
                slew_rate,
                track_rate,
                ramp_ticks,
                ramp_max,
            } = w.kind
            else {
                unreachable!()
            };

            let fleet = ship.fleet;
            let pos = ship.pos;
            let range = w.range;
            let damage = w.damage;
            let crit_chance = w.crit_chance;
            let crit_damage = w.crit_damage;
            let cooldown_ticks = w.cooldown_ticks;

            let target_id =
                super::primary_target(&state.ships, &state.ships[&ship_id], range_sq(range));

            let Some(nearest_id) = target_id else { continue };

            let initial_angle = {
                let t = &state.ships[&nearest_id];
                angle_to(pos, t.pos)
            };

            let beam_id = state.alloc_beam_id();
            state.beams.insert(
                beam_id,
                BeamEntity {
                    id: beam_id,
                    source_id: ship_id,
                    hardpoint_index: hp_idx,
                    owner_fleet: fleet,
                    source_pos: pos,
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

            state.active_beams.insert((ship_id, hp_idx), beam_id);

            consume_hardpoint(state, ship_id, hp_idx, cooldown_ticks);
        }
    }
}

pub(super) fn resolve_beams(state: &mut SimState, config: &SimConfig) {
    let beam_ids: Vec<BeamId> = state.beams.keys().copied().collect();
    let mut beams_to_remove: Vec<BeamId> = Vec::new();

    let ship_snapshot: Vec<(ShipId, Fleet, Pos2, HullClass)> =
        state.ships.iter().map(|(id, s)| (*id, s.fleet, s.pos, s.hull_class)).collect();

    for bid in beam_ids {
        let (source_id, _hardpoint_index) = match state.beams.get(&bid) {
            Some(b) => (b.source_id, b.hardpoint_index),
            None => continue,
        };

        if !state.ships.contains_key(&source_id) {
            beams_to_remove.push(bid);
            continue;
        }

        let source_pos = state.ships[&source_id].pos;
        state.beams.get_mut(&bid).unwrap().source_pos = source_pos;

        let beam = state.beams.get(&bid).unwrap();
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

                let hit_ship = beam_ray_first_hit(
                    &source_pos,
                    angle,
                    range,
                    beam_half_w,
                    &ship_snapshot,
                    owner_fleet,
                    config,
                );

                let ramp_factor = if ramp_ticks > 0 {
                    let frac = I16F16::from_num(on_target_ticks.min(ramp_ticks))
                        / I16F16::from_num(ramp_ticks);
                    I16F16::ONE + (ramp_max - I16F16::ONE) * frac
                } else {
                    I16F16::ONE
                };

                if let Some(target_id) = hit_ship {
                    let final_dmg =
                        roll_damage(damage * ramp_factor, crit_chance, crit_mul, &mut state.rng);
                    apply_damage(state, target_id, final_dmg, owner_fleet);

                    if let Some(beam) = state.beams.get_mut(&bid) {
                        beam.on_target_ticks += 1;
                        if let Some((_, _, tpos, _)) =
                            ship_snapshot.iter().find(|(id, _, _, _)| *id == target_id)
                        {
                            let desired = angle_to(source_pos, *tpos);
                            let diff = normalize_angle(desired - beam.current_angle);
                            beam.current_angle += diff.clamp(-track_rate, track_rate);
                        }
                    }
                } else {
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

    for bid in &beams_to_remove {
        if let Some(beam) = state.beams.remove(bid) {
            state.active_beams.remove(&(beam.source_id, beam.hardpoint_index));
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

    hits.sort_by_key(|(a, _)| *a);
    hits.into_iter().next().map(|(_, id)| id)
}

fn angle_to(from: Pos2, to: Pos2) -> I16F16 {
    I16F16::from_num(cordic::atan2(to.y - from.y, to.x - from.x))
}
