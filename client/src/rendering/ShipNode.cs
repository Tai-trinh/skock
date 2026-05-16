using Godot;
using Skock.Sim;

namespace Skock.Rendering;

// Placeholder geometry — one polygon per hull class, pointing in local -y (tip up).
// Rotation in ApplySnapshot aligns the tip with the sim heading direction.
public partial class ShipNode : Node2D
{
    // ── Shapes ────────────────────────────────────────────────────────────────

    private static readonly Vector2[] CorvetteShape = [new(0, -8), new(6, 8), new(-6, 8)];

    private static readonly Vector2[] FrigateShape =
    [
        new(0, -12),
        new(8, 0),
        new(0, 12),
        new(-8, 0),
    ];

    private static readonly Vector2[] DestroyerShape =
    [
        new(-8, -10),
        new(8, -10),
        new(8, 10),
        new(-8, 10),
    ];

    private static readonly Vector2[] CruiserShape =
    [
        new(-10, -14),
        new(10, -14),
        new(10, 14),
        new(-10, 14),
    ];

    // Pentagon: forward point + rectangular body.
    private static readonly Vector2[] BattlecruiserShape =
    [
        new(0, -16),
        new(12, -6),
        new(12, 14),
        new(-12, 14),
        new(-12, -6),
    ];

    // Forward point + side wings.
    private static readonly Vector2[] DreadnoughtShape =
    [
        new(0, -20),
        new(8, -8),
        new(20, 0),
        new(8, 6),
        new(10, 18),
        new(-10, 18),
        new(-8, 6),
        new(-20, 0),
        new(-8, -8),
    ];

    // Regular hexagon (flat-top orientation matches "hexagon" in spec).
    private static readonly Vector2[] MothershipShape =
    [
        new(0, -20),
        new(17, -10),
        new(17, 10),
        new(0, 20),
        new(-17, 10),
        new(-17, -10),
    ];

    // ── Colors ────────────────────────────────────────────────────────────────

    private static readonly Color FleetAColor = new(0.35f, 0.55f, 1f);
    private static readonly Color FleetBColor = new(1f, 0.35f, 0.35f);
    private static readonly Color MothershipTint = new(1f, 1f, 0.6f);

    // ── State ─────────────────────────────────────────────────────────────────

    private Polygon2D _body = null!;
    private Color _baseColor;

    public uint ShipId { get; private set; }

    // ── Godot lifecycle ───────────────────────────────────────────────────────

    public override void _Ready()
    {
        _body = new Polygon2D();
        AddChild(_body);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Init(uint id, byte fleet, bool isMothership, string blueprintDrawingId)
    {
        ShipId = id;

        _body.Polygon = isMothership ? MothershipShape : ShapeFor(blueprintDrawingId);

        var baseColor = fleet == 0 ? FleetAColor : FleetBColor;
        _baseColor = isMothership ? baseColor * MothershipTint : baseColor;
        _body.Color = _baseColor;
    }

    // worldPos is already in Godot world-space (sim coords scaled and y-flipped).
    public void ApplySnapshot(Vector2 worldPos, float headingRad, float hpFraction)
    {
        Position = worldPos;
        // Godot Rotation is clockwise; sim heading is CCW from +x.
        // +Pi/2 aligns the local -y tip with the heading direction.
        Rotation = -headingRad + Mathf.Pi / 2f;

        // Recalculate from base color each frame — multiplying from current color would darken indefinitely.
        var tint = Mathf.Lerp(0.4f, 1f, hpFraction);
        _body.Color = _baseColor * tint;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Vector2[] ShapeFor(string drawingId) =>
        drawingId switch
        {
            var s when s.StartsWith("frigate") => FrigateShape,
            var s when s.StartsWith("destroyer") => DestroyerShape,
            var s when s.StartsWith("cruiser") => CruiserShape,
            var s when s.StartsWith("battlecruiser") => BattlecruiserShape,
            var s when s.StartsWith("dreadnought") => DreadnoughtShape,
            _ => CorvetteShape,
        };
}
