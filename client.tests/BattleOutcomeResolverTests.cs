using System.Collections.Generic;
using System.Threading.Tasks;
using Skock.Meta;
using Skock.Sim;
using Skock.Tests.Fakes;
using Xunit;

namespace Skock.Tests;

public sealed class BattleOutcomeResolverTests
{
    private static readonly BattleResult Win = new() { Winner = "fleet_a" };
    private static readonly BattleResult Loss = new() { Winner = "fleet_b" };
    private static readonly BattleResult Draw = new() { Winner = "draw" };
    private static readonly BattleInputs NoInputs = new();
    private static readonly FleetJsonData EmptyFleet = new();

    private static FakeRunData MakeRun(int jumpNumber = 1, int lossCount = 0, int salvage = 0, int tech = 0)
    {
        var run = new FakeRunData
        {
            JumpNumber = jumpNumber,
            LossCount = lossCount,
            Salvage = salvage,
            Tech = tech,
            TierRerolls = [1, 2, 3, 4],
        };
        run.Fleet.Mothership.MaxHp = 500;
        run.Fleet.Mothership.Hp = 500;
        return run;
    }

    private static Task<PostBattleTransition> Resolve(FakeRunData run, BattleResult result) =>
        BattleOutcomeResolver.Resolve(run, result, NoInputs, EmptyFleet, EmptyFleet);

    // ── Salvage ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Win_AwardsBaseAndBonusSalvage()
    {
        var run = MakeRun(jumpNumber: 3);
        await Resolve(run, Win);
        Assert.Equal(3 * 10 + 3 * 15, run.Salvage); // base + win bonus
    }

    [Fact]
    public async Task Loss_AwardsBaseSalvageOnly()
    {
        var run = MakeRun(jumpNumber: 3);
        await Resolve(run, Loss);
        Assert.Equal(3 * 10, run.Salvage);
    }

    // ── Tech ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Win_Jump1_Awards1Tech()
    {
        var run = MakeRun(jumpNumber: 1);
        await Resolve(run, Win);
        Assert.Equal(1, run.Tech);
    }

    [Fact]
    public async Task Win_Jump4_Awards2Tech()
    {
        var run = MakeRun(jumpNumber: 4);
        await Resolve(run, Win);
        Assert.Equal(2, run.Tech);
    }

    [Fact]
    public async Task Win_Jump7_Awards3Tech()
    {
        var run = MakeRun(jumpNumber: 7);
        await Resolve(run, Win);
        Assert.Equal(3, run.Tech);
    }

    [Fact]
    public async Task Loss_AwardsNoTech()
    {
        var run = MakeRun(jumpNumber: 4);
        await Resolve(run, Loss);
        Assert.Equal(0, run.Tech);
    }

    // ── Loss count + defeat ───────────────────────────────────────────────────

    [Fact]
    public async Task Loss_IncrementsLossCount()
    {
        var run = MakeRun(lossCount: 1);
        await Resolve(run, Loss);
        Assert.Equal(2, run.LossCount);
    }

    [Fact]
    public async Task ThirdLoss_ReturnsDefeat()
    {
        var run = MakeRun(lossCount: 2);
        var transition = await Resolve(run, Loss);
        Assert.Equal(PostBattleTransition.Defeat, transition);
    }

    [Fact]
    public async Task ThirdLoss_SetsRunComplete()
    {
        var run = MakeRun(lossCount: 2);
        await Resolve(run, Loss);
        Assert.True(run.IsRunComplete);
        Assert.False(run.HasActiveRun);
    }

    // ── Victory (jump 8) ──────────────────────────────────────────────────────

    [Fact]
    public async Task WinOnJump8_ReturnsVictory()
    {
        var run = MakeRun(jumpNumber: 8);
        var transition = await Resolve(run, Win);
        Assert.Equal(PostBattleTransition.Victory, transition);
    }

    [Fact]
    public async Task WinOnJump8_SetsRunComplete()
    {
        var run = MakeRun(jumpNumber: 8);
        await Resolve(run, Win);
        Assert.True(run.IsRunComplete);
        Assert.False(run.HasActiveRun);
    }

    // ── Normal continue ───────────────────────────────────────────────────────

    [Fact]
    public async Task NormalWin_ReturnsNextJump()
    {
        var run = MakeRun(jumpNumber: 1);
        var transition = await Resolve(run, Win);
        Assert.Equal(PostBattleTransition.NextJump, transition);
    }

    [Fact]
    public async Task NormalWin_IncrementsJumpNumber()
    {
        var run = MakeRun(jumpNumber: 3);
        await Resolve(run, Win);
        Assert.Equal(4, run.JumpNumber);
    }

    [Fact]
    public async Task NormalWin_ResetsTierRerolls()
    {
        var run = MakeRun();
        await Resolve(run, Win);
        Assert.All(run.TierRerolls, v => Assert.Equal(0, v));
    }

    // ── Loss behaviour (retry same jump) ─────────────────────────────────────

    [Fact]
    public async Task Loss_DoesNotIncrementJumpNumber()
    {
        var run = MakeRun(jumpNumber: 3);
        await Resolve(run, Loss);
        Assert.Equal(3, run.JumpNumber);
    }

    [Fact]
    public async Task Loss_ReturnsNextJump_ForDockardRetry()
    {
        var run = MakeRun(jumpNumber: 3, lossCount: 1);
        var transition = await Resolve(run, Loss);
        Assert.Equal(PostBattleTransition.NextJump, transition);
    }

    [Fact]
    public async Task Loss_ResetsTierRerolls()
    {
        var run = MakeRun();
        await Resolve(run, Loss);
        Assert.All(run.TierRerolls, v => Assert.Equal(0, v));
    }

    // ── Draw treated as loss ──────────────────────────────────────────────────

    [Fact]
    public async Task Draw_AwardsBaseSalvageOnly()
    {
        var run = MakeRun(jumpNumber: 3);
        await Resolve(run, Draw);
        Assert.Equal(3 * 10, run.Salvage); // base only — no win bonus
    }

    [Fact]
    public async Task Draw_IncrementsLossCount()
    {
        var run = MakeRun(lossCount: 1);
        await Resolve(run, Draw);
        Assert.Equal(2, run.LossCount);
    }

    // ── HP restoration ────────────────────────────────────────────────────────

    [Fact]
    public async Task Survivors_HaveHpRestored()
    {
        var run = MakeRun();
        run.Fleet.Ships.Add(new ShipDefData { HullClass = HullClass.Corvette, Hp = 60, MaxHp = 60 });
        var result = new BattleResult
        {
            Winner = "fleet_a",
            FleetASurvivors =
            [
                new ShipSurvivor { FleetIndex = 0, Hp = 30, IsMothership = false },
            ],
        };
        await BattleOutcomeResolver.Resolve(run, result, NoInputs, EmptyFleet, EmptyFleet);
        // Survivor HP applied, then auto-heal to MaxHp.
        Assert.Equal(60.0, run.Fleet.Ships[0].Hp);
    }

    [Fact]
    public async Task DestroyedShips_SurviveAt1HpBeforeHeal()
    {
        // Ships absent from the survivor list are treated as destroyed — restored to 1 HP,
        // then auto-healed to MaxHp. Verify that the auto-heal brings them to MaxHp.
        var run = MakeRun();
        run.Fleet.Ships.Add(new ShipDefData { HullClass = HullClass.Corvette, Hp = 60, MaxHp = 60 });
        // No survivors for fleet slot 0 — ship was destroyed mid-battle.
        var result = new BattleResult { Winner = "fleet_a", FleetASurvivors = [] };
        await BattleOutcomeResolver.Resolve(run, result, NoInputs, EmptyFleet, EmptyFleet);
        Assert.Equal(60.0, run.Fleet.Ships[0].Hp); // auto-healed to MaxHp
    }

    [Fact]
    public async Task AllShipsHealedToMaxHpAfterBattle()
    {
        var run = MakeRun();
        run.Fleet.Ships.Add(new ShipDefData { HullClass = HullClass.Frigate, Hp = 10, MaxHp = 130 });
        await Resolve(run, Win);
        Assert.Equal(130.0, run.Fleet.Ships[0].Hp);
        Assert.Equal(500.0, run.Fleet.Mothership.Hp);
    }

    // ── Kill counting ─────────────────────────────────────────────────────────

    [Fact]
    public async Task KilledShips_RecordedInJumpHistory()
    {
        var run = MakeRun();
        var result = new BattleResult
        {
            Winner = "fleet_a",
            FleetBKilled =
            [
                new KilledShip { HullClass = "Corvette", IsMothership = false },
                new KilledShip { HullClass = "Corvette", IsMothership = false },
                new KilledShip { HullClass = "Frigate", IsMothership = false },
                new KilledShip { HullClass = "Destroyer", IsMothership = true }, // mothership excluded
            ],
        };
        await BattleOutcomeResolver.Resolve(run, result, NoInputs, EmptyFleet, EmptyFleet);
        var history = run.Stats.GetJumpHistory();
        Assert.Equal(2, history[0].EnemiesKilledByHullClass["Corvette"]);
        Assert.Equal(1, history[0].EnemiesKilledByHullClass["Frigate"]);
        Assert.False(history[0].EnemiesKilledByHullClass.ContainsKey("Destroyer"));
    }
}
