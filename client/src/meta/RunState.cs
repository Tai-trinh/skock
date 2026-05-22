using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
// Dockyard-phase logic is delegated to IDockyard (created per dockyard visit).
// Swap either in _Ready() to switch between offline and online modes.
public partial class RunState : Node, IRunData
{
    public static RunState Instance { get; private set; } = null!;

    public RunEndReason RunEndReason { get; private set; }

    // ── Path resolution ───────────────────────────────────────────────────────

    public string ProjectDir { get; private set; } = "";
    public string SimBinaryPath { get; private set; } = "";
    public string DockBinaryPath { get; private set; } = "";
    public string AdmiralBinaryPath { get; private set; } = "";
    public string PlayerFleetPath { get; private set; } = "";
    public string FallbackFleetPath { get; private set; } = "";

    // ── Settings ──────────────────────────────────────────────────────────────

    public UserSettings Settings { get; private set; } = new();

    // ── Identity ──────────────────────────────────────────────────────────────

    public string PlayerId { get; set; } = $"offline:{System.Guid.NewGuid()}";

    // ── Run state ─────────────────────────────────────────────────────────────

    public ulong RunSeed { get; set; }

    public int Salvage { get; set; } = 50;
    public int Tech { get; set; } = 0;
    public int HangarCapacity { get; set; } = 10;
    public int JumpNumber { get; set; } = 1;
    public int LossCount { get; set; } = 0;
    public string AdmiralId { get; set; } = "";
    public string AdmiralName { get; set; } = "";
    public string AdmiralBonusText { get; set; } = "";
    public FleetJsonData Fleet { get; set; } = DefaultFleet();
    public int[] TierRerolls { get; set; } = new int[4];
    public int[] ResearchRerolls { get; set; } = new int[4];
    public Dictionary<string, int> UpgradePurchases { get; set; } = new();

    public int UsedTonnage => Fleet.Ships.Sum(s => s.HullClass.Tonnage());
    public int FreeTonnage => HangarCapacity - UsedTonnage;
    public bool IsRunOver => LossCount >= 3;
    public bool HasActiveRun { get; set; }
    public bool IsBattleActive { get; private set; }
    public bool IsRunComplete { get; set; }

    // Held for the duration of one battle; cleared when the battle ends.
    private BattleInputs? _currentBattleInputs;
    public FleetJsonData? CurrentOpponentFleet { get; private set; }

    // ── Admiral selection ─────────────────────────────────────────────────────

    public IAdmiralSelection AdmiralSelection { get; private set; } = null!;

    // ── Encounter source ──────────────────────────────────────────────────────

    private IEncounterSource _encounterSource = null!;

    // ── Statistics ────────────────────────────────────────────────────────────

    public IStatsStore Stats { get; private set; } = null!;

    // ── Store ─────────────────────────────────────────────────────────────────

    private IRunStore _store = null!;

    // ── Godot lifecycle ───────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance = this;
        ProjectDir = ProjectSettings.GlobalizePath("res://");

        var binDir = OS.HasFeature("editor")
            ? Path.GetFullPath(Path.Combine(ProjectDir, "..", "target", "release"))
            : Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath()) ?? "", "bin");

        SimBinaryPath = Path.Combine(binDir, "skock-sim.exe");
        DockBinaryPath = Path.Combine(binDir, "skock-dockyard.exe");
        AdmiralBinaryPath = Path.Combine(binDir, "skock-admiral.exe");
        var encounterBinaryPath = Path.Combine(binDir, "skock-encounter.exe");
        PlayerFleetPath = Path.GetFullPath(Path.Combine(ProjectDir, "..", "player_fleet.json"));
        FallbackFleetPath = Path.GetFullPath(
            Path.Combine(ProjectDir, "..", "sim", "test_data", "fleet_a.json")
        );

        // Swap LocalAdmiralAdapter for ServerAdmiralAdapter here when online mode is implemented.
        AdmiralSelection = new LocalAdmiralAdapter(AdmiralBinaryPath);
        // Swap LocalEncounterAdapter for ServerEncounterAdapter here when online mode is implemented.
        _encounterSource = new LocalEncounterAdapter(encounterBinaryPath);
        // Swap LocalStatsStore for ServerStatsStore here when online mode is implemented.
        Stats = new LocalStatsStore();
        // Swap LocalRunStore for ServerRunStore here when online mode is implemented.
        var statePath = Path.GetFullPath(Path.Combine(ProjectDir, "..", "player_state.json"));
        _store = new LocalRunStore(statePath, PlayerFleetPath);

        Settings = UserSettings.Load(SettingsPath());
        Settings.Apply();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var snapshot = await _store.Load();
        if (snapshot is not null)
            ApplySnapshot(snapshot);
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public Task Save() => _store.Save(ToSnapshot());

    public void SaveSettings() => Settings.Save(SettingsPath());

    private string SettingsPath() =>
        Path.GetFullPath(Path.Combine(ProjectDir, "..", "user_settings.json"));

    // ── Run lifecycle ─────────────────────────────────────────────────────────

    public async Task StartRun(Admiral admiral, ulong? runSeed = null)
    {
        AdmiralName = admiral.Name;
        AdmiralBonusText = admiral.BonusText;
        var snapshot = await _store.StartRun(admiral);
        if (runSeed.HasValue)
            snapshot.RunSeed = runSeed.Value;
        ApplySnapshot(snapshot);
        Stats.Reset();
        await _store.Save(ToSnapshot());
    }

    public async Task AbandonCurrentRun()
    {
        IsBattleActive = false;
        _currentBattleInputs = null;
        CurrentOpponentFleet = null;
        await _store.Delete();
        HasActiveRun = false;
        IsRunComplete = false;
        AdmiralId = "";
        UpgradePurchases = new();
        Stats.Reset();
    }

    public async Task SaveAndQuitToMenu()
    {
        await _store.Save(ToSnapshot());
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }

    // ── Dockyard session factory ──────────────────────────────────────────────

    // Swap LocalDockyardAdapter for ServerDockyardAdapter here when online mode is implemented.
    public IDockyard OpenDockyardSession() => new LocalDockyardAdapter(DockBinaryPath);

    // Builds the input snapshot for a new dockyard session from current run state.
    public DockSessionInput BuildDockSessionInput() =>
        new()
        {
            PlayerId = PlayerId,
            RunSeed = RunSeed,
            JumpNumber = JumpNumber,
            TierRerolls = (int[])TierRerolls.Clone(),
            ResearchRerolls = (int[])ResearchRerolls.Clone(),
            Salvage = Salvage,
            Tech = Tech,
            HangarUsed = UsedTonnage,
            HangarCap = HangarCapacity,
            Fleet = Fleet
                .Ships.Select(
                    (s, i) =>
                        new FleetShipRef
                        {
                            Index = i,
                            Tonnage = s.HullClass.Tonnage(),
                            BlueprintId = s.BlueprintDrawingId,
                        }
                )
                .ToList(),
            UpgradePurchases = new(UpgradePurchases),
        };

    // Applies a completed dockyard session delta to run state.
    // Called by DockUi after ShoppingDoneAsync succeeds.
    public void ApplyDockSessionDelta(DockDelta delta, DockOffersResult offers)
    {
        // Add commissioned ships: blueprint IDs from delta, full ShipDef from offer cache.
        foreach (var blueprintId in delta.ShipsCommissioned)
        {
            var ship = offers
                .ShipTiers.SelectMany(t => t.Slots)
                .FirstOrDefault(s => s.BlueprintId == blueprintId);
            if (ship is not null)
                Fleet.Ships.Add(ship.ShipDef);
        }

        // Remove salvaged ships in descending index order so earlier removals
        // do not shift the indices of later ones.
        foreach (var idx in delta.ShipsSalvaged.OrderByDescending(i => i))
        {
            if (idx < Fleet.Ships.Count)
                Fleet.Ships.RemoveAt(idx);
        }

        // Apply purchased upgrades.
        foreach (var offer in delta.UpgradesPurchased)
        {
            foreach (var effect in offer.Effects)
                ResearchCatalog.ApplyEffect(this, effect);
            var prev = UpgradePurchases.GetValueOrDefault(offer.UpgradeId);
            UpgradePurchases[offer.UpgradeId] = prev + 1;
        }

        Salvage = delta.SalvageFinal;
        Tech = delta.TechFinal;
        HangarCapacity = delta.HangarCapFinal;
        TierRerolls = delta.TierRerollsFinal;
        ResearchRerolls = delta.ResearchRerollsFinal;
    }

    // ── Battle lifecycle ──────────────────────────────────────────────────────

    public Task<ulong> GetBattleSeed() => _store.GetBattleSeed();

    // Fetches the encounter fleet for the current jump, sets CurrentOpponentFleet,
    // and returns the raw fleet JSON string for passing to the sim binary.
    // Returns empty string on failure (CurrentOpponentFleet will be null).
    public async Task<string> FetchEncounterJsonAsync()
    {
        // wins = jumps cleared so far (JumpNumber advances only on a win, starts at 1)
        var wins = JumpNumber - 1;
        try
        {
            var fleetJson = await _encounterSource.GetFleetJsonAsync(
                PlayerId,
                JumpNumber,
                LossCount,
                wins
            );
            var opponentFleet = JsonSerializer.Deserialize<FleetJsonData>(fleetJson);
            if (opponentFleet is not null)
                opponentFleet.Mothership.IsMothership = true;
            CurrentOpponentFleet = opponentFleet;
            return fleetJson;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[encounter] Failed to fetch fleet: {e.Message}");
            CurrentOpponentFleet = null;
            return "";
        }
    }

    public void BeginBattle(BattleInputs inputs)
    {
        IsBattleActive = true;
        _currentBattleInputs = inputs;
    }

    // ── Battle result + scene transitions ─────────────────────────────────────

    public async Task RecordBattleResult(BattleResult result)
    {
        IsBattleActive = false;
        var inputs = _currentBattleInputs ?? new BattleInputs();
        var opponentFleet = CurrentOpponentFleet;
        _currentBattleInputs = null;
        CurrentOpponentFleet = null;

        var playerSnapshot = Fleet;
        var opponentSnapshot = opponentFleet ?? new FleetJsonData();

        var transition = await BattleOutcomeResolver.Resolve(
            this,
            result,
            inputs,
            playerSnapshot,
            opponentSnapshot
        );

        switch (transition)
        {
            case PostBattleTransition.Defeat:
                RunEndReason = RunEndReason.Defeat;
                await _store.Save(ToSnapshot());
                GetTree().ChangeSceneToFile("res://scenes/RunEnd.tscn");
                break;
            case PostBattleTransition.Victory:
                RunEndReason = RunEndReason.Victory;
                await _store.Save(ToSnapshot());
                GetTree().ChangeSceneToFile("res://scenes/RunEnd.tscn");
                break;
            case PostBattleTransition.NextJump:
                await _store.Save(ToSnapshot());
                GetTree().ChangeSceneToFile("res://scenes/Dockyard.tscn");
                break;
        }
    }

    // ── Snapshot helpers ──────────────────────────────────────────────────────

    private RunSnapshot ToSnapshot() =>
        new()
        {
            PlayerId = PlayerId,
            RunSeed = RunSeed,
            Salvage = Salvage,
            Tech = Tech,
            HangarCapacity = HangarCapacity,
            JumpNumber = JumpNumber,
            LossCount = LossCount,
            AdmiralId = AdmiralId,
            AdmiralName = AdmiralName,
            AdmiralBonusText = AdmiralBonusText,
            Fleet = Fleet,
            TierRerolls = (int[])TierRerolls.Clone(),
            ResearchRerolls = (int[])ResearchRerolls.Clone(),
            UpgradePurchases = new Dictionary<string, int>(UpgradePurchases),
            IsComplete = IsRunComplete,
            JumpHistory = [.. Stats.GetJumpHistory()],
        };

    private void ApplySnapshot(RunSnapshot snapshot)
    {
        if (!string.IsNullOrEmpty(snapshot.PlayerId))
            PlayerId = snapshot.PlayerId;
        RunSeed = snapshot.RunSeed;
        Salvage = snapshot.Salvage;
        Tech = snapshot.Tech;
        HangarCapacity = snapshot.HangarCapacity;
        JumpNumber = Math.Max(1, snapshot.JumpNumber);
        LossCount = snapshot.LossCount;
        AdmiralId = snapshot.AdmiralId;
        AdmiralName = snapshot.AdmiralName;
        AdmiralBonusText = snapshot.AdmiralBonusText;
        Fleet = snapshot.Fleet;
        TierRerolls = snapshot.TierRerolls ?? new int[4];
        ResearchRerolls = snapshot.ResearchRerolls ?? new int[4];
        UpgradePurchases = snapshot.UpgradePurchases ?? new Dictionary<string, int>();
        HasActiveRun = !snapshot.IsComplete;
        IsRunComplete = snapshot.IsComplete;
        Stats.LoadHistory(snapshot.JumpHistory ?? []);
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
                    SeekNearest = 0.2,
                    SeekMass = 0.1,
                    SeekMothership = 0.0,
                    MaintainRange = 1.0,
                },
                Hardpoints =
                [
                    new HardpointDefData
                    {
                        Type = "hitscan",
                        Damage = 25,
                        Range = 200,
                        CooldownTicks = 45,
                    },
                ],
                CombatStance = "standoff",
                TargetPriority = "most_threatening",
            },
            Ships = [DefaultCorvette(), DefaultCorvette(), DefaultCorvette()],
        };

    private static ShipDefData DefaultCorvette() =>
        new()
        {
            BlueprintDrawingId = "corvette_a",
            HullClass = HullClass.Corvette,
            Role = Role.Fighter,
            Hp = 60,
            MaxHp = 60,
            Speed = 8,
            Acceleration = 2.5,
            TurnRate = 1.5,
            BoidWeights = new BoidWeightsData
            {
                Separation = 1.5,
                Cohesion = 0.3,
                Alignment = 0.3,
                SeekNearest = 1.5,
                SeekMass = 0.2,
                SeekMothership = 0.3,
                MaintainRange = 1.0,
            },
            Hardpoints =
            [
                new HardpointDefData
                {
                    Type = "hitscan",
                    Damage = 15,
                    Range = 120,
                    CooldownTicks = 30,
                },
            ],
            CombatStance = "brawl",
            TargetPriority = "nearest",
        };
}
