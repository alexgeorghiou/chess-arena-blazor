using ChessArena.Web.Data;

namespace ChessArena.Web.Api;

public static class ArenaApi
{
    public static IEndpointRouteBuilder MapArenaApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/games", async (IGameArchive archive, Guid? playerId, int? take) =>
            Results.Ok(await archive.RecentAsync(Math.Clamp(take ?? 20, 1, 100), playerId)));

        api.MapGet("/games/{id:guid}/pgn", async (Guid id, IGameArchive archive) =>
            await archive.FindAsync(id) is { } game
                ? Results.Text(game.Pgn, "application/x-chess-pgn")
                : Results.NotFound());

        api.MapGet("/leaderboard", async (IPlayerStore players, int? take) =>
            Results.Ok(await players.LeaderboardAsync(Math.Clamp(take ?? 20, 1, 100))));

        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
        return app;
    }
}
