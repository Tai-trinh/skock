using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Skock.Meta;

public sealed class DockSessionModel : IAsyncDisposable
{
    private readonly IDockyard _session;
    private readonly List<SessionShip> _fleet;

    public DockResourceState ResourceState { get; private set; } = new();
    public DockOffersResult? Offers { get; private set; }
    public IReadOnlyList<SessionShip> Fleet => _fleet;

    public DockSessionModel(IDockyard session, IReadOnlyList<SessionShip> initialFleet)
    {
        _session = session;
        _fleet = [.. initialFleet];
    }

    public async Task<string?> OpenAsync(DockSessionInput input)
    {
        try
        {
            Offers = await _session.GetOffersAsync(input);
            ResourceState = Offers.State;
            return null;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    public async Task<DockCommissionResult> CommissionAsync(string blueprintId)
    {
        var result = await _session.CommissionAsync(blueprintId);
        if (result.Ok && result.State is not null && result.Ship is not null)
        {
            ResourceState = result.State;
            _fleet.Add(new SessionShip(_fleet.Count, result.Ship.DisplayName, result.Ship.Tonnage));
        }
        return result;
    }

    public async Task<DockSalvageResult> SalvageAsync(int sessionIndex)
    {
        var result = await _session.SalvageFleetShipAsync(sessionIndex);
        if (result.Ok && result.State is not null)
        {
            ResourceState = result.State;
            _fleet.RemoveAt(sessionIndex);
        }
        return result;
    }

    public async Task<DockTierResult> RerollTierAsync(int tierIndex)
    {
        var result = await _session.RerollTierAsync(tierIndex);
        if (result.Ok && result.State is not null)
        {
            ResourceState = result.State;
            if (Offers is not null && result.Tier is not null)
            {
                var idx = Offers.ShipTiers.FindIndex(t => t.TierIndex == tierIndex);
                if (idx >= 0)
                    Offers.ShipTiers[idx] = result.Tier;
            }
        }
        return result;
    }

    public async Task<DockTrackResult> RerollResearchAsync(int trackIndex)
    {
        var result = await _session.RerollResearchAsync(trackIndex);
        if (result.Ok && result.State is not null)
        {
            ResourceState = result.State;
            if (Offers is not null && result.Track is not null)
            {
                var idx = Offers.ResearchTracks.FindIndex(t => t.TrackIndex == trackIndex);
                if (idx >= 0)
                    Offers.ResearchTracks[idx] = result.Track;
            }
        }
        return result;
    }

    public async Task<DockActionResult> BuyResearchAsync(string upgradeId)
    {
        var result = await _session.BuyResearchAsync(upgradeId);
        if (result.Ok && result.State is not null)
            ResourceState = result.State;
        return result;
    }

    public Task<DockSessionDelta> ShoppingDoneAsync() => _session.ShoppingDoneAsync();

    public ValueTask DisposeAsync() => _session.DisposeAsync();
}
