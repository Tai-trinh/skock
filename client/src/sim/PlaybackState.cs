using Godot;

namespace Skock.Sim;

/// <summary>
/// Holds a parsed battle log and a playback cursor. Does not own any Godot nodes.
/// </summary>
public sealed class PlaybackState
{
    private readonly BattleLog _log;

    public bool IsLoaded => _log.Ticks.Length > 0;
    public int TotalTicks => _log.Ticks.Length;
    public BattleResult? Result { get; private set; }

    /// <summary>Current playback position in ticks (fractional).</summary>
    public float Time { get; private set; }

    public float PlaybackSpeed { get; set; } = 1f;

    public bool IsFinished => Time >= TotalTicks - 1;

    public PlaybackState(BattleLog log, BattleResult result)
    {
        _log = log;
        Result = result;
    }

    public void Advance(double delta, int tickRate)
    {
        if (IsFinished)
            return;
        Time = Mathf.Min(Time + (float)(delta * tickRate * PlaybackSpeed), TotalTicks - 1);
    }

    /// <summary>
    /// Returns the two surrounding tick records and the interpolation factor t ∈ [0, 1].
    /// </summary>
    public (TickRecord A, TickRecord B, float T) CurrentFrame()
    {
        var indexA = Mathf.FloorToInt(Time);
        var indexB = Mathf.Min(indexA + 1, TotalTicks - 1);
        var t = Time - indexA;
        return (_log.Ticks[indexA], _log.Ticks[indexB], t);
    }
}
