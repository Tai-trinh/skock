using System;
using System.IO;
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
    public void CommissionShip_DeductsSalvage()
    {
        _store.CommissionShip(Corvette);
        Assert.Equal(40, _run.Salvage);
    }

    [Fact]
    public void CommissionShip_AddsShipToFleet()
    {
        _store.CommissionShip(Corvette);
        Assert.Single(_run.Fleet.Ships);
    }

    [Fact]
    public void CommissionShip_ReturnsFalse_WhenInsufficientSalvage()
    {
        _run.Salvage = 5;
        Assert.False(_store.CommissionShip(Corvette));
        Assert.Empty(_run.Fleet.Ships);
    }

    [Fact]
    public void CommissionShip_ReturnsFalse_WhenInsufficientTonnage()
    {
        _run.HangarCapacity = 1; // Corvette costs 2T
        Assert.False(_store.CommissionShip(Corvette));
        Assert.Empty(_run.Fleet.Ships);
    }

    // ── SalvageShip ───────────────────────────────────────────────────────────

    [Fact]
    public void SalvageShip_ReturnsSalvageYield()
    {
        _store.CommissionShip(Corvette);
        var salvageBefore = _run.Salvage;
        var yield = _store.SalvageShip(0);
        Assert.Equal(Corvette.Template.HullClass.Tonnage() * 3, yield);
        Assert.Equal(salvageBefore + yield, _run.Salvage);
    }

    [Fact]
    public void SalvageShip_RemovesShipFromFleet()
    {
        _store.CommissionShip(Corvette);
        _store.SalvageShip(0);
        Assert.Empty(_run.Fleet.Ships);
    }

    [Fact]
    public void SalvageShip_ReturnsMinus1_WhenIndexOutOfRange()
    {
        Assert.Equal(-1, _store.SalvageShip(0));
    }

    // ── RerollTier ────────────────────────────────────────────────────────────

    [Fact]
    public void RerollTier_DeductsSalvage()
    {
        _store.RerollTier(0, cost: 5);
        Assert.Equal(45, _run.Salvage);
    }

    [Fact]
    public void RerollTier_IncrementsTierRerollCount()
    {
        _store.RerollTier(0, cost: 5);
        Assert.Equal(1, _run.TierRerolls[0]);
    }

    [Fact]
    public void RerollTier_ReturnsFalse_WhenInsufficientSalvage()
    {
        _run.Salvage = 4;
        Assert.False(_store.RerollTier(0, cost: 5));
        Assert.Equal(4, _run.Salvage);
    }

    // ── BuyUpgrade ────────────────────────────────────────────────────────────

    [Fact]
    public void BuyUpgrade_DeductsTech()
    {
        _store.BuyUpgrade("hangar_expansion");
        Assert.Equal(4, _run.Tech);
    }

    [Fact]
    public void BuyUpgrade_AppliesEffect()
    {
        var capacityBefore = _run.HangarCapacity;
        _store.BuyUpgrade("hangar_expansion");
        Assert.Equal(capacityBefore + 2, _run.HangarCapacity);
    }

    [Fact]
    public void BuyUpgrade_ReturnsFalse_WhenInsufficientTech()
    {
        _run.Tech = 0;
        Assert.False(_store.BuyUpgrade("hangar_expansion"));
    }

    [Fact]
    public void BuyUpgrade_ReturnsFalse_WhenMaxed()
    {
        // hangar_expansion maxes at 3 purchases
        _run.Tech = 99;
        _store.BuyUpgrade("hangar_expansion");
        _store.BuyUpgrade("hangar_expansion");
        _store.BuyUpgrade("hangar_expansion");
        Assert.False(_store.BuyUpgrade("hangar_expansion"));
    }
}
