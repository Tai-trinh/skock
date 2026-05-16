namespace Skock.Meta;

// Cross-run lifetime counters for achievement tracking.
// TODO (meta-progression): persist to lifetime_stats.json once the achievement system ships.
// TODO (meta-progression): define Achievement class and evaluate conditions against these counters.
//
// Achievement targets identified during design:
//   TotalRunVictories      >= 10   → "Veteran Admiral"
//   TotalBattlesWon        >= 50   → "Battle-Hardened"
//   TotalEnemyShipsDestroyed >= 500 → "Ship-Breaker"
//   TotalSalvageSpent      >= 1000 → "War Economy"
//   TotalCapitalShipsBought >= 10  → "Capital Fleet"
//   TotalTechSpent         >= 100  → "R&D Program"
//   (per-run) total damage taken < 1000 → "Untouchable" — evaluated from JumpRecord totals at RunEnd
public sealed class LifetimeStats
{
    public int TotalRunVictories { get; set; }
    public int TotalBattlesWon { get; set; }
    public int TotalEnemyShipsDestroyed { get; set; }
    public int TotalSalvageSpent { get; set; }
    public int TotalTechSpent { get; set; }
    public int TotalCapitalShipsBought { get; set; }
}
