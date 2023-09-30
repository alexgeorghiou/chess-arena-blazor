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
public sealed class ArenaConnection(NavigationManager navigation, IOptions<ArenaOptions> options) : IArenaConnection
{
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private HubConnection? _connection;

    public event Func<LobbySnapshotDto, Task>? LobbyUpdated;

    public event Func<MatchFoundDto, Task>? MatchFound;

    public event Func<GameStateDto, Task>? GameUpdated;

    public async Task<LobbySnapshotDto> EnterLobbyAsync(Guid playerId) =>
        await InvokeAsync<LobbySnapshotDto>(nameof(ArenaHub.EnterLobby), playerId);

    public Task<CommandResultDto> SeekAsync(Guid playerId, string timeControl) =>
        InvokeAsync<CommandResultDto>(nameof(ArenaHub.Seek), playerId, timeControl);

    public async Task CancelSeekAsync(Guid playerId)
    {
        var connection = await EnsureStartedAsync();
        await connection.InvokeAsync(nameof(ArenaHub.CancelSeek), playerId);
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
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _startLock.Dispose();
    }

    private async Task<T> InvokeAsync<T>(string method, params object?[] args)
    {
        var connection = await EnsureStartedAsync();
        return await connection.InvokeCoreAsync<T>(method, args);
    }

    /// <summary>
    /// Builds and starts the connection on first use. Construction is deferred because the
    /// navigation base URI is not available while the component tree is being prerendered.
    /// </summary>
    private async Task<HubConnection> EnsureStartedAsync()
    {
        if (_connection is { State: HubConnectionState.Connected } connected)
        {
            return connected;
        }

        await _startLock.WaitAsync();
        try
        {
            _connection ??= Build();
            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync();
            }

            return _connection;
        }
        finally
        {
            _startLock.Release();
        }
    }

    private HubConnection Build()
    {
        var configured = options.Value.HubBaseUrl;
        var baseUri = new Uri(string.IsNullOrWhiteSpace(configured) ? navigation.BaseUri : configured);
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(baseUri, ArenaHub.Path.TrimStart('/')))
            .WithAutomaticReconnect()
            .Build();

        connection.On<LobbySnapshotDto>(nameof(IArenaClient.LobbyUpdated), lobby => Raise(LobbyUpdated, lobby));
        connection.On<MatchFoundDto>(nameof(IArenaClient.MatchFound), match => Raise(MatchFound, match));
        connection.On<GameStateDto>(nameof(IArenaClient.GameUpdated), state => Raise(GameUpdated, state));
        return connection;
    }

    private static Task Raise<T>(Func<T, Task>? handlers, T payload) =>
        handlers is null
            ? Task.CompletedTask
            : Task.WhenAll(handlers.GetInvocationList().Cast<Func<T, Task>>().Select(h => h(payload)));
}
