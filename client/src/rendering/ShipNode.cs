using System.Collections.Generic;
using Godot;
using Skock.Sim;

namespace Skock.Rendering;

public partial class ShipNode : Node2D
{
    // ── Sprite scaling ────────────────────────────────────────────────────────
    //
    // Hull hit radii (sim-units) must match hull_hit_radii in sim_config.json.
    // world_height = radius * 2; scale = world_height / sprite_px.
    // When calibrating: update HullHitRadius + sim_config.json together, update
    // SpriteHeightPx when a sprite is regenerated at a different pixel size.

    private static readonly Dictionary<string, float> HullHitRadius = new()
    {
        ["corvette"] = 15f,
        ["frigate"] = 25f,
        ["destroyer"] = 35f,
        ["cruiser"] = 45f,
        ["battlecruiser"] = 55f,
        ["dreadnought"] = 65f,
        ["mothership"] = 75f,
    };

    private static readonly Dictionary<string, float> SpriteHeightPx = new()
    {
        ["corvette"] = 256f,
        ["frigate"] = 600f,
        ["destroyer"] = 800f,
        ["cruiser"] = 696f,
        ["battlecruiser"] = 1000f,
        ["dreadnought"] = 696f,
        ["mothership"] = 1024f,
    };

    private static float ScaleFor(string hull)
    {
        var radius = HullHitRadius.GetValueOrDefault(hull, 15f);
        var px = SpriteHeightPx.GetValueOrDefault(hull, 256f);
        return radius * 2f / px;
    }

    // ── Colors ────────────────────────────────────────────────────────────────

    private static readonly Color FleetATrail = new(0.35f, 0.55f, 1f);
    private static readonly Color FleetBTint = new(1f, 0.2f, 0.2f);

    // ── State ─────────────────────────────────────────────────────────────────

    private Sprite2D _body = null!;
    private ShaderMaterial _mat = null!;
    private Line2D _trail = null!;
    private Color _baseModulate;
    private const int TrailLength = 30;

    public uint ShipId { get; private set; }
    public bool IsMothership { get; private set; }
    public string BlueprintDrawingId { get; private set; } = "";

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

        _mat = new ShaderMaterial();
        _mat.Shader = GD.Load<Shader>("res://assets/shaders/ship_lighting.gdshader");

        _body = new Sprite2D();
        _body.Material = _mat;
        AddChild(_body);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Init(uint id, byte fleet, bool isMothership, string blueprintDrawingId)
    {
        ShipId = id;
        IsMothership = isMothership;
        BlueprintDrawingId = blueprintDrawingId;

        var hull = HullPrefix(blueprintDrawingId, isMothership);
        _body.Texture = GD.Load<Texture2D>($"res://assets/sprites/{hull}_a.png");
        _body.Scale = Vector2.One * ScaleFor(hull);

        _baseModulate = fleet == 0 ? Colors.White : FleetBTint;
        Modulate = _baseModulate;

        var trailColor = fleet == 0 ? FleetATrail : FleetBTint;
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(trailColor, 0.7f));
        gradient.SetColor(1, new Color(trailColor, 0.0f));
        _trail.Gradient = gradient;
    }

    // worldPos is already in Godot world-space (sim coords scaled and y-flipped).
    public void ApplySnapshot(Vector2 worldPos, float headingRad, float hpFraction)
    {
        PrependTrailPoint(worldPos);

        Position = worldPos;
        // Godot Rotation is clockwise; sim heading is CCW from +x.
        // +Pi/2 aligns the sprite nose (local -y) with the heading direction.
        Rotation = -headingRad + Mathf.Pi / 2f;
        _mat.SetShaderParameter("ship_rotation", Rotation);

        Modulate = _baseModulate * Mathf.Lerp(0.4f, 1f, hpFraction);
    }

    // ── Trail ─────────────────────────────────────────────────────────────────

    private readonly Vector2[] _trailHistory = new Vector2[TrailLength];
    private int _trailCount = 0;

    private void PrependTrailPoint(Vector2 worldPos)
    {
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
        c.AddPoint(new Vector2(0f, 1f));
        c.AddPoint(new Vector2(1f, 0f));
        return c;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string HullPrefix(string drawingId, bool isMothership)
    {
        if (isMothership)
            return "mothership";
        if (drawingId.StartsWith("battlecruiser"))
            return "battlecruiser";
        if (drawingId.StartsWith("dreadnought"))
            return "dreadnought";
        if (drawingId.StartsWith("destroyer"))
            return "destroyer";
        if (drawingId.StartsWith("cruiser"))
            return "cruiser";
        if (drawingId.StartsWith("frigate"))
            return "frigate";
        return "corvette";
    }
}
