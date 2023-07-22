using ChessArena.Engine;

namespace ChessArena.Engine.Tests;

public class GameRulesTests
{
    private static ChessGame Play(string? fen, params string[] sanMoves)
    {
        var game = new ChessGame(fen);
        foreach (var san in sanMoves)
        {
            var outcome = game.TryMoveSan(san);
            Assert.True(outcome.Accepted, $"{san} was rejected: {outcome.Rejection}");
        }

        return game;
    }

    [Fact]
    public void FoolsMateIsCheckmate()
    {
        var game = Play(null, "f3", "e5", "g4", "Qh4#");

        Assert.Equal(GameResult.BlackWins, game.Result);
        Assert.Equal(Termination.Checkmate, game.Termination);
        Assert.Equal("Qh4#", game.LastMove!.San);
        Assert.Empty(game.LegalMoves);
    }

    [Fact]
    public void MovesAreRejectedOnceGameIsOver()
    {
        var game = Play(null, "f3", "e5", "g4", "Qh4#");
        Assert.Equal(MoveRejection.GameOver, game.TryMoveUci("a2a3").Rejection);
    }

    [Fact]
    public void StalemateIsADraw()
    {
        var game = Play("7k/8/6Q1/8/8/8/8/K7 w - - 0 1", "Qf7");

        Assert.Equal(GameResult.Draw, game.Result);
        Assert.Equal(Termination.Stalemate, game.Termination);
    }

    [Fact]
    public void ThreefoldRepetitionEndsTheGame()
    {
        // The start position occurs for the third time after the second knight shuffle.
        var game = Play(null, "Nf3", "Nf6", "Ng1", "Ng8", "Nf3", "Nf6", "Ng1");
        Assert.False(game.IsOver);

        game.TryMoveSan("Ng8");
        Assert.Equal(Termination.ThreefoldRepetition, game.Termination);
        Assert.Equal(GameResult.Draw, game.Result);
    }

    [Fact]
    public void RepetitionRequiresSameCastlingRights()
    {
        // Rook shuffles lose castling rights, so the starting position never recurs even though the
        // pieces return to the same squares.
        var game = Play("r3k3/8/8/8/8/8/8/4K2R w Kq - 0 1",
            "Rh2", "Ra7", "Rh1", "Ra8", "Rh2", "Ra7", "Rh1", "Ra8");
        Assert.False(game.IsOver);

        // The position after 1.Rh2 still had black's queen-side right, so this is only the second occurrence.
        game.TryMoveSan("Rh2");
        Assert.False(game.IsOver);

        game.TryMoveSan("Ra7");
        Assert.Equal(Termination.ThreefoldRepetition, game.Termination);
    }

    [Fact]
    public void UnusableEnPassantSquareDoesNotBreakRepetition()
    {
        // After 1.e4 the FEN carries "e3" but no black pawn can capture, so the position equals one
        // reached later without the en passant marker.
        var first = Position.FromFen("4k3/8/8/8/4P3/8/8/4K3 b - e3 0 1").ComputeKey();
        var second = Position.FromFen("4k3/8/8/8/4P3/8/8/4K3 b - - 0 1").ComputeKey();
        Assert.Equal(first, second);

        var withCapture = Position.FromFen("4k3/8/8/8/3pP3/8/8/4K3 b - e3 0 1").ComputeKey();
        var withoutCapture = Position.FromFen("4k3/8/8/8/3pP3/8/8/4K3 b - - 0 1").ComputeKey();
        Assert.NotEqual(withCapture, withoutCapture);
    }

    [Fact]
    public void FiftyMoveRuleDrawsAtHundredPlies()
    {
        var game = Play("4k3/8/8/8/8/8/R7/4K3 w - - 98 80", "Ra3");
        Assert.False(game.IsOver);

        game.TryMoveSan("Kd7");
        Assert.Equal(Termination.FiftyMoveRule, game.Termination);
    }

    [Fact]
    public void CheckmateOnHundredthPlyBeatsFiftyMoveRule()
    {
        var game = Play("6k1/5ppp/8/8/8/8/8/R5K1 w - - 99 80", "Ra8#");
        Assert.Equal(Termination.Checkmate, game.Termination);
        Assert.Equal(GameResult.WhiteWins, game.Result);
    }

    [Theory]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - - 0 1", true)]
    [InlineData("4k3/8/8/8/8/8/8/4KN2 w - - 0 1", true)]
    [InlineData("4k3/8/8/8/8/8/8/4KB2 w - - 0 1", true)]
    [InlineData("4kb2/8/8/8/8/8/8/2B1K3 w - - 0 1", true)]
    [InlineData("4k1b1/8/8/8/8/8/8/2B1K3 w - - 0 1", false)]
    [InlineData("4k3/8/8/8/8/8/8/3NKN2 w - - 0 1", false)]
    [InlineData("4kn2/8/8/8/8/8/8/4KB2 w - - 0 1", false)]
    [InlineData("4k3/8/8/8/8/8/P7/4K3 w - - 0 1", false)]
    [InlineData("4k3/8/8/8/8/8/8/R3K3 w - - 0 1", false)]
    public void DetectsDeadPositions(string fen, bool insufficient) =>
        Assert.Equal(insufficient, Position.FromFen(fen).HasInsufficientMaterial());

    [Fact]
    public void CapturingLastPieceDrawsByInsufficientMaterial()
    {
        var game = Play("4k3/8/8/8/8/8/3r4/4K3 w - - 0 1", "Kxd2");
        Assert.Equal(Termination.InsufficientMaterial, game.Termination);
    }

    [Fact]
    public void CannotCastleThroughAttackedSquare()
    {
        var position = Position.FromFen("k4r2/8/8/8/8/8/8/4K2R w K - 0 1");
        Assert.DoesNotContain(position.GetLegalMoves(), m => m.ToUci() == "e1g1");
    }

    [Fact]
    public void CannotCastleOutOfCheck()
    {
        var position = Position.FromFen("1k2r3/8/8/8/8/8/8/R3K2R w KQ - 0 1");
        Assert.DoesNotContain(position.GetLegalMoves(), m => m.ToUci() is "e1g1" or "e1c1");
    }

    [Fact]
    public void QueenSideCastlingAllowsAttackedB1()
    {
        var position = Position.FromFen("1r5k/8/8/8/8/8/8/R3K3 w Q - 0 1");
        Assert.Contains(position.GetLegalMoves(), m => m.ToUci() == "e1c1");
    }

    [Fact]
    public void CastlingMovesTheRook()
    {
        var game = Play("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "O-O", "O-O-O");
        Assert.Equal("2kr3r/8/8/8/8/8/8/R4RK1 w - - 2 2", game.Fen);
    }

    [Fact]
    public void EnPassantCaptureRemovesThePawn()
    {
        var game = Play(null, "e4", "a6", "e5", "d5", "exd6");
        Assert.Equal("rnbqkbnr/1pp1pppp/p2P4/8/8/8/PPPP1PPP/RNBQKBNR b KQkq - 0 3", game.Fen);
    }

    [Fact]
    public void EnPassantIsIllegalWhenItExposesTheKing()
    {
        // Capturing d6 en passant would clear the fifth rank between the king and the rook.
        var position = Position.FromFen("8/8/8/KPp4r/8/8/8/6k1 w - c6 0 1");
        Assert.DoesNotContain(position.GetLegalMoves(), m => m.ToUci() == "b5c6");
    }

    [Fact]
    public void UciPromotionDefaultsToQueen()
    {
        var game = new ChessGame("8/P6k/8/8/8/8/8/K7 w - - 0 1");
        var outcome = game.TryMoveUci("a7a8");

        Assert.True(outcome.Accepted);
        Assert.Equal("a8=Q", outcome.Played!.San);
    }

    [Fact]
    public void UnderPromotionIsHonoured()
    {
        var game = new ChessGame("8/P6k/8/8/8/8/8/K7 w - - 0 1");
        Assert.Equal("a8=N", game.TryMoveUci("a7a8n").Played!.San);
    }

    [Theory]
    [InlineData("e2e5", MoveRejection.Illegal)]
    [InlineData("e9e4", MoveRejection.Malformed)]
    [InlineData("nonsense", MoveRejection.Malformed)]
    public void RejectsBadMoves(string uci, MoveRejection expected) =>
        Assert.Equal(expected, new ChessGame().TryMoveUci(uci).Rejection);

    [Fact]
    public void ResignationAwardsTheOpponent()
    {
        var game = new ChessGame();
        game.Resign(Color.White);
        Assert.Equal(GameResult.BlackWins, game.Result);
        Assert.Equal(Termination.Resignation, game.Termination);
        Assert.Throws<InvalidOperationException>(() => game.AgreeDraw());
    }

    [Fact]
    public void FlagFallLoses()
    {
        var game = new ChessGame();
        game.FlagFall(Color.Black);
        Assert.Equal(GameResult.WhiteWins, game.Result);
        Assert.Equal(Termination.Timeout, game.Termination);
    }

    [Fact]
    public void FlagFallAgainstLoneKnightIsADraw()
    {
        var game = new ChessGame("4k3/8/8/8/8/8/PPP5/4KN2 b - - 0 1");
        game.FlagFall(Color.White);
        Assert.Equal(GameResult.Draw, game.Result);
        Assert.Equal(Termination.TimeoutVsInsufficientMaterial, game.Termination);
    }

    [Fact]
    public void GetPositionReturnsIndependentCopy()
    {
        var game = new ChessGame();
        var copy = game.GetPosition();
        copy.MakeMove(copy.GetLegalMoves()[0]);
        Assert.Equal(Position.StartFen, game.Fen);
    }

    [Fact]
    public void PlyNumbersStartAtTwoWhenBlackMovesFirst()
    {
        var game = Play("4k3/8/8/8/8/8/8/R3K3 b - - 0 1", "Kd7", "Ra7+");
        Assert.Equal([2, 3], game.Moves.Select(m => m.Ply));
        Assert.Equal(Color.Black, game.Moves[0].Mover);
    }
}
