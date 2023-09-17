using ChessArena.Core.Clocks;
using ChessArena.Core.Ratings;
using ChessArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace ChessArena.Web.Data;

/// <summary>Everything needed to archive a finished game.</summary>
public sealed record CompletedGame(
    Guid GameId,
    Guid WhiteId,
    Guid BlackId,
    TimeControl TimeControl,
    ChessGame Game,
    IReadOnlyList<long> ClockMillis,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt);

public sealed record GameListItem(
    Guid Id,
    string White,
    string Black,
    string Result,
    string Termination,
    string TimeControl,
    int Plies,
    DateTime EndedAtUtc);

public interface IGameArchive
{
    /// <summary>Applies Elo updates to both players and stores the game, atomically.</summary>
    Task<RatingChange> RecordAsync(CompletedGame completed, CancellationToken ct = default);

    Task<IReadOnlyList<GameListItem>> RecentAsync(int take, Guid? playerId = null, CancellationToken ct = default);

    Task<GameEntity?> FindAsync(Guid id, CancellationToken ct = default);
}

public sealed class GameArchive(ArenaDbContext db) : IGameArchive
{
    public async Task<RatingChange> RecordAsync(CompletedGame completed, CancellationToken ct = default)
    {
        var game = completed.Game;
        if (!game.IsOver)
        {
            throw new InvalidOperationException("Only finished games can be archived.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var white = await db.Players.SingleAsync(p => p.Id == completed.WhiteId, ct);
        var black = await db.Players.SingleAsync(p => p.Id == completed.BlackId, ct);
        var change = Elo.Apply(white.ToRating(), black.ToRating(), game.Result);

        Apply(white, change.WhiteAfter, game.Result, Color.White);
        Apply(black, change.BlackAfter, game.Result, Color.Black);

        var entity = new GameEntity
        {
            Id = completed.GameId,
            WhitePlayerId = white.Id,
            BlackPlayerId = black.Id,
            TimeControl = completed.TimeControl.ToString(),
            StartFen = game.StartFen,
            FinalFen = game.Fen,
            Result = game.Result.ToPgn(),
            Termination = game.Termination.ToString(),
            WhiteRatingBefore = change.WhiteBefore,
            WhiteRatingAfter = change.WhiteAfter,
            BlackRatingBefore = change.BlackBefore,
            BlackRatingAfter = change.BlackAfter,
            StartedAtUtc = completed.StartedAt.UtcDateTime,
            EndedAtUtc = completed.EndedAt.UtcDateTime,
            Pgn = game.ToPgn(new PgnHeaders
            {
                Event = $"Rated {completed.TimeControl.Category.ToString().ToLowerInvariant()} game",
                Date = DateOnly.FromDateTime(completed.StartedAt.UtcDateTime),
                White = white.Handle,
                Black = black.Handle,
                WhiteElo = change.WhiteBefore,
                BlackElo = change.BlackBefore,
                TimeControl = completed.TimeControl.ToPgn(),
            }),
            Moves = game.Moves.Select((m, i) => new MoveEntity
            {
                GameId = completed.GameId,
                Ply = m.Ply,
                Uci = m.Move.ToUci(),
                San = m.San,
                FenAfter = m.FenAfter,
                ClockMillis = i < completed.ClockMillis.Count ? completed.ClockMillis[i] : 0,
            }).ToList(),
        };

        db.Games.Add(entity);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return change;
    }

    public async Task<IReadOnlyList<GameListItem>> RecentAsync(int take, Guid? playerId = null, CancellationToken ct = default)
    {
        var query = db.Games.AsNoTracking();
        if (playerId is { } id)
        {
            query = query.Where(g => g.WhitePlayerId == id || g.BlackPlayerId == id);
        }

        return await query
            .OrderByDescending(g => g.EndedAtUtc)
            .Take(take)
            .Select(g => new GameListItem(
                g.Id,
                g.WhitePlayer!.Handle,
                g.BlackPlayer!.Handle,
                g.Result,
                g.Termination,
                g.TimeControl,
                g.Moves.Count,
                g.EndedAtUtc))
            .ToListAsync(ct);
    }

    public Task<GameEntity?> FindAsync(Guid id, CancellationToken ct = default) =>
        db.Games.AsNoTracking()
            .Include(g => g.WhitePlayer)
            .Include(g => g.BlackPlayer)
            .Include(g => g.Moves.OrderBy(m => m.Ply))
            .FirstOrDefaultAsync(g => g.Id == id, ct);

    private static void Apply(PlayerEntity player, int newRating, GameResult result, Color color)
    {
        player.Rating = newRating;
        player.GamesPlayed++;
        if (result == GameResult.Draw)
        {
            player.Draws++;
        }
        else if (result == GameResultExtensions.WinFor(color))
        {
            player.Wins++;
        }
        else
        {
            player.Losses++;
        }
    }
}
