using ChessArena.Core.Clocks;
using ChessArena.Engine;
using ChessArena.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace ChessArena.Web.Tests.Data;

public class PersistenceTests : SqliteTestBase
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-02-03T18:30:00Z"));

    private async Task<(PlayerSummary White, PlayerSummary Black)> RegisterAsync()
    {
        await using var db = NewContext();
        var store = new PlayerStore(db, _time);
        return (await store.GetOrCreateAsync("alice"), await store.GetOrCreateAsync("bob"));
    }

    private CompletedGame Completed(PlayerSummary white, PlayerSummary black, ChessGame game) => new(
        Guid.NewGuid(), white.Id, black.Id, TimeControl.Blitz3Plus2, game,
        game.Moves.Select((_, i) => 180_000L - i * 1_000).ToList(),
        _time.GetUtcNow().AddMinutes(-3), _time.GetUtcNow());

    [Fact]
    public async Task HandlesAreUniqueAndCaseInsensitive()
    {
        await using var db = NewContext();
        var store = new PlayerStore(db, _time);

        var first = await store.GetOrCreateAsync("Magnus");
        var again = await store.GetOrCreateAsync("  magnus ");

        Assert.Equal(first.Id, again.Id);
        Assert.Equal("Magnus", again.Handle);
        Assert.Equal(1500, first.Rating);
        Assert.Equal(1, await db.Players.CountAsync());
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("waaaaaaaaaaaaaaaaaaaay-too-long")]
    [InlineData("semi;colon")]
    public async Task RejectsInvalidHandles(string handle)
    {
        await using var db = NewContext();
        await Assert.ThrowsAsync<ArgumentException>(() => new PlayerStore(db, _time).GetOrCreateAsync(handle));
    }

    [Fact]
    public async Task RecordingAGameUpdatesRatingsAndStats()
    {
        var (white, black) = await RegisterAsync();
        var game = new ChessGame();
        foreach (var san in new[] { "e4", "e5", "Qh5", "Nc6", "Bc4", "Nf6", "Qxf7#" })
        {
            game.TryMoveSan(san);
        }

        await using (var db = NewContext())
        {
            var change = await new GameArchive(db).RecordAsync(Completed(white, black, game));
            Assert.Equal(1520, change.WhiteAfter);
            Assert.Equal(1480, change.BlackAfter);
        }

        await using (var db = NewContext())
        {
            var alice = await db.Players.SingleAsync(p => p.Id == white.Id);
            var bob = await db.Players.SingleAsync(p => p.Id == black.Id);
            Assert.Equal((1520, 1, 1, 0, 0), (alice.Rating, alice.GamesPlayed, alice.Wins, alice.Losses, alice.Draws));
            Assert.Equal((1480, 1, 0, 1, 0), (bob.Rating, bob.GamesPlayed, bob.Wins, bob.Losses, bob.Draws));
        }
    }

    [Fact]
    public async Task ArchivedGameKeepsMovesClocksAndPgn()
    {
        var (white, black) = await RegisterAsync();
        var game = new ChessGame();
        game.TryMoveSan("d4");
        game.TryMoveSan("d5");
        game.AgreeDraw();
        var completed = Completed(white, black, game);

        await using (var db = NewContext())
        {
            await new GameArchive(db).RecordAsync(completed);
        }

        await using (var db = NewContext())
        {
            var stored = await new GameArchive(db).FindAsync(completed.GameId);
            Assert.NotNull(stored);
            Assert.Equal("1/2-1/2", stored.Result);
            Assert.Equal("DrawAgreement", stored.Termination);
            Assert.Equal(["d4", "d5"], stored.Moves.Select(m => m.San));
            Assert.Equal([180_000L, 179_000L], stored.Moves.Select(m => m.ClockMillis));
            Assert.Contains("[White \"alice\"]", stored.Pgn);
            Assert.Contains("[TimeControl \"180+2\"]", stored.Pgn);
            Assert.Contains("[Date \"2026.02.03\"]", stored.Pgn);
            Assert.Contains("1. d4 d5 1/2-1/2", stored.Pgn);
        }
    }

    [Fact]
    public async Task RefusesToArchiveUnfinishedGame()
    {
        var (white, black) = await RegisterAsync();
        await using var db = NewContext();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new GameArchive(db).RecordAsync(Completed(white, black, new ChessGame())));
    }

    [Fact]
    public async Task RecentGamesAndLeaderboardReflectResults()
    {
        var (white, black) = await RegisterAsync();
        for (var i = 0; i < 3; i++)
        {
            var game = new ChessGame();
            game.Resign(Color.Black);
            _time.Advance(TimeSpan.FromMinutes(5));
            await using var db = NewContext();
            await new GameArchive(db).RecordAsync(Completed(white, black, game));
        }

        await using (var db = NewContext())
        {
            var recent = await new GameArchive(db).RecentAsync(2, white.Id);
            Assert.Equal(2, recent.Count);
            Assert.True(recent[0].EndedAtUtc > recent[1].EndedAtUtc);
            Assert.All(recent, g => Assert.Equal(("alice", "bob", "1-0"), (g.White, g.Black, g.Result)));

            var leaders = await new PlayerStore(db, _time).LeaderboardAsync(10);
            Assert.Equal(["alice", "bob"], leaders.Select(p => p.Handle));
            Assert.True(leaders[0].Rating > 1500);
        }
    }
}
