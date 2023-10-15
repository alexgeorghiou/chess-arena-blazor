using ChessArena.Web.Data;
using ChessArena.Web.Hubs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace ChessArena.Web.Tests.Integration;

/// <summary>Drives the real hub and background workers end to end over an in-memory test server.</summary>
public sealed class ArenaHubTests : IClassFixture<ArenaHubTests.ArenaFactory>
{
    private readonly ArenaFactory _factory;

    public ArenaHubTests(ArenaFactory factory) => _factory = factory;

    [Fact]
    public async Task TwoPlayersGetPairedPlayAndAreRated()
    {
        var alice = await RegisterAsync("alice_it");
        var bob = await RegisterAsync("bob_it");
        await using var aliceHub = await ConnectAsync();
        await using var bobHub = await ConnectAsync();

        var aliceMatch = new TaskCompletionSource<MatchFoundDto>();
        var bobMatch = new TaskCompletionSource<MatchFoundDto>();
        aliceHub.On<MatchFoundDto>(nameof(IArenaClient.MatchFound), m => aliceMatch.TrySetResult(m));
        bobHub.On<MatchFoundDto>(nameof(IArenaClient.MatchFound), m => bobMatch.TrySetResult(m));

        await aliceHub.InvokeAsync<LobbySnapshotDto>(nameof(ArenaHub.EnterLobby), alice.Id);
        await bobHub.InvokeAsync<LobbySnapshotDto>(nameof(ArenaHub.EnterLobby), bob.Id);
        Assert.True((await aliceHub.InvokeAsync<CommandResultDto>(nameof(ArenaHub.Seek), alice.Id, "1+0")).Ok);
        Assert.True((await bobHub.InvokeAsync<CommandResultDto>(nameof(ArenaHub.Seek), bob.Id, "1+0")).Ok);

        var match = await aliceMatch.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(match.GameId, (await bobMatch.Task.WaitAsync(TimeSpan.FromSeconds(10))).GameId);

        var (white, whiteHub, black, blackHub) = match.Color == "White"
            ? (alice, aliceHub, bob, bobHub)
            : (bob, bobHub, alice, aliceHub);

        var finished = new TaskCompletionSource<GameStateDto>();
        whiteHub.On<GameStateDto>(nameof(IArenaClient.GameUpdated), s =>
        {
            if (s.IsOver && s.WhiteRatingDelta is not null)
            {
                finished.TrySetResult(s);
            }
        });

        var initial = await whiteHub.InvokeAsync<GameStateDto?>(nameof(ArenaHub.WatchGame), match.GameId, white.Id);
        await blackHub.InvokeAsync<GameStateDto?>(nameof(ArenaHub.WatchGame), match.GameId, black.Id);
        Assert.NotNull(initial);
        Assert.Equal(20, initial.LegalMoves.Count);

        foreach (var (hub, player, uci) in new[]
                 {
                     (whiteHub, white, "f2f3"), (blackHub, black, "e7e5"),
                     (whiteHub, white, "g2g4"), (blackHub, black, "d8h4"),
                 })
        {
            var result = await hub.InvokeAsync<CommandResultDto>(nameof(ArenaHub.MakeMove), match.GameId, player.Id, uci);
            Assert.True(result.Ok, result.Error);
        }

        var final = await finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("0-1", final.Result);
        Assert.Equal("Checkmate", final.Termination);
        Assert.True(final.BlackRatingDelta > 0);

        using var http = _factory.CreateClient();
        var pgn = await http.GetStringAsync($"/api/games/{match.GameId}/pgn");
        Assert.Contains("1. f3 e5 2. g4 Qh4# 0-1", pgn);
    }

    [Fact]
    public async Task SpectatorSeesLiveGame()
    {
        await using var hub = await ConnectAsync();
        Assert.Null(await hub.InvokeAsync<GameStateDto?>(nameof(ArenaHub.WatchGame), Guid.NewGuid(), (Guid?)null));
    }

    [Fact]
    public async Task HealthEndpointResponds()
    {
        using var http = _factory.CreateClient();
        Assert.Contains("ok", await http.GetStringAsync("/healthz"));
    }

    private async Task<PlayerSummary> RegisterAsync(string handle)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPlayerStore>().GetOrCreateAsync(handle);
    }

    private async Task<HubConnection> ConnectAsync()
    {
        var server = _factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, ArenaHub.Path.TrimStart('/')), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();
        await connection.StartAsync();
        return connection;
    }

    public sealed class ArenaFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"chess-arena-it-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Arena", $"Data Source={_databasePath};Pooling=False");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try
            {
                File.Delete(_databasePath);
            }
            catch (IOException)
            {
                // Best effort clean-up of the temporary database.
            }
        }
    }
}
