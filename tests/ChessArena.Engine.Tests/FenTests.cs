using ChessArena.Engine;

namespace ChessArena.Engine.Tests;

public class FenTests
{
    [Theory]
    [InlineData(Position.StartFen)]
    [InlineData("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1")]
    [InlineData("rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq c6 0 2")]
    [InlineData("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1")]
    [InlineData("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8")]
    [InlineData("4k3/8/8/8/8/8/8/4K3 b - - 99 150")]
    public void RoundTripsExactly(string fen) => Assert.Equal(fen, Position.FromFen(fen).ToFen());

    [Fact]
    public void ParsesAllFields()
    {
        var position = Position.FromFen("rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w Kq e6 3 7");

        Assert.Equal(Color.White, position.SideToMove);
        Assert.Equal(CastlingRights.WhiteKingSide | CastlingRights.BlackQueenSide, position.Castling);
        Assert.Equal(Square.Parse("e6"), position.EnPassant);
        Assert.Equal(3, position.HalfmoveClock);
        Assert.Equal(7, position.FullmoveNumber);
        Assert.Equal(Piece.WhiteKing, position[Square.Parse("e1")]);
        Assert.Equal(Piece.BlackPawn, position[Square.Parse("e5")]);
    }

    [Fact]
    public void DefaultsMissingMoveCounters()
    {
        var position = Position.FromFen("4k3/8/8/8/8/8/8/4K3 w - -");
        Assert.Equal(0, position.HalfmoveClock);
        Assert.Equal(1, position.FullmoveNumber);
    }

    [Fact]
    public void DropsCastlingRightsWithoutRookOnHomeSquare()
    {
        var position = Position.FromFen("4k3/8/8/8/8/8/8/R3K3 w KQkq - 0 1");
        Assert.Equal(CastlingRights.WhiteQueenSide, position.Castling);
    }

    [Theory]
    [InlineData("")]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP w KQkq - 0 1")]
    [InlineData("rnbqkbnr/pppppppp/9/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1")]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR x KQkq - 0 1")]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkx - 0 1")]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq e4 0 1")]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - -1 1")]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQQBNR w KQkq - 0 1")]
    [InlineData("rnbqkbnP/pppppppp/8/8/8/8/PPPPPPP1/RNBQKBNR w KQkq - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K2R w - - 0 1 extra")]
    public void RejectsMalformedInput(string fen)
    {
        Assert.Throws<FenException>(() => Position.FromFen(fen));
        Assert.False(Fen.TryParse(fen, out _));
    }

    [Fact]
    public void RejectsPositionWhereSideNotToMoveIsInCheck() =>
        Assert.Throws<FenException>(() => Position.FromFen("4k3/8/8/8/8/8/8/4R1K1 w - - 0 1"));

    [Fact]
    public void FenTracksMovesPlayed()
    {
        var game = new ChessGame();
        game.TryMoveSan("e4");
        Assert.Equal("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1", game.Fen);
        game.TryMoveSan("Nf6");
        Assert.Equal("rnbqkb1r/pppppppp/5n2/8/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 1 2", game.Fen);
    }
}
