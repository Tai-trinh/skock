using System.Collections.Generic;

namespace Skock.Meta;

public sealed class Admiral
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string BonusText { get; init; }
    public required int StartingSalvage { get; init; }
    public required int StartingTech { get; init; }
    public required int StartingHangarCapacity { get; init; }
    public required FleetJsonData StartingFleet { get; init; }
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
            StartingFleet = MakeFleet("kira",
                BlueprintCatalog.All[0].Instantiate(),  // Fighter Corvette
                BlueprintCatalog.All[0].Instantiate()), // Fighter Corvette
        },
        new Admiral
        {
            Id = "voss",
            Name = "Admiral Voss",
            BonusText = "Beam weapons +10% damage.",
            StartingSalvage = 30,
            StartingTech = 1,
            StartingHangarCapacity = 10,
            StartingFleet = MakeFleet("voss",
                BlueprintCatalog.All[2].Instantiate()), // Artillery Destroyer
        },
        new Admiral
        {
            Id = "shen",
            Name = "Admiral Shen",
            BonusText = "Hangar capacity +4T. Start with extra space for heavier ships.",
            StartingSalvage = 40,
            StartingTech = 0,
            StartingHangarCapacity = 14,
            StartingFleet = MakeFleet("shen",
                BlueprintCatalog.All[1].Instantiate()), // Fighter Frigate
        },
    ];

    private static FleetJsonData MakeFleet(string admiralId, params ShipDefData[] ships) => new()
    {
        Faction = "player",
        AdmiralId = admiralId,
        Formation = "wedge",
        Mothership = DefaultMothership(),
        Ships = [.. ships],
    };

    private static ShipDefData DefaultMothership() => new()
    {
        IsMothership = true,
        BlueprintDrawingId = "mothership_a",
        HullClass = HullClass.Dreadnought,
        Role = Role.Artillery,
        Hp = 500, MaxHp = 500,
        Speed = 1, Acceleration = 0.3, TurnRate = 0.1,
        BoidWeights = new BoidWeightsData
        {
            Separation = 2.0, Cohesion = 0.0, Alignment = 0.0,
            SeekEnemy = 0.2, MaintainRange = 1.0,
        },
        Weapon = new WeaponDefData
        {
            Type = "hitscan", Damage = 25, Range = 200, CooldownTicks = 45,
        },
    };
}
