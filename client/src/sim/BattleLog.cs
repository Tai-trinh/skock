using MessagePack;

namespace Skock.Sim;

[MessagePackObject]
public sealed class LogHeader
{
    [Key("schema_version")]
    public uint SchemaVersion { get; init; }
}

[MessagePackObject]
public sealed class TickRecord
{
    [Key("tick")]
    public uint Tick { get; init; }

    [Key("ships")]
    public ShipSnapshot[] Ships { get; init; } = [];

    [Key("projectiles")]
    public ProjectileSnapshot[] Projectiles { get; init; } = [];

    [Key("beams")]
    public BeamSnapshot[] Beams { get; init; } = [];

    [Key("events")]
    public LogEvent[] Events { get; init; } = [];
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

    [IgnoreMember]
    public float WorldX => (float)(PosX / 4_294_967_296.0);

    [IgnoreMember]
    public float WorldY => (float)(PosY / 4_294_967_296.0);

    [IgnoreMember]
    public float HeadingRad => Heading / 65_536f;

    [IgnoreMember]
    public float HpFraction => MaxHp > 0 ? (float)Hp / MaxHp : 0f;
}

[MessagePackObject]
public sealed class ProjectileSnapshot
{
    [Key("id")]
    public uint Id { get; init; }

    [Key("fleet")]
    public byte Fleet { get; init; }

    [Key("pos_x")]
    public long PosX { get; init; }

    [Key("pos_y")]
    public long PosY { get; init; }

    [Key("heading")]
    public int Heading { get; init; }

    /// <summary>0 = seeking_missile, 1 = torpedo, 2 = mine</summary>
    [Key("subtype")]
    public byte Subtype { get; init; }

    [IgnoreMember]
    public float WorldX => (float)(PosX / 4_294_967_296.0);

    [IgnoreMember]
    public float WorldY => (float)(PosY / 4_294_967_296.0);

    [IgnoreMember]
    public float HeadingRad => Heading / 65_536f;
}

[MessagePackObject]
public sealed class BeamSnapshot
{
    [Key("id")]
    public uint Id { get; init; }

    [Key("fleet")]
    public byte Fleet { get; init; }

    [Key("source_pos_x")]
    public long SourcePosX { get; init; }

    [Key("source_pos_y")]
    public long SourcePosY { get; init; }

    [Key("current_angle")]
    public int CurrentAngle { get; init; }

    /// <summary>0 = charging, 1 = firing</summary>
    [Key("phase")]
    public byte Phase { get; init; }

    [Key("beam_width")]
    public int BeamWidth { get; init; }

    [Key("range")]
    public int Range { get; init; }

    [IgnoreMember]
    public float SourceWorldX => (float)(SourcePosX / 4_294_967_296.0);

    [IgnoreMember]
    public float SourceWorldY => (float)(SourcePosY / 4_294_967_296.0);

    [IgnoreMember]
    public float AngleRad => CurrentAngle / 65_536f;

    [IgnoreMember]
    public float BeamWidthUnits => BeamWidth / 65_536f;

    [IgnoreMember]
    public float RangeUnits => Range / 65_536f;
}

/// <summary>
/// Flat event record — check Type before reading type-specific fields.
/// Missing fields default to 0 / empty when not present in the MessagePack map.
/// </summary>
[MessagePackObject]
public sealed class LogEvent
{
    [Key("type")]
    public string Type { get; init; } = "";

    // ── Common / hitscan ──────────────────────────────────────────────────────

    [Key("source_id")]
    public uint SourceId { get; init; }

    [Key("target_id")]
    public uint TargetId { get; init; }

    [Key("damage")]
    public int Damage { get; init; }

    [Key("fleet")]
    public byte Fleet { get; init; }

    [Key("source_pos_x")]
    public long SourcePosX { get; init; }

    [Key("source_pos_y")]
    public long SourcePosY { get; init; }

    [Key("target_pos_x")]
    public long TargetPosX { get; init; }

    [Key("target_pos_y")]
    public long TargetPosY { get; init; }

    // ── Ship destroyed / low HP ───────────────────────────────────────────────

    [Key("id")]
    public uint Id { get; init; }

    // ── Explosion ─────────────────────────────────────────────────────────────

    [Key("pos_x")]
    public long PosX { get; init; }

    [Key("pos_y")]
    public long PosY { get; init; }

    [Key("radius")]
    public int Radius { get; init; }

    // ── Helpers ───────────────────────────────────────────────────────────────

    [IgnoreMember]
    public float SourceWorldX => (float)(SourcePosX / 4_294_967_296.0);

    [IgnoreMember]
    public float SourceWorldY => (float)(SourcePosY / 4_294_967_296.0);

    [IgnoreMember]
    public float TargetWorldX => (float)(TargetPosX / 4_294_967_296.0);

    [IgnoreMember]
    public float TargetWorldY => (float)(TargetPosY / 4_294_967_296.0);

    [IgnoreMember]
    public float ExplosionWorldX => (float)(PosX / 4_294_967_296.0);

    [IgnoreMember]
    public float ExplosionWorldY => (float)(PosY / 4_294_967_296.0);

    [IgnoreMember]
    public float RadiusUnits => Radius / 65_536f;
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
