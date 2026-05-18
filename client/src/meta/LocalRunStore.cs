using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skock.Meta;

// Offline IRunStore — reads/writes local JSON files, no network calls.
// TODO (online mode): implement ServerRunStore for the same interface; swap in RunState._Ready().
public sealed class LocalRunStore : IRunStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly IRunData _run;

    public LocalRunStore(IRunData run) => _run = run;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public Task Load()
    {
        // TODO (online mode): fetch run state from server by Run ID instead.
        var statePath = StatePath();
        if (!File.Exists(statePath) || !File.Exists(_run.PlayerFleetPath))
            return Task.CompletedTask;

        try
        {
            var saved = JsonSerializer.Deserialize<SavedState>(
                File.ReadAllText(statePath),
                JsonOptions
            );
            var fleet = JsonSerializer.Deserialize<FleetJsonData>(
                File.ReadAllText(_run.PlayerFleetPath),
                JsonOptions
            );
            if (saved is null || fleet is null)
                return Task.CompletedTask;

            _run.RunSeed = saved.RunSeed;
            _run.Salvage = saved.Salvage;
            _run.Tech = saved.Tech;
            _run.HangarCapacity = saved.HangarCapacity;
            _run.JumpNumber = Math.Max(1, saved.JumpNumber);
            _run.LossCount = saved.LossCount;
            _run.AdmiralId = saved.AdmiralId;
            fleet.Mothership.IsMothership = true;
            _run.Fleet = fleet;
            _run.TierRerolls = saved.TierRerolls ?? new int[4];
            _run.ResearchRerolls = saved.ResearchRerolls ?? new int[4];
            _run.UpgradePurchases = saved.UpgradePurchases ?? new Dictionary<string, int>();
            _run.PlayerId = saved.PlayerId ?? GenerateOfflinePlayerId();
            _run.HasActiveRun = !saved.IsComplete;
            if (saved.JumpHistory is { Count: > 0 })
                _run.Stats.LoadHistory(saved.JumpHistory);
        }
        catch
        {
            // corrupt save — keep defaults
        }

        return Task.CompletedTask;
    }

    public Task Save()
    {
        // TODO (online mode): also sync run state to server after writing locally.
        File.WriteAllText(
            _run.PlayerFleetPath,
            JsonSerializer.Serialize(BuildFleetForSim(), JsonOptions)
        );
        File.WriteAllText(
            StatePath(),
            JsonSerializer.Serialize(
                new SavedState
                {
                    RunSeed = _run.RunSeed,
                    Salvage = _run.Salvage,
                    Tech = _run.Tech,
                    HangarCapacity = _run.HangarCapacity,
                    JumpNumber = _run.JumpNumber,
                    LossCount = _run.LossCount,
                    AdmiralId = _run.AdmiralId,
                    TierRerolls = _run.TierRerolls,
                    ResearchRerolls = _run.ResearchRerolls,
                    UpgradePurchases = _run.UpgradePurchases,
                    PlayerId = _run.PlayerId,
                    IsComplete = _run.IsRunComplete,
                    JumpHistory = [.. _run.Stats.GetJumpHistory()],
                },
                JsonOptions
            )
        );
        return Task.CompletedTask;
    }

    public Task DeleteSave()
    {
        // TODO (online mode): DELETE /runs/{RunId} on server.
        if (File.Exists(StatePath()))
            File.Delete(StatePath());
        if (File.Exists(_run.PlayerFleetPath))
            File.Delete(_run.PlayerFleetPath);
        return Task.CompletedTask;
    }

    // ── Run start ─────────────────────────────────────────────────────────────

    public async Task StartRun(Admiral admiral)
    {
        // TODO (online mode): POST /runs to create a server-side run record; receive Run ID.
        _run.RunSeed = (ulong)Random.Shared.NextInt64();
        _run.Salvage = admiral.StartingSalvage;
        _run.Tech = admiral.StartingTech;
        _run.HangarCapacity = admiral.StartingHangarCapacity;
        _run.JumpNumber = 1;
        _run.LossCount = 0;
        _run.AdmiralId = admiral.Id;
        _run.Fleet = admiral.StartingFleet;
        _run.TierRerolls = new int[4];
        _run.ResearchRerolls = new int[4];
        _run.UpgradePurchases = new Dictionary<string, int>();
        _run.HasActiveRun = true;
        _run.IsRunComplete = false;
        _run.Stats.Reset();
        await Save();
    }

    // ── Battle ────────────────────────────────────────────────────────────────

    public Task<ulong> GetBattleSeed()
    {
        // TODO (online mode): POST /runs/{RunId}/battles to get a server-assigned seed.
        return Task.FromResult((ulong)Random.Shared.NextInt64());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private FleetJsonData BuildFleetForSim()
    {
        var effects = _run.Catalog.FindAdmiral(_run.AdmiralId)?.ShipEffects ?? [];

        var clonedShips = _run
            .Fleet.Ships.Select(s =>
            {
                var clone = s.Clone();
                foreach (var effect in effects)
                    if (effect.Matches(clone))
                        effect.Apply(clone);
                return clone;
            })
            .ToList();

        return new FleetJsonData
        {
            Faction = _run.Fleet.Faction,
            AdmiralId = _run.Fleet.AdmiralId,
            Formation = _run.Fleet.Formation,
            Mothership = _run.Fleet.Mothership.Clone(),
            Ships = clonedShips,
        };
    }

    private string StatePath() =>
        Path.GetFullPath(Path.Combine(_run.ProjectDir, "..", "player_state.json"));

    private static string GenerateOfflinePlayerId() => $"offline:{Guid.NewGuid()}";

    private sealed class SavedState
    {
        public ulong RunSeed { get; set; }
        public int Salvage { get; set; }
        public int Tech { get; set; }
        public int HangarCapacity { get; set; }
        public int JumpNumber { get; set; }
        public int LossCount { get; set; }
        public string AdmiralId { get; set; } = "";
        public int[]? TierRerolls { get; set; }
        public int[]? ResearchRerolls { get; set; }
        public Dictionary<string, int>? UpgradePurchases { get; set; }
        public string? PlayerId { get; set; }
        public bool IsComplete { get; set; }
        public List<JumpRecord>? JumpHistory { get; set; }
    }
}
