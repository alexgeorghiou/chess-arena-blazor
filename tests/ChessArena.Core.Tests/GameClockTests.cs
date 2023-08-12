using ChessArena.Core.Clocks;
using ChessArena.Engine;
using Microsoft.Extensions.Time.Testing;

namespace ChessArena.Core.Tests;

public class GameClockTests
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-01-01T12:00:00Z"));

    private GameClock StartedClock(string control = "3+2")
    {
        var clock = new GameClock(TimeControl.Parse(control), _time);
        clock.Start();
        return clock;
    }

    [Fact]
    public void OnlyTheRunningSideLosesTime()
    {
        var clock = StartedClock();
        _time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromSeconds(170), clock.Remaining(Color.White));
        Assert.Equal(TimeSpan.FromSeconds(180), clock.Remaining(Color.Black));
    }

    [Fact]
    public void PressAddsIncrementAndSwitchesSides()
    {
        var clock = StartedClock();
        _time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(PressResult.Accepted, clock.Press(Color.White));
        Assert.Equal(TimeSpan.FromSeconds(172), clock.Remaining(Color.White));
        Assert.Equal(Color.Black, clock.Running);

        _time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(175), clock.Remaining(Color.Black));
        Assert.Equal(TimeSpan.FromSeconds(172), clock.Remaining(Color.White));
    }

    [Fact]
    public void IncrementCanGrowTimeBeyondInitial()
    {
        var clock = StartedClock();
        clock.Press(Color.White);
        clock.Press(Color.Black);
        clock.Press(Color.White);
        Assert.Equal(TimeSpan.FromSeconds(184), clock.Remaining(Color.White));
    }

    [Fact]
    public void PressingOutOfTurnThrows()
    {
        var clock = StartedClock();
        Assert.Throws<InvalidOperationException>(() => clock.Press(Color.Black));
    }

    [Fact]
    public void FlagFallsWhenTimeRunsOut()
    {
        var clock = StartedClock("1+0");
        _time.Advance(TimeSpan.FromSeconds(59.9));
        Assert.Null(clock.CheckFlag());

        _time.Advance(TimeSpan.FromSeconds(0.2));
        Assert.Equal(Color.White, clock.CheckFlag());
        Assert.Null(clock.Running);
        Assert.Equal(TimeSpan.Zero, clock.Remaining(Color.White));
    }

    [Fact]
    public void LatePressIsReportedAsFlagged()
    {
        var clock = StartedClock("1+0");
        clock.Press(Color.White);
        _time.Advance(TimeSpan.FromSeconds(61));

        Assert.Equal(PressResult.Flagged, clock.Press(Color.Black));
        Assert.Equal(Color.Black, clock.Flagged);
    }

    [Fact]
    public void IncrementIsNotAddedAfterFlagFall()
    {
        var clock = StartedClock("1+5");
        _time.Advance(TimeSpan.FromSeconds(60));
        Assert.Equal(PressResult.Flagged, clock.Press(Color.White));
        Assert.Equal(TimeSpan.Zero, clock.Remaining(Color.White));
    }

    [Fact]
    public void StopFreezesBothSides()
    {
        var clock = StartedClock();
        _time.Advance(TimeSpan.FromSeconds(30));
        clock.Stop();
        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(TimeSpan.FromSeconds(150), clock.Remaining(Color.White));
        Assert.Null(clock.CheckFlag());
    }

    [Fact]
    public void SnapshotCapturesBothClocksAtOneInstant()
    {
        var clock = StartedClock();
        _time.Advance(TimeSpan.FromSeconds(4));

        var snapshot = clock.Snapshot();
        Assert.Equal(TimeSpan.FromSeconds(176), snapshot.White);
        Assert.Equal(TimeSpan.FromSeconds(180), snapshot.For(Color.Black));
        Assert.Equal(Color.White, snapshot.Running);
        Assert.Equal(_time.GetUtcNow(), snapshot.TakenAt);
    }

    [Fact]
    public void CannotStartTwice()
    {
        var clock = StartedClock();
        Assert.Throws<InvalidOperationException>(() => clock.Start());
    }

    [Fact]
    public void RemainingNeverGoesNegative()
    {
        var clock = StartedClock("1+0");
        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.Zero, clock.Remaining(Color.White));
    }
}
