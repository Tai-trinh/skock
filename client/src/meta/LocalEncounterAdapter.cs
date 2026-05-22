using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skock.Meta;

// Offline encounter source — spawns skock-encounter, sends the request, reads back
// the fleet JSON, then the process exits.
//
// TODO (online mode): implement ServerEncounterAdapter for the same contract.
// Swap point: RunState._encounterSource in _Ready().
public sealed class LocalEncounterAdapter : IEncounterSource
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = null };

    private readonly string _binaryPath;

    public LocalEncounterAdapter(string binaryPath) => _binaryPath = binaryPath;

    public async Task<string> GetFleetJsonAsync(
        string playerId,
        int runNumber,
        int losses,
        int wins
    )
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

        Process process;
        try
        {
            process =
                Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start skock-encounter");
        }
        catch (Exception e) when (e is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to start skock-encounter: {e.Message}", e);
        }

        using var _ = process;

        var request = JsonSerializer.Serialize(
            new
            {
                player_id = playerId,
                run_number = runNumber,
                losses,
                wins,
            },
            JsonOpts
        );
        await process.StandardInput.WriteLineAsync(request);
        process.StandardInput.Close();

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"skock-encounter exited with code {process.ExitCode}"
            );

        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException("skock-encounter closed stdout without a response");

        return output;
    }
}
