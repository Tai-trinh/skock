using System.Collections.Generic;
using Skock.Meta;
using Xunit;

namespace Skock.Tests;

public sealed class LocalStatsStoreTests
{
    private static JumpRecord WonBattle(int jump = 1, int enemiesKilled = 2) =>
        new()
        {
            JumpNumber = jump,
            Won = true,
            DurationTicks = 300,
            EnemiesKilledByHullClass = new Dictionary<string, int> { ["Corvette"] = enemiesKilled },
            OwnShipsLostByHullClass = new Dictionary<string, int>(),
            DamageDealt = 500f,
            DamageTaken = 100f,
            PlayerFleetSnapshot = new FleetJsonData(),
            OpponentFleetSnapshot = new FleetJsonData(),
            PlayerUpgrades = new Dictionary<string, int>(),
            PlayerAdmiralId = "admiral_a",
        };

    private static JumpRecord LostBattle() =>
        new()
        {
            JumpNumber = 1,
            Won = false,
            DurationTicks = 300,
            EnemiesKilledByHullClass = new Dictionary<string, int>(),
            OwnShipsLostByHullClass = new Dictionary<string, int>(),
            DamageDealt = 0f,
            DamageTaken = 500f,
            PlayerFleetSnapshot = new FleetJsonData(),
            OpponentFleetSnapshot = new FleetJsonData(),
            PlayerUpgrades = new Dictionary<string, int>(),
            PlayerAdmiralId = "admiral_a",
        };

    // ── RecordBattle ──────────────────────────────────────────────────────────

    [Fact]
    public void RecordBattle_AddsToHistory()
    {
        var store = new LocalStatsStore();
        store.RecordBattle(WonBattle(), new BattleInputs());
        Assert.Single(store.GetJumpHistory());
    }

    [Fact]
    public void RecordBattle_Win_IncrementsWinCount()
    {
        var store = new LocalStatsStore();
        store.RecordBattle(WonBattle(), new BattleInputs());
        Assert.Equal(1, store.GetLifetimeStats().TotalBattlesWon);
    }

    [Fact]
    public void RecordBattle_Loss_DoesNotIncrementWinCount()
    {
        var store = new LocalStatsStore();
        store.RecordBattle(LostBattle(), new BattleInputs());
        Assert.Equal(0, store.GetLifetimeStats().TotalBattlesWon);
    }

    [Fact]
    public void RecordBattle_CountsEnemiesKilled()
    {
        var store = new LocalStatsStore();
        store.RecordBattle(WonBattle(enemiesKilled: 3), new BattleInputs());
        Assert.Equal(3, store.GetLifetimeStats().TotalEnemyShipsDestroyed);
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsJumpHistory()
    {
        var store = new LocalStatsStore();
        store.RecordBattle(WonBattle(), new BattleInputs());
        store.Reset();
        Assert.Empty(store.GetJumpHistory());
    }

    [Fact]
    public void Reset_DoesNotClearLifetimeStats()
    {
        var store = new LocalStatsStore();
        store.RecordBattle(WonBattle(), new BattleInputs());
        store.Reset();
        Assert.Equal(1, store.GetLifetimeStats().TotalBattlesWon);
    }

    // ── Resource tracking ─────────────────────────────────────────────────────

    [Fact]
    public void RecordSalvageSpent_Accumulates()
    {
        var store = new LocalStatsStore();
        store.RecordSalvageSpent(10);
        store.RecordSalvageSpent(15);
        Assert.Equal(25, store.GetLifetimeStats().TotalSalvageSpent);
    }

    [Fact]
    public void RecordRunVictory_IncrementsCounter()
    {
        var store = new LocalStatsStore();
        store.RecordRunVictory();
        store.RecordRunVictory();
        Assert.Equal(2, store.GetLifetimeStats().TotalRunVictories);
    }
}
