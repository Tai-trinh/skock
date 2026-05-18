using System;
using System.Collections.Generic;
using System.Globalization;

namespace Skock.Meta;

// Only Id and Apply live here — all display metadata (name, description, cost, cap)
// comes from the dockyard via ResearchItemOffer.
public sealed class ResearchUpgrade
{
    public required string Id { get; init; }
    public required Action<IRunData> Apply { get; init; }
}

public static class ResearchCatalog
{
    public static readonly IReadOnlyList<ResearchUpgrade> All =
    [
        new ResearchUpgrade { Id = "hangar_expansion", Apply = run => run.HangarCapacity += 4 },
        new ResearchUpgrade { Id = "expanded_hangar", Apply = run => run.HangarCapacity += 8 },
        new ResearchUpgrade
        {
            Id = "reinforced_hull",
            Apply = run =>
            {
                run.Fleet.Mothership.MaxHp += 100;
                run.Fleet.Mothership.Hp += 100;
            },
        },
        new ResearchUpgrade
        {
            Id = "weapons_overcharge",
            Apply = run =>
            {
                if (run.Fleet.Mothership.Weapon is not null)
                    run.Fleet.Mothership.Weapon.Damage += 5;
            },
        },
        new ResearchUpgrade
        {
            Id = "mothership_range_boost",
            Apply = run =>
            {
                if (run.Fleet.Mothership.Weapon is not null)
                    run.Fleet.Mothership.Weapon.Range += 40;
            },
        },
        new ResearchUpgrade
        {
            Id = "fleet_armor_plating",
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
            Apply = run =>
            {
                foreach (var ship in run.Fleet.Ships)
                    ship.Speed += 1.0;
            },
        },
        new ResearchUpgrade
        {
            Id = "targeting_array",
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
            Apply = run =>
            {
                foreach (var ship in run.Fleet.Ships)
                    if (ship.Weapon is not null)
                        ship.Weapon.CooldownTicks = Math.Max(5, ship.Weapon.CooldownTicks - 4);
            },
        },
        new ResearchUpgrade { Id = "salvage_cache", Apply = run => run.Salvage += 30 },
        new ResearchUpgrade
        {
            Id = "fleet_damage_boost",
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
            Apply = run =>
            {
                foreach (var ship in run.Fleet.Ships)
                    ship.Armor = Math.Min(0.9, ship.Armor + 0.10);
            },
        },
    ];

    // Converts a snake_case upgrade ID to a Title Case label for historical display.
    public static string LabelFor(string id) =>
        CultureInfo.CurrentCulture.TextInfo.ToTitleCase(id.Replace('_', ' '));
}
