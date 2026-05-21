using System.Threading.Tasks;

namespace Skock.Meta;

// Seam between the admiral selection binary and its callers.
//
// Offline:  LocalAdmiralAdapter  — spawns skock-admiral as a subprocess.
// Online:   ServerAdmiralAdapter — fetches from the matchmaking API.
// Swap point: RunState.AdmiralSelection in _Ready().
public interface IAdmiralSelection
{
    Task<AdmiralOffersResult> GetOffersAsync(string playerId);
}
