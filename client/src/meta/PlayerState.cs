using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Skock.Meta;

public sealed class PlayerState
{
    public int Salvage { get; set; } = 50;
    public int Tech { get; set; } = 0;
    public int HangarCapacity { get; set; } = 10;
    public FleetJsonData Fleet { get; set; } = DefaultFleet();

    public int UsedTonnage => Fleet.Ships.Sum(s => s.HullClass.Tonnage());
    public int FreeTonnage => HangarCapacity - UsedTonnage;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string FleetPath(string projectDir) =>
        Path.GetFullPath(Path.Combine(projectDir, "..", "player_fleet.json"));

    private static string StatePath(string projectDir) =>
        Path.GetFullPath(Path.Combine(projectDir, "..", "player_state.json"));

    public void Save(string projectDir)
    {
        File.WriteAllText(FleetPath(projectDir), JsonSerializer.Serialize(Fleet, JsonOptions));
        File.WriteAllText(StatePath(projectDir), JsonSerializer.Serialize(new SerializedState
        {
            Salvage = Salvage, Tech = Tech, HangarCapacity = HangarCapacity,
        }, JsonOptions));
    }

    public static PlayerState LoadOrDefault(string projectDir)
    {
        var statePath = StatePath(projectDir);
        var fleetPath = FleetPath(projectDir);

        if (!File.Exists(statePath) || !File.Exists(fleetPath))
            return new PlayerState();

        try
        {
            var saved = JsonSerializer.Deserialize<SerializedState>(
                File.ReadAllText(statePath), JsonOptions);
            var fleet = JsonSerializer.Deserialize<FleetJsonData>(
                File.ReadAllText(fleetPath), JsonOptions);

            if (saved is null || fleet is null)
                return new PlayerState();

            return new PlayerState
            {
                Salvage = saved.Salvage,
                Tech = saved.Tech,
                HangarCapacity = saved.HangarCapacity,
                Fleet = fleet,
            };
        }
        catch
        {
            return new PlayerState();
        }
    }

    private static FleetJsonData DefaultFleet() => new()
    {
        Faction = "player",
        AdmiralId = "player",
        Formation = "wedge",
        Mothership = new ShipDefData
        {
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
        },
        Ships =
        [
            BlueprintCatalog.All[0].Instantiate(),
            BlueprintCatalog.All[0].Instantiate(),
            BlueprintCatalog.All[0].Instantiate(),
        ],
    };

    private sealed class SerializedState
    {
        public int Salvage { get; set; }
        public int Tech { get; set; }
        public int HangarCapacity { get; set; }
    }
}
