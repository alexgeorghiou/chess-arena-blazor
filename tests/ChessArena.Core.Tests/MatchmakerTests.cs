using ChessArena.Core.Clocks;
using ChessArena.Core.Matchmaking;
using Microsoft.Extensions.Time.Testing;

namespace ChessArena.Core.Tests;

public class MatchmakerTests
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-01-01T12:00:00Z"));
    private readonly Matchmaker _matchmaker;

    public MatchmakerTests()
    {
        var options = new MatchmakingOptions { InitialWindow = 50, WindowGrowthPerSecond = 10, MaxWindow = 300 };
        _matchmaker = new Matchmaker(options, _time, new Random(42));
    }

    private Guid Seek(int rating, string control = "3+2")
    {
        var id = Guid.NewGuid();
        _matchmaker.Enqueue(id, $"p{rating}", rating, TimeControl.Parse(control));
        return id;
    }

    [Fact]
    public void PairsCloseRatingsImmediately()
    {
        var a = Seek(1500);
        var b = Seek(1540);

        var pairing = Assert.Single(_matchmaker.MatchPending());
        Assert.Equal(new[] { a, b }.Order(), new[] { pairing.White.PlayerId, pairing.Black.PlayerId }.Order());
        Assert.Equal(0, _matchmaker.Count);
    }

    [Fact]
    public void DoesNotPairDistantRatingsAtFirst()
    {
        Seek(1500);
        Seek(1650);
        Assert.Empty(_matchmaker.MatchPending());
        Assert.Equal(2, _matchmaker.Count);
    }

    [Fact]
    public void WindowWidensWhileWaiting()
    {
        Seek(1500);
        Seek(1650);

        _time.Advance(TimeSpan.FromSeconds(9));
        Assert.Empty(_matchmaker.MatchPending());

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Single(_matchmaker.MatchPending());
    }

    [Fact]
    public void WindowIsCapped()
    {
        Seek(1200);
        Seek(1600);
        _time.Advance(TimeSpan.FromHours(1));
        Assert.Empty(_matchmaker.MatchPending());
        Assert.Equal(300, _matchmaker.WindowFor(_matchmaker.Pending[0]));
    }

    [Fact]
    public void BothSidesMustAcceptTheGap()
    {
        // A has waited long enough to accept a 150-point gap; newcomer C has not.
        var a = Seek(1500);
        _time.Advance(TimeSpan.FromSeconds(20));
        var c = Seek(1650);

        Assert.Empty(_matchmaker.MatchPending());

        _time.Advance(TimeSpan.FromSeconds(10));
        var pairing = Assert.Single(_matchmaker.MatchPending());
        Assert.Contains(pairing.White.PlayerId, new[] { a, c });
    }

    [Fact]
    public void PrefersClosestRatedOpponent()
    {
        var a = Seek(1500);
        Seek(1545);
        var close = Seek(1510);

        var pairing = Assert.Single(_matchmaker.MatchPending());
        var ids = new[] { pairing.White.PlayerId, pairing.Black.PlayerId };
        Assert.Contains(a, ids);
        Assert.Contains(close, ids);
    }

    [Fact]
    public void OldestSeekIsServedFirst()
    {
        var oldest = Seek(1500);
        _time.Advance(TimeSpan.FromSeconds(1));
        var middle = Seek(1530);
        var newest = Seek(1505);

        // The oldest seek picks its closest opponent (newest) even though middle and newest are closer to each other.
        var pairing = Assert.Single(_matchmaker.MatchPending());
        var ids = new[] { pairing.White.PlayerId, pairing.Black.PlayerId };
        Assert.Contains(oldest, ids);
        Assert.Contains(newest, ids);
        Assert.True(_matchmaker.IsSeeking(middle));
    }

    [Fact]
    public void OnlyPairsIdenticalTimeControls()
    {
        Seek(1500, "3+2");
        Seek(1500, "5+0");
        Assert.Empty(_matchmaker.MatchPending());

        Seek(1500, "5+0");
        var pairing = Assert.Single(_matchmaker.MatchPending());
        Assert.Equal(TimeControl.Parse("5+0"), pairing.TimeControl);
    }

    [Fact]
    public void PairsManyPlayersInOnePass()
    {
        for (var i = 0; i < 10; i++)
        {
            Seek(1500 + i * 5);
        }

        Assert.Equal(5, _matchmaker.MatchPending().Count);
        Assert.Equal(0, _matchmaker.Count);
    }

    [Fact]
    public void ReseekingReplacesPreviousSeek()
    {
        var id = Guid.NewGuid();
        _matchmaker.Enqueue(id, "me", 1500, TimeControl.Blitz3Plus2);
        _matchmaker.Enqueue(id, "me", 1500, TimeControl.Rapid10Plus5);

        Assert.Equal(1, _matchmaker.Count);
        Assert.Equal(TimeControl.Rapid10Plus5, _matchmaker.Pending[0].TimeControl);
    }

    [Fact]
    public void CancelledSeeksAreNotPaired()
    {
        var a = Seek(1500);
        Assert.True(_matchmaker.Cancel(a));
        Assert.False(_matchmaker.Cancel(a));
        Seek(1500);
        Assert.Empty(_matchmaker.MatchPending());
    }

    [Fact]
    public void ColoursAreAssignedRandomly()
    {
        var whites = 0;
        for (var i = 0; i < 40; i++)
        {
            var first = Seek(1500);
            Seek(1500);
            if (_matchmaker.MatchPending()[0].White.PlayerId == first)
            {
                whites++;
            }
        }

        Assert.InRange(whites, 5, 35);
    }
}
