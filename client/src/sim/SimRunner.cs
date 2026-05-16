using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skock.Sim;

public sealed class ShipSurvivor
{
    public string BlueprintDrawingId { get; init; } = "";
    public float Hp { get; init; }
    public bool IsMothership { get; init; }
}

public sealed class KilledShip
{
    public string HullClass { get; init; } = "";
    public bool IsMothership { get; init; }
}

public sealed class BattleResult
{
    public string Winner { get; init; } = "";
    public uint Ticks { get; init; }
    public string Reason { get; init; } = "";
    public IReadOnlyList<ShipSurvivor> FleetASurvivors { get; init; } = [];
    public IReadOnlyList<ShipSurvivor> FleetBSurvivors { get; init; } = [];
    public IReadOnlyList<KilledShip> FleetAKilled { get; init; } = [];
    public IReadOnlyList<KilledShip> FleetBKilled { get; init; } = [];
    public float FleetADamageDealt { get; init; }
    public float FleetBDamageDealt { get; init; }
}

public sealed class SimRunException(string message) : Exception(message);

public static class SimRunner
{
    private const string ResultPrefix = "RESULT:";

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

        using var process =
            Process.Start(psi)
            ?? throw new SimRunException($"Failed to start sim binary: {simBinaryPath}");

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

        var resultLine = FindResultLine(stderrText);

        if (resultLine is null)
            throw new SimRunException(
                $"Sim exited with code {process.ExitCode} and no RESULT line. stderr:\n{stderrText}"
            );

        return (logBytes, ParseResult(resultLine));
    }

    private static string? FindResultLine(string stderr) =>
        Array
            .Find(
                stderr.Split(
                    '\n',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ),
                l => l.StartsWith(ResultPrefix)
            )
            ?[ResultPrefix.Length..];

    private static BattleResult ParseResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errorCode))
        {
            var message = root.TryGetProperty("message", out var msg) ? msg.GetString() : null;
            throw new SimRunException($"Sim error [{errorCode.GetString()}]: {message}");
        }

        return new BattleResult
        {
            Winner = root.GetProperty("winner").GetString() ?? "",
            Ticks = root.GetProperty("ticks").GetUInt32(),
            Reason = root.GetProperty("reason").GetString() ?? "",
            FleetASurvivors = ParseSurvivors(root, "fleet_a_survivors"),
            FleetBSurvivors = ParseSurvivors(root, "fleet_b_survivors"),
            FleetAKilled = ParseKilled(root, "fleet_a_killed"),
            FleetBKilled = ParseKilled(root, "fleet_b_killed"),
            FleetADamageDealt = root.TryGetProperty("fleet_a_damage_dealt", out var ad)
                ? ad.GetSingle()
                : 0f,
            FleetBDamageDealt = root.TryGetProperty("fleet_b_damage_dealt", out var bd)
                ? bd.GetSingle()
                : 0f,
        };
    }

    private static IReadOnlyList<ShipSurvivor> ParseSurvivors(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var arr))
            return [];
        var list = new List<ShipSurvivor>(arr.GetArrayLength());
        foreach (var item in arr.EnumerateArray())
        {
            list.Add(
                new ShipSurvivor
                {
                    BlueprintDrawingId = item.GetProperty("blueprint_drawing_id").GetString() ?? "",
                    Hp = item.GetProperty("hp").GetSingle(),
                    IsMothership =
                        item.TryGetProperty("is_mothership", out var ms) && ms.GetBoolean(),
                }
            );
        }
        return list;
    }

    private static IReadOnlyList<KilledShip> ParseKilled(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var arr))
            return [];
        var list = new List<KilledShip>(arr.GetArrayLength());
        foreach (var item in arr.EnumerateArray())
        {
            list.Add(
                new KilledShip
                {
                    HullClass = item.GetProperty("hull_class").GetString() ?? "",
                    IsMothership =
                        item.TryGetProperty("is_mothership", out var ms) && ms.GetBoolean(),
                }
            );
        }
        return list;
    }
}
