using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Skock.Sim;

namespace Skock.Meta;

public enum RunEndReason
{
    Defeat,
    Victory,
}

// Autoload singleton — in-memory run state + scene transitions.
// All persistence/sync is delegated to IRunStore (_store).
// Swap _store in _Ready() to switch between offline and online modes.
public partial class RunState : Node
{
    public static RunState Instance { get; private set; } = null!;

    public RunEndReason RunEndReason { get; private set; }

    // ── Path resolution ───────────────────────────────────────────────────────

    public string ProjectDir { get; private set; } = "";
    public string SimBinaryPath { get; private set; } = "";
    public string PlayerFleetPath { get; private set; } = "";
    public string FallbackFleetPath { get; private set; } = "";

    // ── Settings ──────────────────────────────────────────────────────────────

    public UserSettings Settings { get; private set; } = new();

    // ── Run state ─────────────────────────────────────────────────────────────

    public ulong RunSeed { get; set; }

    public int Salvage { get; set; } = 50;
    public int Tech { get; set; } = 0;
    public int HangarCapacity { get; set; } = 10;
    public int JumpNumber { get; set; } = 1;
    public int LossCount { get; set; } = 0;
    public string AdmiralId { get; set; } = "";
    public FleetJsonData Fleet { get; set; } = DefaultFleet();
    public int[] TierRerolls { get; internal set; } = new int[4];
    public Dictionary<string, int> UpgradePurchases { get; set; } = new();

    public int UsedTonnage => Fleet.Ships.Sum(s => s.HullClass.Tonnage());
    public int FreeTonnage => HangarCapacity - UsedTonnage;
    public bool IsRunOver => LossCount >= 3;
    public bool HasActiveRun { get; internal set; }
    public bool IsBattleActive { get; set; }

    // Read by LocalRunStore.Save() to write the IsComplete flag.
    internal bool IsRunComplete { get; set; }

    // ── Admiral / faction catalog ─────────────────────────────────────────────

    public IAdmiralStore Catalog { get; private set; } = null!;

    // ── Statistics ────────────────────────────────────────────────────────────

    // Per-run history and lifetime counters behind a swappable seam (see IStatsStore / ADR-0005).
    public IStatsStore Stats { get; private set; } = null!;

    // Captured in BattleRenderer._Ready() just before invoking the sim; consumed by RecordBattleResult.
    public BattleInputs? CurrentBattleInputs { get; set; }

    // Loaded by BattleRenderer before the battle and read by RecordBattleResult to snapshot it.
    public FleetJsonData? CurrentOpponentFleet { get; set; }

    // ── Store ─────────────────────────────────────────────────────────────────

    private IRunStore _store = null!;

    // ── Godot lifecycle ───────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance = this;
        ProjectDir = ProjectSettings.GlobalizePath("res://");
        // In an exported build the sim binary sits next to skock.exe.
        // In the editor it lives in the Rust target directory.
        SimBinaryPath = OS.HasFeature("editor")
            ? Path.GetFullPath(Path.Combine(ProjectDir, "..", "target", "release", "skock-sim.exe"))
            : Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath()) ?? "", "skock-sim.exe");
        PlayerFleetPath = Path.GetFullPath(Path.Combine(ProjectDir, "..", "player_fleet.json"));
        FallbackFleetPath = Path.GetFullPath(
            Path.Combine(ProjectDir, "..", "sim", "test_data", "fleet_a.json")
        );

        // Swap LocalAdmiralStore for ServerAdmiralStore here when online mode is implemented.
        Catalog = new LocalAdmiralStore(Path.Combine(ProjectDir, "data"));
        Catalog.Load();
        // Swap LocalStatsStore for ServerStatsStore here when online mode is implemented.
        Stats = new LocalStatsStore();
        // Swap LocalRunStore for ServerRunStore here when online mode is implemented.
        _store = new LocalRunStore(this);

        Settings = UserSettings.Load(SettingsPath());
        Settings.Apply();
        _store.Load();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public void Save() => _store.Save();

    public void SaveSettings() => Settings.Save(SettingsPath());

    private string SettingsPath() =>
        Path.GetFullPath(Path.Combine(ProjectDir, "..", "user_settings.json"));

    // ── Run lifecycle ─────────────────────────────────────────────────────────

    public void StartRun(Admiral admiral) => _store.StartRun(admiral);

    public void AbandonCurrentRun()
    {
        _store.DeleteSave();
        HasActiveRun = false;
        IsRunComplete = false;
        AdmiralId = "";
        UpgradePurchases = new();
        Stats.Reset();
        CurrentOpponentFleet = null;
    }

    public string GetOpponentFleetPath() =>
        Path.GetFullPath(Path.Combine(ProjectDir, "data", "opponents", $"jump_{JumpNumber}.json"));

    public void SaveAndQuitToMenu()
    {
        _store.Save();
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }

    // ── Dockyard actions ──────────────────────────────────────────────────────

    public ulong GetBattleSeed() => _store.GetBattleSeed();

    public bool CommissionShip(Blueprint bp) => _store.CommissionShip(bp);

    public int SalvageShip(int index) => _store.SalvageShip(index);

    public bool RerollTier(int tierIndex, int cost) => _store.RerollTier(tierIndex, cost);

    public bool BuyUpgrade(string upgradeId) => _store.BuyUpgrade(upgradeId);

    // ── Battle result + scene transitions ─────────────────────────────────────

    public void RecordBattleResult(BattleResult result)
    {
        var playerWon = result.Winner == "fleet_a";

        // Snapshot fleets before any HP mutation.
        var playerSnapshot = Fleet;
        var opponentSnapshot = CurrentOpponentFleet ?? new FleetJsonData();

        // Build kill counts by hull class.
        var enemiesKilled = CountByHullClass(result.FleetBKilled);
        var ownLost = CountByHullClass(result.FleetAKilled);

        Stats.RecordBattle(
            new JumpRecord
            {
                JumpNumber = JumpNumber,
                Won = playerWon,
                DurationTicks = result.Ticks,
                EnemiesKilledByHullClass = enemiesKilled,
                OwnShipsLostByHullClass = ownLost,
                DamageDealt = result.FleetADamageDealt,
                DamageTaken = result.FleetBDamageDealt,
                PlayerFleetSnapshot = playerSnapshot,
                OpponentFleetSnapshot = opponentSnapshot,
                PlayerUpgrades = new Dictionary<string, int>(UpgradePurchases),
                PlayerAdmiralId = AdmiralId,
            },
            CurrentBattleInputs ?? new BattleInputs()
        );

        if (!playerWon)
            LossCount++;

        // Salvage: flat base every battle + flat win bonus, both scaling with JumpNumber.
        // TODO (playtesting): tune multipliers.
        Salvage += JumpNumber * 10;
        if (playerWon)
            Salvage += JumpNumber * 15;

        // Tech on victory scales with JumpNumber: 1 (jumps 1–3), 2 (4–6), 3 (7–8).
        // TODO (playtesting): tune Tech scaling.
        if (playerWon)
            Tech += (JumpNumber + 2) / 3;

        // Restore fleet HP from battle survivors; destroyed ships survive at 1 HP minimum.
        ApplySurvivorHp(result.FleetASurvivors);
        // Auto-heal all ships between jumps.
        HealAllShips();

        if (IsRunOver)
        {
            RunEndReason = RunEndReason.Defeat;
            HasActiveRun = false;
            IsRunComplete = true;
            _store.Save();
            GetTree().ChangeSceneToFile("res://scenes/RunEnd.tscn");
            return;
        }

        if (JumpNumber >= 8)
        {
            // TODO: check flawless run + top-10% score for hidden final encounter.
            RunEndReason = RunEndReason.Victory;
            Stats.RecordRunVictory();
            HasActiveRun = false;
            IsRunComplete = true;
            _store.Save();
            GetTree().ChangeSceneToFile("res://scenes/RunEnd.tscn");
            return;
        }

        Array.Fill(TierRerolls, 0);
        JumpNumber++;
        _store.Save();
        GetTree().ChangeSceneToFile("res://scenes/Dockyard.tscn");
    }

    // ── Battle result helpers ─────────────────────────────────────────────────

    private void ApplySurvivorHp(IReadOnlyList<ShipSurvivor> survivors)
    {
        // Default all fleet ships to 1 HP — ships destroyed mid-battle survive at minimum.
        foreach (var ship in Fleet.Ships)
            ship.Hp = 1.0;

        foreach (var survivor in survivors)
        {
            if (survivor.IsMothership)
            {
                Fleet.Mothership.Hp = survivor.Hp;
                continue;
            }
            // fleet_index from the sim identifies the exact slot; no ambiguity for same-type ships.
            if (survivor.FleetIndex is { } idx && idx >= 0 && idx < Fleet.Ships.Count)
                Fleet.Ships[idx].Hp = survivor.Hp;
        }
    }

    private void HealAllShips()
    {
        Fleet.Mothership.Hp = Fleet.Mothership.MaxHp;
        foreach (var ship in Fleet.Ships)
            ship.Hp = ship.MaxHp;
    }

    // ── Statistics helpers ────────────────────────────────────────────────────

    private static Dictionary<string, int> CountByHullClass(IReadOnlyList<KilledShip> killed)
    {
        var counts = new Dictionary<string, int>();
        foreach (var ship in killed)
        {
            if (ship.IsMothership)
                continue;
            counts.TryGetValue(ship.HullClass, out var n);
            counts[ship.HullClass] = n + 1;
        }
        return counts;
    }

    // ── Defaults ──────────────────────────────────────────────────────────────

    private static FleetJsonData DefaultFleet() =>
        new()
        {
            Faction = "player",
            AdmiralId = "player",
            Formation = "wedge",
            Mothership = new ShipDefData
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
            },
            Ships =
            [
                BlueprintCatalog.All[0].Instantiate(),
                BlueprintCatalog.All[0].Instantiate(),
                BlueprintCatalog.All[0].Instantiate(),
            ],
        };
}
