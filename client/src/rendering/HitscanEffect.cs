using Godot;

namespace Skock.Rendering;

/// <summary>
/// Temporary hitscan flash line: drawn from source to target pos, fades alpha to 0 over
/// 12 display frames, then queues itself free.
/// </summary>
public partial class HitscanEffect : Node2D
{
    private static readonly Color FleetAColor = new(0.35f, 0.55f, 1f);
    private static readonly Color FleetBColor = new(1f, 0.35f, 0.35f);

    private Line2D _line = null!;
    private int _framesLeft;
    private const int FadeDuration = 12;
    private Color _baseColor;

    public override void _Ready()
    {
        _line = new Line2D { Width = 2f, DefaultColor = Colors.White };
        AddChild(_line);
    }

    public void Spawn(Vector2 sourceWorld, Vector2 targetWorld, byte fleet, float simScale)
    {
        _baseColor = fleet == 0 ? FleetAColor : FleetBColor;
        _line.ClearPoints();
        _line.AddPoint(sourceWorld * simScale);
        _line.AddPoint(targetWorld * simScale);
        _line.DefaultColor = _baseColor;
        _framesLeft = FadeDuration;
    }

    public override void _Process(double delta)
    {
        if (_framesLeft <= 0)
        {
            QueueFree();
            return;
        }

        var alpha = (float)_framesLeft / FadeDuration;
        _line.DefaultColor = new Color(_baseColor, alpha);
        _framesLeft--;
    }
}
