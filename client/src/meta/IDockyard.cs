using System;
using System.Threading.Tasks;

namespace Skock.Meta;

// Session-based dockyard interface. One instance per dockyard visit.
// The pipe to the binary (or server) stays open for the full shopping session.
//
// Offline: LocalDockyardAdapter — spawns skock-dockyard as a subprocess.
// Online:  ServerDockyardAdapter — talks to the game server (TODO: implement when server is built).
//
// Swap point: RunState.OpenDockyardSession().
public interface IDockyard : IAsyncDisposable
{
    // ── Session open ──────────────────────────────────────────────────────────

    // Sends initial context; returns the full offer set. Must be called first.
    Task<DockOffersResult> GetOffersAsync(DockSessionInput input);

    // ── Actions ───────────────────────────────────────────────────────────────

    Task<DockCommissionResult> CommissionAsync(string blueprintId);

    // Salvages a ship from the player's fleet by its current session-fleet index.
    // The binary owns the yield formula (tonnage × 3) and validates the index.
    Task<DockSalvageResult> SalvageFleetShipAsync(int index);

    Task<DockTierResult> RerollTierAsync(int tierIndex);

    Task<DockTrackResult> RerollResearchAsync(int trackIndex);

    Task<DockActionResult> BuyResearchAsync(string upgradeId);

    // ── Session close ─────────────────────────────────────────────────────────

    // Finalizes the session. Returns the authoritative delta to apply to RunState.
    Task<DockSessionDelta> ShoppingDoneAsync();
}
