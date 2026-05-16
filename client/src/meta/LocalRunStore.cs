using System;
using System.IO;
using System.Text.Json;

namespace Skock.Meta;

// Offline IRunStore — reads/writes local JSON files, no network calls.
// TODO (online mode): implement ServerRunStore for the same interface; swap in RunState._Ready().
public sealed class LocalRunStore : IRunStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly RunState _run;

    public LocalRunStore(RunState run) => _run = run;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Load()
    {
        // TODO (online mode): fetch run state from server by Run ID instead.
        // Server is authoritative; local file is a cache. See CONTEXT.md § "Run state (online mode)".
        var statePath = StatePath();
        if (!File.Exists(statePath) || !File.Exists(_run.PlayerFleetPath))
            return;

        try
        {
            var saved = JsonSerializer.Deserialize<SavedState>(File.ReadAllText(statePath), JsonOptions);
            var fleet = JsonSerializer.Deserialize<FleetJsonData>(File.ReadAllText(_run.PlayerFleetPath), JsonOptions);
            if (saved is null || fleet is null)
                return;

            _run.Salvage       = saved.Salvage;
            _run.Tech          = saved.Tech;
            _run.HangarCapacity = saved.HangarCapacity;
            _run.JumpNumber    = Math.Max(1, saved.JumpNumber);
            _run.LossCount     = saved.LossCount;
            _run.AdmiralId     = saved.AdmiralId;
            fleet.Mothership.IsMothership = true;
            _run.Fleet         = fleet;
            _run.TierRerolls   = saved.TierRerolls ?? new int[4];
            _run.HasActiveRun  = !saved.IsComplete;
        }
        catch
        {
            // corrupt save — keep defaults
        }
    }

    public void Save()
    {
        // TODO (online mode): also sync run state to server after writing locally.
        File.WriteAllText(_run.PlayerFleetPath, JsonSerializer.Serialize(_run.Fleet, JsonOptions));
        File.WriteAllText(StatePath(), JsonSerializer.Serialize(new SavedState
        {
            Salvage        = _run.Salvage,
            Tech           = _run.Tech,
            HangarCapacity = _run.HangarCapacity,
            JumpNumber     = _run.JumpNumber,
            LossCount      = _run.LossCount,
            AdmiralId      = _run.AdmiralId,
            TierRerolls    = _run.TierRerolls,
            IsComplete     = _run.IsRunComplete,
        }, JsonOptions));
    }

    public void DeleteSave()
    {
        // TODO (online mode): DELETE /runs/{RunId} on server.
        if (File.Exists(StatePath()))        File.Delete(StatePath());
        if (File.Exists(_run.PlayerFleetPath)) File.Delete(_run.PlayerFleetPath);
    }

    // ── Run start ─────────────────────────────────────────────────────────────

    public void StartRun(Admiral admiral)
    {
        // TODO (online mode): POST /runs to create a server-side run record; receive Run ID.
        _run.Salvage        = admiral.StartingSalvage;
        _run.Tech           = admiral.StartingTech;
        _run.HangarCapacity = admiral.StartingHangarCapacity;
        _run.JumpNumber     = 1;
        _run.LossCount      = 0;
        _run.AdmiralId      = admiral.Id;
        _run.Fleet          = admiral.StartingFleet;
        _run.TierRerolls    = new int[4];
        _run.HasActiveRun   = true;
        _run.IsRunComplete  = false;
        Save();
    }

    // ── Dockyard actions ──────────────────────────────────────────────────────

    public bool CommissionShip(Blueprint bp)
    {
        // TODO (online mode): POST /runs/{RunId}/actions/commission — server validates before applying.
        if (_run.Salvage < bp.SalvageCost || _run.FreeTonnage < bp.Tonnage)
            return false;
        _run.Fleet.Ships.Add(bp.Instantiate());
        _run.Salvage -= bp.SalvageCost;
        Save();
        return true;
    }

    public int SalvageShip(int index)
    {
        // TODO (online mode): POST /runs/{RunId}/actions/salvage — server validates before applying.
        if (index >= _run.Fleet.Ships.Count)
            return -1;
        // Mothership lives in Fleet.Mothership, never in Fleet.Ships — this is a defensive guard.
        if (_run.Fleet.Ships[index].IsMothership)
            return -1;
        var ship = _run.Fleet.Ships[index];
        var yield = ship.HullClass.Tonnage() * 3;
        _run.Fleet.Ships.RemoveAt(index);
        _run.Salvage += yield;
        Save();
        return yield;
    }

    public bool RerollTier(int tierIndex, int cost)
    {
        // TODO (online mode): POST /runs/{RunId}/actions/reroll — server validates before applying.
        if (_run.Salvage < cost)
            return false;
        _run.Salvage -= cost;
        _run.TierRerolls[tierIndex]++;
        Save();
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string StatePath() =>
        Path.GetFullPath(Path.Combine(_run.ProjectDir, "..", "player_state.json"));

    private sealed class SavedState
    {
        public int      Salvage        { get; set; }
        public int      Tech           { get; set; }
        public int      HangarCapacity { get; set; }
        public int      JumpNumber     { get; set; }
        public int      LossCount      { get; set; }
        public string   AdmiralId      { get; set; } = "";
        public int[]?   TierRerolls    { get; set; }
        public bool     IsComplete     { get; set; }
    }
}
