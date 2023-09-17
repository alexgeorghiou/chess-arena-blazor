using ChessArena.Core.Clocks;
using ChessArena.Core.Ratings;
using ChessArena.Engine;
using ChessArena.Web.Hubs;

namespace ChessArena.Web.Live;

public sealed record Seat(Guid PlayerId, string Handle, int Rating);

/// <summary>
/// A game in progress: rules, clock, seats and spectators. All mutation goes through
/// <see cref="Gate"/> so a move, a flag fall and a resignation can never interleave.
/// </summary>
public sealed class LiveGame
{
    private readonly HashSet<string> _spectators = [];
    private readonly List<long> _clockMillis = [];

    public LiveGame(Guid id, Seat white, Seat black, TimeControl timeControl, TimeProvider time)
    {
        Id = id;
        White = white;
        Black = black;
        TimeControl = timeControl;
        Game = new ChessGame();
        Clock = new GameClock(timeControl, time);
        StartedAt = time.GetUtcNow();
    }

    public Guid Id { get; }

    public Seat White { get; }

    public Seat Black { get; }

    public TimeControl TimeControl { get; }

    public ChessGame Game { get; }

    public GameClock Clock { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? EndedAt { get; private set; }

    public object Gate { get; } = new();

    public Color? DrawOfferedBy { get; set; }

    public RatingChange? RatingChange { get; set; }

    /// <summary>Remaining time of the mover after each ply, recorded for the archive.</summary>
    public IReadOnlyList<long> ClockMillis => _clockMillis;

    public int SpectatorCount => _spectators.Count;

    public Color? ColorOf(Guid playerId) =>
        playerId == White.PlayerId ? Color.White :
        playerId == Black.PlayerId ? Color.Black :
        null;

    public Seat SeatOf(Color color) => color == Color.White ? White : Black;

    public bool AddSpectator(string connectionId) => _spectators.Add(connectionId);

    public bool RemoveSpectator(string connectionId) => _spectators.Remove(connectionId);

    internal void RecordClock(Color mover) => _clockMillis.Add((long)Clock.Remaining(mover).TotalMilliseconds);

    internal void MarkEnded(DateTimeOffset at)
    {
        Clock.Stop();
        EndedAt ??= at;
    }

    public GameStateDto ToDto()
    {
        var snapshot = Clock.Snapshot();
        return new GameStateDto
        {
            GameId = Id,
            White = new PlayerSeatDto(White.PlayerId, White.Handle, White.Rating),
            Black = new PlayerSeatDto(Black.PlayerId, Black.Handle, Black.Rating),
            TimeControl = TimeControl.ToString(),
            Fen = Game.Fen,
            SideToMove = Game.SideToMove.ToString(),
            IsCheck = Game.IsInCheck,
            LegalMoves = Game.LegalMoves.Select(m => m.ToUci()).ToList(),
            SanMoves = Game.Moves.Select(m => m.San).ToList(),
            LastMove = Game.LastMove?.Move.ToUci(),
            WhiteMillis = (long)snapshot.White.TotalMilliseconds,
            BlackMillis = (long)snapshot.Black.TotalMilliseconds,
            ClockRunning = snapshot.Running?.ToString(),
            ServerTime = snapshot.TakenAt,
            Result = Game.Result.ToPgn(),
            Termination = Game.Termination.ToString(),
            DrawOfferedBy = DrawOfferedBy?.ToString(),
            Spectators = SpectatorCount,
            WhiteRatingDelta = RatingChange?.WhiteDelta,
            BlackRatingDelta = RatingChange?.BlackDelta,
        };
    }
}
