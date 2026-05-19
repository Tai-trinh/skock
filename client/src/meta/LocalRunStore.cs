using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skock.Meta;

// Offline IRunStore — reads/writes local JSON files, no network calls.
// TODO (online mode): implement ServerRunStore for the same interface; swap in RunState._Ready().
public sealed class LocalRunStore : IRunStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _statePath;
    private readonly string _fleetPath;

    public LocalRunStore(string statePath, string fleetPath)
    {
        _statePath = statePath;
        _fleetPath = fleetPath;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public Task<RunSnapshot?> Load()
    {
        // TODO (online mode): fetch run state from server by Run ID instead.
        if (!File.Exists(_statePath) || !File.Exists(_fleetPath))
            return Task.FromResult<RunSnapshot?>(null);

        try
        {
            var snapshot = JsonSerializer.Deserialize<RunSnapshot>(
                File.ReadAllText(_statePath),
                JsonOptions
            );
            var fleet = JsonSerializer.Deserialize<FleetJsonData>(
                File.ReadAllText(_fleetPath),
                JsonOptions
            );
            if (snapshot is null || fleet is null)
                return Task.FromResult<RunSnapshot?>(null);

            fleet.Mothership.IsMothership = true;
            snapshot.Fleet = fleet;
            return Task.FromResult<RunSnapshot?>(snapshot);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[LocalRunStore] Failed to load save ({ex.GetType().Name}): {ex.Message}"
            );
            return Task.FromResult<RunSnapshot?>(null);
        }
    }

    public Task Save(RunSnapshot snapshot)
    {
        // TODO (online mode): also sync run state to server after writing locally.
        File.WriteAllText(_fleetPath, JsonSerializer.Serialize(snapshot.Fleet, JsonOptions));
        File.WriteAllText(_statePath, JsonSerializer.Serialize(snapshot, JsonOptions));
        return Task.CompletedTask;
    }

    public Task Delete()
    {
        // TODO (online mode): DELETE /runs/{RunId} on server.
        if (File.Exists(_statePath))
            File.Delete(_statePath);
        if (File.Exists(_fleetPath))
            File.Delete(_fleetPath);
        return Task.CompletedTask;
    }

    // ── Run start ─────────────────────────────────────────────────────────────

    public Task<RunSnapshot> StartRun(Admiral admiral)
    {
        // TODO (online mode): POST /runs to create a server-side run record; receive Run ID.
        return Task.FromResult(
            new RunSnapshot
            {
                RunSeed = (ulong)Random.Shared.NextInt64(),
                Salvage = admiral.StartingSalvage,
                Tech = admiral.StartingTech,
                HangarCapacity = admiral.StartingHangarCapacity,
                JumpNumber = 1,
                LossCount = 0,
                AdmiralId = admiral.Id,
                Fleet = admiral.StartingFleet,
                TierRerolls = new int[4],
                ResearchRerolls = new int[4],
                UpgradePurchases = new Dictionary<string, int>(),
                IsComplete = false,
                JumpHistory = [],
            }
        );
    }

    // ── Battle ────────────────────────────────────────────────────────────────

    public Task<ulong> GetBattleSeed()
    {
        // TODO (online mode): POST /runs/{RunId}/battles to get a server-assigned seed.
        return Task.FromResult((ulong)Random.Shared.NextInt64());
    }
}
