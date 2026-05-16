using System.Text.Json.Serialization;

namespace Skock.Meta;

public sealed class Faction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}
