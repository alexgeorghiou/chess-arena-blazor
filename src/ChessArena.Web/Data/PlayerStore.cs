using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace ChessArena.Web.Data;

public sealed record PlayerSummary(Guid Id, string Handle, int Rating, int GamesPlayed, int Wins, int Losses, int Draws)
{
    public static PlayerSummary From(PlayerEntity p) => new(p.Id, p.Handle, p.Rating, p.GamesPlayed, p.Wins, p.Losses, p.Draws);
}

public interface IPlayerStore
{
    Task<PlayerSummary> GetOrCreateAsync(string handle, CancellationToken ct = default);

    Task<PlayerSummary?> FindAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<PlayerSummary>> LeaderboardAsync(int take, CancellationToken ct = default);
}

public sealed partial class PlayerStore(ArenaDbContext db, TimeProvider time) : IPlayerStore
{
    public static bool IsValidHandle(string? handle) => handle is not null && HandlePattern().IsMatch(handle);

    public async Task<PlayerSummary> GetOrCreateAsync(string handle, CancellationToken ct = default)
    {
        handle = handle.Trim();
        if (!IsValidHandle(handle))
        {
            throw new ArgumentException("Handles are 3-20 letters, digits, '_' or '-'.", nameof(handle));
        }

        var existing = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Handle == handle, ct);
        if (existing is not null)
        {
            return PlayerSummary.From(existing);
        }

        var player = new PlayerEntity { Id = Guid.NewGuid(), Handle = handle, CreatedAtUtc = time.GetUtcNow().UtcDateTime };
        db.Players.Add(player);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race with another circuit registering the same handle; the winner's row is the player.
            db.Entry(player).State = EntityState.Detached;
            var winner = await db.Players.AsNoTracking().FirstAsync(p => p.Handle == handle, ct);
            return PlayerSummary.From(winner);
        }

        return PlayerSummary.From(player);
    }

    public async Task<PlayerSummary?> FindAsync(Guid id, CancellationToken ct = default)
    {
        var player = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return player is null ? null : PlayerSummary.From(player);
    }

    public async Task<IReadOnlyList<PlayerSummary>> LeaderboardAsync(int take, CancellationToken ct = default) =>
        await db.Players.AsNoTracking()
            .Where(p => p.GamesPlayed > 0)
            .OrderByDescending(p => p.Rating)
            .ThenBy(p => p.Handle)
            .Take(take)
            .Select(p => new PlayerSummary(p.Id, p.Handle, p.Rating, p.GamesPlayed, p.Wins, p.Losses, p.Draws))
            .ToListAsync(ct);

    [GeneratedRegex("^[A-Za-z0-9_-]{3,20}$")]
    private static partial Regex HandlePattern();
}
