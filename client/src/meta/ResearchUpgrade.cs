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
    public required Action<RunState> Apply { get; init; }
}

// TODO (playtesting): tune costs, caps, and magnitudes once the loop is tested.
public static class ResearchCatalog
{
    public static readonly IReadOnlyList<ResearchUpgrade> All =
    [
        new ResearchUpgrade
        {
            Id           = "hangar_expansion",
            DisplayName  = "Hangar Expansion",
            Description  = "+2T hangar capacity",
            TechCost     = 1,
            MaxPurchases = 3,
            Apply        = run => run.HangarCapacity += 2,
        },
        new ResearchUpgrade
        {
            Id           = "reinforced_hull",
            DisplayName  = "Reinforced Hull",
            Description  = "Mothership +100 max HP",
            TechCost     = 2,
            MaxPurchases = 2,
            Apply        = run =>
            {
                run.Fleet.Mothership.MaxHp += 100;
                run.Fleet.Mothership.Hp   += 100;
            },
        },
        new ResearchUpgrade
        {
            Id           = "weapons_overcharge",
            DisplayName  = "Weapons Overcharge",
            Description  = "Mothership weapon +5 damage",
            TechCost     = 2,
            MaxPurchases = 2,
            Apply        = run =>
            {
                if (run.Fleet.Mothership.Weapon is not null)
                    run.Fleet.Mothership.Weapon.Damage += 5;
            },
        },
    ];
}
