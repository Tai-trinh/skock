using Skock.Sim;
using Xunit;

namespace Skock.Tests;

public sealed class PlaybackStateTests
{
    private static BattleLog MakeLog(int tickCount)
    {
        var ticks = new TickRecord[tickCount];
        for (var i = 0; i < tickCount; i++)
            ticks[i] = new TickRecord { Tick = (uint)i, Ships = [] };
        return new BattleLog(new LogHeader { SchemaVersion = 1 }, ticks);
    }

    // ── Advance ───────────────────────────────────────────────────────────────

    [Fact]
    public void Advance_MovesTimeForward()
    {
        var pb = new PlaybackState(MakeLog(10), new BattleResult());
        pb.Advance(delta: 1.0, tickRate: 1);
        Assert.Equal(1f, pb.Time, precision: 5);
    }

    [Fact]
    public void Advance_ClampsAtLastTick()
    {
        var pb = new PlaybackState(MakeLog(5), new BattleResult());
        pb.Advance(delta: 100.0, tickRate: 30);
        Assert.Equal(4f, pb.Time, precision: 5); // TotalTicks - 1 = 4
    }

    [Fact]
    public void Advance_DoesNothing_WhenFinished()
    {
        var pb = new PlaybackState(MakeLog(3), new BattleResult());
        pb.Advance(delta: 100.0, tickRate: 30); // clamp to 2
        pb.Advance(delta: 1.0, tickRate: 30);
        Assert.Equal(2f, pb.Time, precision: 5);
    }

    [Fact]
    public void IsFinished_FalseAtStart()
    {
        var pb = new PlaybackState(MakeLog(3), new BattleResult());
        Assert.False(pb.IsFinished);
    }

    [Fact]
    public void IsFinished_TrueAfterAdvancingToEnd()
    {
        var pb = new PlaybackState(MakeLog(3), new BattleResult());
        pb.Advance(delta: 100.0, tickRate: 30);
        Assert.True(pb.IsFinished);
    }

    // ── CurrentFrame ──────────────────────────────────────────────────────────

    [Fact]
    public void CurrentFrame_AtStart_ReturnsTick0And1()
    {
        var pb = new PlaybackState(MakeLog(5), new BattleResult());
        var (a, b, t) = pb.CurrentFrame();
        Assert.Equal(0u, a.Tick);
        Assert.Equal(1u, b.Tick);
        Assert.Equal(0f, t, precision: 5);
    }

    [Fact]
    public void CurrentFrame_AtLastTick_ReturnsSameTickForBoth()
    {
        var pb = new PlaybackState(MakeLog(3), new BattleResult());
        pb.Advance(delta: 100.0, tickRate: 30); // clamp to 2
        var (a, b, _) = pb.CurrentFrame();
        Assert.Equal(2u, a.Tick);
        Assert.Equal(2u, b.Tick); // indexB clamped to TotalTicks - 1
    }

    [Fact]
    public void CurrentFrame_InterpolationFactor_IsFractionalPart()
    {
        var pb = new PlaybackState(MakeLog(10), new BattleResult());
        pb.Advance(delta: 1.0 / 30.0, tickRate: 30); // advance exactly one tick
        var (_, _, t) = pb.CurrentFrame();
        Assert.Equal(0f, t, precision: 4);
    }
}
