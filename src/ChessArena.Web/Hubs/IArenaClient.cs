namespace ChessArena.Web.Hubs;

/// <summary>Server-to-client messages of <see cref="ArenaHub"/>.</summary>
public interface IArenaClient
{
    Task LobbyUpdated(LobbySnapshotDto lobby);

    Task MatchFound(MatchFoundDto match);

    Task GameUpdated(GameStateDto state);
}
