using ChessArena.Core.Clocks;
using ChessArena.Core.Matchmaking;
using ChessArena.Core.Ratings;
using ChessArena.Engine;
using ChessArena.Web.Data;
using ChessArena.Web.Hubs;
using ChessArena.Web.Live;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ChessArena.Web.Tests.Live;

public class GameManagerTests
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-01-01T12:00:00Z"));
    private readonly RecordingNotifier _notifier = new();
    private readonly FakeRecorder _recorder = new();
    private readonly GameManager _manager;
    private readonly Seek _alice;
    private readonly Seek _bob;

    public GameManagerTests()
    {
        _manager = new GameManager(_notifier, _recorder, _time, NullLogger<GameManager>.Instance);
        _alice = new Seek(Guid.NewGuid(), "alice", 1500, TimeControl.Blitz3Plus2, _time.GetUtcNow());
        _bob = new Seek(Guid.NewGuid(), "bob", 1520, TimeControl.Blitz3Plus2, _time.GetUtcNow());
    }

    private LiveGame StartGame(string control = "3+2") =>
        _manager.Create(new Pairing(_alice, _bob, TimeControl.Parse(control)));

    [Fact]
    public async Task PlayersAlternateMoves()
    {
        var game = StartGame();

        Assert.True((await _manager.MoveAsync(game.Id, _alice.PlayerId, "e2e4")).Ok);
        Assert.True((await _manager.MoveAsync(game.Id, _bob.PlayerId, "e7e5")).Ok);

        Assert.Equal(["e4", "e5"], game.Game.Moves.Select(m => m.San));
        Assert.Equal(2, _notifier.Updates.Count);
        Assert.Equal("e7e5", _notifier.Updates[^1].LastMove);
    }

    [Fact]
    public async Task RejectsMovesOutOfTurnOrFromOutsiders()
    {
        var game = StartGame();

        var outOfTurn = await _manager.MoveAsync(game.Id, _bob.PlayerId, "e7e5");
        var outsider = await _manager.MoveAsync(game.Id, Guid.NewGuid(), "e2e4");
        var illegal = await _manager.MoveAsync(game.Id, _alice.PlayerId, "e2e5");
        var unknown = await _manager.MoveAsync(Guid.NewGuid(), _alice.PlayerId, "e2e4");

        Assert.Equal("It is not your turn.", outOfTurn.Error);
        Assert.Equal("You are not playing in this game.", outsider.Error);
        Assert.Equal("e2e5 is not legal here.", illegal.Error);
        Assert.Equal("Game not found.", unknown.Error);
        Assert.Empty(game.Game.Moves);
        Assert.Empty(_notifier.Updates);
    }

    [Fact]
    public async Task ThinkingTimeIsChargedAndIncrementAdded()
    {
        var game = StartGame();
        _time.Advance(TimeSpan.FromSeconds(10));

        await _manager.MoveAsync(game.Id, _alice.PlayerId, "e2e4");

        Assert.Equal(TimeSpan.FromSeconds(172), game.Clock.Remaining(Color.White));
        Assert.Equal([172_000L], game.ClockMillis);
        Assert.Equal("Black", _notifier.Updates[^1].ClockRunning);
    }

    [Fact]
    public async Task CheckmateFinishesAndRecordsOnce()
    {
        var game = StartGame();
        foreach (var (player, uci) in new[] { (_alice, "f2f3"), (_bob, "e7e5"), (_alice, "g2g4"), (_bob, "d8h4") })
        {
            await _manager.MoveAsync(game.Id, player.PlayerId, uci);
        }

        Assert.Equal(GameResult.BlackWins, game.Game.Result);
        Assert.Single(_recorder.Recorded);
        Assert.Equal(-9, _notifier.Updates[^1].WhiteRatingDelta);
        Assert.True(_notifier.Updates[^1].IsOver);
        Assert.Null(game.Clock.Running);
        Assert.Null(_manager.ActiveGameFor(_alice.PlayerId));

        var late = await _manager.ResignAsync(game.Id, _alice.PlayerId);
        Assert.False(late.Ok);
        Assert.Single(_recorder.Recorded);
    }

    [Fact]
    public async Task ResignationEndsTheGame()
    {
        var game = StartGame();
        await _manager.ResignAsync(game.Id, _bob.PlayerId);

        Assert.Equal(GameResult.WhiteWins, game.Game.Result);
        Assert.Equal(Termination.Resignation, game.Game.Termination);
        Assert.Single(_recorder.Recorded);
    }

    [Fact]
    public async Task DrawNeedsBothPlayers()
    {
        var game = StartGame();

        await _manager.OfferDrawAsync(game.Id, _alice.PlayerId);
        Assert.Equal(Color.White, game.DrawOfferedBy);
        Assert.False(game.Game.IsOver);

        await _manager.OfferDrawAsync(game.Id, _alice.PlayerId);
        Assert.False(game.Game.IsOver);

        await _manager.OfferDrawAsync(game.Id, _bob.PlayerId);
        Assert.Equal(Termination.DrawAgreement, game.Game.Termination);
    }

    [Fact]
    public async Task MovingDeclinesPendingDrawOffer()
    {
        var game = StartGame();
        await _manager.OfferDrawAsync(game.Id, _bob.PlayerId);
        await _manager.MoveAsync(game.Id, _alice.PlayerId, "d2d4");

        Assert.Null(game.DrawOfferedBy);
    }

    [Fact]
    public async Task WatchdogFlagsExpiredClock()
    {
        var game = StartGame("1+0");
        await _manager.MoveAsync(game.Id, _alice.PlayerId, "e2e4");

        _time.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(0, await _manager.CheckClocksAsync());

        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(1, await _manager.CheckClocksAsync());
        Assert.Equal(GameResult.WhiteWins, game.Game.Result);
        Assert.Equal(Termination.Timeout, game.Game.Termination);
        Assert.Equal(0, await _manager.CheckClocksAsync());
        Assert.Single(_recorder.Recorded);
    }

    [Fact]
    public async Task MoveAfterFlagFallLosesOnTime()
    {
        var game = StartGame("1+0");
        _time.Advance(TimeSpan.FromSeconds(61));

        var result = await _manager.MoveAsync(game.Id, _alice.PlayerId, "e2e4");

        Assert.True(result.Ok);
        Assert.Empty(game.Game.Moves);
        Assert.Equal(GameResult.BlackWins, game.Game.Result);
        Assert.Equal(Termination.Timeout, game.Game.Termination);
    }

    [Fact]
    public void SpectatorsAreCountedPerConnection()
    {
        var game = StartGame();

        Assert.Equal(1, _manager.AddSpectator(game.Id, "c1")!.Spectators);
        Assert.Equal(1, _manager.AddSpectator(game.Id, "c1")!.Spectators);
        Assert.Equal(2, _manager.AddSpectator(game.Id, "c2")!.Spectators);
        Assert.Null(_manager.AddSpectator(Guid.NewGuid(), "c3"));

        var changed = Assert.Single(_manager.RemoveSpectator("c1"));
        Assert.Equal(1, changed.Spectators);
        Assert.Empty(_manager.RemoveSpectator("c1"));
    }

    [Fact]
    public async Task FinishedGamesArePurgedAfterRetention()
    {
        var game = StartGame();
        await _manager.ResignAsync(game.Id, _alice.PlayerId);

        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(0, _manager.PurgeFinished(TimeSpan.FromMinutes(10)));

        _time.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(1, _manager.PurgeFinished(TimeSpan.FromMinutes(10)));
        Assert.Null(_manager.Find(game.Id));
    }

    [Fact]
    public async Task ConcurrentMovesAreSerialised()
    {
        var game = StartGame();

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => _manager.MoveAsync(game.Id, _alice.PlayerId, "e2e4"))));

        Assert.Single(results, r => r.Ok);
        Assert.Single(game.Game.Moves);
    }

    private sealed class RecordingNotifier : IArenaNotifier
    {
        private readonly List<GameStateDto> _updates = [];

        public IReadOnlyList<GameStateDto> Updates
        {
            get
            {
                lock (_updates)
                {
                    return [.. _updates];
                }
            }
        }

        public Task GameUpdatedAsync(GameStateDto state)
        {
            lock (_updates)
            {
                _updates.Add(state);
            }

            return Task.CompletedTask;
        }

        public Task MatchFoundAsync(Guid playerId, MatchFoundDto match) => Task.CompletedTask;

        public Task LobbyUpdatedAsync(LobbySnapshotDto lobby) => Task.CompletedTask;
    }

    private sealed class FakeRecorder : IGameRecorder
    {
        public List<CompletedGame> Recorded { get; } = [];

        public Task<RatingChange?> RecordAsync(CompletedGame completed)
        {
            Recorded.Add(completed);
            var white = new PlayerRating(1500, 50);
            var black = new PlayerRating(1520, 50);
            return Task.FromResult<RatingChange?>(Elo.Apply(white, black, completed.Game.Result));
        }
    }
}
