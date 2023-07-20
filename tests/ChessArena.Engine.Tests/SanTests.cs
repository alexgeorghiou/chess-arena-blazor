using ChessArena.Engine;

namespace ChessArena.Engine.Tests;

public class SanTests
{
    [Theory]
    [InlineData(Position.StartFen, "e2e4", "e4")]
    [InlineData(Position.StartFen, "g1f3", "Nf3")]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "e1g1", "O-O")]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "e1c1", "O-O-O")]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 0 1", "e8c8", "O-O-O")]
    [InlineData("rnbqkbnr/ppp1pppp/8/3p4/4P3/8/PPPP1PPP/RNBQKBNR w KQkq d6 0 2", "e4d5", "exd5")]
    [InlineData("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3", "e5f6", "exf6")]
    [InlineData("8/P6k/8/8/8/8/8/K7 w - - 0 1", "a7a8q", "a8=Q")]
    [InlineData("8/P6k/8/8/8/8/8/K7 w - - 0 1", "a7a8n", "a8=N")]
    [InlineData("1r5k/P7/8/8/8/8/8/K7 w - - 0 1", "a7b8q", "axb8=Q+")]
    [InlineData("4k3/8/8/8/8/8/8/R3K2R w - - 0 1", "a1a8", "Ra8+")]
    [InlineData("6k1/5ppp/8/8/8/8/8/R5K1 w - - 0 1", "a1a8", "Ra8#")]
    public void FormatsMoves(string fen, string uci, string expected)
    {
        var position = Position.FromFen(fen);
        Assert.True(Move.TryParseUci(uci, out var move));
        Assert.Equal(expected, San.ToSan(position, move));
    }

    [Theory]
    // Two knights can reach d2: disambiguate by file.
    [InlineData("4k3/8/8/8/8/8/8/1N2KN2 w - - 0 1", "b1d2", "Nbd2")]
    // Two rooks on the same file: disambiguate by rank.
    [InlineData("4k3/R7/8/8/8/8/8/R3K3 w - - 0 1", "a1a4", "R1a4")]
    // Three queens attacking e4 so neither file nor rank alone is unique.
    [InlineData("8/8/1k6/8/Q6Q/8/8/K6Q w - - 0 1", "h4e4", "Qh4e4")]
    public void DisambiguatesMinimally(string fen, string uci, string expected)
    {
        var position = Position.FromFen(fen);
        Assert.True(Move.TryParseUci(uci, out var move));
        Assert.Equal(expected, San.ToSan(position, move));
    }

    [Fact]
    public void PinnedPieceDoesNotCauseDisambiguation()
    {
        // The c3 knight is pinned by the bishop on a5, so only the g1 knight can go to e2.
        var position = Position.FromFen("4k3/8/8/b7/8/2N5/8/4K1N1 w - - 0 1");
        Assert.True(Move.TryParseUci("g1e2", out var move));
        Assert.Equal("Ne2", San.ToSan(position, move));
    }

    [Theory]
    [InlineData("Nf3", "g1f3")]
    [InlineData("Nf3!?", "g1f3")]
    [InlineData("e4", "e2e4")]
    public void ParsesFromStartPosition(string san, string uci) =>
        Assert.Equal(uci, San.Parse(Position.Initial(), san).ToUci());

    [Theory]
    [InlineData("O-O", "e1g1")]
    [InlineData("0-0", "e1g1")]
    [InlineData("O-O-O+", "e1c1")]
    public void ParsesCastling(string san, string uci) =>
        Assert.Equal(uci, San.Parse(Position.FromFen("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1"), san).ToUci());

    [Theory]
    [InlineData("a8=Q", "a7a8q")]
    [InlineData("a8Q", "a7a8q")]
    [InlineData("a8=R", "a7a8r")]
    public void ParsesPromotions(string san, string uci) =>
        Assert.Equal(uci, San.Parse(Position.FromFen("8/P6k/8/8/8/8/8/K7 w - - 0 1"), san).ToUci());

    [Theory]
    [InlineData("e5")]
    [InlineData("Nd2")]
    [InlineData("O-O")]
    [InlineData("")]
    [InlineData("hello")]
    public void RejectsIllegalOrMalformed(string san) =>
        Assert.False(San.TryParse(Position.Initial(), san, out _));

    [Fact]
    public void ToSanRejectsIllegalMove() =>
        Assert.Throws<ArgumentException>(() => San.ToSan(Position.Initial(), Move.TryParseUci("e2e5", out var m) ? m : default));

    [Fact]
    public void EveryLegalMoveRoundTripsThroughSan()
    {
        var position = Position.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        foreach (var move in position.GetLegalMoves())
        {
            var san = San.ToSan(position, move);
            Assert.Equal(move, San.Parse(position, san));
        }
    }
}
