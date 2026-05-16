using System.Collections.Generic;

namespace Skock.Meta;

// Seam between in-memory statistics (per-run history and lifetime counters) and their
// persistence / reporting backend.
//
// Offline:  LocalStatsStore  — keeps history in memory, no network calls.
// Online:   ServerStatsStore — also transmits BattleInputs to the server so it can
//                              retroactively re-simulate battles for anti-cheat (see ADR-0005).
//
// To add online mode: implement this interface, assign it to RunState.Stats at startup.
public interface IStatsStore
{
    // ── Battle recording ──────────────────────────────────────────────────────

    // Called once per completed battle.
    // Online: also POST BattleInputs to the server for anti-cheat storage.
    // During development, server re-simulates every battle; in production, sample-based.
    void RecordBattle(JumpRecord record, BattleInputs inputs);

    // ── Resource spend tracking ────────────────────────────────────────────────
    // Online: server compares these against run state deltas for anomaly detection.

    void RecordSalvageSpent(int amount);
    void RecordTechSpent(int amount);
    void RecordCapitalShipBought();

    // ── Run-level events ──────────────────────────────────────────────────────

    void RecordRunVictory();

    // ── Readers ───────────────────────────────────────────────────────────────

    IReadOnlyList<JumpRecord> GetJumpHistory();
    LifetimeStats GetLifetimeStats();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    // Reset per-run history (called at run start). Lifetime counters are not reset.
    void Reset();
}
