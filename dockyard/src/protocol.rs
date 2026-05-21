use std::collections::BTreeMap;

use serde::{Deserialize, Serialize};
use types::ShipDef;

use crate::catalog::UpgradeEffect;

// ── Inbound ───────────────────────────────────────────────────────────────────

#[derive(Debug, Deserialize)]
#[serde(tag = "action", rename_all = "snake_case")]
pub enum InMessage {
    GetOffers(GetOffersMsg),
    Commission { blueprint_id: String },
    SalvageFleetShip { index: usize },
    RerollTier { tier_index: usize },
    RerollResearch { track_index: usize },
    BuyResearch { upgrade_id: String },
    ShoppingDone,
}

#[derive(Debug, Deserialize)]
pub struct GetOffersMsg {
    /// Stored for future metaprogression DB lookup (offline path: deferred).
    #[allow(dead_code)]
    pub player_id: String,
    pub run_seed: u64,
    pub jump_number: u32,
    pub tier_rerolls: [u32; 4],
    pub research_rerolls: [u32; 4],
    pub salvage: i32,
    pub tech: i32,
    pub hangar_used: i32,
    pub hangar_cap: i32,
    pub fleet: Vec<FleetShipRef>,
    #[serde(default)]
    pub upgrade_purchases: BTreeMap<String, i32>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct FleetShipRef {
    pub index: usize,
    pub tonnage: i32,
    /// Informational — not used for validation.
    #[allow(dead_code)]
    #[serde(default)]
    pub blueprint_id: Option<String>,
}

// ── Outbound ──────────────────────────────────────────────────────────────────

#[derive(Debug, Default, Serialize)]
pub struct OutMessage {
    pub ok: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<&'static str>,
    // GetOffers response
    #[serde(skip_serializing_if = "Option::is_none")]
    pub ship_tiers: Option<Vec<TierOffer>>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub research_tracks: Option<Vec<ResearchTrackOffer>>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub state: Option<ResourceState>,
    // Commission response: ship added
    #[serde(skip_serializing_if = "Option::is_none")]
    pub ship: Option<ShipOffer>,
    // Salvage response: yield
    #[serde(skip_serializing_if = "Option::is_none")]
    pub salvage_yield: Option<i32>,
    // Reroll tier response: updated tier
    #[serde(skip_serializing_if = "Option::is_none")]
    pub tier: Option<TierOffer>,
    // Reroll research response: updated track
    #[serde(skip_serializing_if = "Option::is_none")]
    pub track: Option<ResearchTrackOffer>,
    // ShoppingDone response
    #[serde(skip_serializing_if = "Option::is_none")]
    pub delta: Option<SessionDelta>,
}

impl OutMessage {
    #[allow(dead_code)]
    pub fn ok() -> Self {
        Self { ok: true, ..Default::default() }
    }
    pub fn err(msg: &'static str) -> Self {
        Self { ok: false, error: Some(msg), ..Default::default() }
    }
}

// ── Shared offer types ────────────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize)]
pub struct TierOffer {
    pub tier_index: usize,
    pub label: &'static str,
    pub reroll_cost: i32,
    pub slots: Vec<ShipOffer>,
}

#[derive(Debug, Clone, Serialize)]
pub struct ShipOffer {
    pub blueprint_id: &'static str,
    pub display_name: &'static str,
    pub salvage_cost: i32,
    pub tonnage: i32,
    pub ship_def: ShipDef,
}

#[derive(Debug, Clone, Serialize)]
pub struct ResearchTrackOffer {
    pub track_index: usize,
    pub label: &'static str,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub reroll_cost: Option<i32>,
    pub items: Vec<ResearchItemOffer>,
}

#[derive(Debug, Clone, Serialize)]
pub struct ResearchItemOffer {
    pub upgrade_id: &'static str,
    pub display_name: &'static str,
    pub description: &'static str,
    pub tech_cost: i32,
    pub max_purchases: i32,
    pub purchased: i32,
    pub effects: &'static [UpgradeEffect],
}

#[derive(Debug, Clone, Default, Serialize)]
pub struct ResourceState {
    pub salvage: i32,
    pub tech: i32,
    pub hangar_used: i32,
    pub hangar_cap: i32,
}

#[derive(Debug, Serialize)]
pub struct SessionDelta {
    pub salvage_final: i32,
    pub tech_final: i32,
    pub hangar_used_final: i32,
    pub hangar_cap_final: i32,
    pub tier_rerolls_final: [u32; 4],
    pub research_rerolls_final: [u32; 4],
    pub ships_commissioned: Vec<&'static str>,
    pub ships_salvaged: Vec<usize>,
    pub upgrades_purchased: Vec<ResearchItemOffer>,
}
