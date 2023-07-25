using ChessArena.Engine;

namespace ChessArena.Core.Ratings;

public readonly record struct PlayerRating(int Rating, int GamesPlayed);

public readonly record struct RatingChange(int WhiteBefore, int WhiteAfter, int BlackBefore, int BlackAfter)
{
    public int WhiteDelta => WhiteAfter - WhiteBefore;

    public int BlackDelta => BlackAfter - BlackBefore;
}

/// <summary>
/// Classic Elo with FIDE-style K-factors: 40 while provisional, 10 for established 2400+ players,
/// 20 otherwise. Each side is updated with its own K, so a provisional player moves faster than
/// their established opponent, exactly as in over-the-board rating lists.
/// </summary>
public static class Elo
{
    public const int DefaultRating = 1500;
    public const int RatingFloor = 100;
    public const int ProvisionalGames = 30;

    public static double ExpectedScore(int rating, int opponentRating) =>
        1.0 / (1.0 + Math.Pow(10.0, (opponentRating - rating) / 400.0));

    public static int KFactor(PlayerRating player) => player switch
    {
        { GamesPlayed: < ProvisionalGames } => 40,
        { Rating: >= 2400 } => 10,
        _ => 20,
    };

    /// <summary>New rating after scoring <paramref name="score"/> (1, 0.5 or 0) against an opponent.</summary>
    public static int NewRating(PlayerRating player, int opponentRating, double score)
    {
        if (score is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "Score must be between 0 and 1.");
        }

        var expected = ExpectedScore(player.Rating, opponentRating);
        var updated = player.Rating + (int)Math.Round(KFactor(player) * (score - expected), MidpointRounding.AwayFromZero);
        return Math.Max(RatingFloor, updated);
    }

    public static RatingChange Apply(PlayerRating white, PlayerRating black, GameResult result)
    {
        var whiteScore = result.WhiteScore();
        return new RatingChange(
            white.Rating,
            NewRating(white, black.Rating, whiteScore),
            black.Rating,
            NewRating(black, white.Rating, 1.0 - whiteScore));
    }
}
