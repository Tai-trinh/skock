using System;
using System.Threading.Tasks;
using Skock.Meta;

namespace Skock.Tests.Fakes;

internal sealed class FakeDockyard : IDockyard
{
    public DockOffersResult GetOffersResponse { get; set; } = new();
    public Exception? ThrowOnGetOffers { get; set; }
    public DockCommissionResult CommissionResponse { get; set; } = new();
    public DockSalvageResult SalvageResponse { get; set; } = new();
    public DockTierResult RerollTierResponse { get; set; } = new();
    public DockTrackResult RerollResearchResponse { get; set; } = new();
    public DockActionResult BuyResearchResponse { get; set; } = new();
    public DockSessionDelta ShoppingDoneResponse { get; set; } = new();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<DockOffersResult> GetOffersAsync(DockSessionInput input)
    {
        if (ThrowOnGetOffers is not null)
            throw ThrowOnGetOffers;
        return Task.FromResult(GetOffersResponse);
    }

    public Task<DockCommissionResult> CommissionAsync(string blueprintId) =>
        Task.FromResult(CommissionResponse);

    public Task<DockSalvageResult> SalvageFleetShipAsync(int index) =>
        Task.FromResult(SalvageResponse);

    public Task<DockTierResult> RerollTierAsync(int tierIndex) =>
        Task.FromResult(RerollTierResponse);

    public Task<DockTrackResult> RerollResearchAsync(int trackIndex) =>
        Task.FromResult(RerollResearchResponse);

    public Task<DockActionResult> BuyResearchAsync(string upgradeId) =>
        Task.FromResult(BuyResearchResponse);

    public Task<DockSessionDelta> ShoppingDoneAsync() =>
        Task.FromResult(ShoppingDoneResponse);
}
