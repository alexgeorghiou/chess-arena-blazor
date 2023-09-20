using System.Collections.Concurrent;
using ChessArena.Core.Matchmaking;
using ChessArena.Engine;
using ChessArena.Web.Data;
using ChessArena.Web.Hubs;

namespace ChessArena.Web.Live;

/// <summary>
/// Owns every live game in the process. Commands are validated and applied under the game's lock;
/// notifications and persistence happen after the lock is released so slow I/O never blocks play.
/// </summary>
public sealed class GameManager(
    IArenaNotifier notifier,
    IGameRecorder recorder,
    TimeProvider time,
    ILogger<GameManager> logger)
{
    private readonly ConcurrentDictionary<Guid, LiveGame> _games = new();

    public IReadOnlyCollection<LiveGame> All => [.. _games.Values];

    public IReadOnlyList<LiveGame> Active => _games.Values.Where(g => !g.Game.IsOver).OrderBy(g => g.StartedAt).ToList();

    public LiveGame? Find(Guid gameId) => _games.GetValueOrDefault(gameId);

    public LiveGame? ActiveGameFor(Guid playerId) =>
        _games.Values.FirstOrDefault(g => !g.Game.IsOver && g.ColorOf(playerId) is not null);

    public LiveGame Create(Pairing pairing)
    {
        var game = new LiveGame(
            Guid.NewGuid(),
            new Seat(pairing.White.PlayerId, pairing.White.Handle, pairing.White.Rating),
            new Seat(pairing.Black.PlayerId, pairing.Black.Handle, pairing.Black.Rating),
            pairing.TimeControl,
            time);

        game.Clock.Start(Color.White);
        _games[game.Id] = game;
        logger.LogInformation("Game {GameId} started: {White} vs {Black} ({TimeControl})",
            game.Id, game.White.Handle, game.Black.Handle, game.TimeControl);
        return game;
    }

    public Task<CommandResultDto> MoveAsync(Guid gameId, Guid playerId, string uci) =>
        ExecuteAsync(gameId, playerId, (live, color) =>
        {
            if (live.Game.SideToMove != color)
            {
                return "It is not your turn.";
            }

            if (!live.Game.TryResolveUci(uci, out var move, out var rejection))
            {
                return rejection == MoveRejection.Malformed ? $"'{uci}' is not a move." : $"{uci} is not legal here.";
            }

            // The clock decides first: a legal move made after the flag fell does not count.
            if (live.Clock.Press(color) == Core.Clocks.PressResult.Flagged)
            {
                live.Game.FlagFall(color);
                return null;
            }

            live.Game.TryMove(move);
            live.RecordClock(color);
            live.DrawOfferedBy = null;
            return null;
        });

    public Task<CommandResultDto> ResignAsync(Guid gameId, Guid playerId) =>
        ExecuteAsync(gameId, playerId, (live, color) =>
        {
            live.Game.Resign(color);
            return null;
        });

    /// <summary>Offers a draw, or accepts the opponent's standing offer.</summary>
    public Task<CommandResultDto> OfferDrawAsync(Guid gameId, Guid playerId) =>
        ExecuteAsync(gameId, playerId, (live, color) =>
        {
            if (live.DrawOfferedBy == color.Opponent())
            {
                live.Game.AgreeDraw();
            }
            else
            {
                live.DrawOfferedBy = color;
            }

            return null;
        });

    /// <summary>Flags every game whose running clock has expired. Called by the clock watchdog.</summary>
    public async Task<int> CheckClocksAsync()
    {
        var flagged = new List<LiveGame>();
        foreach (var live in _games.Values)
        {
            lock (live.Gate)
            {
                if (live.Game.IsOver || live.Clock.CheckFlag() is not { } color)
                {
                    continue;
                }

                live.Game.FlagFall(color);
                live.MarkEnded(time.GetUtcNow());
                flagged.Add(live);
            }
        }

        foreach (var live in flagged)
        {
            await FinishAsync(live);
        }

        return flagged.Count;
    }

    public GameStateDto? AddSpectator(Guid gameId, string connectionId)
    {
        if (Find(gameId) is not { } live)
        {
            return null;
        }

        lock (live.Gate)
        {
            live.AddSpectator(connectionId);
            return live.ToDto();
        }
    }

    /// <summary>Removes a disconnected viewer everywhere and returns the games whose audience changed.</summary>
    public IReadOnlyList<GameStateDto> RemoveSpectator(string connectionId)
    {
        var changed = new List<GameStateDto>();
        foreach (var live in _games.Values)
        {
            lock (live.Gate)
            {
                if (live.RemoveSpectator(connectionId))
                {
                    changed.Add(live.ToDto());
                }
            }
        }

        return changed;
    }

    /// <summary>Drops finished games from memory once nobody needs their live view any more.</summary>
    public int PurgeFinished(TimeSpan retention)
    {
        var cutoff = time.GetUtcNow() - retention;
        var removed = 0;
        foreach (var (id, live) in _games)
        {
            if (live.EndedAt is { } ended && ended < cutoff && _games.TryRemove(id, out _))
            {
                removed++;
            }
        }

        return removed;
    }

    private async Task<CommandResultDto> ExecuteAsync(Guid gameId, Guid playerId, Func<LiveGame, Color, string?> command)
    {
        if (Find(gameId) is not { } live)
        {
            return CommandResultDto.Fail("Game not found.");
        }

        GameStateDto state;
        bool finished;
        lock (live.Gate)
        {
            if (live.Game.IsOver)
            {
                return CommandResultDto.Fail("The game is over.");
            }

            if (live.ColorOf(playerId) is not { } color)
            {
                return CommandResultDto.Fail("You are not playing in this game.");
            }

            if (command(live, color) is { } error)
            {
                return CommandResultDto.Fail(error);
            }

            finished = live.Game.IsOver;
            if (finished)
            {
                live.MarkEnded(time.GetUtcNow());
            }

            state = live.ToDto();
        }

        if (finished)
        {
            await FinishAsync(live);
        }
        else
        {
            await notifier.GameUpdatedAsync(state);
        }

        return CommandResultDto.Success;
    }

    private async Task FinishAsync(LiveGame live)
    {
        logger.LogInformation("Game {GameId} ended {Result} by {Termination}",
            live.Id, live.Game.Result.ToPgn(), live.Game.Termination);

        var completed = new CompletedGame(
            live.Id,
            live.White.PlayerId,
            live.Black.PlayerId,
            live.TimeControl,
            live.Game,
            live.ClockMillis,
            live.StartedAt,
            live.EndedAt ?? time.GetUtcNow());

        var change = await recorder.RecordAsync(completed);

        GameStateDto state;
        lock (live.Gate)
        {
            live.RatingChange = change;
            state = live.ToDto();
        }

        await notifier.GameUpdatedAsync(state);
    }
}
