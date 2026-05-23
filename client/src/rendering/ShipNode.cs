using Godot;
using Skock.Sim;

namespace Skock.Rendering;

public partial class ShipNode : Node2D
{
	// ── Sprite scales: target_world_height / sprite_pixel_height ─────────────

	private static float ScaleFor(string hull) =>
		hull switch
		{
			"frigate" => 37.5f / 600f,
			"destroyer" => 52.5f / 800f,
			"cruiser" => 67.5f / 696f,
			"battlecruiser" => 82.5f / 1000f,
			"dreadnought" => 97.5f / 696f,
			"mothership" => 112.5f / 1024f,
			_ => 22.5f / 256f, // corvette
		};

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
