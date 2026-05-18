using System.Collections.Generic;

namespace Skock.Meta;

public sealed class Blueprint
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required int SalvageCost { get; init; }
    public required ShipDefData Template { get; init; }

    public int Tonnage => Template.HullClass.Tonnage();

    public ShipDefData Instantiate() => Template.Clone();
}

public static class BlueprintCatalog
{
    public static readonly IReadOnlyList<Blueprint> All =
    [
        // ── Corvettes (2T) ────────────────────────────────────────────────────
        new Blueprint
        {
            Id = "fighter_corvette",
            DisplayName = "Fighter Corvette",
            SalvageCost = 10,
            Template = new ShipDefData
            {
                BlueprintDrawingId = "corvette_a",
                HullClass = HullClass.Corvette,
                Role = Role.Fighter,
                Hp = 60,
                MaxHp = 60,
                Speed = 8,
                Acceleration = 2.0,
                TurnRate = 1.2,
                BoidWeights = new BoidWeightsData
                {
                    Separation = 1.5,
                    Cohesion = 0.4,
                    Alignment = 0.3,
                    SeekEnemy = 2.0,
                    MaintainRange = 1.0,
                },
                Weapon = new WeaponDefData
                {
                    Type = "hitscan",
                    Damage = 10,
                    Range = 80,
                    CooldownTicks = 15,
                },
            },
        },
        new Blueprint
        {
            Id = "missile_corvette",
            DisplayName = "Missile Corvette",
            SalvageCost = 15,
            Template = new ShipDefData
            {
                BlueprintDrawingId = "corvette_a",
                HullClass = HullClass.Corvette,
                Role = Role.Missile,
                Hp = 50,
                MaxHp = 50,
                Speed = 9,
                Acceleration = 2.5,
                TurnRate = 1.5,
                BoidWeights = new BoidWeightsData
                {
                    Separation = 2.0,
                    Cohesion = 0.3,
                    Alignment = 0.2,
                    SeekEnemy = 1.8,
                    MaintainRange = 1.5,
                },
                Weapon = new WeaponDefData
                {
                    Type = "hitscan",
                    Damage = 8,
                    Range = 120,
                    CooldownTicks = 10,
                },
            },
        },
        // ── Frigates (4T) ────────────────────────────────────────────────────
        new Blueprint
        {
            Id = "fighter_frigate",
            DisplayName = "Fighter Frigate",
            SalvageCost = 25,
            Template = new ShipDefData
            {
                BlueprintDrawingId = "frigate_a",
                HullClass = HullClass.Frigate,
                Role = Role.Fighter,
                Hp = 130,
                MaxHp = 130,
                Speed = 5,
                Acceleration = 1.2,
                TurnRate = 0.8,
                BoidWeights = new BoidWeightsData
                {
                    Separation = 1.2,
                    Cohesion = 0.5,
                    Alignment = 0.4,
                    SeekEnemy = 1.5,
                    MaintainRange = 1.2,
                },
                Weapon = new WeaponDefData
                {
                    Type = "hitscan",
                    Damage = 20,
                    Range = 100,
                    CooldownTicks = 20,
                },
            },
        },
        new Blueprint
        {
            Id = "point_defense_frigate",
            DisplayName = "Point Defense Frigate",
            SalvageCost = 30,
            Template = new ShipDefData
            {
                BlueprintDrawingId = "frigate_a",
                HullClass = HullClass.Frigate,
                Role = Role.PointDefense,
                Hp = 110,
                MaxHp = 110,
                Speed = 4.5,
                Acceleration = 1.0,
                TurnRate = 0.7,
                BoidWeights = new BoidWeightsData
                {
                    Separation = 1.8,
                    Cohesion = 0.6,
                    Alignment = 0.5,
                    SeekEnemy = 1.0,
                    MaintainRange = 2.0,
                },
                Weapon = new WeaponDefData
                {
                    Type = "hitscan",
                    Damage = 6,
                    Range = 65,
                    CooldownTicks = 8,
                },
            },
        },
        new Blueprint
        {
            Id = "missile_frigate",
            DisplayName = "Missile Frigate",
            SalvageCost = 35,
            Template = new ShipDefData
            {
                BlueprintDrawingId = "frigate_a",
                HullClass = HullClass.Frigate,
                Role = Role.Missile,
                Hp = 120,
                MaxHp = 120,
                Speed = 4.0,
                Acceleration = 1.0,
                TurnRate = 0.6,
                BoidWeights = new BoidWeightsData
                {
                    Separation = 1.3,
                    Cohesion = 0.4,
                    Alignment = 0.3,
                    SeekEnemy = 1.2,
                    MaintainRange = 2.0,
                },
                Weapon = new WeaponDefData
                {
                    Type = "hitscan",
                    Damage = 15,
                    Range = 140,
                    CooldownTicks = 18,
                },
            },
        },
        // ── Destroyers (6T) ──────────────────────────────────────────────────
        new Blueprint
        {
            Id = "artillery_destroyer",
            DisplayName = "Artillery Destroyer",
            SalvageCost = 45,
            Template = new ShipDefData
            {
                BlueprintDrawingId = "destroyer_a",
                HullClass = HullClass.Destroyer,
                Role = Role.Artillery,
                Hp = 200,
                MaxHp = 200,
                Speed = 3,
                Acceleration = 0.8,
                TurnRate = 0.5,
                BoidWeights = new BoidWeightsData
                {
                    Separation = 1.0,
                    Cohesion = 0.3,
                    Alignment = 0.3,
                    SeekEnemy = 0.8,
                    MaintainRange = 1.5,
                },
                Weapon = new WeaponDefData
                {
                    Type = "hitscan",
                    Damage = 40,
                    Range = 150,
                    CooldownTicks = 45,
                },
            },
        },
        new Blueprint
        {
            Id = "torpedo_destroyer",
            DisplayName = "Torpedo Destroyer",
            SalvageCost = 55,
            Template = new ShipDefData
            {
                BlueprintDrawingId = "destroyer_a",
                HullClass = HullClass.Destroyer,
                Role = Role.Torpedo,
                Hp = 250,
                MaxHp = 250,
                Speed = 2.5,
                Acceleration = 0.6,
                TurnRate = 0.4,
                BoidWeights = new BoidWeightsData
                {
                    Separation = 1.0,
                    Cohesion = 0.2,
                    Alignment = 0.2,
                    SeekEnemy = 0.6,
                    MaintainRange = 2.5,
                },
                Weapon = new WeaponDefData
                {
                    Type = "hitscan",
                    Damage = 90,
                    Range = 180,
                    CooldownTicks = 90,
                },
            },
        },
        new Blueprint
        {
            Id = "railgun_destroyer",
            DisplayName = "Railgun Destroyer",
            SalvageCost = 60,
            Template = new ShipDefData
            {
                BlueprintDrawingId = "destroyer_a",
                HullClass = HullClass.Destroyer,
                Role = Role.Railgun,
                Hp = 150,
                MaxHp = 150,
                Speed = 2.0,
                Acceleration = 0.5,
                TurnRate = 0.3,
                BoidWeights = new BoidWeightsData
                {
                    Separation = 1.2,
                    Cohesion = 0.2,
                    Alignment = 0.2,
                    SeekEnemy = 0.5,
                    MaintainRange = 3.0,
                },
                Weapon = new WeaponDefData
                {
                    Type = "hitscan",
                    Damage = 65,
                    Range = 260,
                    CooldownTicks = 60,
                },
            },
        },
        // ── Cruisers (10T) ───────────────────────────────────────────────────
        new Blueprint
        {
            Id = "fighter_cruiser",
            DisplayName = "Fighter Cruiser",
            SalvageCost = 85,
            Template = new ShipDefData
            {
                BlueprintDrawingId = "cruiser_a",
                HullClass = HullClass.Cruiser,
                Role = Role.Fighter,
                Hp = 350,
                MaxHp = 350,
                Speed = 5.0,
                Acceleration = 1.0,
                TurnRate = 0.7,
                BoidWeights = new BoidWeightsData
                {
                    Separation = 1.0,
                    Cohesion = 0.6,
                    Alignment = 0.4,
                    SeekEnemy = 1.8,
                    MaintainRange = 1.0,
                },
                Weapon = new WeaponDefData
                {
                    Type = "hitscan",
                    Damage = 28,
                    Range = 95,
                    CooldownTicks = 18,
                },
            },
        },
        new Blueprint
        {
            Id = "artillery_cruiser",
            DisplayName = "Artillery Cruiser",
            SalvageCost = 95,
            Template = new ShipDefData
            {
                BlueprintDrawingId = "cruiser_a",
                HullClass = HullClass.Cruiser,
                Role = Role.Artillery,
                Hp = 280,
                MaxHp = 280,
                Speed = 2.5,
                Acceleration = 0.6,
                TurnRate = 0.4,
                BoidWeights = new BoidWeightsData
                {
                    Separation = 1.0,
                    Cohesion = 0.3,
                    Alignment = 0.3,
                    SeekEnemy = 0.7,
                    MaintainRange = 2.0,
                },
                Weapon = new WeaponDefData
                {
                    Type = "hitscan",
                    Damage = 70,
                    Range = 210,
                    CooldownTicks = 55,
                },
            },
        },
        // ── Battlecruisers (16T) ─────────────────────────────────────────────
        new Blueprint
        {
            Id = "assault_battlecruiser",
            DisplayName = "Assault Battlecruiser",
            SalvageCost = 160,
            Template = new ShipDefData
            {
                BlueprintDrawingId = "battlecruiser_a",
                HullClass = HullClass.Battlecruiser,
                Role = Role.Fighter,
                Hp = 600,
                MaxHp = 600,
                Speed = 3.5,
                Acceleration = 0.7,
                TurnRate = 0.4,
                BoidWeights = new BoidWeightsData
                {
                    Separation = 0.8,
                    Cohesion = 0.5,
                    Alignment = 0.4,
                    SeekEnemy = 1.5,
                    MaintainRange = 1.0,
                },
                Weapon = new WeaponDefData
                {
                    Type = "hitscan",
                    Damage = 55,
                    Range = 120,
                    CooldownTicks = 30,
                },
            },
        },
        new Blueprint
        {
            Id = "siege_battlecruiser",
            DisplayName = "Siege Battlecruiser",
            SalvageCost = 180,
            Template = new ShipDefData
            {
                BlueprintDrawingId = "battlecruiser_a",
                HullClass = HullClass.Battlecruiser,
                Role = Role.Artillery,
                Hp = 450,
                MaxHp = 450,
                Speed = 2.0,
                Acceleration = 0.5,
                TurnRate = 0.3,
                BoidWeights = new BoidWeightsData
                {
                    Separation = 1.0,
                    Cohesion = 0.2,
                    Alignment = 0.2,
                    SeekEnemy = 0.5,
                    MaintainRange = 2.5,
                },
                Weapon = new WeaponDefData
                {
                    Type = "hitscan",
                    Damage = 120,
                    Range = 250,
                    CooldownTicks = 75,
                },
            },
        },
    ];
}
