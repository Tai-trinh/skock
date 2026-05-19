using System.Collections.Generic;

namespace Skock.Meta;

// The mutable run state consumed by BattleOutcomeResolver and ResearchUpgrade.Apply.
// RunState implements this; test code uses hand-rolled fakes.
public interface IRunData
{
    // ── Identity ──────────────────────────────────────────────────────────────

    string AdmiralId { get; }

    // ── Run state ─────────────────────────────────────────────────────────────

    int Salvage { get; set; }
    int Tech { get; set; }
    int HangarCapacity { get; set; }
    int JumpNumber { get; set; }
    int LossCount { get; set; }
    FleetJsonData Fleet { get; set; }
    int[] TierRerolls { get; set; }
    Dictionary<string, int> UpgradePurchases { get; }
    bool HasActiveRun { get; set; }
    bool IsRunComplete { get; set; }

    // ── Dependencies ──────────────────────────────────────────────────────────

    IStatsStore Stats { get; }
}
