using ChessArena.Web.Live;
using Microsoft.AspNetCore.SignalR;

namespace ChessArena.Web.Hubs;

public sealed class HubArenaNotifier(IHubContext<ArenaHub, IArenaClient> hub) : IArenaNotifier
{
    public Task GameUpdatedAsync(GameStateDto state) =>
        hub.Clients.Group(ArenaHub.GameGroup(state.GameId)).GameUpdated(state);

    public Task MatchFoundAsync(Guid playerId, MatchFoundDto match) =>
        hub.Clients.Group(ArenaHub.PlayerGroup(playerId)).MatchFound(match);

    public Task LobbyUpdatedAsync(LobbySnapshotDto lobby) =>
        hub.Clients.Group(ArenaHub.LobbyGroup).LobbyUpdated(lobby);
}
