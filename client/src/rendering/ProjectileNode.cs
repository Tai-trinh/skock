using Godot;
using Skock.Sim;

namespace Skock.Rendering;

/// <summary>
/// Renders a single projectile. Shape depends on subtype; missiles and torpedos have trails.
/// All graphics are placeholder until final art ships.
/// </summary>
public partial class ProjectileNode : Node2D
{
	// subtype constants matching Rust ProjectileSubtype byte encoding
	public const byte SubtypeMissile = 0;
	public const byte SubtypeTorpedo = 1;
	public const byte SubtypeMine = 2;

	// Keep private aliases for internal _Draw usage.
	private const byte SeekingMissile = SubtypeMissile;
	private const byte Torpedo = SubtypeTorpedo;
	private const byte Mine = SubtypeMine;

	private static readonly Color FleetAColor = new(0.35f, 0.55f, 1f);
	private static readonly Color FleetBColor = new(1f, 0.35f, 0.35f);

	private const int TrailLength = 10;
	private readonly Vector2[] _trailHistory = new Vector2[TrailLength];
	private int _trailCount = 0;

	private byte _subtype;
	private Color _color;
	private Line2D? _trail;
	private float _rotation; // updated each tick

	public uint ProjId { get; private set; }

	public override void _Ready()
	{
		// Draw shape via _Draw — nothing to add in _Ready for shapes
	}

	public void Init(uint id, byte fleet, byte subtype)
	{
		ProjId = id;
		_subtype = subtype;
		_color = fleet == 0 ? FleetAColor : FleetBColor;

		if (subtype == SeekingMissile || subtype == Torpedo)
		{
			_trail = new Line2D
			{
				Width = 2f,
				DefaultColor = new Color(_color, 0.7f),
				BeginCapMode = Line2D.LineCapMode.None,
				EndCapMode = Line2D.LineCapMode.None,
			};
			_trail.WidthCurve = BuildTrailCurve();
			_trail.TopLevel = true; // render in world space, independent of projectile rotation
			var gradient = new Gradient();
			gradient.SetColor(0, new Color(_color, 0.6f));
			gradient.SetColor(1, new Color(_color, 0f));
			_trail.Gradient = gradient;
			AddChild(_trail);
		}

		QueueRedraw();
	}

	public void ApplySnapshot(Vector2 worldPos, float headingRad)
	{
		_rotation = headingRad;

		if (_trail != null)
		{
			// Prepend world position to trail
			var count = Mathf.Min(_trailCount + 1, TrailLength);
			for (var i = count - 1; i > 0; i--)
				_trailHistory[i] = _trailHistory[i - 1];
			_trailHistory[0] = worldPos;
			_trailCount = count;

			_trail.ClearPoints();
			for (var i = 0; i < _trailCount; i++)
				_trail.AddPoint(_trailHistory[i]);
		}

		Position = worldPos;
		Rotation = -headingRad + Mathf.Pi / 2f;
		QueueRedraw();
	}

	public override void _Draw()
	{
		switch (_subtype)
		{
			case SeekingMissile:
				DrawMissileArrow();
				break;
			case Torpedo:
				DrawTorpedoArc();
				break;
			case Mine:
				DrawMineStar();
				break;
			default:
				DrawCircle(Vector2.Zero, 3f, _color);
				break;
		}
	}

	// ── Shape drawers (local space, tip pointing in -y = forward after rotation) ──

	private void DrawMissileArrow()
	{
		// Small arrowhead pointing in -y (forward)
		DrawColoredPolygon([new Vector2(0, -6), new Vector2(3, 2), new Vector2(-3, 2)], _color);
	}

	private void DrawTorpedoArc()
	{
		// U-shape: open end trailing (nose forward = -y)
		// 6 points forming an open arc with the opening at +y (tail)
		var pts = new Vector2[]
		{
			new(-4, 4), // bottom-left (open end)
			new(-5, 0),
			new(-4, -4), // nose-left
			new(4, -4), // nose-right
			new(5, 0),
			new(4, 4), // bottom-right (open end)
		};
		for (var i = 0; i < pts.Length - 1; i++)
			DrawLine(pts[i], pts[i + 1], _color, 1.5f);
	}

	private void DrawMineStar()
	{
		// 6 radiating lines from center — slowly rotating in renderer, but
		// rotation is applied via Node2D.Rotation which we set to a slow spin.
		for (var i = 0; i < 6; i++)
		{
			var a = i * Mathf.Pi / 3f;
			var end = new Vector2(Mathf.Cos(a) * 6f, Mathf.Sin(a) * 6f);
			DrawLine(Vector2.Zero, end, _color, 1.5f);
		}
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	private static Curve BuildTrailCurve()
	{
		var c = new Curve();
		c.AddPoint(new Vector2(0f, 1f));
		c.AddPoint(new Vector2(1f, 0f));
		return c;
	}
}
