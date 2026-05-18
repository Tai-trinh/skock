using System;
using System.Collections.Generic;

namespace Skock.Meta;

public sealed class ResearchUpgrade
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required int TechCost { get; init; }
    public required int MaxPurchases { get; init; }
    public required Action<IRunData> Apply { get; init; }
}

// TODO (playtesting): tune costs, caps, and magnitudes once the loop is tested.
public static class ResearchCatalog
{
    public static readonly IReadOnlyList<ResearchUpgrade> All =
    [
        // ── Mothership Upgrades ───────────────────────────────────────────────
        new ResearchUpgrade
        {
            Id = "hangar_expansion",
            DisplayName = "Hangar Expansion",
            Description = "+4T hangar capacity",
            TechCost = 1,
            MaxPurchases = 5,
            Apply = run => run.HangarCapacity += 4,
        },
        new ResearchUpgrade
        {
            Id = "expanded_hangar",
            DisplayName = "Expanded Hangar",
            Description = "+8T hangar capacity",
            TechCost = 2,
            MaxPurchases = 3,
            Apply = run => run.HangarCapacity += 8,
        },
        new ResearchUpgrade
        {
            Id = "reinforced_hull",
            DisplayName = "Reinforced Hull",
            Description = "Mothership +100 max HP",
            TechCost = 2,
            MaxPurchases = 3,
            Apply = run =>
            {
                run.Fleet.Mothership.MaxHp += 100;
                run.Fleet.Mothership.Hp += 100;
            },
        },
        new ResearchUpgrade
        {
            Id = "weapons_overcharge",
            DisplayName = "Weapons Overcharge",
            Description = "Mothership weapon +5 damage",
            TechCost = 2,
            MaxPurchases = 3,
            Apply = run =>
            {
                if (run.Fleet.Mothership.Weapon is not null)
                    run.Fleet.Mothership.Weapon.Damage += 5;
            },
        },
        new ResearchUpgrade
        {
            Id = "mothership_range_boost",
            DisplayName = "Long-Range Sensors",
            Description = "Mothership weapon range +40",
            TechCost = 2,
            MaxPurchases = 2,
            Apply = run =>
            {
                if (run.Fleet.Mothership.Weapon is not null)
                    run.Fleet.Mothership.Weapon.Range += 40;
            },
        },
        // ── Doctrines ─────────────────────────────────────────────────────────
        new ResearchUpgrade
        {
            Id = "fleet_armor_plating",
            DisplayName = "Fleet Armor Plating",
            Description = "All fleet ships +20 max HP",
            TechCost = 2,
            MaxPurchases = 3,
            Apply = run =>
            {
                foreach (var ship in run.Fleet.Ships)
                {
                    ship.MaxHp += 20;
                    ship.Hp += 20;
                }
            },
        },
        new ResearchUpgrade
        {
            Id = "advanced_propulsion",
            DisplayName = "Advanced Propulsion",
            Description = "All fleet ships +1 speed",
            TechCost = 2,
            MaxPurchases = 2,
            Apply = run =>
            {
                foreach (var ship in run.Fleet.Ships)
                    ship.Speed += 1.0;
            },
        },
        new ResearchUpgrade
        {
            Id = "targeting_array",
            DisplayName = "Targeting Array",
            Description = "All fleet ship weapons +20 range",
            TechCost = 2,
            MaxPurchases = 3,
            Apply = run =>
            {
                foreach (var ship in run.Fleet.Ships)
                    if (ship.Weapon is not null)
                        ship.Weapon.Range += 20;
            },
        },
        new ResearchUpgrade
        {
            Id = "rapid_fire_capacitors",
            DisplayName = "Rapid-Fire Capacitors",
            Description = "All fleet ship weapon cooldown -4 ticks (min 5)",
            TechCost = 3,
            MaxPurchases = 2,
            Apply = run =>
            {
                foreach (var ship in run.Fleet.Ships)
                    if (ship.Weapon is not null)
                        ship.Weapon.CooldownTicks = Math.Max(5, ship.Weapon.CooldownTicks - 4);
            },
        },
        // ── Role Equipment ────────────────────────────────────────────────────
        new ResearchUpgrade
        {
            Id = "salvage_cache",
            DisplayName = "Salvage Cache",
            Description = "+30 salvage immediately",
            TechCost = 1,
            MaxPurchases = 3,
            Apply = run => run.Salvage += 30,
        },
        new ResearchUpgrade
        {
            Id = "fleet_damage_boost",
            DisplayName = "Weapons Upgrade",
            Description = "All fleet ship weapons +5 damage",
            TechCost = 3,
            MaxPurchases = 2,
            Apply = run =>
            {
                foreach (var ship in run.Fleet.Ships)
                    if (ship.Weapon is not null)
                        ship.Weapon.Damage += 5;
            },
        },
        new ResearchUpgrade
        {
            Id = "fleet_armor",
            DisplayName = "Composite Armor",
            Description = "All fleet ships gain 10% armor (damage reduction)",
            TechCost = 3,
            MaxPurchases = 1,
            Apply = run =>
            {
                foreach (var ship in run.Fleet.Ships)
                    ship.Armor = Math.Min(0.9, ship.Armor + 0.10);
            },
        },
    ];
}
