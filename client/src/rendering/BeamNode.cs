using Godot;
using Skock.Sim;

namespace Skock.Rendering;

/// <summary>
/// Renders a single beam weapon. A thick Line2D from source along current_angle.
/// 30% alpha when charging, 100% when firing. Width = beam_width sim units × SimScale.
/// All graphics are placeholder.
/// </summary>
public partial class BeamNode : Node2D
{
    private static readonly Color FleetAColor = new(0.35f, 0.55f, 1f);
    private static readonly Color FleetBColor = new(1f, 0.35f, 0.35f);

    private Line2D _line = null!;
    private Color _baseColor;

    public uint BeamId { get; private set; }

    public override void _Ready()
    {
        _line = new Line2D
        {
            DefaultColor = Colors.White,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
        };
        AddChild(_line);
    }

    public void Init(uint id, byte fleet)
    {
        BeamId = id;
        _baseColor = fleet == 0 ? FleetAColor : FleetBColor;
    }

    public void ApplySnapshot(BeamSnapshot snap, float simScale)
    {
        var src = new Vector2(snap.SourceWorldX, -snap.SourceWorldY) * simScale;
        var angle = snap.AngleRad;
        var range = snap.RangeUnits * simScale;
        var width = Mathf.Max(snap.BeamWidthUnits * simScale, 1f);

        // End point along the beam direction
        var endDir = new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)); // y-flipped for screen
        var end = src + endDir * range;

        _line.ClearPoints();
        _line.AddPoint(src);
        _line.AddPoint(end);
        _line.Width = width;

        // Charging = dim, Firing = bright
        var alpha = snap.Phase == 0 ? 0.30f : 1.0f;
        _line.DefaultColor = new Color(_baseColor, alpha);
    }
}
