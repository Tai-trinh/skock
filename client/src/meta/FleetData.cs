using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Skock.Meta;

// C# mirror of the Rust FleetJson type — serializes to the format the sim reads.
public sealed class FleetJsonData
{
    [JsonPropertyName("faction")] public string Faction { get; set; } = "";
    [JsonPropertyName("admiral_id")] public string AdmiralId { get; set; } = "";
    [JsonPropertyName("formation")] public string Formation { get; set; } = "wedge";
    [JsonPropertyName("mothership")] public ShipDefData Mothership { get; set; } = new();
    [JsonPropertyName("ships")] public List<ShipDefData> Ships { get; set; } = [];
    [JsonPropertyName("doctrines")] public List<object> Doctrines { get; set; } = [];
    [JsonPropertyName("role_equipment")] public List<object> RoleEquipment { get; set; } = [];
    [JsonPropertyName("faction_effects")] public List<object> FactionEffects { get; set; } = [];
    [JsonPropertyName("admiral_effects")] public List<object> AdmiralEffects { get; set; } = [];
}

public sealed class ShipDefData
{
    [JsonPropertyName("blueprint_drawing_id")] public string BlueprintDrawingId { get; set; } = "";
    // HullClass and Role are PascalCase in Rust (no serde rename_all).
    [JsonPropertyName("hull_class")] public string HullClass { get; set; } = "Corvette";
    [JsonPropertyName("role")] public string Role { get; set; } = "Fighter";
    [JsonPropertyName("weight")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Weight { get; set; }
    [JsonPropertyName("hp")] public double Hp { get; set; }
    [JsonPropertyName("max_hp")] public double MaxHp { get; set; }
    [JsonPropertyName("speed")] public double Speed { get; set; }
    [JsonPropertyName("acceleration")] public double Acceleration { get; set; }
    [JsonPropertyName("turn_rate")] public double TurnRate { get; set; }
    [JsonPropertyName("boid_weights")] public BoidWeightsData BoidWeights { get; set; } = new();
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
    [JsonPropertyName("equipment")] public List<object> Equipment { get; set; } = [];

    public ShipDefData Clone() => new()
    {
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
        Weapon = Weapon is null ? null : new WeaponDefData
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
    [JsonPropertyName("separation")] public double Separation { get; set; } = 1.0;
    [JsonPropertyName("cohesion")] public double Cohesion { get; set; } = 1.0;
    [JsonPropertyName("alignment")] public double Alignment { get; set; } = 1.0;
    [JsonPropertyName("seek_enemy")] public double SeekEnemy { get; set; } = 1.0;
    [JsonPropertyName("maintain_range")] public double MaintainRange { get; set; } = 1.0;
}

public sealed class WeaponDefData
{
    // WeaponType uses serde rename_all = "snake_case" in Rust.
    [JsonPropertyName("type")] public string Type { get; set; } = "hitscan";
    [JsonPropertyName("damage")] public double Damage { get; set; }
    [JsonPropertyName("range")] public double Range { get; set; }
    [JsonPropertyName("cooldown_ticks")] public int CooldownTicks { get; set; }
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
    public static int TonnageFor(string hullClass) => hullClass switch
    {
        "Corvette" => 2,
        "Frigate" => 4,
        "Destroyer" => 6,
        "Cruiser" => 10,
        "Battlecruiser" => 16,
        "Dreadnought" => 24,
        _ => 2,
    };

    public static string NameFor(ShipDefData ship)
    {
        var weight = ship.Weight is null ? "" : ship.Weight + " ";
        return $"{weight}{ship.Role} {ship.HullClass}";
    }
}
