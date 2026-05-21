using Godot;

namespace Skock.Rendering;

/// <summary>
/// Ship destruction VFX: expanding shockwave ring + inner fire flash + debris fragments.
/// Radius is scaled to the ship's hull size so larger ships produce bigger blasts.
/// Placeholder until final VFX assets ship.
/// </summary>
public partial class ShipDestroyedEffect : Node2D
{
    private static readonly Color FleetAColor = new(0.35f, 0.55f, 1f);
    private static readonly Color FleetBColor = new(1f, 0.35f, 0.35f);
    private static readonly Color FireColor = new(1f, 0.65f, 0.1f);

    private const int FadeDuration = 45;
    private const int DebrisCount = 8;

    private float _radius;
    private Color _baseColor;
    private int _framesLeft;
    private readonly (Vector2 vel, float len)[] _debris = new (Vector2, float)[DebrisCount];

    public void Spawn(Vector2 worldPos, float radius, byte fleet)
    {
        Position = worldPos;
        _radius = radius;
        _baseColor = fleet == 0 ? FleetAColor : FleetBColor;
        _framesLeft = FadeDuration;

        for (var i = 0; i < DebrisCount; i++)
        {
            var angle = i * Mathf.Tau / DebrisCount + (float)GD.RandRange(-0.3, 0.3);
            var speed = (float)GD.RandRange(radius * 0.3f, radius * 0.75f);
            var len = (float)GD.RandRange(radius * 0.12f, radius * 0.30f);
            _debris[i] = (new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, len);
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
        var t = 1f - (float)_framesLeft / FadeDuration;
        var alpha = (float)_framesLeft / FadeDuration;

        // Outer shockwave ring: expands from 0.3× to 2.0× radius.
        var ringRadius = Mathf.Lerp(_radius * 0.3f, _radius * 2.0f, t);
        DrawArc(
            Vector2.Zero,
            ringRadius,
            0f,
            Mathf.Tau,
            40,
            new Color(_baseColor, alpha * 0.85f),
            2.5f
        );

        // Inner fire flash: visible only in the first half, fades twice as fast.
        var fireAlpha = Mathf.Max(0f, 1f - t * 2f) * alpha;
        if (fireAlpha > 0f)
            DrawArc(
                Vector2.Zero,
                ringRadius * 0.45f,
                0f,
                Mathf.Tau,
                24,
                new Color(FireColor, fireAlpha),
                4f
            );

        // Debris fragments: lines flying outward from the explosion centre.
        foreach (var (vel, len) in _debris)
        {
            var tip = vel * t;
            var tail = tip - vel.Normalized() * len;
            DrawLine(tail, tip, new Color(_baseColor, alpha * 0.90f), 1.5f);
        }
    }
}
