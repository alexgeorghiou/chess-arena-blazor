using ChessArena.Core.Ratings;

namespace ChessArena.Web.Data;

public sealed class PlayerEntity
{
    public Guid Id { get; set; }

    public required string Handle { get; set; }

    public int Rating { get; set; } = Elo.DefaultRating;

    public int GamesPlayed { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public int Draws { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public PlayerRating ToRating() => new(Rating, GamesPlayed);
}

public sealed class GameEntity
{
    public Guid Id { get; set; }

    public Guid WhitePlayerId { get; set; }

    public PlayerEntity? WhitePlayer { get; set; }

    public Guid BlackPlayerId { get; set; }

    public PlayerEntity? BlackPlayer { get; set; }

    public required string TimeControl { get; set; }

    public required string StartFen { get; set; }

    public required string FinalFen { get; set; }

    /// <summary>PGN result token: 1-0, 0-1 or 1/2-1/2.</summary>
    public required string Result { get; set; }

    public required string Termination { get; set; }

    public int WhiteRatingBefore { get; set; }

    public int WhiteRatingAfter { get; set; }

    public int BlackRatingBefore { get; set; }

    public int BlackRatingAfter { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime EndedAtUtc { get; set; }

    public required string Pgn { get; set; }

    public List<MoveEntity> Moves { get; set; } = [];
}

public sealed class MoveEntity
{
    public long Id { get; set; }

    public Guid GameId { get; set; }

    public int Ply { get; set; }

    public required string Uci { get; set; }

    public required string San { get; set; }

    public required string FenAfter { get; set; }

    /// <summary>The mover's remaining clock time after the move, increment included.</summary>
    public long ClockMillis { get; set; }
}
