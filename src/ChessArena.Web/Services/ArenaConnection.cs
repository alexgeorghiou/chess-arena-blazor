using ChessArena.Web.Hubs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace ChessArena.Web.Services;

/// <summary>Typed client for <see cref="ArenaHub"/> used by interactive components.</summary>
public interface IArenaConnection : IAsyncDisposable
{
    event Func<LobbySnapshotDto, Task>? LobbyUpdated;

    event Func<MatchFoundDto, Task>? MatchFound;

    event Func<GameStateDto, Task>? GameUpdated;

    Task<LobbySnapshotDto> EnterLobbyAsync(Guid playerId);

    Task<CommandResultDto> SeekAsync(Guid playerId, string timeControl);

    Task CancelSeekAsync(Guid playerId);

    Task<GameStateDto?> WatchGameAsync(Guid gameId, Guid? playerId);

    Task<CommandResultDto> MakeMoveAsync(Guid gameId, Guid playerId, string uci);

    Task<CommandResultDto> ResignAsync(Guid gameId, Guid playerId);

    Task<CommandResultDto> OfferDrawAsync(Guid gameId, Guid playerId);
}

/// <summary>
/// One hub connection per circuit, opened lazily. Blazor Server components run on the server, so
/// this is a server-to-server SignalR connection; it gives components exactly the same protocol and
/// group semantics as any external client of the hub.
/// </summary>
public sealed class ArenaConnection : IArenaConnection
{
    private readonly HubConnection _connection;
    private readonly SemaphoreSlim _startLock = new(1, 1);

    public ArenaConnection(NavigationManager navigation, IOptions<ArenaOptions> options)
    {
        var baseUri = new Uri(options.Value.HubBaseUrl ?? navigation.BaseUri);
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(baseUri, ArenaHub.Path.TrimStart('/')))
            .WithAutomaticReconnect()
            .Build();

        _connection.On<LobbySnapshotDto>(nameof(IArenaClient.LobbyUpdated), lobby => Raise(LobbyUpdated, lobby));
        _connection.On<MatchFoundDto>(nameof(IArenaClient.MatchFound), match => Raise(MatchFound, match));
        _connection.On<GameStateDto>(nameof(IArenaClient.GameUpdated), state => Raise(GameUpdated, state));
    }

    public event Func<LobbySnapshotDto, Task>? LobbyUpdated;

    public event Func<MatchFoundDto, Task>? MatchFound;

    public event Func<GameStateDto, Task>? GameUpdated;

    public async Task<LobbySnapshotDto> EnterLobbyAsync(Guid playerId) =>
        await InvokeAsync<LobbySnapshotDto>(nameof(ArenaHub.EnterLobby), playerId);

    public Task<CommandResultDto> SeekAsync(Guid playerId, string timeControl) =>
        InvokeAsync<CommandResultDto>(nameof(ArenaHub.Seek), playerId, timeControl);

    public async Task CancelSeekAsync(Guid playerId)
    {
        await EnsureStartedAsync();
        await _connection.InvokeAsync(nameof(ArenaHub.CancelSeek), playerId);
    }

    public Task<GameStateDto?> WatchGameAsync(Guid gameId, Guid? playerId) =>
        InvokeAsync<GameStateDto?>(nameof(ArenaHub.WatchGame), gameId, playerId);

    public Task<CommandResultDto> MakeMoveAsync(Guid gameId, Guid playerId, string uci) =>
        InvokeAsync<CommandResultDto>(nameof(ArenaHub.MakeMove), gameId, playerId, uci);

    public Task<CommandResultDto> ResignAsync(Guid gameId, Guid playerId) =>
        InvokeAsync<CommandResultDto>(nameof(ArenaHub.Resign), gameId, playerId);

    public Task<CommandResultDto> OfferDrawAsync(Guid gameId, Guid playerId) =>
        InvokeAsync<CommandResultDto>(nameof(ArenaHub.OfferDraw), gameId, playerId);

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        _startLock.Dispose();
    }

    private async Task<T> InvokeAsync<T>(string method, params object?[] args)
    {
        await EnsureStartedAsync();
        return await _connection.InvokeCoreAsync<T>(method, args);
    }

    private async Task EnsureStartedAsync()
    {
        if (_connection.State == HubConnectionState.Connected)
        {
            return;
        }

        await _startLock.WaitAsync();
        try
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync();
            }
        }
        finally
        {
            _startLock.Release();
        }
    }

    private static Task Raise<T>(Func<T, Task>? handlers, T payload) =>
        handlers is null
            ? Task.CompletedTask
            : Task.WhenAll(handlers.GetInvocationList().Cast<Func<T, Task>>().Select(h => h(payload)));
}
