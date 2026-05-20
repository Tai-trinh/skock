using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Skock.Meta;

public sealed class AdmiralOffersResult
{
    [JsonPropertyName("run_seed")]
    public ulong RunSeed { get; init; }

    [JsonPropertyName("offers")]
    public List<AdmiralOfferDto> Offers { get; init; } = [];
}

public sealed class AdmiralOfferDto
{
    [JsonPropertyName("admiral_id")]
    public string AdmiralId { get; init; } = "";

    [JsonPropertyName("admiral_name")]
    public string AdmiralName { get; init; } = "";

    [JsonPropertyName("faction_id")]
    public string FactionId { get; init; } = "";

    [JsonPropertyName("faction_name")]
    public string FactionName { get; init; } = "";

    [JsonPropertyName("bonus_text")]
    public string BonusText { get; init; } = "";

    [JsonPropertyName("starting_salvage")]
    public int StartingSalvage { get; init; }

    [JsonPropertyName("starting_tech")]
    public int StartingTech { get; init; }

    [JsonPropertyName("starting_hangar_capacity")]
    public int StartingHangarCapacity { get; init; }

    [JsonPropertyName("starting_fleet")]
    public FleetJsonData StartingFleet { get; init; } = new();
}
