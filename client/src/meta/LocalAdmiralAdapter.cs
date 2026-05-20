using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skock.Meta;

// Offline admiral offer source — spawns skock-admiral, sends the player_id request,
// reads back the offer JSON, then the process exits.
//
// TODO (online mode): implement ServerAdmiralAdapter for the same contract.
// Swap point: AdmiralSelectUi.LoadOffersAsync().
public sealed class LocalAdmiralAdapter
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = null };

    private readonly string _binaryPath;

    public LocalAdmiralAdapter(string binaryPath) => _binaryPath = binaryPath;

    public async Task<AdmiralOffersResult> GetOffersAsync(string playerId)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _binaryPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            // Leave stderr unredir — binary errors surface in the Godot console.
        };

        using var process =
            Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start skock-admiral");

        var request = JsonSerializer.Serialize(new { player_id = playerId }, JsonOpts);
        await process.StandardInput.WriteLineAsync(request);
        process.StandardInput.Close();

        var line =
            await process.StandardOutput.ReadLineAsync()
            ?? throw new InvalidOperationException(
                "skock-admiral closed stdout without a response"
            );

        await process.WaitForExitAsync();

        return JsonSerializer.Deserialize<AdmiralOffersResult>(line, JsonOpts)
            ?? throw new InvalidOperationException("Null response from skock-admiral");
    }
}
