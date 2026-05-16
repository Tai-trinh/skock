using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Skock.Sim;

namespace Skock.Meta;

public enum RunEndReason { Defeat, Victory }

// Autoload singleton — in-memory run state + scene transitions.
// All persistence/sync is delegated to IRunStore (_store).
// Swap _store in _Ready() to switch between offline and online modes.
public partial class RunState : Node
{
    public static RunState Instance { get; private set; } = null!;

    public RunEndReason RunEndReason { get; private set; }

    // ── Path resolution ───────────────────────────────────────────────────────

    public string ProjectDir      { get; private set; } = "";
    public string SimBinaryPath   { get; private set; } = "";
    public string PlayerFleetPath { get; private set; } = "";
    public string FallbackFleetPath { get; private set; } = "";

    // ── Settings ──────────────────────────────────────────────────────────────

    public UserSettings Settings { get; private set; } = new();

    // ── Run state ─────────────────────────────────────────────────────────────

    public int          Salvage        { get; set; } = 50;
    public int          Tech           { get; set; } = 0;
    public int          HangarCapacity { get; set; } = 10;
    public int          JumpNumber     { get; set; } = 1;
    public int          LossCount      { get; set; } = 0;
    public string       AdmiralId      { get; set; } = "";
    public FleetJsonData Fleet         { get; set; } = DefaultFleet();
    public int[]        TierRerolls    { get; internal set; } = new int[4];

    public int  UsedTonnage  => Fleet.Ships.Sum(s => s.HullClass.Tonnage());
    public int  FreeTonnage  => HangarCapacity - UsedTonnage;
    public bool IsRunOver    => LossCount >= 3;
    public bool HasActiveRun { get; internal set; }
    public bool IsBattleActive { get; set; }

    // Read by LocalRunStore.Save() to write the IsComplete flag.
    internal bool IsRunComplete { get; set; }

    // ── Store ─────────────────────────────────────────────────────────────────

    private IRunStore _store = null!;

    // ── Godot lifecycle ───────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance = this;
        ProjectDir      = ProjectSettings.GlobalizePath("res://");
        SimBinaryPath   = Path.GetFullPath(Path.Combine(ProjectDir, "..", "target", "release", "skock-sim.exe"));
        PlayerFleetPath = Path.GetFullPath(Path.Combine(ProjectDir, "..", "player_fleet.json"));
        FallbackFleetPath = Path.GetFullPath(Path.Combine(ProjectDir, "..", "sim", "test_data", "fleet_a.json"));

        // Swap LocalRunStore for ServerRunStore here when online mode is implemented.
        _store = new LocalRunStore(this);

        Settings = UserSettings.Load(SettingsPath());
        Settings.Apply();
        _store.Load();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public void Save()         => _store.Save();
    public void SaveSettings() => Settings.Save(SettingsPath());

    private string SettingsPath() =>
        Path.GetFullPath(Path.Combine(ProjectDir, "..", "user_settings.json"));

    // ── Run lifecycle ─────────────────────────────────────────────────────────

    public void StartRun(Admiral admiral) => _store.StartRun(admiral);

    public void AbandonCurrentRun()
    {
        _store.DeleteSave();
        HasActiveRun   = false;
        IsRunComplete  = false;
        AdmiralId      = "";
    }

    public void SaveAndQuitToMenu()
    {
        _store.Save();
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }

    // ── Dockyard actions ──────────────────────────────────────────────────────

    public bool CommissionShip(Blueprint bp)    => _store.CommissionShip(bp);
    public int  SalvageShip(int index)          => _store.SalvageShip(index);
    public bool RerollTier(int tierIndex, int cost) => _store.RerollTier(tierIndex, cost);

    // ── Battle result + scene transitions ─────────────────────────────────────

    public void RecordBattleResult(BattleResult result, int enemyKillCount)
    {
        var playerWon = result.Winner == "fleet_a";
        if (!playerWon) LossCount++;
        // TODO (playtesting): tune Tech per victory and salvage per kill.
        if (playerWon) Tech += 1;

        // Restore fleet HP from battle survivors; destroyed ships survive at 1 HP minimum.
        ApplySurvivorHp(result.FleetASurvivors);
        // Auto-heal all ships between jumps. TODO: replace with per-ship heal choice in DockUi (skip-heal mechanic).
        HealAllShips();

        // Earn salvage for every enemy non-mothership ship destroyed (10 per kill — tune via playtesting).
        Salvage += enemyKillCount * 10;

        if (IsRunOver)
        {
            RunEndReason   = RunEndReason.Defeat;
            HasActiveRun   = false;
            IsRunComplete  = true;
            _store.Save();
            GetTree().ChangeSceneToFile("res://scenes/RunEnd.tscn");
            return;
        }

        if (JumpNumber >= 8)
        {
            // TODO: check flawless run + top-10% score for hidden final encounter.
            RunEndReason   = RunEndReason.Victory;
            HasActiveRun   = false;
            IsRunComplete  = true;
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
        // Build per-blueprint queues (BTreeMap order from sim ≈ spawn order for same hull class).
        var hpQueues = new Dictionary<string, Queue<float>>();
        foreach (var s in survivors)
        {
            if (s.IsMothership) continue;
            if (!hpQueues.TryGetValue(s.BlueprintDrawingId, out var q))
                hpQueues[s.BlueprintDrawingId] = q = new Queue<float>();
            q.Enqueue(s.Hp);
        }
        foreach (var ship in Fleet.Ships)
        {
            if (hpQueues.TryGetValue(ship.BlueprintDrawingId, out var q) && q.Count > 0)
                ship.Hp = q.Dequeue();
            else
                ship.Hp = 1.0; // destroyed mid-battle — minimum survival HP
        }
    }

    private void HealAllShips()
    {
        Fleet.Mothership.Hp = Fleet.Mothership.MaxHp;
        foreach (var ship in Fleet.Ships)
            ship.Hp = ship.MaxHp;
    }

    // ── Defaults ──────────────────────────────────────────────────────────────

    private static FleetJsonData DefaultFleet() => new()
    {
        Faction    = "player",
        AdmiralId  = "player",
        Formation  = "wedge",
        Mothership = new ShipDefData
        {
            IsMothership = true,
            BlueprintDrawingId = "mothership_a",
            HullClass = HullClass.Dreadnought,
            Role      = Role.Artillery,
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
}
