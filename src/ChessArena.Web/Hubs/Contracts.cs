namespace ChessArena.Web.Hubs;

/// <summary>Full state of a live game as pushed to players and spectators.</summary>
public sealed record GameStateDto
{
    public required Guid GameId { get; init; }
    public required PlayerSeatDto White { get; init; }
    public required PlayerSeatDto Black { get; init; }
    public required string TimeControl { get; init; }
    public required string Fen { get; init; }
    public required string SideToMove { get; init; }
    public required bool IsCheck { get; init; }
    public required IReadOnlyList<string> LegalMoves { get; init; }
    public required IReadOnlyList<string> SanMoves { get; init; }
    public string? LastMove { get; init; }
    public required long WhiteMillis { get; init; }
    public required long BlackMillis { get; init; }
    public string? ClockRunning { get; init; }
    public required DateTimeOffset ServerTime { get; init; }
    public required string Result { get; init; }
    public required string Termination { get; init; }
    public string? DrawOfferedBy { get; init; }
    public required int Spectators { get; init; }
    public int? WhiteRatingDelta { get; init; }
    public int? BlackRatingDelta { get; init; }

    public bool IsOver => Result != "*";
}

public sealed record PlayerSeatDto(Guid Id, string Handle, int Rating);

public sealed record LiveGameSummaryDto(Guid GameId, string White, string Black, int WhiteRating, int BlackRating, string TimeControl, int Plies, int Spectators);

public sealed record SeekDto(string Handle, int Rating, string TimeControl, int WaitingSeconds);

public sealed record LobbySnapshotDto(IReadOnlyList<SeekDto> Seeks, IReadOnlyList<LiveGameSummaryDto> Games, int OnlinePlayers);

public sealed record MatchFoundDto(Guid GameId, string Color, string Opponent, int OpponentRating, string TimeControl);

public sealed record CommandResultDto(bool Ok, string? Error = null)
{
    public static readonly CommandResultDto Success = new(true);

    public static CommandResultDto Fail(string error) => new(false, error);
}
