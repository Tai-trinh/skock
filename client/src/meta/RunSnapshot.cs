using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Skock.Meta;

// Serializable snapshot of all persistent run state.
// IRunStore.Load() returns one; IRunStore.Save() takes one.
// Fleet is excluded from the state JSON (written separately to player_fleet.json).
public sealed class RunSnapshot
{
    public string? PlayerId { get; set; }
    public ulong RunSeed { get; set; }
    public int Salvage { get; set; }
    public int Tech { get; set; }
    public int HangarCapacity { get; set; }
    public int JumpNumber { get; set; }
    public int LossCount { get; set; }
    public string AdmiralId { get; set; } = "";
    public string AdmiralName { get; set; } = "";
    public string AdmiralBonusText { get; set; } = "";
    public int[]? TierRerolls { get; set; }
    public int[]? ResearchRerolls { get; set; }
    public Dictionary<string, int>? UpgradePurchases { get; set; }
    public bool IsComplete { get; set; }
    public List<JumpRecord>? JumpHistory { get; set; }

    // Written to player_fleet.json; excluded from the state JSON.
    [JsonIgnore]
    public FleetJsonData Fleet { get; set; } = new();
}
