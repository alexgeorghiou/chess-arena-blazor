using ChessArena.Engine;
using ChessArena.Web.Hubs;

namespace ChessArena.Web.Live;

/// <summary>
/// Periodically pairs waiting seeks. Running the matcher on a fixed tick (rather than on every
/// seek) is what lets rating windows widen with time and keeps pairing logic single-threaded.
/// </summary>
public sealed class MatchmakingWorker(
    LobbyService lobby,
    GameManager games,
    IArenaNotifier notifier,
    TimeProvider time,
    ILogger<MatchmakingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan LobbyRefresh = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Tick, time);
        var lastLobbyPush = DateTimeOffset.MinValue;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var paired = await RunOnceAsync();
                var now = time.GetUtcNow();
                if (paired > 0 || now - lastLobbyPush >= LobbyRefresh)
                {
                    await notifier.LobbyUpdatedAsync(lobby.Snapshot());
                    lastLobbyPush = now;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Matchmaking tick failed");
            }
        }
    }

    /// <summary>Pairs all compatible seeks and notifies both players of each new game.</summary>
    public async Task<int> RunOnceAsync()
    {
        var pairings = lobby.Matchmaker.MatchPending();
        foreach (var pairing in pairings)
        {
            var live = games.Create(pairing);
            var control = live.TimeControl.ToString();
            await notifier.MatchFoundAsync(live.White.PlayerId,
                new MatchFoundDto(live.Id, nameof(Color.White), live.Black.Handle, live.Black.Rating, control));
            await notifier.MatchFoundAsync(live.Black.PlayerId,
                new MatchFoundDto(live.Id, nameof(Color.Black), live.White.Handle, live.White.Rating, control));
            await notifier.GameUpdatedAsync(live.ToDto());
        }

        return pairings.Count;
    }
}
