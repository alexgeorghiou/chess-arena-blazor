using ChessArena.Web.Components.Board;
using ChessArena.Web.Components.Shared;
using ChessArena.Web.Hubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace ChessArena.Web.Tests.Components;

public class WidgetTests : TestContext
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-01-01T12:00:00Z"));

    public WidgetTests() => Services.AddSingleton<TimeProvider>(_time);

    [Theory]
    [InlineData(185_000, "3:05")]
    [InlineData(60_000, "1:00")]
    [InlineData(10_000, "0:10")]
    [InlineData(9_870, "9.8")]
    [InlineData(0, "0.0")]
    [InlineData(3_723_000, "1:02:03")]
    public void ClockFormatsLikeOnlineClocks(long millis, string expected) =>
        Assert.Equal(expected, ChessClock.Format(TimeSpan.FromMilliseconds(millis)));

    [Fact]
    public void RunningClockCountsDownFromSyncPoint()
    {
        var clock = RenderComponent<ChessClock>(p => p
            .Add(c => c.Millis, 60_000)
            .Add(c => c.Running, true)
            .Add(c => c.SyncedAt, _time.GetUtcNow())
            .Add(c => c.Label, "White"));

        Assert.Equal("1:00", clock.Find(".time").TextContent);

        _time.Advance(TimeSpan.FromSeconds(15));
        clock.WaitForAssertion(() => Assert.Equal("0:45", clock.Find(".time").TextContent));
        Assert.Contains("running", clock.Find(".clock").ClassList);
    }

    [Fact]
    public void StoppedClockDoesNotMove()
    {
        var clock = RenderComponent<ChessClock>(p => p
            .Add(c => c.Millis, 8_000)
            .Add(c => c.SyncedAt, _time.GetUtcNow()));

        _time.Advance(TimeSpan.FromSeconds(30));
        clock.Render();

        Assert.Equal("8.0", clock.Find(".time").TextContent);
        Assert.DoesNotContain("running", clock.Find(".clock").ClassList);
    }

    [Fact]
    public void MoveListPairsMovesByNumber()
    {
        var list = RenderComponent<MoveList>(p => p.Add(m => m.SanMoves, ["e4", "e5", "Nf3"]));

        var rows = list.FindAll("li");
        Assert.Equal(2, rows.Count);
        Assert.Equal("1.", rows[0].QuerySelector(".no")!.TextContent);
        Assert.Equal("Nf3", list.Find(".latest").TextContent);
    }

    [Fact]
    public void EmptyMoveListShowsPlaceholder() =>
        RenderComponent<MoveList>().Find(".empty").MarkupMatches("<li class=\"empty\">No moves yet</li>");

    [Theory]
    [InlineData("1-0", "Checkmate", "alice wins", "by checkmate")]
    [InlineData("0-1", "Timeout", "bob wins", "on time")]
    [InlineData("1/2-1/2", "ThreefoldRepetition", "Draw", "by threefold repetition")]
    public void GameOverBannerDescribesTheResult(string result, string termination, string headline, string reason)
    {
        var state = new GameStateDto
        {
            GameId = Guid.NewGuid(),
            White = new PlayerSeatDto(Guid.NewGuid(), "alice", 1500),
            Black = new PlayerSeatDto(Guid.NewGuid(), "bob", 1500),
            TimeControl = "3+2",
            Fen = Engine.Position.StartFen,
            SideToMove = "White",
            IsCheck = false,
            LegalMoves = [],
            SanMoves = [],
            WhiteMillis = 0,
            BlackMillis = 0,
            ServerTime = _time.GetUtcNow(),
            Result = result,
            Termination = termination,
            Spectators = 0,
        };

        var banner = RenderComponent<GameOverBanner>(p => p.Add(b => b.State, state));

        Assert.Equal(headline, banner.Find("strong").TextContent);
        Assert.Equal(reason, banner.Find("span").TextContent);
    }

    [Fact]
    public void PlayerStripShowsRatingDelta()
    {
        var strip = RenderComponent<PlayerStrip>(p => p
            .Add(s => s.Seat, new PlayerSeatDto(Guid.NewGuid(), "carol", 1612))
            .Add(s => s.RatingDelta, -8)
            .Add(s => s.IsYou, true));

        Assert.Equal("(1612)", strip.Find(".rating").TextContent);
        Assert.Equal("-8", strip.Find(".delta.down").TextContent);
        Assert.NotNull(strip.Find(".you"));
    }
}
