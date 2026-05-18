using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skock.Meta;

// Offline IDockyard — spawns skock-dockyard as a subprocess and drives the
// stdin/stdout JSON protocol for the full dockyard session.
//
// TODO (online mode): implement ServerDockyardAdapter for the same interface.
// Swap point: RunState.OpenDockyardSession().
public sealed class LocalDockyardAdapter : IDockyard
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = null, // fields use explicit JsonPropertyName attributes
    };

    private readonly Process _process;
    private readonly StreamWriter _stdin;
    private readonly StreamReader _stdout;
    private bool _disposed;

    public LocalDockyardAdapter(string binaryPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = binaryPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            // Leave stderr unredir — binary errors surface in the Godot console.
        };
        _process =
            Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start skock-dockyard");
        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;
    }

    // ── IDockyard ─────────────────────────────────────────────────────────────

    public async Task<DockOffersResult> GetOffersAsync(DockSessionInput input)
    {
        var payload = new GetOffersEnvelope(input);
        var raw = await SendReceiveAsync(payload);
        var result =
            JsonSerializer.Deserialize<DockOffersResult>(raw, JsonOpts)
            ?? throw new InvalidOperationException("Null response from dockyard binary");
        return result;
    }

    public async Task<DockCommissionResult> CommissionAsync(string blueprintId)
    {
        var raw = await SendReceiveAsync(new { action = "commission", blueprint_id = blueprintId });
        return Deserialize<DockCommissionResult>(raw);
    }

    public async Task<DockSalvageResult> SalvageFleetShipAsync(int index)
    {
        var raw = await SendReceiveAsync(new { action = "salvage_fleet_ship", index });
        return Deserialize<DockSalvageResult>(raw);
    }

    public async Task<DockTierResult> RerollTierAsync(int tierIndex)
    {
        var raw = await SendReceiveAsync(new { action = "reroll_tier", tier_index = tierIndex });
        return Deserialize<DockTierResult>(raw);
    }

    public async Task<DockTrackResult> RerollResearchAsync(int trackIndex)
    {
        var raw = await SendReceiveAsync(
            new { action = "reroll_research", track_index = trackIndex }
        );
        return Deserialize<DockTrackResult>(raw);
    }

    public async Task<DockActionResult> BuyResearchAsync(string upgradeId)
    {
        var raw = await SendReceiveAsync(new { action = "buy_research", upgrade_id = upgradeId });
        return Deserialize<DockActionResult>(raw);
    }

    public async Task<DockSessionDelta> ShoppingDoneAsync()
    {
        var raw = await SendReceiveAsync(new { action = "shopping_done" });
        return Deserialize<DockSessionDelta>(raw);
    }

    // ── IAsyncDisposable ──────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            _stdin.Close();
        }
        catch { }
        await _process.WaitForExitAsync();
        _process.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> SendReceiveAsync(object message)
    {
        var json = JsonSerializer.Serialize(message, JsonOpts);
        await _stdin.WriteLineAsync(json);
        await _stdin.FlushAsync();
        return await _stdout.ReadLineAsync()
            ?? throw new InvalidOperationException("skock-dockyard closed stdout unexpectedly");
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOpts)
        ?? throw new InvalidOperationException($"Null {typeof(T).Name} from dockyard binary");

    // Wraps DockSessionInput in the envelope that adds "action": "get_offers".
    // Using a wrapper avoids modifying DockSessionInput with an extra property.
    private sealed class GetOffersEnvelope
    {
        [System.Text.Json.Serialization.JsonPropertyName("action")]
        public string Action => "get_offers";

        [System.Text.Json.Serialization.JsonPropertyName("player_id")]
        public string PlayerId { get; }

        [System.Text.Json.Serialization.JsonPropertyName("run_seed")]
        public ulong RunSeed { get; }

        [System.Text.Json.Serialization.JsonPropertyName("jump_number")]
        public int JumpNumber { get; }

        [System.Text.Json.Serialization.JsonPropertyName("tier_rerolls")]
        public int[] TierRerolls { get; }

        [System.Text.Json.Serialization.JsonPropertyName("research_rerolls")]
        public int[] ResearchRerolls { get; }

        [System.Text.Json.Serialization.JsonPropertyName("salvage")]
        public int Salvage { get; }

        [System.Text.Json.Serialization.JsonPropertyName("tech")]
        public int Tech { get; }

        [System.Text.Json.Serialization.JsonPropertyName("hangar_used")]
        public int HangarUsed { get; }

        [System.Text.Json.Serialization.JsonPropertyName("hangar_cap")]
        public int HangarCap { get; }

        [System.Text.Json.Serialization.JsonPropertyName("fleet")]
        public System.Collections.Generic.List<FleetShipRef> Fleet { get; }

        [System.Text.Json.Serialization.JsonPropertyName("upgrade_purchases")]
        public System.Collections.Generic.Dictionary<string, int> UpgradePurchases { get; }

        public GetOffersEnvelope(DockSessionInput input)
        {
            PlayerId = input.PlayerId;
            RunSeed = input.RunSeed;
            JumpNumber = input.JumpNumber;
            TierRerolls = input.TierRerolls;
            ResearchRerolls = input.ResearchRerolls;
            Salvage = input.Salvage;
            Tech = input.Tech;
            HangarUsed = input.HangarUsed;
            HangarCap = input.HangarCap;
            Fleet = input.Fleet;
            UpgradePurchases = input.UpgradePurchases;
        }
    }
}
