using System.Collections.Generic;
using Skock.Meta;

namespace Skock.Tests.Fakes;

internal sealed class FakeRunData : IRunData
{
    public string AdmiralId { get; set; } = "";

    public int Salvage { get; set; }
    public int Tech { get; set; }
    public int HangarCapacity { get; set; } = 10;
    public int JumpNumber { get; set; } = 1;
    public int LossCount { get; set; }
    public FleetJsonData Fleet { get; set; } =
        new() { Mothership = new ShipDefData { IsMothership = true }, Ships = [] };
    public int[] TierRerolls { get; set; } = new int[4];
    public Dictionary<string, int> UpgradePurchases { get; set; } = new();
    public bool HasActiveRun { get; set; }
    public bool IsRunComplete { get; set; }

    public IStatsStore Stats { get; set; } = new LocalStatsStore();
}
