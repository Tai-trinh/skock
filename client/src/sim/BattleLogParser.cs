using System.Buffers;
using System.Collections.Generic;
using System.IO;
using MessagePack;

namespace Skock.Sim;

public static class BattleLogParser
{
    private const uint ExpectedSchemaVersion = 1;

    public static BattleLog Parse(byte[] bytes)
    {
        var options = MessagePackSerializerOptions.Standard;
        var sequence = new ReadOnlySequence<byte>(bytes);
        var reader = new MessagePackReader(sequence);

        var header = MessagePackSerializer.Deserialize<LogHeader>(ref reader, options);
        sequence = sequence.Slice(reader.Consumed);

        if (header.SchemaVersion != ExpectedSchemaVersion)
            throw new InvalidDataException(
                $"Battle log schema version mismatch: expected {ExpectedSchemaVersion}, got {header.SchemaVersion}. "
                    + "Rebuild the sim binary."
            );

        var ticks = new List<TickRecord>();
        while (!sequence.IsEmpty)
        {
            reader = new MessagePackReader(sequence);
            var tick = MessagePackSerializer.Deserialize<TickRecord>(ref reader, options);
            sequence = sequence.Slice(reader.Consumed);
            ticks.Add(tick);
        }

        return new BattleLog(header, [.. ticks]);
    }
}
