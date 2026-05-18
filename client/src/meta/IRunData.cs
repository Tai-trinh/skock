using System.Collections.Generic;

namespace Skock.Meta;

// The mutable run state that IRunStore implementations read and write.
// RunState implements this; test code uses hand-rolled fakes.
public interface IRunData
{
    // ── Paths ─────────────────────────────────────────────────────────────────

    string PlayerFleetPath { get; }
    string ProjectDir { get; }
    string DockBinaryPath { get; }

    // ── Identity ──────────────────────────────────────────────────────────────

    // Stable player identifier. "offline:<uuid>" in single-player.
    // Platform ID (Steam, Apple, etc.) in online mode.
    string PlayerId { get; set; }

    // ── Run state ─────────────────────────────────────────────────────────────

    ulong RunSeed { get; set; }
    int Salvage { get; set; }
    int Tech { get; set; }
    int HangarCapacity { get; set; }
    int JumpNumber { get; set; }
    int LossCount { get; set; }
    string AdmiralId { get; set; }
    FleetJsonData Fleet { get; set; }
    int[] TierRerolls { get; set; }
    int[] ResearchRerolls { get; set; }
    Dictionary<string, int> UpgradePurchases { get; set; }
    bool HasActiveRun { get; set; }
    bool IsRunComplete { get; set; }

    // ── Computed ──────────────────────────────────────────────────────────────

    int FreeTonnage { get; }

    // ── Dependencies ──────────────────────────────────────────────────────────

    IStatsStore Stats { get; }
    IAdmiralStore Catalog { get; }
}
