using Godot;

namespace Skock.Rendering;

/// <summary>
/// Expanding circle outline that fades alpha to 0 over 20 display frames, then frees itself.
/// </summary>
public partial class ExplosionEffect : Node2D
{
    private static readonly Color FleetAColor = new(0.35f, 0.55f, 1f);
    private static readonly Color FleetBColor = new(1f, 0.35f, 0.35f);

    private float _startRadius;
    private float _endRadius;
    private Color _baseColor;
    private int _framesLeft;
    private const int FadeDuration = 20;

    public void Spawn(Vector2 worldPos, float radius, byte fleet)
    {
        Position = worldPos;
        _startRadius = radius * 0.5f;
        _endRadius = radius * 1.5f;
        _baseColor = fleet == 0 ? FleetAColor : FleetBColor;
        _framesLeft = FadeDuration;
    }

    public override void _Process(double delta)
    {
        if (_framesLeft <= 0)
        {
            QueueFree();
            return;
        }

        _framesLeft--;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var t = 1f - (float)_framesLeft / FadeDuration;
        var radius = Mathf.Lerp(_startRadius, _endRadius, t);
        var alpha = (float)_framesLeft / FadeDuration;
        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 24, new Color(_baseColor, alpha), 1.5f);
    }
}
