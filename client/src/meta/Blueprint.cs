using System.Collections.Generic;

namespace Skock.Meta;

public sealed class Blueprint
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required int SalvageCost { get; init; }
    public required int Tonnage { get; init; }
    public required ShipDefData Template { get; init; }

    public ShipDefData Instantiate() => Template.Clone();
}

public static class BlueprintCatalog
{
    public static readonly IReadOnlyList<Blueprint> All =
    [
        new Blueprint
        {
            Id = "fighter_corvette",
            DisplayName = "Fighter Corvette",
            SalvageCost = 10,
            Tonnage = 2,
            Template = new ShipDefData
            {
                BlueprintDrawingId = "fighter_a",
                HullClass = "Corvette",
                Role = "Fighter",
                Hp = 60, MaxHp = 60,
                Speed = 8, Acceleration = 2.0, TurnRate = 1.2,
                BoidWeights = new BoidWeightsData
                {
                    Separation = 1.5, Cohesion = 0.4, Alignment = 0.3,
                    SeekEnemy = 2.0, MaintainRange = 1.0,
                },
                Weapon = new WeaponDefData
                {
                    Type = "hitscan", Damage = 10, Range = 80, CooldownTicks = 15,
                },
            },
        },
        new Blueprint
        {
            Id = "fighter_frigate",
            DisplayName = "Fighter Frigate",
            SalvageCost = 25,
            Tonnage = 4,
            Template = new ShipDefData
            {
                BlueprintDrawingId = "fighter_a",
                HullClass = "Frigate",
                Role = "Fighter",
                Hp = 130, MaxHp = 130,
                Speed = 5, Acceleration = 1.2, TurnRate = 0.8,
                BoidWeights = new BoidWeightsData
                {
                    Separation = 1.2, Cohesion = 0.5, Alignment = 0.4,
                    SeekEnemy = 1.5, MaintainRange = 1.2,
                },
                Weapon = new WeaponDefData
                {
                    Type = "hitscan", Damage = 20, Range = 100, CooldownTicks = 20,
                },
            },
        },
        new Blueprint
        {
            Id = "artillery_destroyer",
            DisplayName = "Artillery Destroyer",
            SalvageCost = 45,
            Tonnage = 6,
            Template = new ShipDefData
            {
                BlueprintDrawingId = "fighter_a",
                HullClass = "Destroyer",
                Role = "Artillery",
                Hp = 200, MaxHp = 200,
                Speed = 3, Acceleration = 0.8, TurnRate = 0.5,
                BoidWeights = new BoidWeightsData
                {
                    Separation = 1.0, Cohesion = 0.3, Alignment = 0.3,
                    SeekEnemy = 0.8, MaintainRange = 1.5,
                },
                Weapon = new WeaponDefData
                {
                    Type = "hitscan", Damage = 40, Range = 150, CooldownTicks = 45,
                },
            },
        },
    ];
}
