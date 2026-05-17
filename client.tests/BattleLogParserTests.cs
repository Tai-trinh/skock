using System.IO;
using MessagePack;
using Skock.Sim;
using Xunit;

namespace Skock.Tests;

public sealed class BattleLogParserTests
{
    private static readonly MessagePackSerializerOptions Options =
        MessagePackSerializerOptions.Standard;

    // Builds a raw MessagePack byte sequence in the same format as the sim:
    // one LogHeader record followed by zero or more TickRecord records.
    private static byte[] BuildLog(params TickRecord[] ticks)
    {
        var header = new LogHeader { SchemaVersion = 1 };
        using var ms = new System.IO.MemoryStream();
        ms.Write(MessagePackSerializer.Serialize(header, Options));
        foreach (var tick in ticks)
            ms.Write(MessagePackSerializer.Serialize(tick, Options));
        return ms.ToArray();
    }

    [Fact]
    public void Parse_NoTicks_ReturnsEmptyLog()
    {
        var log = BattleLogParser.Parse(BuildLog());
        Assert.Empty(log.Ticks);
        Assert.Equal(1u, log.Header.SchemaVersion);
    }

    [Fact]
    public void Parse_SingleTick_ReturnsTick()
    {
        var tick = new TickRecord
        {
            Tick = 7,
            Ships =
            [
                new ShipSnapshot
                {
                    Id = 3,
                    Fleet = 0,
                    BlueprintDrawingId = "fighter_a",
                    IsMothership = false,
                    Hp = 65_536,
                    MaxHp = 65_536,
                },
            ],
        };

        var log = BattleLogParser.Parse(BuildLog(tick));

        Assert.Single(log.Ticks);
        Assert.Equal(7u, log.Ticks[0].Tick);
        Assert.Single(log.Ticks[0].Ships);
        var ship = log.Ticks[0].Ships[0];
        Assert.Equal(3u, ship.Id);
        Assert.Equal(0, ship.Fleet);
        Assert.Equal("fighter_a", ship.BlueprintDrawingId);
        Assert.False(ship.IsMothership);
    }

    [Fact]
    public void Parse_MultipleTicks_PreservesOrder()
    {
        var log = BattleLogParser.Parse(
            BuildLog(
                new TickRecord { Tick = 1, Ships = [] },
                new TickRecord { Tick = 2, Ships = [] },
                new TickRecord { Tick = 3, Ships = [] }
            )
        );

        Assert.Equal(3, log.Ticks.Length);
        Assert.Equal(1u, log.Ticks[0].Tick);
        Assert.Equal(2u, log.Ticks[1].Tick);
        Assert.Equal(3u, log.Ticks[2].Tick);
    }

    [Fact]
    public void Parse_WrongSchemaVersion_Throws()
    {
        var badHeader = MessagePackSerializer.Serialize(
            new LogHeader { SchemaVersion = 99 },
            Options
        );
        Assert.Throws<InvalidDataException>(() => BattleLogParser.Parse(badHeader));
    }

    // ── ShipSnapshot fixed-point helpers ──────────────────────────────────────

    [Fact]
    public void HpFraction_FullHp_ReturnsOne()
    {
        var snap = new ShipSnapshot { Hp = 6_553_600, MaxHp = 6_553_600 };
        Assert.Equal(1f, snap.HpFraction);
    }

    [Fact]
    public void HpFraction_HalfHp_ReturnsHalf()
    {
        var snap = new ShipSnapshot { Hp = 3_276_800, MaxHp = 6_553_600 };
        Assert.Equal(0.5f, snap.HpFraction, precision: 5);
    }

    [Fact]
    public void HpFraction_ZeroMaxHp_ReturnsZero()
    {
        var snap = new ShipSnapshot { Hp = 0, MaxHp = 0 };
        Assert.Equal(0f, snap.HpFraction);
    }

    [Fact]
    public void WorldX_OneSimUnit_ReturnsOne()
    {
        // I32F32: 1.0 = 1 << 32 = 4_294_967_296
        var snap = new ShipSnapshot { PosX = 4_294_967_296L };
        Assert.Equal(1f, snap.WorldX, precision: 5);
    }

    [Fact]
    public void HeadingRad_OneRadian_ReturnsOne()
    {
        // I16F16: 1.0 = 1 << 16 = 65_536
        var snap = new ShipSnapshot { Heading = 65_536 };
        Assert.Equal(1f, snap.HeadingRad, precision: 5);
    }
}
