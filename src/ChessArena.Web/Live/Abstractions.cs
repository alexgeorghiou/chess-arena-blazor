using ChessArena.Core.Ratings;
using ChessArena.Web.Data;
using ChessArena.Web.Hubs;

namespace ChessArena.Web.Live;

/// <summary>Pushes live events to connected clients. Implemented over SignalR; faked in tests.</summary>
public interface IArenaNotifier
{
    Task GameUpdatedAsync(GameStateDto state);

    Task MatchFoundAsync(Guid playerId, MatchFoundDto match);

    Task LobbyUpdatedAsync(LobbySnapshotDto lobby);
}

/// <summary>Persists a finished game and returns the rating change, or null if persistence failed.</summary>
public interface IGameRecorder
{
    Task<RatingChange?> RecordAsync(CompletedGame completed);
}
