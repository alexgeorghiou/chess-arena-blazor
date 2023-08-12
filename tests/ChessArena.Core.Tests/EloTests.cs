using ChessArena.Core.Ratings;
using ChessArena.Engine;

namespace ChessArena.Core.Tests;

public class EloTests
{
    private static readonly PlayerRating Established1500 = new(1500, 100);

    [Fact]
    public void EqualRatingsExpectHalfAPoint() =>
        Assert.Equal(0.5, Elo.ExpectedScore(1500, 1500), 6);

    [Theory]
    [InlineData(1600, 1400, 0.759747)]
    [InlineData(1400, 1600, 0.240253)]
    [InlineData(2000, 1200, 0.990099)]
    public void ExpectedScoreFollowsLogisticCurve(int rating, int opponent, double expected) =>
        Assert.Equal(expected, Elo.ExpectedScore(rating, opponent), 5);

    [Fact]
    public void ExpectedScoresAreComplementary() =>
        Assert.Equal(1.0, Elo.ExpectedScore(1723, 1488) + Elo.ExpectedScore(1488, 1723), 10);

    [Theory]
    [InlineData(1500, 0, 40)]
    [InlineData(1500, 29, 40)]
    [InlineData(1500, 30, 20)]
    [InlineData(2399, 200, 20)]
    [InlineData(2400, 200, 10)]
    [InlineData(2600, 10, 40)]
    public void KFactorDependsOnExperienceAndStrength(int rating, int games, int expected) =>
        Assert.Equal(expected, Elo.KFactor(new PlayerRating(rating, games)));

    [Fact]
    public void WinBetweenEqualsMovesTenPointsAtK20()
    {
        var change = Elo.Apply(Established1500, Established1500, GameResult.WhiteWins);
        Assert.Equal(1510, change.WhiteAfter);
        Assert.Equal(1490, change.BlackAfter);
        Assert.Equal(10, change.WhiteDelta);
        Assert.Equal(-10, change.BlackDelta);
    }

    [Fact]
    public void DrawBetweenEqualsChangesNothing()
    {
        var change = Elo.Apply(Established1500, Established1500, GameResult.Draw);
        Assert.Equal(0, change.WhiteDelta);
        Assert.Equal(0, change.BlackDelta);
    }

    [Fact]
    public void DrawAgainstStrongerPlayerGainsRating()
    {
        var change = Elo.Apply(new PlayerRating(1400, 100), new PlayerRating(1600, 100), GameResult.Draw);
        Assert.Equal(1405, change.WhiteAfter);
        Assert.Equal(1595, change.BlackAfter);
    }

    [Fact]
    public void UpsetIsRewardedMoreThanExpectedWin()
    {
        var upset = Elo.Apply(new PlayerRating(1400, 100), new PlayerRating(1600, 100), GameResult.WhiteWins);
        var expected = Elo.Apply(new PlayerRating(1600, 100), new PlayerRating(1400, 100), GameResult.WhiteWins);
        Assert.Equal(15, upset.WhiteDelta);
        Assert.Equal(5, expected.WhiteDelta);
    }

    [Fact]
    public void ProvisionalPlayerMovesFasterThanEstablishedOpponent()
    {
        var change = Elo.Apply(new PlayerRating(1500, 3), Established1500, GameResult.WhiteWins);
        Assert.Equal(20, change.WhiteDelta);
        Assert.Equal(-10, change.BlackDelta);
    }

    [Fact]
    public void RatingNeverDropsBelowFloor() =>
        Assert.Equal(Elo.RatingFloor, Elo.NewRating(new PlayerRating(105, 5), 105, 0));

    [Fact]
    public void RejectsOutOfRangeScore() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Elo.NewRating(Established1500, 1500, 1.5));

    [Fact]
    public void UnfinishedGameCannotBeRated() =>
        Assert.Throws<InvalidOperationException>(() => Elo.Apply(Established1500, Established1500, GameResult.Ongoing));
}
