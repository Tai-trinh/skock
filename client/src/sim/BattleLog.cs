using MessagePack;

namespace Skock.Sim;

[MessagePackObject]
public sealed class LogHeader
{
    [Key("schema_version")]
    public uint SchemaVersion { get; init; }
}

/// <summary>
/// One record per sim tick — full state snapshot plus any events that fired.
/// Unknown map keys (projectiles, beams, events) are silently ignored until implemented.
/// </summary>
[MessagePackObject]
public sealed class TickRecord
{
    [Key("tick")]
    public uint Tick { get; init; }

    [Key("ships")]
    public ShipSnapshot[] Ships { get; init; } = [];
}

[MessagePackObject]
public sealed class ShipSnapshot
{
    [Key("id")]
    public uint Id { get; init; }

    /// <summary>0 = fleet A, 1 = fleet B.</summary>
    [Key("fleet")]
    public byte Fleet { get; init; }

    [Key("blueprint_drawing_id")]
    public string BlueprintDrawingId { get; init; } = "";

    [Key("is_mothership")]
    public bool IsMothership { get; init; }

    /// <summary>I32F32 raw bits — 32 fractional bits.</summary>
    [Key("pos_x")]
    public long PosX { get; init; }

    [Key("pos_y")]
    public long PosY { get; init; }

    /// <summary>I16F16 raw bits — 16 fractional bits.</summary>
    [Key("heading")]
    public int Heading { get; init; }

    [Key("hp")]
    public int Hp { get; init; }

    [Key("max_hp")]
    public int MaxHp { get; init; }

    [Key("shield_hp")]
    public int ShieldHp { get; init; }

    [Key("shield_max_hp")]
    public int ShieldMaxHp { get; init; }

    // ── Fixed-point → float helpers ──────────────────────────────────────────

    [IgnoreMember]
    public float WorldX => (float)(PosX / 4_294_967_296.0); // 2^32

    [IgnoreMember]
    public float WorldY => (float)(PosY / 4_294_967_296.0);

    [IgnoreMember]
    public float HeadingRad => Heading / 65_536f; // 2^16

    [IgnoreMember]
    public float HpFraction => MaxHp > 0 ? (float)Hp / MaxHp : 0f;
}

public sealed class BattleLog
{
    public LogHeader Header { get; }
    public TickRecord[] Ticks { get; }

    public BattleLog(LogHeader header, TickRecord[] ticks)
    {
        Header = header;
        Ticks = ticks;
    }
}
