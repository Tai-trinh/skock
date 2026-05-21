using System.Collections.Generic;
using System.Threading.Tasks;
using Skock.Meta;
using Skock.Tests.Fakes;
using Xunit;

namespace Skock.Tests;

public sealed class DockSessionModelTests
{
    private static readonly DockSessionInput NoInput = new();

    private static DockSessionModel MakeModel(
        FakeDockyard dock,
        IReadOnlyList<SessionShip>? fleet = null
    ) => new(dock, fleet ?? []);

    // ── Open ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Open_SetsResourceStateAndOffers()
    {
        var dock = new FakeDockyard
        {
            GetOffersResponse = new DockOffersResult
            {
                State = new DockResourceState { Salvage = 100, Tech = 5 },
                ShipTiers = [],
                ResearchTracks = [],
            },
        };
        var model = MakeModel(dock);
        var error = await model.OpenAsync(NoInput);
        Assert.Null(error);
        Assert.Equal(100, model.ResourceState.Salvage);
        Assert.Equal(5, model.ResourceState.Tech);
        Assert.NotNull(model.Offers);
    }

    [Fact]
    public async Task Open_ReturnsErrorString_WhenDockyardThrows()
    {
        var dock = new FakeDockyard { ThrowOnGetOffers = new System.Exception("unreachable") };
        var model = MakeModel(dock);
        var error = await model.OpenAsync(NoInput);
        Assert.NotNull(error);
        Assert.Contains("unreachable", error);
    }

    // ── Commission ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Commission_Success_UpdatesResourceStateAndAddsShipToFleet()
    {
        var dock = new FakeDockyard
        {
            CommissionResponse = new DockCommissionResult
            {
                Ok = true,
                Ship = new ShipSlotOffer { DisplayName = "Viper", Tonnage = 30 },
                State = new DockResourceState { Salvage = 50 },
            },
        };
        var model = MakeModel(dock);
        var result = await model.CommissionAsync("viper_id");
        Assert.True(result.Ok);
        Assert.Equal(50, model.ResourceState.Salvage);
        Assert.Single(model.Fleet);
        Assert.Equal("Viper", model.Fleet[0].Name);
        Assert.Equal(30, model.Fleet[0].Tonnage);
    }

    [Fact]
    public async Task Commission_Failure_LeavesStateUnchanged()
    {
        var dock = new FakeDockyard
        {
            CommissionResponse = new DockCommissionResult { Ok = false, Error = "no tonnage" },
        };
        var model = MakeModel(dock);
        var result = await model.CommissionAsync("viper_id");
        Assert.False(result.Ok);
        Assert.Empty(model.Fleet);
    }

    // ── Salvage ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Salvage_Success_RemovesShipAndUpdatesResources()
    {
        var dock = new FakeDockyard
        {
            SalvageResponse = new DockSalvageResult
            {
                Ok = true,
                SalvageYield = 90,
                State = new DockResourceState { Salvage = 190 },
            },
        };
        var model = MakeModel(dock, [new SessionShip(0, "Viper", 30)]);
        var result = await model.SalvageAsync(0);
        Assert.True(result.Ok);
        Assert.Equal(90, result.SalvageYield);
        Assert.Empty(model.Fleet);
        Assert.Equal(190, model.ResourceState.Salvage);
    }

    [Fact]
    public async Task Salvage_Failure_LeavesFleetUnchanged()
    {
        var dock = new FakeDockyard
        {
            SalvageResponse = new DockSalvageResult { Ok = false, Error = "invalid index" },
        };
        var model = MakeModel(dock, [new SessionShip(0, "Viper", 30)]);
        await model.SalvageAsync(0);
        Assert.Single(model.Fleet);
    }

    // ── Reroll tier ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RerollTier_Success_UpdatesOffersAndResources()
    {
        var newTier = new ShipTierOffer { TierIndex = 1, Label = "Tier 2 (rerolled)", Slots = [] };
        var dock = new FakeDockyard
        {
            GetOffersResponse = new DockOffersResult
            {
                State = new DockResourceState { Salvage = 100 },
                ShipTiers = [new ShipTierOffer { TierIndex = 1, Label = "Tier 2", Slots = [] }],
                ResearchTracks = [],
            },
            RerollTierResponse = new DockTierResult
            {
                Ok = true,
                Tier = newTier,
                State = new DockResourceState { Salvage = 80 },
            },
        };
        var model = MakeModel(dock);
        await model.OpenAsync(NoInput);
        await model.RerollTierAsync(1);
        Assert.Equal(80, model.ResourceState.Salvage);
        Assert.Equal("Tier 2 (rerolled)", model.Offers!.ShipTiers[0].Label);
    }

    // ── Reroll research ───────────────────────────────────────────────────────

    [Fact]
    public async Task RerollResearch_Success_UpdatesOffersAndResources()
    {
        var newTrack = new ResearchTrackOffer
        {
            TrackIndex = 0,
            Label = "Weapons (rerolled)",
            Items = [],
        };
        var dock = new FakeDockyard
        {
            GetOffersResponse = new DockOffersResult
            {
                State = new DockResourceState { Tech = 5 },
                ShipTiers = [],
                ResearchTracks =
                [
                    new ResearchTrackOffer { TrackIndex = 0, Label = "Weapons", Items = [] },
                ],
            },
            RerollResearchResponse = new DockTrackResult
            {
                Ok = true,
                Track = newTrack,
                State = new DockResourceState { Tech = 3 },
            },
        };
        var model = MakeModel(dock);
        await model.OpenAsync(NoInput);
        await model.RerollResearchAsync(0);
        Assert.Equal(3, model.ResourceState.Tech);
        Assert.Equal("Weapons (rerolled)", model.Offers!.ResearchTracks[0].Label);
    }

    // ── Buy research ──────────────────────────────────────────────────────────

    [Fact]
    public async Task BuyResearch_Success_UpdatesResources()
    {
        var dock = new FakeDockyard
        {
            BuyResearchResponse = new DockActionResult
            {
                Ok = true,
                State = new DockResourceState { Tech = 2 },
            },
        };
        var model = MakeModel(dock);
        var result = await model.BuyResearchAsync("upgrade_1");
        Assert.True(result.Ok);
        Assert.Equal(2, model.ResourceState.Tech);
    }

    // ── Shopping done ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ShoppingDone_ReturnsDelta()
    {
        var dock = new FakeDockyard
        {
            ShoppingDoneResponse = new DockSessionDelta
            {
                Ok = true,
                Delta = new DockDelta { SalvageFinal = 75 },
            },
        };
        var model = MakeModel(dock);
        var result = await model.ShoppingDoneAsync();
        Assert.True(result.Ok);
        Assert.Equal(75, result.Delta!.SalvageFinal);
    }
}
