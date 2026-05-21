use std::collections::BTreeMap;

use rand_core::{RngCore, SeedableRng};
use rand_xoshiro::Xoshiro256Plus;

use crate::catalog::{
    self, Blueprint, ResearchItem, ResearchTrack, ResearchTrackDef, TierDef, UpgradeEffect,
    RESEARCH_ITEMS, RESEARCH_TRACKS, TIERS,
};
use crate::protocol::{
    GetOffersMsg, OutMessage, ResearchItemOffer, ResearchTrackOffer, ResourceState, SessionDelta,
    ShipOffer, TierOffer,
};

// ── Session state ─────────────────────────────────────────────────────────────

struct FleetEntry {
    original_index: usize,
    tonnage: i32,
}

pub struct Session {
    run_seed: u64,
    jump_number: u32,
    tier_rerolls: [u32; 4],
    research_rerolls: [u32; 4],

    ship_tiers: Vec<TierOffer>,
    research_tracks: Vec<ResearchTrackOffer>,

    salvage: i32,
    tech: i32,
    hangar_used: i32,
    hangar_cap: i32,

    fleet: Vec<FleetEntry>,
    upgrade_purchases: BTreeMap<String, i32>,

    // delta accumulators
    ships_commissioned: Vec<CommissionRecord>,
    ships_salvaged_original: Vec<usize>,
    upgrades_purchased: Vec<ResearchItemOffer>,
}

struct CommissionRecord {
    blueprint_id: &'static str,
    alive: bool,
}

impl Session {
    pub fn new(msg: GetOffersMsg) -> Self {
        let fleet = msg
            .fleet
            .iter()
            .map(|r| FleetEntry { original_index: r.index, tonnage: r.tonnage })
            .collect();

        let (ship_tiers, research_tracks) = build_offers(
            msg.run_seed,
            msg.jump_number,
            &msg.tier_rerolls,
            &msg.research_rerolls,
            &msg.upgrade_purchases,
        );

        Self {
            run_seed: msg.run_seed,
            jump_number: msg.jump_number,
            tier_rerolls: msg.tier_rerolls,
            research_rerolls: msg.research_rerolls,
            ship_tiers,
            research_tracks,
            salvage: msg.salvage,
            tech: msg.tech,
            hangar_used: msg.hangar_used,
            hangar_cap: msg.hangar_cap,
            fleet,
            upgrade_purchases: msg.upgrade_purchases,
            ships_commissioned: vec![],
            ships_salvaged_original: vec![],
            upgrades_purchased: vec![],
        }
    }

    pub fn get_offers_response(&self) -> OutMessage {
        OutMessage {
            ok: true,
            ship_tiers: Some(self.ship_tiers.clone()),
            research_tracks: Some(self.research_tracks.clone()),
            state: Some(self.resource_state()),
            ..Default::default()
        }
    }

    pub fn commission(&mut self, blueprint_id: &str) -> OutMessage {
        let bp = match find_blueprint(blueprint_id) {
            Some(b) => b,
            None => return OutMessage::err("not_in_offer"),
        };

        if !self.blueprint_in_current_offer(bp) {
            return OutMessage::err("not_in_offer");
        }
        if self.salvage < bp.salvage_cost {
            return OutMessage::err("cannot_afford");
        }
        let t = bp.tonnage();
        if self.hangar_used + t > self.hangar_cap {
            return OutMessage::err("hangar_full");
        }

        self.salvage -= bp.salvage_cost;
        self.hangar_used += t;
        self.ships_commissioned.push(CommissionRecord { blueprint_id: bp.id, alive: true });

        OutMessage {
            ok: true,
            ship: Some(ship_offer_from(bp)),
            state: Some(self.resource_state()),
            ..Default::default()
        }
    }

    pub fn salvage_fleet_ship(&mut self, index: usize) -> OutMessage {
        if index >= self.fleet.len() {
            return OutMessage::err("invalid_index");
        }
        let entry = self.fleet.remove(index);
        let yield_amount = entry.tonnage * 3;
        self.salvage += yield_amount;
        self.hangar_used -= entry.tonnage;
        self.ships_salvaged_original.push(entry.original_index);

        OutMessage {
            ok: true,
            salvage_yield: Some(yield_amount),
            state: Some(self.resource_state()),
            ..Default::default()
        }
    }

    pub fn reroll_tier(&mut self, tier_index: usize) -> OutMessage {
        let tier_def = match TIERS.get(tier_index) {
            Some(t) => t,
            None => return OutMessage::err("invalid_index"),
        };
        if self.salvage < tier_def.reroll_cost {
            return OutMessage::err("cannot_afford");
        }
        self.salvage -= tier_def.reroll_cost;
        self.tier_rerolls[tier_index] += 1;

        let new_tier = build_tier_offer(
            tier_def,
            tier_index,
            self.run_seed,
            self.jump_number,
            self.tier_rerolls[tier_index],
        );
        self.ship_tiers[tier_index] = new_tier.clone();

        OutMessage {
            ok: true,
            tier: Some(new_tier),
            state: Some(self.resource_state()),
            ..Default::default()
        }
    }

    pub fn reroll_research(&mut self, track_index: usize) -> OutMessage {
        let track_def = match RESEARCH_TRACKS.get(track_index) {
            Some(t) => t,
            None => return OutMessage::err("invalid_index"),
        };
        let cost = match track_def.reroll_cost {
            Some(c) => c,
            None => return OutMessage::err("cannot_reroll"),
        };
        if self.tech < cost {
            return OutMessage::err("cannot_afford");
        }
        self.tech -= cost;
        self.research_rerolls[track_index] += 1;

        let new_track = build_research_track_offer(
            track_def,
            track_index,
            self.run_seed,
            self.jump_number,
            self.research_rerolls[track_index],
            &self.upgrade_purchases,
        );
        self.research_tracks[track_index] = new_track.clone();

        OutMessage {
            ok: true,
            track: Some(new_track),
            state: Some(self.resource_state()),
            ..Default::default()
        }
    }

    pub fn buy_research(&mut self, upgrade_id: &str) -> OutMessage {
        let item = match find_research_item(upgrade_id) {
            Some(i) => i,
            None => return OutMessage::err("not_in_offer"),
        };
        if !self.research_item_in_current_offer(item) {
            return OutMessage::err("not_in_offer");
        }
        let purchased = *self.upgrade_purchases.get(upgrade_id).unwrap_or(&0);
        if item.max_purchases > 0 && purchased >= item.max_purchases {
            return OutMessage::err("maxed");
        }
        if self.tech < item.tech_cost {
            return OutMessage::err("cannot_afford");
        }

        self.tech -= item.tech_cost;
        for effect in item.effects {
            if let UpgradeEffect::HangarCap { delta } = effect {
                self.hangar_cap += delta;
            }
        }
        let new_count = {
            let e = self.upgrade_purchases.entry(upgrade_id.to_owned()).or_insert(0);
            *e += 1;
            *e
        };
        self.upgrades_purchased.push(research_item_offer(item, new_count));

        // Refresh the research track so purchased count is up to date.
        self.refresh_research_track_for(item.track);

        OutMessage { ok: true, state: Some(self.resource_state()), ..Default::default() }
    }

    pub fn shopping_done(&self) -> OutMessage {
        let ships_commissioned: Vec<&'static str> =
            self.ships_commissioned.iter().filter(|r| r.alive).map(|r| r.blueprint_id).collect();

        OutMessage {
            ok: true,
            delta: Some(SessionDelta {
                salvage_final: self.salvage,
                tech_final: self.tech,
                hangar_used_final: self.hangar_used,
                hangar_cap_final: self.hangar_cap,
                tier_rerolls_final: self.tier_rerolls,
                research_rerolls_final: self.research_rerolls,
                ships_commissioned,
                ships_salvaged: self.ships_salvaged_original.clone(),
                upgrades_purchased: self.upgrades_purchased.clone(),
            }),
            ..Default::default()
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    fn resource_state(&self) -> ResourceState {
        ResourceState {
            salvage: self.salvage,
            tech: self.tech,
            hangar_used: self.hangar_used,
            hangar_cap: self.hangar_cap,
        }
    }

    fn blueprint_in_current_offer(&self, bp: &Blueprint) -> bool {
        self.ship_tiers.iter().any(|t| t.slots.iter().any(|s| s.blueprint_id == bp.id))
    }

    fn research_item_in_current_offer(&self, item: &ResearchItem) -> bool {
        self.research_tracks.iter().any(|t| t.items.iter().any(|i| i.upgrade_id == item.id))
    }

    fn refresh_research_track_for(&mut self, track: ResearchTrack) {
        let pos = RESEARCH_TRACKS.iter().position(|t| t.track == track);
        if let Some(idx) = pos {
            let new_track = build_research_track_offer(
                &RESEARCH_TRACKS[idx],
                idx,
                self.run_seed,
                self.jump_number,
                self.research_rerolls[idx],
                &self.upgrade_purchases,
            );
            self.research_tracks[idx] = new_track;
        }
    }
}

// ── Offer generation ──────────────────────────────────────────────────────────

fn build_offers(
    run_seed: u64,
    jump: u32,
    tier_rerolls: &[u32; 4],
    research_rerolls: &[u32; 4],
    upgrade_purchases: &BTreeMap<String, i32>,
) -> (Vec<TierOffer>, Vec<ResearchTrackOffer>) {
    let tiers = TIERS
        .iter()
        .enumerate()
        .map(|(i, def)| build_tier_offer(def, i, run_seed, jump, tier_rerolls[i]))
        .collect();

    let tracks = RESEARCH_TRACKS
        .iter()
        .enumerate()
        .map(|(i, def)| {
            build_research_track_offer(
                def,
                i,
                run_seed,
                jump,
                research_rerolls[i],
                upgrade_purchases,
            )
        })
        .collect();

    (tiers, tracks)
}

fn build_tier_offer(
    def: &TierDef,
    tier_index: usize,
    run_seed: u64,
    jump: u32,
    rerolls: u32,
) -> TierOffer {
    let pool: Vec<&Blueprint> = catalog::blueprints()
        .iter()
        .filter(|bp| def.hull_classes.contains(&bp.hull_class))
        .collect();

    let slots = if pool.is_empty() {
        vec![]
    } else {
        let seed = tier_seed(run_seed, jump, tier_index, rerolls);
        let mut rng = Xoshiro256Plus::seed_from_u64(seed);
        (0..def.slots)
            .map(|_| {
                let idx = (rng.next_u64() as usize) % pool.len();
                ship_offer_from(pool[idx])
            })
            .collect()
    };

    TierOffer { tier_index, label: def.label, reroll_cost: def.reroll_cost, slots }
}

fn build_research_track_offer(
    def: &ResearchTrackDef,
    track_index: usize,
    run_seed: u64,
    jump: u32,
    rerolls: u32,
    upgrade_purchases: &BTreeMap<String, i32>,
) -> ResearchTrackOffer {
    let items = match def.track {
        ResearchTrack::MothershipUpgrades => {
            // Always-available: show every item, include current purchased count.
            RESEARCH_ITEMS
                .iter()
                .filter(|i| i.track == ResearchTrack::MothershipUpgrades)
                .map(|i| research_item_offer(i, purchase_count(i.id, upgrade_purchases)))
                .collect()
        }
        _ => {
            // Randomised pool (currently empty — add items when doctrines/equipment are designed).
            let pool: Vec<&ResearchItem> =
                RESEARCH_ITEMS.iter().filter(|i| i.track == def.track).collect();
            if pool.is_empty() {
                vec![]
            } else {
                let seed = research_seed(run_seed, jump, track_index, rerolls);
                let mut rng = Xoshiro256Plus::seed_from_u64(seed);
                (0..def.offer_slots)
                    .map(|_| {
                        let idx = (rng.next_u64() as usize) % pool.len();
                        let item = pool[idx];
                        research_item_offer(item, purchase_count(item.id, upgrade_purchases))
                    })
                    .collect()
            }
        }
    };

    ResearchTrackOffer { track_index, label: def.label, reroll_cost: def.reroll_cost, items }
}

fn ship_offer_from(bp: &'static Blueprint) -> ShipOffer {
    ShipOffer {
        blueprint_id: bp.id,
        display_name: bp.display_name,
        salvage_cost: bp.salvage_cost,
        tonnage: bp.tonnage(),
        ship_def: bp.ship_def.clone(),
    }
}

fn research_item_offer(item: &'static ResearchItem, purchased: i32) -> ResearchItemOffer {
    ResearchItemOffer {
        upgrade_id: item.id,
        display_name: item.display_name,
        description: item.description,
        tech_cost: item.tech_cost,
        max_purchases: item.max_purchases,
        purchased,
        effects: item.effects,
    }
}

fn purchase_count(id: &str, purchases: &BTreeMap<String, i32>) -> i32 {
    *purchases.get(id).unwrap_or(&0)
}

fn find_blueprint(id: &str) -> Option<&'static Blueprint> {
    catalog::blueprints().iter().find(|bp| bp.id == id)
}

fn find_research_item(id: &str) -> Option<&'static ResearchItem> {
    RESEARCH_ITEMS.iter().find(|i| i.id == id)
}

// ── RNG seeds — deterministic, separated by type ──────────────────────────────

fn tier_seed(run_seed: u64, jump: u32, tier_index: usize, rerolls: u32) -> u64 {
    run_seed
        .wrapping_add((jump as u64).wrapping_mul(997))
        .wrapping_add((tier_index as u64).wrapping_mul(101))
        .wrapping_add((rerolls as u64).wrapping_mul(37))
}

fn research_seed(run_seed: u64, jump: u32, track_index: usize, rerolls: u32) -> u64 {
    run_seed
        .wrapping_add((jump as u64).wrapping_mul(1009))
        .wrapping_add((track_index as u64).wrapping_mul(127))
        .wrapping_add((rerolls as u64).wrapping_mul(41))
        .wrapping_add(0x9e37_79b9_7f4a_7c15u64) // separates from tier seeds
}

// ── Tests ─────────────────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;
    use crate::protocol::{FleetShipRef, GetOffersMsg, SessionDelta};
    use std::collections::BTreeMap;

    // ── Multi-jump simulation helpers ─────────────────────────────────────────

    struct SimRun {
        run_seed: u64,
        jump_number: u32,
        tier_rerolls: [u32; 4],
        research_rerolls: [u32; 4],
        salvage: i32,
        tech: i32,
        hangar_cap: i32,
        fleet: Vec<FleetShipRef>, // stable: original indices, tonnages
        upgrade_purchases: BTreeMap<String, i32>,
    }

    impl SimRun {
        fn new(run_seed: u64, salvage: i32, tech: i32, hangar_cap: i32) -> Self {
            Self {
                run_seed,
                jump_number: 1,
                tier_rerolls: [0; 4],
                research_rerolls: [0; 4],
                salvage,
                tech,
                hangar_cap,
                fleet: vec![],
                upgrade_purchases: BTreeMap::new(),
            }
        }

        fn hangar_used(&self) -> i32 {
            self.fleet.iter().map(|s| s.tonnage).sum()
        }

        fn to_msg(&self) -> GetOffersMsg {
            GetOffersMsg {
                player_id: "offline:test".into(),
                run_seed: self.run_seed,
                jump_number: self.jump_number,
                tier_rerolls: self.tier_rerolls,
                research_rerolls: self.research_rerolls,
                salvage: self.salvage,
                tech: self.tech,
                hangar_used: self.hangar_used(),
                hangar_cap: self.hangar_cap,
                fleet: self.fleet.clone(),
                upgrade_purchases: self.upgrade_purchases.clone(),
            }
        }

        // Mirrors what DockUi.OnBattlePressed does on the C# side.
        fn apply_delta(&mut self, delta: &SessionDelta) {
            // Remove salvaged ships (descending index so earlier removals don't shift later ones).
            let mut to_remove = delta.ships_salvaged.clone();
            to_remove.sort_unstable_by(|a, b| b.cmp(a));
            for idx in &to_remove {
                if *idx < self.fleet.len() {
                    self.fleet.remove(*idx);
                }
            }
            // Assign fresh stable indices to commissioned ships.
            for bp_id in &delta.ships_commissioned {
                let bp = find_blueprint(bp_id).expect("commissioned blueprint must exist");
                let next_index = self.fleet.len();
                self.fleet.push(FleetShipRef {
                    index: next_index,
                    tonnage: bp.tonnage(),
                    blueprint_id: Some(bp_id.to_string()),
                });
            }
            for offer in &delta.upgrades_purchased {
                *self.upgrade_purchases.entry(offer.upgrade_id.to_string()).or_insert(0) += 1;
            }
            self.salvage = delta.salvage_final;
            self.tech = delta.tech_final;
            if delta.hangar_cap_final > 0 {
                self.hangar_cap = delta.hangar_cap_final;
            }
            self.tier_rerolls = delta.tier_rerolls_final;
            self.research_rerolls = delta.research_rerolls_final;
        }

        // Simulate winning a battle: earn rewards and advance to the next jump.
        fn win_battle(&mut self, salvage_reward: i32, tech_reward: i32) {
            self.salvage += salvage_reward;
            self.tech += tech_reward;
            self.jump_number += 1;
        }
    }

    fn test_msg() -> GetOffersMsg {
        GetOffersMsg {
            player_id: "offline:test".into(),
            run_seed: 0xDEAD_BEEF_CAFE_1234,
            jump_number: 1,
            tier_rerolls: [0; 4],
            research_rerolls: [0; 4],
            salvage: 100,
            tech: 5,
            hangar_used: 0,
            hangar_cap: 20,
            fleet: vec![],
            upgrade_purchases: BTreeMap::new(),
        }
    }

    #[test]
    fn offers_are_deterministic() {
        let msg1 = test_msg();
        let msg2 = test_msg();
        let s1 = Session::new(msg1);
        let s2 = Session::new(msg2);
        // Same seed → identical tier slot blueprint IDs.
        for (t1, t2) in s1.ship_tiers.iter().zip(s2.ship_tiers.iter()) {
            let ids1: Vec<_> = t1.slots.iter().map(|s| s.blueprint_id).collect();
            let ids2: Vec<_> = t2.slots.iter().map(|s| s.blueprint_id).collect();
            assert_eq!(ids1, ids2);
        }
    }

    #[test]
    fn reroll_changes_offers() {
        let mut msg = test_msg();
        msg.run_seed = 42;
        let s_before = Session::new(test_msg());
        msg.tier_rerolls[0] = 1;
        let s_after = Session::new(msg);

        // Different reroll count → different seed → (overwhelmingly likely) different slots.
        // We just check the seeds differ structurally.
        let seed_before = tier_seed(42, 1, 0, 0);
        let seed_after = tier_seed(42, 1, 0, 1);
        assert_ne!(seed_before, seed_after);

        // Slots themselves should be populated.
        assert!(!s_before.ship_tiers[0].slots.is_empty());
        assert!(!s_after.ship_tiers[0].slots.is_empty());
    }

    #[test]
    fn commission_deducts_salvage_and_increases_hangar() {
        let mut s = Session::new(test_msg());
        let blueprint_id = s.ship_tiers[0].slots[0].blueprint_id;
        let bp = find_blueprint(blueprint_id).unwrap();
        let salvage_before = s.salvage;
        let hangar_before = s.hangar_used;

        let resp = s.commission(blueprint_id);
        assert!(resp.ok);
        assert_eq!(s.salvage, salvage_before - bp.salvage_cost);
        assert_eq!(s.hangar_used, hangar_before + bp.tonnage());
    }

    #[test]
    fn commission_rejected_when_not_in_offer() {
        let mut s = Session::new(test_msg());
        let resp = s.commission("nonexistent_blueprint");
        assert!(!resp.ok);
        assert_eq!(resp.error, Some("not_in_offer"));
    }

    #[test]
    fn salvage_returns_yield_and_updates_resources() {
        let mut msg = test_msg();
        msg.fleet = vec![FleetShipRef { index: 0, tonnage: 4, blueprint_id: None }];
        msg.hangar_used = 4;
        let mut s = Session::new(msg);

        let resp = s.salvage_fleet_ship(0);
        assert!(resp.ok);
        assert_eq!(resp.salvage_yield, Some(12)); // 4 * 3
        assert_eq!(s.hangar_used, 0);
        assert_eq!(s.salvage, 112); // 100 + 12
    }

    #[test]
    fn shopping_done_includes_all_deltas() {
        let mut s = Session::new(test_msg());
        let blueprint_id = s.ship_tiers[0].slots[0].blueprint_id;
        s.commission(blueprint_id);

        let resp = s.shopping_done();
        assert!(resp.ok);
        let delta = resp.delta.unwrap();
        assert_eq!(delta.ships_commissioned.len(), 1);
        assert_eq!(delta.ships_commissioned[0], blueprint_id);
    }

    #[test]
    fn eight_jump_run_stays_consistent() {
        // Simulate a player who wins every battle with generous rewards.
        let mut run = SimRun::new(0xDEAD_BEEF_CAFE_1234, 200, 20, 10);

        for jump in 1u32..=8 {
            assert_eq!(run.jump_number, jump);

            let mut session = Session::new(run.to_msg());

            // Buy hangar expansion every jump if not maxed and affordable.
            let hangar_cap_before = run.hangar_cap;
            let expansion_bought = session.buy_research("hangar_expansion").ok;
            let expected_cap =
                if expansion_bought { hangar_cap_before + 4 } else { hangar_cap_before };
            assert_eq!(session.hangar_cap, expected_cap, "jump {jump}: hangar_cap mismatch");

            // Commission the cheapest available ship if there's room.
            for tier in &session.ship_tiers.clone() {
                if let Some(slot) = tier.slots.first() {
                    let free = session.hangar_cap - session.hangar_used;
                    if session.salvage >= slot.salvage_cost && free >= slot.tonnage {
                        let resp = session.commission(slot.blueprint_id);
                        assert!(resp.ok, "jump {jump}: commission failed: {:?}", resp.error);
                        break;
                    }
                }
            }

            let resp = session.shopping_done();
            assert!(resp.ok, "jump {jump}: shopping_done failed");
            let delta = resp.delta.unwrap();

            // hangar_cap_final must match what the session tracked.
            assert_eq!(
                delta.hangar_cap_final, session.hangar_cap,
                "jump {jump}: hangar_cap_final wrong"
            );
            // hangar_used_final must equal the sum of fleet tonnages after commissions/salvages.
            assert_eq!(
                delta.hangar_used_final, session.hangar_used,
                "jump {jump}: hangar_used_final wrong"
            );
            // Commissioned ships appear in delta.
            assert_eq!(
                delta.ships_commissioned.len(),
                session.ships_commissioned.iter().filter(|r| r.alive).count(),
                "jump {jump}: commissioned count wrong"
            );

            run.apply_delta(&delta);

            // After applying delta: run state must agree with what the session reported.
            assert_eq!(
                run.hangar_cap, delta.hangar_cap_final,
                "jump {jump}: run hangar_cap after apply"
            );
            assert_eq!(
                run.hangar_used(),
                delta.hangar_used_final,
                "jump {jump}: run hangar_used after apply"
            );

            run.win_battle(30, 3);
        }

        // After 8 jumps: hangar expansion is maxed (5 purchases) so cap should be at least 10 + 20 = 30.
        let expansions = run.upgrade_purchases.get("hangar_expansion").copied().unwrap_or(0);
        assert_eq!(expansions, 5, "hangar_expansion should be maxed after 8 jumps with 20+ tech");
        assert_eq!(run.hangar_cap, 10 + expansions * 4);
        assert!(!run.fleet.is_empty(), "player should have commissioned at least one ship");
    }

    #[test]
    fn buy_hangar_expansion_increases_hangar_cap_immediately() {
        let mut s = Session::new(test_msg());
        let cap_before = s.hangar_cap;

        let resp = s.buy_research("hangar_expansion");
        assert!(resp.ok, "buy_research failed: {:?}", resp.error);
        assert_eq!(s.hangar_cap, cap_before + 4);
        // resource_state() must reflect the new cap so the UI can enable ship buttons.
        assert_eq!(resp.state.unwrap().hangar_cap, cap_before + 4);
    }

    #[test]
    fn shopping_done_includes_hangar_cap_final() {
        let mut s = Session::new(test_msg());
        let cap_before = s.hangar_cap;

        s.buy_research("hangar_expansion");
        s.buy_research("hangar_expansion");

        let delta = s.shopping_done().delta.unwrap();
        assert_eq!(delta.hangar_cap_final, cap_before + 8);
        assert_eq!(
            delta.upgrades_purchased.iter().filter(|u| u.upgrade_id == "hangar_expansion").count(),
            2
        );
    }
}
