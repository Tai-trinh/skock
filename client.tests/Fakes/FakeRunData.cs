using System.Collections.Generic;
using System.Linq;
using Skock.Meta;

namespace Skock.Tests.Fakes;

internal sealed class FakeRunData : IRunData
{
    public string PlayerFleetPath { get; set; } = "";
    public string ProjectDir { get; set; } = "";
    public string DockBinaryPath { get; set; } = "";

    public string PlayerId { get; set; } = "offline:test";

    public ulong RunSeed { get; set; }
    public int Salvage { get; set; }
    public int Tech { get; set; }
    public int HangarCapacity { get; set; } = 10;
    public int JumpNumber { get; set; } = 1;
    public int LossCount { get; set; }
    public string AdmiralId { get; set; } = "";
    public FleetJsonData Fleet { get; set; } =
        new() { Mothership = new ShipDefData { IsMothership = true }, Ships = [] };
    public int[] TierRerolls { get; set; } = new int[4];
    public int[] ResearchRerolls { get; set; } = new int[4];
    public Dictionary<string, int> UpgradePurchases { get; set; } = new();
    public bool HasActiveRun { get; set; }
    public bool IsRunComplete { get; set; }

    public int FreeTonnage => HangarCapacity - Fleet.Ships.Sum(s => s.HullClass.Tonnage());

    public IStatsStore Stats { get; set; } = new LocalStatsStore();
    public IAdmiralStore Catalog { get; set; } = new NullAdmiralStore();
}
