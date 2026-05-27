use fixed::types::{I16F16, I32F32};
use types::ShipId;

use crate::state::{Event, Fleet, SimState};

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
    if ship.hp <= low_hp && !ship.low_hp_flagged {
        ship.low_hp_flagged = true;
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
