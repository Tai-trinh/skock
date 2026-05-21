using Godot;

namespace Skock.Rendering;

/// <summary>
/// Short spark burst at the hitscan impact point: radiating lines that fly outward
/// and fade over 8 display frames. Placeholder until final VFX assets ship.
/// </summary>
public partial class HitscanImpactEffect : Node2D
{
    private static readonly Color FleetAColor = new(0.35f, 0.55f, 1f);
    private static readonly Color FleetBColor = new(1f, 0.35f, 0.35f);

    private const int FadeDuration = 8;
    private const int SparkCount = 6;
    private const float SparkSpeedMin = 20f;
    private const float SparkSpeedMax = 45f;
    private const float SparkLenMin = 2f;
    private const float SparkLenMax = 6f;

    // Each spark: outward velocity vector + line length.
    private readonly (Vector2 vel, float len)[] _sparks = new (Vector2, float)[SparkCount];
    private Color _baseColor;
    private int _framesLeft;

    public void Spawn(Vector2 worldPos, byte fleet)
    {
        Position = worldPos;
        _baseColor = fleet == 0 ? FleetAColor : FleetBColor;
        _framesLeft = FadeDuration;

        for (var i = 0; i < SparkCount; i++)
        {
            var angle = i * Mathf.Tau / SparkCount + (float)GD.RandRange(-0.4, 0.4);
            var speed = (float)GD.RandRange(SparkSpeedMin, SparkSpeedMax);
            var len = (float)GD.RandRange(SparkLenMin, SparkLenMax);
            _sparks[i] = (new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, len);
        }
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
        var t = 1f - (float)_framesLeft / FadeDuration; // 0→1 as sparks expand
        var alpha = (float)_framesLeft / FadeDuration;
        var color = new Color(_baseColor, alpha);

        foreach (var (vel, len) in _sparks)
        {
            // Sparks travel outward from centre; trail line follows behind the tip.
            var tip = vel * t;
            var tail = tip - vel.Normalized() * len;
            DrawLine(tail, tip, color, 1f);
        }
    }
}
