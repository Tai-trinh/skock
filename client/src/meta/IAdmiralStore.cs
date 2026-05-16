using System.Collections.Generic;

namespace Skock.Meta;

// Seam between the hardcoded admiral/faction catalog and its data source.
//
// Offline:  LocalAdmiralStore  — reads data/admirals.json + data/factions.json.
// Online:   ServerAdmiralStore — fetches from REST API; may expose rotating or player-unlocked admirals.
//
// To add online mode: implement this interface, assign it to RunState.Catalog at startup.
public interface IAdmiralStore
{
    void Load(); // Offline: read JSON files. Online: fetch from server.

    IReadOnlyList<Admiral> GetAdmirals();
    IReadOnlyList<Faction> GetFactions();

    Admiral? FindAdmiral(string id);
    Faction? FindFaction(string id);

    // Display name for any admiral ID, including NPC opponents without a full record.
    string NameFor(string admiralId);
}
