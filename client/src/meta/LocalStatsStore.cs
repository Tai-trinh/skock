using System.Collections.Generic;
using System.Threading.Tasks;

namespace Skock.Meta;

// Offline IStatsStore — keeps history in memory, ignores BattleInputs (no server to report to).
// TODO (online mode): implement ServerStatsStore for the same interface; swap in RunState._Ready().
// ServerStatsStore.RecordBattle should POST BattleInputs to the server so it can re-simulate
// sampled battles for anti-cheat verification. See ADR-0005.
public sealed class LocalStatsStore : IStatsStore
{
    private readonly List<JumpRecord> _history = [];
    private readonly LifetimeStats _lifetime = new();

    // ── Battle recording ──────────────────────────────────────────────────────

    public Task RecordBattle(JumpRecord record, BattleInputs inputs)
    {
        // TODO (online mode): POST inputs to server. During dev re-sim all; in prod sample-based.
        _history.Add(record);
        if (record.Won)
            _lifetime.TotalBattlesWon++;
        foreach (var (_, count) in record.EnemiesKilledByHullClass)
            _lifetime.TotalEnemyShipsDestroyed += count;
        return Task.CompletedTask;
    }

    // ── Resource spend tracking ────────────────────────────────────────────────

    public void RecordSalvageSpent(int amount) => _lifetime.TotalSalvageSpent += amount;

    public void RecordTechSpent(int amount) => _lifetime.TotalTechSpent += amount;

    public void RecordCapitalShipBought() => _lifetime.TotalCapitalShipsBought++;

    // ── Run-level events ──────────────────────────────────────────────────────

    public void RecordRunVictory() => _lifetime.TotalRunVictories++;

    // ── Readers ───────────────────────────────────────────────────────────────

    public IReadOnlyList<JumpRecord> GetJumpHistory() => _history;

    public LifetimeStats GetLifetimeStats() => _lifetime;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Reset() => _history.Clear();
}
