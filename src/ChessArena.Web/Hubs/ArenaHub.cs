using ChessArena.Web.Data;
using ChessArena.Web.Live;
using Microsoft.AspNetCore.SignalR;

namespace ChessArena.Web.Hubs;

/// <summary>
/// Real-time endpoint for lobby, matchmaking and play. Players are identified by the id they
/// registered their handle with; there is deliberately no password-based authentication (see README).
/// </summary>
public sealed class ArenaHub(LobbyService lobby, GameManager games, IPlayerStore players) : Hub<IArenaClient>
{
    public const string Path = "/hubs/arena";
    public const string LobbyGroup = "lobby";

    public static string PlayerGroup(Guid playerId) => $"player:{playerId:N}";

    public static string GameGroup(Guid gameId) => $"game:{gameId:N}";

    public async Task<LobbySnapshotDto> EnterLobby(Guid playerId)
    {
        if (await players.FindAsync(playerId) is null)
        {
            throw new HubException("Unknown player.");
        }

        lobby.Connected(Context.ConnectionId, playerId);
        await Groups.AddToGroupAsync(Context.ConnectionId, LobbyGroup);
        await Groups.AddToGroupAsync(Context.ConnectionId, PlayerGroup(playerId));
        return lobby.Snapshot();
    }

    public async Task<CommandResultDto> Seek(Guid playerId, string timeControl)
    {
        var player = await players.FindAsync(playerId);
        if (player is null)
        {
            return CommandResultDto.Fail("Unknown player.");
        }

        var error = lobby.TrySeek(player.Id, player.Handle, player.Rating, timeControl);
        if (error is not null)
        {
            return CommandResultDto.Fail(error);
        }

        await Clients.Group(LobbyGroup).LobbyUpdated(lobby.Snapshot());
        return CommandResultDto.Success;
    }

    public async Task CancelSeek(Guid playerId)
    {
        if (lobby.Matchmaker.Cancel(playerId))
        {
            await Clients.Group(LobbyGroup).LobbyUpdated(lobby.Snapshot());
        }
    }

    /// <summary>Subscribes to a game's updates. Anyone who is not seated counts as a spectator.</summary>
    public async Task<GameStateDto?> WatchGame(Guid gameId, Guid? playerId)
    {
        var live = games.Find(gameId);
        if (live is null)
        {
            return null;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GameGroup(gameId));
        if (playerId is { } id && live.ColorOf(id) is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, PlayerGroup(id));
            return live.ToDto();
        }

        var state = games.AddSpectator(gameId, Context.ConnectionId);
        if (state is not null)
        {
            await Clients.OthersInGroup(GameGroup(gameId)).GameUpdated(state);
        }

        return state;
    }

    public Task<CommandResultDto> MakeMove(Guid gameId, Guid playerId, string uci) => games.MoveAsync(gameId, playerId, uci);

    public Task<CommandResultDto> Resign(Guid gameId, Guid playerId) => games.ResignAsync(gameId, playerId);

    public Task<CommandResultDto> OfferDraw(Guid gameId, Guid playerId) => games.OfferDrawAsync(gameId, playerId);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        lobby.Disconnected(Context.ConnectionId);
        foreach (var state in games.RemoveSpectator(Context.ConnectionId))
        {
            await Clients.Group(GameGroup(state.GameId)).GameUpdated(state);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
