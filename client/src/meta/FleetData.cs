using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Skock.Meta;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HullClass
{
    Corvette,
    Frigate,
    Destroyer,
    Cruiser,
    Battlecruiser,
    Dreadnought,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Role
{
    Fighter,
    Missile,
    Torpedo,
    Mine,
    PointDefense,
    Artillery,
    Railgun,
}

public static class HullClassExtensions
{
    public static int Tonnage(this HullClass h) =>
        h switch
        {
            HullClass.Corvette => 2,
            HullClass.Frigate => 4,
            HullClass.Destroyer => 6,
            HullClass.Cruiser => 10,
            HullClass.Battlecruiser => 16,
            HullClass.Dreadnought => 24,
            _ => throw new ArgumentOutOfRangeException(nameof(h), h, null),
        };
}

// C# mirror of the Rust FleetJson type — serializes to the format the sim reads.
public sealed class FleetJsonData
{
    [JsonPropertyName("faction")]
    public string Faction { get; set; } = "";

    [JsonPropertyName("admiral_id")]
    public string AdmiralId { get; set; } = "";

    [JsonPropertyName("formation")]
    public string Formation { get; set; } = "wedge";

    [JsonPropertyName("mothership")]
    public ShipDefData Mothership { get; set; } = new();

    [JsonPropertyName("ships")]
    public List<ShipDefData> Ships { get; set; } = [];

    [JsonPropertyName("doctrines")]
    public List<object> Doctrines { get; set; } = [];

    [JsonPropertyName("role_equipment")]
    public List<object> RoleEquipment { get; set; } = [];

    [JsonPropertyName("faction_effects")]
    public List<object> FactionEffects { get; set; } = [];

    [JsonPropertyName("admiral_effects")]
    public List<object> AdmiralEffects { get; set; } = [];
}

public sealed class ShipDefData
{
    // Not serialized — set after construction or load. Guards against salvage of the Mothership.
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsMothership { get; set; }

    [JsonPropertyName("blueprint_drawing_id")]
    public string BlueprintDrawingId { get; set; } = "";

    [JsonPropertyName("hull_class")]
    public HullClass HullClass { get; set; } = HullClass.Corvette;

    [JsonPropertyName("role")]
    public Role Role { get; set; } = Role.Fighter;

    [JsonPropertyName("weight")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Weight { get; set; }

    [JsonPropertyName("hp")]
    public double Hp { get; set; }

    [JsonPropertyName("max_hp")]
    public double MaxHp { get; set; }

    [JsonPropertyName("speed")]
    public double Speed { get; set; }

    [JsonPropertyName("acceleration")]
    public double Acceleration { get; set; }

    [JsonPropertyName("turn_rate")]
    public double TurnRate { get; set; }

    [JsonPropertyName("boid_weights")]
    public BoidWeightsData BoidWeights { get; set; } = new();

    [JsonPropertyName("armor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Armor { get; set; }

    [JsonPropertyName("shield_hp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double ShieldHp { get; set; }

    [JsonPropertyName("shield_max_hp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double ShieldMaxHp { get; set; }

    [JsonPropertyName("shield_recharge_rate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double ShieldRechargeRate { get; set; }

    [JsonPropertyName("weapon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WeaponDefData? Weapon { get; set; }

    [JsonPropertyName("equipment")]
    public List<object> Equipment { get; set; } = [];

    public ShipDefData Clone() =>
        new()
        {
            IsMothership = IsMothership,
            BlueprintDrawingId = BlueprintDrawingId,
            HullClass = HullClass,
            Role = Role,
            Weight = Weight,
            Hp = Hp,
            MaxHp = MaxHp,
            Speed = Speed,
            Acceleration = Acceleration,
            TurnRate = TurnRate,
            BoidWeights = new BoidWeightsData
            {
                Separation = BoidWeights.Separation,
                Cohesion = BoidWeights.Cohesion,
                Alignment = BoidWeights.Alignment,
                SeekEnemy = BoidWeights.SeekEnemy,
                MaintainRange = BoidWeights.MaintainRange,
            },
            Armor = Armor,
            ShieldHp = ShieldHp,
            ShieldMaxHp = ShieldMaxHp,
            ShieldRechargeRate = ShieldRechargeRate,
            Weapon = Weapon is null
                ? null
                : new WeaponDefData
                {
                    Type = Weapon.Type,
                    Damage = Weapon.Damage,
                    Range = Weapon.Range,
                    CooldownTicks = Weapon.CooldownTicks,
                    MissChance = Weapon.MissChance,
                    CritChance = Weapon.CritChance,
                    CritDamage = Weapon.CritDamage,
                },
        };
}

public sealed class BoidWeightsData
{
    [JsonPropertyName("separation")]
    public double Separation { get; set; } = 1.0;

    [JsonPropertyName("cohesion")]
    public double Cohesion { get; set; } = 1.0;

    [JsonPropertyName("alignment")]
    public double Alignment { get; set; } = 1.0;

    [JsonPropertyName("seek_enemy")]
    public double SeekEnemy { get; set; } = 1.0;

    [JsonPropertyName("maintain_range")]
    public double MaintainRange { get; set; } = 1.0;
}

public sealed class WeaponDefData
{
    // WeaponType uses serde rename_all = "snake_case" in Rust.
    [JsonPropertyName("type")]
    public string Type { get; set; } = "hitscan";

    [JsonPropertyName("damage")]
    public double Damage { get; set; }

    [JsonPropertyName("range")]
    public double Range { get; set; }

    [JsonPropertyName("cooldown_ticks")]
    public int CooldownTicks { get; set; }

    [JsonPropertyName("miss_chance")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double MissChance { get; set; }

    [JsonPropertyName("crit_chance")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double CritChance { get; set; }

    [JsonPropertyName("crit_damage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double CritDamage { get; set; }
}

public static class ShipDisplay
{
    public static string NameFor(ShipDefData ship)
    {
        var weight = ship.Weight is null ? "" : ship.Weight + " ";
        return $"{weight}{ship.Role} {ship.HullClass}";
    }
}
