using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Skock.Meta;
using Skock.Tests.Fakes;
using Xunit;

namespace Skock.Tests;

public sealed class LocalRunStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly FakeRunData _run;
    private readonly LocalRunStore _store;

    // Corvette: 10 salvage, 2T
    private static readonly Blueprint Corvette = BlueprintCatalog.All[0];

    public LocalRunStoreTests()
    {
        Directory.CreateDirectory(_tempDir);
        _run = new FakeRunData
        {
            PlayerFleetPath = Path.Combine(_tempDir, "player_fleet.json"),
            ProjectDir = _tempDir,
            Salvage = 50,
            Tech = 5,
            HangarCapacity = 10,
        };
        _store = new LocalRunStore(_run);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // ── CommissionShip ────────────────────────────────────────────────────────

    [Fact]
    public async Task CommissionShip_DeductsSalvage()
    {
        await _store.CommissionShip(Corvette);
        Assert.Equal(40, _run.Salvage);
    }

    [Fact]
    public async Task CommissionShip_AddsShipToFleet()
    {
        await _store.CommissionShip(Corvette);
        Assert.Single(_run.Fleet.Ships);
    }

    [Fact]
    public async Task CommissionShip_ReturnsFalse_WhenInsufficientSalvage()
    {
        _run.Salvage = 5;
        Assert.False(await _store.CommissionShip(Corvette));
        Assert.Empty(_run.Fleet.Ships);
    }

    [Fact]
    public async Task CommissionShip_ReturnsFalse_WhenInsufficientTonnage()
    {
        _run.HangarCapacity = 1; // Corvette costs 2T
        Assert.False(await _store.CommissionShip(Corvette));
        Assert.Empty(_run.Fleet.Ships);
    }

    // ── SalvageShip ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SalvageShip_ReturnsSalvageYield()
    {
        await _store.CommissionShip(Corvette);
        var salvageBefore = _run.Salvage;
        var yield = await _store.SalvageShip(0);
        Assert.Equal(Corvette.Template.HullClass.Tonnage() * 3, yield);
        Assert.Equal(salvageBefore + yield, _run.Salvage);
    }

    [Fact]
    public async Task SalvageShip_RemovesShipFromFleet()
    {
        await _store.CommissionShip(Corvette);
        await _store.SalvageShip(0);
        Assert.Empty(_run.Fleet.Ships);
    }

    [Fact]
    public async Task SalvageShip_ReturnsMinus1_WhenIndexOutOfRange()
    {
        Assert.Equal(-1, await _store.SalvageShip(0));
    }

    // ── RerollTier ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RerollTier_DeductsSalvage()
    {
        await _store.RerollTier(0, cost: 5);
        Assert.Equal(45, _run.Salvage);
    }

    [Fact]
    public async Task RerollTier_IncrementsTierRerollCount()
    {
        await _store.RerollTier(0, cost: 5);
        Assert.Equal(1, _run.TierRerolls[0]);
    }

    [Fact]
    public async Task RerollTier_ReturnsFalse_WhenInsufficientSalvage()
    {
        _run.Salvage = 4;
        Assert.False(await _store.RerollTier(0, cost: 5));
        Assert.Equal(4, _run.Salvage);
    }

    // ── BuyUpgrade ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuyUpgrade_DeductsTech()
    {
        await _store.BuyUpgrade("hangar_expansion");
        Assert.Equal(4, _run.Tech);
    }

    [Fact]
    public async Task BuyUpgrade_AppliesEffect()
    {
        var capacityBefore = _run.HangarCapacity;
        await _store.BuyUpgrade("hangar_expansion");
        Assert.Equal(capacityBefore + 2, _run.HangarCapacity);
    }

    [Fact]
    public async Task BuyUpgrade_ReturnsFalse_WhenInsufficientTech()
    {
        _run.Tech = 0;
        Assert.False(await _store.BuyUpgrade("hangar_expansion"));
    }

    [Fact]
    public async Task BuyUpgrade_ReturnsFalse_WhenMaxed()
    {
        // hangar_expansion maxes at 3 purchases
        _run.Tech = 99;
        await _store.BuyUpgrade("hangar_expansion");
        await _store.BuyUpgrade("hangar_expansion");
        await _store.BuyUpgrade("hangar_expansion");
        Assert.False(await _store.BuyUpgrade("hangar_expansion"));
    }

    // ── JumpHistory persistence ───────────────────────────────────────────────

    [Fact]
    public async Task Save_And_Load_RestoresJumpHistory()
    {
        var record = new JumpRecord
        {
            JumpNumber = 3,
            Won = true,
            DurationTicks = 450,
            EnemiesKilledByHullClass = new Dictionary<string, int> { ["Corvette"] = 2 },
            OwnShipsLostByHullClass = new Dictionary<string, int>(),
            DamageDealt = 800f,
            DamageTaken = 200f,
            PlayerFleetSnapshot = new FleetJsonData(),
            OpponentFleetSnapshot = new FleetJsonData(),
            PlayerUpgrades = new Dictionary<string, int>(),
            PlayerAdmiralId = "kira",
        };
        await _run.Stats.RecordBattle(record, new BattleInputs());
        await _store.Save();

        // Load into a fresh run / store over the same temp dir.
        var freshRun = new FakeRunData
        {
            PlayerFleetPath = _run.PlayerFleetPath,
            ProjectDir = _run.ProjectDir,
        };
        var freshStore = new LocalRunStore(freshRun);
        await freshStore.Load();

        var history = freshRun.Stats.GetJumpHistory();
        Assert.Single(history);
        Assert.Equal(3, history[0].JumpNumber);
        Assert.True(history[0].Won);
        Assert.Equal("kira", history[0].PlayerAdmiralId);
        Assert.Equal(2, history[0].EnemiesKilledByHullClass["Corvette"]);
    }
}
