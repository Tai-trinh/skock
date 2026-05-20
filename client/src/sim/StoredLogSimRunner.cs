using System.IO;

namespace Skock.Sim;

/// Replays a pre-recorded battle log from disk. Ignores seed and fleet path arguments.
/// Uses System.IO — res:// paths are not supported.
public sealed class StoredLogSimRunner(string msgpackPath) : ISimRunner
{
    public (BattleLog Log, BattleResult Result) Run(
        ulong seed,
        string fleetAPath,
        string fleetBPath
    )
    {
        var bytes = File.ReadAllBytes(msgpackPath);
        var log = BattleLogParser.Parse(bytes);
        var result = new BattleResult
        {
            Winner = "unknown",
            Ticks = (uint)log.Ticks.Length,
            Reason = "loaded_from_file",
        };
        return (log, result);
    }
}
