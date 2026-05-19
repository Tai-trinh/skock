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
    private Line2D _trail = null!;
    private Color _baseColor;
    private const int TrailLength = 30;

    public uint ShipId { get; private set; }

    // ── Godot lifecycle ───────────────────────────────────────────────────────

    public override void _Ready()
    {
        // Trail drawn behind the ship (added first so it renders below the body)
        _trail = new Line2D
        {
            Width = 3f,
            DefaultColor = Colors.White,
            BeginCapMode = Line2D.LineCapMode.None,
            EndCapMode = Line2D.LineCapMode.None,
        };
        _trail.WidthCurve = BuildTrailCurve();
        _trail.TopLevel = true; // render in world space, independent of ship rotation
        AddChild(_trail);

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

        // Trail gradient: fleet color at head → transparent at tail
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(_baseColor, 0.7f)); // tip
        gradient.SetColor(1, new Color(_baseColor, 0.0f)); // tail
        _trail.Gradient = gradient;
    }

    // worldPos is already in Godot world-space (sim coords scaled and y-flipped).
    public void ApplySnapshot(Vector2 worldPos, float headingRad, float hpFraction)
    {
        // Prepend current position to trail (world-space, independent of ship rotation)
        var trailPos = worldPos - Position; // trail points are relative to this node's position
        // Actually, trail should be in world space. Use a global-space trail by reparenting to canvas
        // For simplicity, store trail points in world space and convert to local space each frame.
        PrependTrailPoint(worldPos);

        Position = worldPos;
        // Godot Rotation is clockwise; sim heading is CCW from +x.
        // +Pi/2 aligns the local -y tip with the heading direction.
        Rotation = -headingRad + Mathf.Pi / 2f;

        var tint = Mathf.Lerp(0.4f, 1f, hpFraction);
        _body.Color = _baseColor * tint;
    }

    // ── Trail ─────────────────────────────────────────────────────────────────

    private readonly Vector2[] _trailHistory = new Vector2[TrailLength];
    private int _trailCount = 0;

    private void PrependTrailPoint(Vector2 worldPos)
    {
        // Shift history forward
        var count = Mathf.Min(_trailCount + 1, TrailLength);
        for (var i = count - 1; i > 0; i--)
            _trailHistory[i] = _trailHistory[i - 1];
        _trailHistory[0] = worldPos;
        _trailCount = count;

        _trail.ClearPoints();
        for (var i = 0; i < _trailCount; i++)
            _trail.AddPoint(_trailHistory[i]);
    }

    private static Curve BuildTrailCurve()
    {
        var c = new Curve();
        c.AddPoint(new Vector2(0f, 1f)); // at ship: full width
        c.AddPoint(new Vector2(1f, 0f)); // at tail: zero width
        return c;
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
