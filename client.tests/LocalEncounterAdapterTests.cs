using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Skock.Meta;
using Xunit;

namespace Skock.Tests;

public sealed class LocalEncounterAdapterTests
{
    private static string? FindEncounterBinary()
    {
        var dir = Path.GetDirectoryName(typeof(LocalEncounterAdapterTests).Assembly.Location)!;
        // Traverse client.tests/bin/{config}/net8.0 → repo root (4 levels up)
        for (var i = 0; i < 4; i++)
            dir = Path.GetDirectoryName(dir)!;

        var release = Path.Combine(dir, "target", "release", "skock-encounter.exe");
        if (File.Exists(release))
            return release;

        var debug = Path.Combine(dir, "target", "debug", "skock-encounter.exe");
        if (File.Exists(debug))
            return debug;

        return null;
    }

    private static LocalEncounterAdapter RequireAdapter()
    {
        var path =
            FindEncounterBinary()
            ?? throw new InvalidOperationException(
                "skock-encounter binary not found — run: cargo build -p encounter --release"
            );
        return new LocalEncounterAdapter(path);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFleetJsonAsync_Jump1_ReturnsDeserializableFleet()
    {
        var adapter = RequireAdapter();

        var json = await adapter.GetFleetJsonAsync("offline:test", runNumber: 1, losses: 0, wins: 0);

        var fleet = JsonSerializer.Deserialize<FleetJsonData>(json);
        Assert.NotNull(fleet);
        Assert.NotEmpty(fleet.Faction);
        Assert.NotNull(fleet.Mothership);
        Assert.NotEmpty(fleet.Ships);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task GetFleetJsonAsync_AllJumps_ReturnValidFleet(int runNumber)
    {
        var adapter = RequireAdapter();

        var json = await adapter.GetFleetJsonAsync(
            "offline:test",
            runNumber,
            losses: 0,
            wins: runNumber - 1
        );

        var fleet = JsonSerializer.Deserialize<FleetJsonData>(json);
        Assert.NotNull(fleet);
        Assert.NotEmpty(fleet.Faction);
        Assert.NotNull(fleet.Mothership);
    }

    // ── Error paths ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public async Task GetFleetJsonAsync_InvalidRunNumber_Throws(int runNumber)
    {
        var adapter = RequireAdapter();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.GetFleetJsonAsync("offline:test", runNumber, losses: 0, wins: 0)
        );
    }

    [Fact]
    public async Task GetFleetJsonAsync_MissingBinary_Throws()
    {
        var adapter = new LocalEncounterAdapter("nonexistent-binary.exe");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.GetFleetJsonAsync("offline:test", runNumber: 1, losses: 0, wins: 0)
        );
    }
}
