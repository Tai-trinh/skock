using System.Buffers;
using MessagePack;

namespace Skock.Sim;

public static class BattleLogParser
{
    private const uint ExpectedSchemaVersion = 1;

    /// <summary>
    /// Parses a complete MessagePack battle log from a byte array.
    /// The log is a sequence of top-level MessagePack objects: one LogHeader
    /// followed by one TickRecord per simulated tick.
    /// </summary>
    public static BattleLog Parse(byte[] bytes)
    {
        var options = MessagePackSerializerOptions.Standard;
        var sequence = new ReadOnlySequence<byte>(bytes);

        var header = MessagePackSerializer.Deserialize<LogHeader>(sequence, out var pos, options);
        sequence = sequence.Slice(pos);

        if (header.SchemaVersion != ExpectedSchemaVersion)
            throw new InvalidDataException(
                $"Battle log schema version mismatch: expected {ExpectedSchemaVersion}, got {header.SchemaVersion}. "
                    + "Rebuild the sim binary."
            );

        var ticks = new List<TickRecord>();
        while (!sequence.IsEmpty)
        {
            var tick = MessagePackSerializer.Deserialize<TickRecord>(sequence, out pos, options);
            sequence = sequence.Slice(pos);
            ticks.Add(tick);
        }

        return new BattleLog(header, [.. ticks]);
    }
}
