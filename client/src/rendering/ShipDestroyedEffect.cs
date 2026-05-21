using Godot;

namespace Skock.Rendering;

/// <summary>
/// Ship destruction VFX: expanding shockwave ring + inner fire flash + debris streaks
/// + spinning rectangular bulkhead panels that tumble outward from the blast.
/// Radius is scaled to the ship's hull size so larger ships produce bigger blasts.
/// Placeholder until final VFX assets ship.
/// </summary>
public partial class ShipDestroyedEffect : Node2D
{
    private static readonly Color FleetAColor = new(0.35f, 0.55f, 1f);
    private static readonly Color FleetBColor = new(1f, 0.35f, 0.35f);
    private static readonly Color FireColor = new(1f, 0.65f, 0.1f);

    private const int FadeDuration = 45;
    private const int DebrisCount = 16;
    private const int BulkheadCount = 8;

    private float _radius;
    private Color _baseColor;
    private int _framesLeft;
    private readonly (Vector2 vel, float len)[] _debris = new (Vector2, float)[DebrisCount];
    private readonly (
        Vector2 vel,
        float totalRotation,
        float initAngle,
        Vector2 halfSize
    )[] _bulkheads = new (Vector2, float, float, Vector2)[BulkheadCount];

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

        for (var i = 0; i < BulkheadCount; i++)
        {
            var angle = i * Mathf.Tau / BulkheadCount + (float)GD.RandRange(-0.5, 0.5);
            var speed = (float)GD.RandRange(radius * 0.15f, radius * 0.5f);
            // Flat panel: width noticeably larger than height to read as a hull plate
            var w = (float)GD.RandRange(radius * 0.10f, radius * 0.14f);
            w = Mathf.Min(w, 3f);

            var h = w * (float)GD.RandRange(0.25f, 0.50f);

            // Total spin over lifetime in radians; random direction
            var spin = (float)GD.RandRange(5.0, 14.0) * (GD.Randf() > 0.5f ? 1f : -1f);
            var initAngle = (float)GD.RandRange(0, Mathf.Tau);
            _bulkheads[i] = (
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                spin,
                initAngle,
                new Vector2(w, h)
            );
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

        // Debris streaks: lines flying outward from the explosion centre.
        foreach (var (vel, len) in _debris)
        {
            var tip = vel * t;
            var tail = tip - vel.Normalized() * len;
            DrawLine(tail, tip, new Color(_baseColor, alpha * 0.90f), 1.5f);
        }

        // Spinning bulkhead panels: rectangular hull fragments tumbling outward.
        var panelColor = new Color(_baseColor, alpha * 0.80f);
        foreach (var (vel, totalRotation, initAngle, halfSize) in _bulkheads)
        {
            var pos = vel * t;
            var rot = initAngle + totalRotation * t;

            DrawSetTransform(pos, rot);
            DrawColoredPolygon(
                [
                    new Vector2(-halfSize.X, -halfSize.Y),
                    new Vector2(halfSize.X, -halfSize.Y),
                    new Vector2(halfSize.X, halfSize.Y),
                    new Vector2(-halfSize.X, halfSize.Y),
                ],
                panelColor
            );
            DrawSetTransform(Vector2.Zero);
        }
    }
}
