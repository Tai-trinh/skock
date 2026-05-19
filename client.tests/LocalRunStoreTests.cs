using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Skock.Meta;
using Xunit;

namespace Skock.Tests;

public sealed class LocalRunStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly LocalRunStore _store;

    public LocalRunStoreTests()
    {
        Directory.CreateDirectory(_tempDir);
        _store = new LocalRunStore(
            Path.Combine(_tempDir, "player_state.json"),
            Path.Combine(_tempDir, "player_fleet.json")
        );
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static RunSnapshot BaseSnapshot() =>
        new()
        {
            Salvage = 50,
            Fleet = new FleetJsonData
            {
                Mothership = new ShipDefData { IsMothership = true },
                Ships = [],
            },
        };

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

        var snapshot = BaseSnapshot();
        snapshot.JumpHistory = [record];
        await _store.Save(snapshot);

        var loaded = await _store.Load();

        Assert.NotNull(loaded);
        Assert.Single(loaded!.JumpHistory!);
        Assert.Equal(3, loaded.JumpHistory![0].JumpNumber);
        Assert.True(loaded.JumpHistory[0].Won);
        Assert.Equal("kira", loaded.JumpHistory[0].PlayerAdmiralId);
        Assert.Equal(2, loaded.JumpHistory[0].EnemiesKilledByHullClass["Corvette"]);
    }

    // ── ResearchRerolls + PlayerId persistence ────────────────────────────────

    [Fact]
    public async Task Save_And_Load_RestoresResearchRerollsAndPlayerId()
    {
        var snapshot = BaseSnapshot();
        snapshot.PlayerId = "offline:test-player-id";
        snapshot.ResearchRerolls = [0, 2, 1, 0];
        await _store.Save(snapshot);

        var loaded = await _store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(new[] { 0, 2, 1, 0 }, loaded!.ResearchRerolls);
        Assert.Equal("offline:test-player-id", loaded.PlayerId);
    }
}
