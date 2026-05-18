using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Skock.Meta;
using Skock.Tests.Fakes;
using Xunit;

namespace Skock.Tests;

public sealed class LocalRunStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly FakeRunData _run;
    private readonly LocalRunStore _store;

    public LocalRunStoreTests()
    {
        Directory.CreateDirectory(_tempDir);
        _run = new FakeRunData
        {
            PlayerFleetPath = Path.Combine(_tempDir, "player_fleet.json"),
            ProjectDir = _tempDir,
            Salvage = 50,
            Tech = 5,
            HangarCapacity = 10,
        };
        _store = new LocalRunStore(_run);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // ── JumpHistory persistence ───────────────────────────────────────────────

    [Fact]
    public async Task Save_And_Load_RestoresJumpHistory()
    {
        var record = new JumpRecord
        {
            JumpNumber = 3,
            Won = true,
            DurationTicks = 450,
            EnemiesKilledByHullClass = new Dictionary<string, int> { ["Corvette"] = 2 },
            OwnShipsLostByHullClass = new Dictionary<string, int>(),
            DamageDealt = 800f,
            DamageTaken = 200f,
            PlayerFleetSnapshot = new FleetJsonData(),
            OpponentFleetSnapshot = new FleetJsonData(),
            PlayerUpgrades = new Dictionary<string, int>(),
            PlayerAdmiralId = "kira",
        };
        await _run.Stats.RecordBattle(record, new BattleInputs());
        await _store.Save();

        var freshRun = new FakeRunData
        {
            PlayerFleetPath = _run.PlayerFleetPath,
            ProjectDir = _run.ProjectDir,
        };
        var freshStore = new LocalRunStore(freshRun);
        await freshStore.Load();

        var history = freshRun.Stats.GetJumpHistory();
        Assert.Single(history);
        Assert.Equal(3, history[0].JumpNumber);
        Assert.True(history[0].Won);
        Assert.Equal("kira", history[0].PlayerAdmiralId);
        Assert.Equal(2, history[0].EnemiesKilledByHullClass["Corvette"]);
    }

    // ── ResearchRerolls + PlayerId persistence ────────────────────────────────

    [Fact]
    public async Task Save_And_Load_RestoresResearchRerollsAndPlayerId()
    {
        _run.ResearchRerolls = [0, 2, 1, 0];
        _run.PlayerId = "offline:test-player-id";
        await _store.Save();

        var freshRun = new FakeRunData
        {
            PlayerFleetPath = _run.PlayerFleetPath,
            ProjectDir = _run.ProjectDir,
        };
        await new LocalRunStore(freshRun).Load();

        Assert.Equal(new[] { 0, 2, 1, 0 }, freshRun.ResearchRerolls);
        Assert.Equal("offline:test-player-id", freshRun.PlayerId);
    }
}
