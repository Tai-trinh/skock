using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    // ── Admiral / faction catalog ─────────────────────────────────────────────

    public IAdmiralStore Catalog { get; private set; } = null!;

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
        PlayerFleetPath = Path.GetFullPath(Path.Combine(ProjectDir, "..", "player_fleet.json"));
        FallbackFleetPath = Path.GetFullPath(
            Path.Combine(ProjectDir, "..", "sim", "test_data", "fleet_a.json")
        );

        // Swap LocalAdmiralStore for ServerAdmiralStore here when online mode is implemented.
        Catalog = new LocalAdmiralStore(Path.Combine(ProjectDir, "data"));
        // Swap LocalStatsStore for ServerStatsStore here when online mode is implemented.
        Stats = new LocalStatsStore();
        // Swap LocalRunStore for ServerRunStore here when online mode is implemented.
        _store = new LocalRunStore(this);

        Settings = UserSettings.Load(SettingsPath());
        Settings.Apply();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        await Catalog.Load();
        await _store.Load();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public Task Save() => _store.Save();

    public void SaveSettings() => Settings.Save(SettingsPath());

    private string SettingsPath() =>
        Path.GetFullPath(Path.Combine(ProjectDir, "..", "user_settings.json"));

    // ── Run lifecycle ─────────────────────────────────────────────────────────

    public Task StartRun(Admiral admiral) => _store.StartRun(admiral);

    public async Task AbandonCurrentRun()
    {
        IsBattleActive = false;
        _currentBattleInputs = null;
        CurrentOpponentFleet = null;
        await _store.DeleteSave();
        HasActiveRun = false;
        IsRunComplete = false;
        AdmiralId = "";
        UpgradePurchases = new();
        Stats.Reset();
    }

    public string GetOpponentFleetPath() =>
        Path.GetFullPath(Path.Combine(ProjectDir, "data", "opponents", $"jump_{JumpNumber}.json"));

    public async Task SaveAndQuitToMenu()
    {
        await _store.Save();
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
    public void ApplyDockSessionDelta(DockDelta delta)
    {
        // Add commissioned ships (binary returns full ShipDef via DockUi's cached offers).
        // Commissioned ships are handled by DockUi which has the ShipDef from the offer cache.

        // Remove salvaged ships: apply in descending index order so earlier removals
        // do not shift the indices of later ones.
        foreach (var idx in delta.ShipsSalvaged)
        {
            if (idx < Fleet.Ships.Count)
                Fleet.Ships.RemoveAt(idx);
        }

        // Apply purchased upgrades.
        foreach (var upgradeId in delta.UpgradesPurchased)
        {
            ResearchCatalog.All.FirstOrDefault(u => u.Id == upgradeId)?.Apply(this);
            var prev = UpgradePurchases.GetValueOrDefault(upgradeId);
            UpgradePurchases[upgradeId] = prev + 1;
        }

        Salvage = delta.SalvageFinal;
        Tech = delta.TechFinal;
        TierRerolls = delta.TierRerollsFinal;
        ResearchRerolls = delta.ResearchRerollsFinal;
    }

    // ── Battle lifecycle ──────────────────────────────────────────────────────

    public Task<ulong> GetBattleSeed() => _store.GetBattleSeed();

    public void BeginBattle(BattleInputs inputs, FleetJsonData? opponentFleet)
    {
        IsBattleActive = true;
        _currentBattleInputs = inputs;
        CurrentOpponentFleet = opponentFleet;
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
                await _store.Save();
                GetTree().ChangeSceneToFile("res://scenes/RunEnd.tscn");
                break;
            case PostBattleTransition.Victory:
                RunEndReason = RunEndReason.Victory;
                await _store.Save();
                GetTree().ChangeSceneToFile("res://scenes/RunEnd.tscn");
                break;
            case PostBattleTransition.NextJump:
                await _store.Save();
                GetTree().ChangeSceneToFile("res://scenes/Dockyard.tscn");
                break;
        }
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
