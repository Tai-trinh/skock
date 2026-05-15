using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using Skock.Sim;

namespace Skock.Meta;

// Autoload singleton — single source of truth for all in-run state.
// Owns path resolution, save/load, scene transitions, and run-end logic.
public partial class RunState : Node
{
    public static RunState Instance { get; private set; } = null!;

    // ── Path resolution ───────────────────────────────────────────────────────

    public string ProjectDir { get; private set; } = "";
    public string SimBinaryPath { get; private set; } = "";
    public string PlayerFleetPath { get; private set; } = "";
    public string FallbackFleetPath { get; private set; } = "";

    // ── Run state ─────────────────────────────────────────────────────────────

    public int Salvage { get; set; } = 50;
    public int Tech { get; set; } = 0;
    public int HangarCapacity { get; set; } = 10;
    public int JumpNumber { get; set; } = 1;
    public int LossCount { get; set; } = 0;
    public string AdmiralId { get; set; } = "";
    public FleetJsonData Fleet { get; set; } = DefaultFleet();

    public int UsedTonnage => Fleet.Ships.Sum(s => s.HullClass.Tonnage());
    public int FreeTonnage => HangarCapacity - UsedTonnage;
    public bool IsRunOver => LossCount >= 3;

    // ── Godot lifecycle ───────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public override void _Ready()
    {
        Instance = this;
        ProjectDir = ProjectSettings.GlobalizePath("res://");
        SimBinaryPath = Path.GetFullPath(Path.Combine(ProjectDir, "..", "target", "release", "skock-sim.exe"));
        PlayerFleetPath = Path.GetFullPath(Path.Combine(ProjectDir, "..", "player_fleet.json"));
        FallbackFleetPath = Path.GetFullPath(Path.Combine(ProjectDir, "..", "sim", "test_data", "fleet_a.json"));
        LoadOrDefault();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public void Save()
    {
        File.WriteAllText(PlayerFleetPath, JsonSerializer.Serialize(Fleet, JsonOptions));
        File.WriteAllText(StatePath(), JsonSerializer.Serialize(new SerializedState
        {
            Salvage = Salvage,
            Tech = Tech,
            HangarCapacity = HangarCapacity,
            JumpNumber = JumpNumber,
            LossCount = LossCount,
            AdmiralId = AdmiralId,
        }, JsonOptions));
    }

    private void LoadOrDefault()
    {
        var statePath = StatePath();
        if (!File.Exists(statePath) || !File.Exists(PlayerFleetPath))
            return;

        try
        {
            var saved = JsonSerializer.Deserialize<SerializedState>(File.ReadAllText(statePath), JsonOptions);
            var fleet = JsonSerializer.Deserialize<FleetJsonData>(File.ReadAllText(PlayerFleetPath), JsonOptions);
            if (saved is null || fleet is null)
                return;

            Salvage = saved.Salvage;
            Tech = saved.Tech;
            HangarCapacity = saved.HangarCapacity;
            JumpNumber = Math.Max(1, saved.JumpNumber);
            LossCount = saved.LossCount;
            AdmiralId = saved.AdmiralId;
            Fleet = fleet;
        }
        catch
        {
            // corrupt save — keep defaults
        }
    }

    private string StatePath() =>
        Path.GetFullPath(Path.Combine(ProjectDir, "..", "player_state.json"));

    // ── Run lifecycle ─────────────────────────────────────────────────────────

    public void StartRun(Admiral admiral)
    {
        Salvage = admiral.StartingSalvage;
        Tech = admiral.StartingTech;
        HangarCapacity = admiral.StartingHangarCapacity;
        JumpNumber = 1;
        LossCount = 0;
        AdmiralId = admiral.Id;
        Fleet = admiral.StartingFleet;
        Save();
    }

    // ── Battle result + scene transitions ─────────────────────────────────────

    public void RecordBattleResult(BattleResult result)
    {
        var playerWon = result.Winner == "fleet_a";

        if (!playerWon)
            LossCount++;

        // TODO: earn Salvage per enemy ship destroyed (needs kill count from BattleResult).
        // TODO: earn Tech on victory (amount TBD via playtesting).
        if (playerWon)
            Tech += 1;

        Save();

        if (IsRunOver)
        {
            // TODO: transition to RunEnd (defeat).
            GetTree().ChangeSceneToFile("res://scenes/Dockyard.tscn");
            return;
        }

        if (JumpNumber >= 8)
        {
            // TODO: check flawless run + top-10% score for hidden final encounter.
            // TODO: transition to RunEnd (victory).
            GetTree().ChangeSceneToFile("res://scenes/Dockyard.tscn");
            return;
        }

        JumpNumber++;
        GetTree().ChangeSceneToFile("res://scenes/Dockyard.tscn");
    }

    // ── Defaults ──────────────────────────────────────────────────────────────

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
        public int JumpNumber { get; set; }
        public int LossCount { get; set; }
        public string AdmiralId { get; set; } = "";
    }
}
