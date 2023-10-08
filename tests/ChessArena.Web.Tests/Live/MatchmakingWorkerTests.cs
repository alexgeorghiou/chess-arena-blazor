using ChessArena.Core.Clocks;
using ChessArena.Core.Matchmaking;
using ChessArena.Core.Ratings;
using ChessArena.Web.Data;
using ChessArena.Web.Hubs;
using ChessArena.Web.Live;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ChessArena.Web.Tests.Live;

public class MatchmakingWorkerTests
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-01-01T12:00:00Z"));
    private readonly Notifier _notifier = new();
    private readonly GameManager _games;
    private readonly LobbyService _lobby;
    private readonly MatchmakingWorker _worker;

    public MatchmakingWorkerTests()
    {
        _games = new GameManager(_notifier, new NullRecorder(), _time, NullLogger<GameManager>.Instance);
        var matchmaker = new Matchmaker(new MatchmakingOptions(), _time, new Random(7));
        _lobby = new LobbyService(matchmaker, _games, _time);
        _worker = new MatchmakingWorker(_lobby, _games, _notifier, _time, NullLogger<MatchmakingWorker>.Instance);
    }

    [Fact]
    public async Task PairedPlayersBothHearAboutTheirGame()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        Assert.Null(_lobby.TrySeek(alice, "alice", 1500, "3+2"));
        Assert.Null(_lobby.TrySeek(bob, "bob", 1510, "3+2"));

        Assert.Equal(1, await _worker.RunOnceAsync());

        Assert.Equal(2, _notifier.Matches.Count);
        var gameIds = _notifier.Matches.Select(m => m.Match.GameId).Distinct();
        var game = _games.Find(Assert.Single(gameIds))!;
        Assert.Equal(["Black", "White"], _notifier.Matches.Select(m => m.Match.Color).Order());
        Assert.NotNull(game.ColorOf(alice));
        Assert.NotNull(game.ColorOf(bob));
        Assert.Equal(TimeControl.Blitz3Plus2, game.TimeControl);
    }

    [Fact]
    public async Task PlayerInAGameCannotSeek()
    {
        var alice = Guid.NewGuid();
        _lobby.TrySeek(alice, "alice", 1500, "3+2");
        _lobby.TrySeek(Guid.NewGuid(), "bob", 1500, "3+2");
        await _worker.RunOnceAsync();

        Assert.Equal("Finish your current game first.", _lobby.TrySeek(alice, "alice", 1500, "3+2"));
        Assert.Equal("'banana' is not a valid time control.", _lobby.TrySeek(Guid.NewGuid(), "x", 1500, "banana"));
    }

    [Fact]
    public void LobbySnapshotListsSeeksAndPresence()
    {
        _lobby.Connected("c1", Guid.NewGuid());
        var me = Guid.NewGuid();
        _lobby.Connected("c2", me);
        _lobby.Connected("c3", me);
        _lobby.TrySeek(me, "me", 1700, "5+0");
        _time.Advance(TimeSpan.FromSeconds(4));

        var snapshot = _lobby.Snapshot();
        Assert.Equal(2, snapshot.OnlinePlayers);
        var seek = Assert.Single(snapshot.Seeks);
        Assert.Equal(4, seek.WaitingSeconds);

        _lobby.Disconnected("c2");
        Assert.Single(_lobby.Snapshot().Seeks);
        _lobby.Disconnected("c3");
        Assert.Empty(_lobby.Snapshot().Seeks);
    }

    private sealed class Notifier : IArenaNotifier
    {
        public List<(Guid PlayerId, MatchFoundDto Match)> Matches { get; } = [];

        public Task GameUpdatedAsync(GameStateDto state) => Task.CompletedTask;

        public Task MatchFoundAsync(Guid playerId, MatchFoundDto match)
        {
            Matches.Add((playerId, match));
            return Task.CompletedTask;
        }

        public Task LobbyUpdatedAsync(LobbySnapshotDto lobby) => Task.CompletedTask;
    }

    private sealed class NullRecorder : IGameRecorder
    {
        public Task<RatingChange?> RecordAsync(CompletedGame completed) => Task.FromResult<RatingChange?>(null);
    }
}
