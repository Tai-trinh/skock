using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Skock.Meta;

// Offline IAdmiralStore — reads data/admirals.json and data/factions.json at startup.
// TODO (online mode): implement ServerAdmiralStore for the same interface; swap in RunState._Ready().
public sealed class LocalAdmiralStore : IAdmiralStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _dataDir;
    private List<Admiral> _admirals = [];
    private List<Faction> _factions = [];

    public LocalAdmiralStore(string dataDir) => _dataDir = dataDir;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Load()
    {
        _factions = LoadFactions();
        _admirals = LoadAdmirals();
    }

    // ── IAdmiralStore ─────────────────────────────────────────────────────────

    public IReadOnlyList<Admiral> GetAdmirals() => _admirals;

    public IReadOnlyList<Faction> GetFactions() => _factions;

    public Admiral? FindAdmiral(string id) => _admirals.FirstOrDefault(a => a.Id == id);

    public Faction? FindFaction(string id) => _factions.FirstOrDefault(f => f.Id == id);

    public string NameFor(string admiralId) =>
        FindAdmiral(admiralId)?.Name
        ?? admiralId switch
        {
            "bob" => "Admiral Bob",
            "none" or "" => "Unknown",
            _ => admiralId,
        };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private List<Faction> LoadFactions()
    {
        var path = Path.Combine(_dataDir, "factions.json");
        if (!File.Exists(path))
            return [];
        return JsonSerializer.Deserialize<List<Faction>>(File.ReadAllText(path), JsonOptions) ?? [];
    }

    private List<Admiral> LoadAdmirals()
    {
        var path = Path.Combine(_dataDir, "admirals.json");
        if (!File.Exists(path))
            return [];

        var dtos =
            JsonSerializer.Deserialize<List<AdmiralDto>>(File.ReadAllText(path), JsonOptions) ?? [];

        return dtos.Select(dto =>
            {
                dto.StartingFleet.Mothership.IsMothership = true;
                return new Admiral
                {
                    Id = dto.Id,
                    Name = dto.Name,
                    FactionId = dto.FactionId,
                    BonusText = dto.BonusText,
                    StartingSalvage = dto.StartingSalvage,
                    StartingTech = dto.StartingTech,
                    StartingHangarCapacity = dto.StartingHangarCapacity,
                    StartingFleet = dto.StartingFleet,
                    ShipEffects = AdmiralEffectsRegistry.ForAdmiral(dto.Id),
                };
            })
            .ToList();
    }

    // JSON DTO — flat mirror of the admiral JSON shape.
    private sealed class AdmiralDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("faction_id")]
        public string FactionId { get; set; } = "";

        [JsonPropertyName("bonus_text")]
        public string BonusText { get; set; } = "";

        [JsonPropertyName("starting_salvage")]
        public int StartingSalvage { get; set; }

        [JsonPropertyName("starting_tech")]
        public int StartingTech { get; set; }

        [JsonPropertyName("starting_hangar_capacity")]
        public int StartingHangarCapacity { get; set; }

        [JsonPropertyName("starting_fleet")]
        public FleetJsonData StartingFleet { get; set; } = new();
    }
}
