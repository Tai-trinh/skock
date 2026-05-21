using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Skock.Meta;

// ── Session input ─────────────────────────────────────────────────────────────

public sealed class DockSessionInput
{
    [JsonPropertyName("player_id")]
    public string PlayerId { get; init; } = "";

    [JsonPropertyName("run_seed")]
    public ulong RunSeed { get; init; }

    [JsonPropertyName("jump_number")]
    public int JumpNumber { get; init; }

    [JsonPropertyName("tier_rerolls")]
    public int[] TierRerolls { get; init; } = new int[4];

    [JsonPropertyName("research_rerolls")]
    public int[] ResearchRerolls { get; init; } = new int[4];

    [JsonPropertyName("salvage")]
    public int Salvage { get; init; }

    [JsonPropertyName("tech")]
    public int Tech { get; init; }

    [JsonPropertyName("hangar_used")]
    public int HangarUsed { get; init; }

    [JsonPropertyName("hangar_cap")]
    public int HangarCap { get; init; }

    [JsonPropertyName("fleet")]
    public List<FleetShipRef> Fleet { get; init; } = [];

    [JsonPropertyName("upgrade_purchases")]
    public Dictionary<string, int> UpgradePurchases { get; init; } = new();
}

public sealed class FleetShipRef
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("tonnage")]
    public int Tonnage { get; init; }

    [JsonPropertyName("blueprint_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BlueprintId { get; init; }
}

// ── Offer types ───────────────────────────────────────────────────────────────

public sealed class DockOffersResult
{
    [JsonPropertyName("ship_tiers")]
    public List<ShipTierOffer> ShipTiers { get; init; } = [];

    [JsonPropertyName("research_tracks")]
    public List<ResearchTrackOffer> ResearchTracks { get; init; } = [];

    [JsonPropertyName("state")]
    public DockResourceState State { get; init; } = new();
}

public sealed class ShipTierOffer
{
    [JsonPropertyName("tier_index")]
    public int TierIndex { get; init; }

    [JsonPropertyName("label")]
    public string Label { get; init; } = "";

    [JsonPropertyName("reroll_cost")]
    public int RerollCost { get; init; }

    [JsonPropertyName("slots")]
    public List<ShipSlotOffer> Slots { get; init; } = [];
}

public sealed class ShipSlotOffer
{
    [JsonPropertyName("blueprint_id")]
    public string BlueprintId { get; init; } = "";

    [JsonPropertyName("display_name")]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("salvage_cost")]
    public int SalvageCost { get; init; }

    [JsonPropertyName("tonnage")]
    public int Tonnage { get; init; }

    [JsonPropertyName("ship_def")]
    public ShipDefData ShipDef { get; init; } = new();
}

public sealed class ResearchTrackOffer
{
    [JsonPropertyName("track_index")]
    public int TrackIndex { get; init; }

    [JsonPropertyName("label")]
    public string Label { get; init; } = "";

    [JsonPropertyName("reroll_cost")]
    public int? RerollCost { get; init; }

    [JsonPropertyName("items")]
    public List<ResearchItemOffer> Items { get; init; } = [];
}

public sealed class ResearchItemOffer
{
    [JsonPropertyName("upgrade_id")]
    public string UpgradeId { get; init; } = "";

    [JsonPropertyName("display_name")]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("tech_cost")]
    public int TechCost { get; init; }

    [JsonPropertyName("max_purchases")]
    public int MaxPurchases { get; init; }

    [JsonPropertyName("purchased")]
    public int Purchased { get; init; }

    [JsonPropertyName("effects")]
    public List<UpgradeEffect> Effects { get; init; } = [];
}

// ── Resource state ────────────────────────────────────────────────────────────

public sealed class DockResourceState
{
    [JsonPropertyName("salvage")]
    public int Salvage { get; init; }

    [JsonPropertyName("tech")]
    public int Tech { get; init; }

    [JsonPropertyName("hangar_used")]
    public int HangarUsed { get; init; }

    [JsonPropertyName("hangar_cap")]
    public int HangarCap { get; init; }
}

// ── Action results ────────────────────────────────────────────────────────────

public sealed class DockActionResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("state")]
    public DockResourceState? State { get; init; }
}

public sealed class DockCommissionResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("ship")]
    public ShipSlotOffer? Ship { get; init; }

    [JsonPropertyName("state")]
    public DockResourceState? State { get; init; }
}

public sealed class DockSalvageResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("salvage_yield")]
    public int SalvageYield { get; init; }

    [JsonPropertyName("state")]
    public DockResourceState? State { get; init; }
}

public sealed class DockTierResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("tier")]
    public ShipTierOffer? Tier { get; init; }

    [JsonPropertyName("state")]
    public DockResourceState? State { get; init; }
}

public sealed class DockTrackResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("track")]
    public ResearchTrackOffer? Track { get; init; }

    [JsonPropertyName("state")]
    public DockResourceState? State { get; init; }
}

// ── Local fleet mirror ────────────────────────────────────────────────────────

// Lightweight snapshot of a ship held in the player's fleet during a dockyard session.
// Owned by DockSessionModel; updated on commission and salvage.
public sealed record SessionShip(int CurrentIndex, string Name, int Tonnage);

// ── Session delta (returned by ShoppingDone) ──────────────────────────────────

public sealed class DockSessionDelta
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("delta")]
    public DockDelta? Delta { get; init; }
}

public sealed class DockDelta
{
    [JsonPropertyName("salvage_final")]
    public int SalvageFinal { get; init; }

    [JsonPropertyName("tech_final")]
    public int TechFinal { get; init; }

    [JsonPropertyName("hangar_used_final")]
    public int HangarUsedFinal { get; init; }

    [JsonPropertyName("hangar_cap_final")]
    public int HangarCapFinal { get; init; }

    [JsonPropertyName("tier_rerolls_final")]
    public int[] TierRerollsFinal { get; init; } = new int[4];

    [JsonPropertyName("research_rerolls_final")]
    public int[] ResearchRerollsFinal { get; init; } = new int[4];

    [JsonPropertyName("ships_commissioned")]
    public List<string> ShipsCommissioned { get; init; } = [];

    [JsonPropertyName("ships_salvaged")]
    public List<int> ShipsSalvaged { get; init; } = [];

    [JsonPropertyName("upgrades_purchased")]
    public List<ResearchItemOffer> UpgradesPurchased { get; init; } = [];
}

// ── Upgrade effects ───────────────────────────────────────────────────────────

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(HangarCapEffect), "hangar_cap")]
[JsonDerivedType(typeof(MothershipHpEffect), "mothership_hp")]
[JsonDerivedType(typeof(MothershipWeaponDamageEffect), "mothership_weapon_damage")]
[JsonDerivedType(typeof(MothershipWeaponRangeEffect), "mothership_weapon_range")]
[JsonDerivedType(typeof(FleetHpEffect), "fleet_hp")]
[JsonDerivedType(typeof(FleetSpeedEffect), "fleet_speed")]
[JsonDerivedType(typeof(FleetWeaponDamageEffect), "fleet_weapon_damage")]
[JsonDerivedType(typeof(FleetWeaponRangeEffect), "fleet_weapon_range")]
[JsonDerivedType(typeof(FleetWeaponCooldownEffect), "fleet_weapon_cooldown")]
[JsonDerivedType(typeof(FleetArmorEffect), "fleet_armor")]
[JsonDerivedType(typeof(SalvageEffect), "salvage")]
public abstract class UpgradeEffect { }

public sealed class HangarCapEffect : UpgradeEffect
{
    [JsonPropertyName("delta")]
    public int Delta { get; init; }
}

public sealed class MothershipHpEffect : UpgradeEffect
{
    [JsonPropertyName("delta")]
    public double Delta { get; init; }
}

public sealed class MothershipWeaponDamageEffect : UpgradeEffect
{
    [JsonPropertyName("delta")]
    public double Delta { get; init; }
}

public sealed class MothershipWeaponRangeEffect : UpgradeEffect
{
    [JsonPropertyName("delta")]
    public double Delta { get; init; }
}

public sealed class FleetHpEffect : UpgradeEffect
{
    [JsonPropertyName("delta")]
    public double Delta { get; init; }
}

public sealed class FleetSpeedEffect : UpgradeEffect
{
    [JsonPropertyName("delta")]
    public double Delta { get; init; }
}

public sealed class FleetWeaponDamageEffect : UpgradeEffect
{
    [JsonPropertyName("delta")]
    public double Delta { get; init; }
}

public sealed class FleetWeaponRangeEffect : UpgradeEffect
{
    [JsonPropertyName("delta")]
    public double Delta { get; init; }
}

public sealed class FleetWeaponCooldownEffect : UpgradeEffect
{
    [JsonPropertyName("delta")]
    public int Delta { get; init; }

    [JsonPropertyName("min")]
    public int Min { get; init; }
}

public sealed class FleetArmorEffect : UpgradeEffect
{
    [JsonPropertyName("delta")]
    public double Delta { get; init; }

    [JsonPropertyName("max")]
    public double Max { get; init; }
}

public sealed class SalvageEffect : UpgradeEffect
{
    [JsonPropertyName("delta")]
    public int Delta { get; init; }
}
