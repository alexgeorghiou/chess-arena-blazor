using ChessArena.Engine;

namespace ChessArena.Engine.Tests;

/// <summary>
/// Reference node counts from the Chess Programming Wiki "Perft Results" page.
/// </summary>
public class PerftTests
{
    private const string Kiwipete = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
    private const string Position3 = "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1";
    private const string Position4 = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
    private const string Position4Mirrored = "r2q1rk1/pP1p2pp/Q4n2/bbp1p3/Np6/1B3NBn/pPPP1PPP/R3K2R b KQ - 0 1";
    private const string Position5 = "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";

    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 400)]
    [InlineData(3, 8_902)]
    [InlineData(4, 197_281)]
    public void StartPosition(int depth, long expected) =>
        Assert.Equal(expected, Perft.Count(Position.Initial(), depth));

    [Theory]
    [InlineData(1, 48)]
    [InlineData(2, 2_039)]
    [InlineData(3, 97_862)]
    public void KiwipeteStressesCastlingAndPins(int depth, long expected) =>
        Assert.Equal(expected, Perft.Count(Position.FromFen(Kiwipete), depth));

    [Theory]
    [InlineData(1, 14)]
    [InlineData(2, 191)]
    [InlineData(3, 2_812)]
    [InlineData(4, 43_238)]
    public void Position3StressesEnPassantDiscoveredChecks(int depth, long expected) =>
        Assert.Equal(expected, Perft.Count(Position.FromFen(Position3), depth));

    [Theory]
    [InlineData(Position4, 1, 6)]
    [InlineData(Position4, 2, 264)]
    [InlineData(Position4, 3, 9_467)]
    [InlineData(Position4Mirrored, 1, 6)]
    [InlineData(Position4Mirrored, 2, 264)]
    [InlineData(Position4Mirrored, 3, 9_467)]
    public void Position4StressesPromotionsUnderCheck(string fen, int depth, long expected) =>
        Assert.Equal(expected, Perft.Count(Position.FromFen(fen), depth));

    [Theory]
    [InlineData(1, 44)]
    [InlineData(2, 1_486)]
    [InlineData(3, 62_379)]
    public void Position5CatchesPromotionBugs(int depth, long expected) =>
        Assert.Equal(expected, Perft.Count(Position.FromFen(Position5), depth));

    [Fact]
    [Trait("Category", "Slow")]
    public void StartPositionDepth5() =>
        Assert.Equal(4_865_609, Perft.Count(Position.Initial(), 5));

    [Fact]
    [Trait("Category", "Slow")]
    public void KiwipeteDepth4() =>
        Assert.Equal(4_085_603, Perft.Count(Position.FromFen(Kiwipete), 4));

    [Fact]
    public void DivideSumsToPerft()
    {
        var position = Position.FromFen(Kiwipete);
        var divide = Perft.Divide(position, 2);

        Assert.Equal(48, divide.Count);
        Assert.Equal(2_039, divide.Values.Sum());
        Assert.Equal(Kiwipete, position.ToFen());
    }

    [Fact]
    public void PerftLeavesPositionUnchanged()
    {
        var position = Position.FromFen(Position5);
        Perft.Count(position, 3);
        Assert.Equal(Position5, position.ToFen());
    }
}
