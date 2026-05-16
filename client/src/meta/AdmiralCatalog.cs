using System;
using System.Collections.Generic;
using System.Linq;

namespace Skock.Meta;

public sealed class AdmiralShipEffect
{
    public required Func<ShipDefData, bool> Matches { get; init; }
    public required Action<ShipDefData> Apply { get; init; }
}

public sealed class Admiral
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string BonusText { get; init; }
    public required int StartingSalvage { get; init; }
    public required int StartingTech { get; init; }
    public required int StartingHangarCapacity { get; init; }
    public required FleetJsonData StartingFleet { get; init; }
    public required IReadOnlyList<AdmiralShipEffect> ShipEffects { get; init; }
}

public static class AdmiralCatalog
{
    public static readonly IReadOnlyList<Admiral> All =
    [
        new Admiral
        {
            Id = "kira",
            Name = "Admiral Kira",
            BonusText = "All Fighters +15% speed.",
            StartingSalvage = 60,
            StartingTech = 0,
            StartingHangarCapacity = 10,
            StartingFleet = MakeFleet(
                "kira",
                BlueprintCatalog.All[0].Instantiate(), // Fighter Corvette
                BlueprintCatalog.All[0].Instantiate()
            ), // Fighter Corvette
            ShipEffects =
            [
                new AdmiralShipEffect
                {
                    Matches = s => s.Role == Role.Fighter,
                    Apply = s => s.Speed *= 1.15,
                },
            ],
        },
        new Admiral
        {
            Id = "voss",
            Name = "Admiral Voss",
            BonusText = "Artillery weapons +10% damage.",
            StartingSalvage = 30,
            StartingTech = 1,
            StartingHangarCapacity = 10,
            StartingFleet = MakeFleet("voss", BlueprintCatalog.All[2].Instantiate()), // Artillery Destroyer
            ShipEffects =
            [
                new AdmiralShipEffect
                {
                    Matches = s => s.Role == Role.Artillery && s.Weapon is not null,
                    Apply = s => s.Weapon!.Damage *= 1.10,
                },
            ],
        },
        new Admiral
        {
            Id = "shen",
            Name = "Admiral Shen",
            BonusText = "Hangar capacity +4T. Start with extra space for heavier ships.",
            StartingSalvage = 40,
            StartingTech = 0,
            StartingHangarCapacity = 14,
            StartingFleet = MakeFleet("shen", BlueprintCatalog.All[1].Instantiate()), // Fighter Frigate
            ShipEffects = [],
        },
    ];

    // Returns display name for any admiral ID, including NPC opponents.
    public static string NameFor(string admiralId) =>
        All.FirstOrDefault(a => a.Id == admiralId)?.Name
        ?? admiralId switch
        {
            "bob" => "Admiral Bob",
            "none" or "" => "Unknown",
            _ => admiralId,
        };

    private static FleetJsonData MakeFleet(string admiralId, params ShipDefData[] ships) =>
        new()
        {
            Faction = "player",
            AdmiralId = admiralId,
            Formation = "wedge",
            Mothership = DefaultMothership(),
            Ships = [.. ships],
        };

    private static ShipDefData DefaultMothership() =>
        new()
        {
            IsMothership = true,
            BlueprintDrawingId = "mothership_a",
            HullClass = HullClass.Dreadnought,
            Role = Role.Artillery,
            Hp = 500,
            MaxHp = 500,
            Speed = 1,
            Acceleration = 0.3,
            TurnRate = 0.1,
            BoidWeights = new BoidWeightsData
            {
                Separation = 2.0,
                Cohesion = 0.0,
                Alignment = 0.0,
                SeekEnemy = 0.2,
                MaintainRange = 1.0,
            },
            Weapon = new WeaponDefData
            {
                Type = "hitscan",
                Damage = 25,
                Range = 200,
                CooldownTicks = 45,
            },
        };
}
