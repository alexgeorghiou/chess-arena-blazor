namespace ChessArena.Web.Live;

/// <summary>
/// Single loop that makes flags fall for players who stop moving, and evicts finished games from
/// memory after a grace period during which late spectators can still see the final position.
/// </summary>
public sealed class ClockWatchdog(GameManager games, TimeProvider time, ILogger<ClockWatchdog> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Tick, time);
        var ticks = 0;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await games.CheckClocksAsync();
                if (++ticks % 600 == 0)
                {
                    games.PurgeFinished(Retention);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Clock watchdog tick failed");
            }
        }
    }
}
