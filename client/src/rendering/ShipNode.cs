using Godot;
using Skock.Sim;

namespace Skock.Rendering;

/// <summary>
/// Visual representation of a single ship. Rendered as a triangle pointing in
/// the ship's heading direction. Placeholder until real mesh assets exist.
/// </summary>
public partial class ShipNode : Node2D
{
	private static readonly Vector2[] FighterShape =
	[
		new(0, -10),
		new(7, 10),
		new(-7, 10),
	];

	private static readonly Vector2[] MothershipShape =
	[
		new(0, -20),
		new(14, 20),
		new(-14, 20),
	];

	private static readonly Color FleetAColor = new(0.35f, 0.55f, 1f);
	private static readonly Color FleetBColor = new(1f, 0.35f, 0.35f);
	private static readonly Color MothershipTint = new(1f, 1f, 0.6f);

	private Polygon2D _body = null!;
	private bool _isMothership;

	public uint ShipId { get; private set; }

	public override void _Ready()
	{
		_body = new Polygon2D();
		AddChild(_body);
	}

	public void Init(uint id, byte fleet, bool isMothership)
	{
		ShipId = id;
		_isMothership = isMothership;

		_body.Polygon = isMothership ? MothershipShape : FighterShape;

		var baseColor = fleet == 0 ? FleetAColor : FleetBColor;
		_body.Color = isMothership ? baseColor * MothershipTint : baseColor;
	}

	/// <summary>
	/// Updates transform and HP tint from an interpolated snapshot.
	/// worldPos is already in Godot world-space (sim coords scaled and y-flipped).
	/// </summary>
	public void ApplySnapshot(Vector2 worldPos, float headingRad, float hpFraction)
	{
		Position = worldPos;
		// Godot's Rotation is clockwise; sim heading is standard math (CCW from +x).
		// Rotate 90° so the triangle tip (local -y) points in the heading direction.
		Rotation = -headingRad + Mathf.Pi / 2f;

		// Darken ship as it takes damage
		var tint = Mathf.Lerp(0.4f, 1f, hpFraction);
		_body.Color = new Color(_body.Color.R * tint, _body.Color.G * tint, _body.Color.B * tint);
	}
}
