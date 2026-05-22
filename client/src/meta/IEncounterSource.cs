using System.Threading.Tasks;

namespace Skock.Meta;

// Seam between the encounter fleet source and its callers.
//
// Offline:  LocalEncounterAdapter  — spawns skock-encounter as a subprocess.
// Online:   ServerEncounterAdapter — fetches from the matchmaking API (fleet matched by win/loss ratio).
// Swap point: RunState._encounterSource in _Ready().
//
// TODO (online mode): implement ServerEncounterAdapter.
// TODO (wire format): switch to MessagePack in production. Apply consistently across
//   skock-admiral, skock-dockyard, and skock-encounter.
public interface IEncounterSource
{
    Task<string> GetFleetJsonAsync(string playerId, int runNumber, int losses, int wins);
}
