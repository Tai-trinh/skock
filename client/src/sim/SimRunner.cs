using System;
using System.Diagnostics;
using System.IO;
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
	private const string ResultPrefix = "RESULT:";

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

		var resultLine = FindResultLine(stderrText);

		if (resultLine is null)
			throw new SimRunException(
				$"Sim exited with code {process.ExitCode} and no RESULT line. stderr:\n{stderrText}"
			);

		return (logBytes, ParseResult(resultLine));
	}

	private static string? FindResultLine(string stderr) =>
		Array.Find(
			stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
			l => l.StartsWith(ResultPrefix)
		)?[ResultPrefix.Length..];

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
		};
	}
}
