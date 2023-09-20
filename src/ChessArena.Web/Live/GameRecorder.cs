using ChessArena.Core.Ratings;
using ChessArena.Web.Data;

namespace ChessArena.Web.Live;

/// <summary>Bridges the singleton game manager to the scoped EF Core archive.</summary>
public sealed class GameRecorder(IServiceScopeFactory scopes, ILogger<GameRecorder> logger) : IGameRecorder
{
    public async Task<RatingChange?> RecordAsync(CompletedGame completed)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var archive = scope.ServiceProvider.GetRequiredService<IGameArchive>();
            return await archive.RecordAsync(completed);
        }
        catch (Exception ex)
        {
            // The game result stands for the players even if storage hiccups; it is just not rated.
            logger.LogError(ex, "Failed to archive game {GameId}", completed.GameId);
            return null;
        }
    }
}
