using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skock.Sim;

public sealed class BattleResult
{
    public string Winner { get; init; } = "";
    public uint Ticks { get; init; }
    public string Reason { get; init; } = "";
}

public sealed class SimRunException(string message) : Exception(message);

public static class SimRunner
{
    /// <summary>
    /// Runs the sim binary and returns the raw MessagePack stdout bytes.
    /// Throws <see cref="SimRunException"/> if the sim exits with an error.
    /// </summary>
    public static (byte[] LogBytes, BattleResult Result) Run(
        string simBinaryPath,
        ulong seed,
        string fleetAPath,
        string fleetBPath,
        string? configPath = null
    )
    {
        var args = $"--seed {seed} \"{fleetAPath}\" \"{fleetBPath}\"";
        if (configPath is not null)
            args += $" --config \"{configPath}\"";

        var psi = new ProcessStartInfo(simBinaryPath, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new SimRunException($"Failed to start sim binary: {simBinaryPath}");

        // Read stdout into memory (battle log) and stderr (result JSON)
        // Read both streams concurrently to avoid deadlock on large output.
        byte[] logBytes = [];
        string stderrText = "";

        var stdoutTask = Task.Run(() =>
        {
            using var ms = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(ms);
            logBytes = ms.ToArray();
        });

        var stderrTask = Task.Run(() => stderrText = process.StandardError.ReadToEnd());

        process.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);

        // stderr may contain compiler warnings before the JSON line — take the last line.
        var resultLine = stderrText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(l => l.StartsWith('{')) ?? "";

        if (process.ExitCode != 0 || string.IsNullOrEmpty(resultLine))
            throw new SimRunException(
                $"Sim exited with code {process.ExitCode}. stderr:\n{stderrText}"
            );

        var result = ParseResult(resultLine);
        if (result.Winner == "")
            throw new SimRunException($"Sim reported an error: {resultLine}");

        return (logBytes, result);
    }

    private static BattleResult ParseResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out _))
            return new BattleResult(); // empty Winner signals error to caller

        return new BattleResult
        {
            Winner = root.GetProperty("winner").GetString() ?? "",
            Ticks = root.GetProperty("ticks").GetUInt32(),
            Reason = root.GetProperty("reason").GetString() ?? "",
        };
    }
}
