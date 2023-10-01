using ChessArena.Engine;
using ChessArena.Web.Components.Board;

namespace ChessArena.Web.Tests.Components;

public class ChessBoardTests : TestContext
{
    private static readonly string[] StartMoves = Position.Initial().GetLegalMoves().Select(m => m.ToUci()).ToArray();

    [Fact]
    public void RendersAllSquaresAndPieces()
    {
        var board = RenderComponent<ChessBoard>(p => p.Add(b => b.Fen, Position.StartFen));

        Assert.Equal(64, board.FindAll(".square").Count);
        Assert.Equal(32, board.FindAll(".piece").Count);
        Assert.Equal(16, board.FindAll(".piece.white").Count);
        Assert.Equal("a8", board.Find(".square").GetAttribute("data-square"));
    }

    [Fact]
    public void BlackOrientationFlipsTheBoard()
    {
        var board = RenderComponent<ChessBoard>(p => p
            .Add(b => b.Fen, Position.StartFen)
            .Add(b => b.Orientation, Color.Black));

        Assert.Equal("h1", board.Find(".square").GetAttribute("data-square"));
        Assert.Equal("a8", board.FindAll(".square")[^1].GetAttribute("data-square"));
    }

    [Fact]
    public void SquareColoursFollowTheBoardPattern()
    {
        var board = RenderComponent<ChessBoard>(p => p.Add(b => b.Fen, Position.StartFen));

        Assert.Contains("dark", board.Find("[data-square=a1]").ClassList);
        Assert.Contains("light", board.Find("[data-square=h1]").ClassList);
    }

    [Fact]
    public void SelectingAPieceHighlightsItsTargets()
    {
        var board = RenderComponent<ChessBoard>(p => p
            .Add(b => b.Fen, Position.StartFen)
            .Add(b => b.LegalMoves, StartMoves)
            .Add(b => b.Interactive, true));

        board.Find("[data-square=g1]").Click();

        Assert.Contains("selected", board.Find("[data-square=g1]").ClassList);
        var targets = board.FindAll(".target").Select(e => e.GetAttribute("data-square")!).Order().ToArray();
        Assert.Equal(new[] { "f3", "h3" }, targets);
    }

    [Fact]
    public void ClickingATargetEmitsTheUciMove()
    {
        string? played = null;
        var board = RenderComponent<ChessBoard>(p => p
            .Add(b => b.Fen, Position.StartFen)
            .Add(b => b.LegalMoves, StartMoves)
            .Add(b => b.Interactive, true)
            .Add(b => b.OnMove, uci => played = uci));

        board.Find("[data-square=e2]").Click();
        board.Find("[data-square=e4]").Click();

        Assert.Equal("e2e4", played);
        Assert.Empty(board.FindAll(".selected"));
    }

    [Fact]
    public void ClickingAPieceWithoutMovesSelectsNothing()
    {
        var board = RenderComponent<ChessBoard>(p => p
            .Add(b => b.Fen, Position.StartFen)
            .Add(b => b.LegalMoves, StartMoves)
            .Add(b => b.Interactive, true));

        board.Find("[data-square=e8]").Click();
        board.Find("[data-square=a1]").Click();

        Assert.Empty(board.FindAll(".selected"));
    }

    [Fact]
    public void PromotionAsksForThePiece()
    {
        const string fen = "8/P6k/8/8/8/8/8/K7 w - - 0 1";
        var moves = Position.FromFen(fen).GetLegalMoves().Select(m => m.ToUci()).ToArray();
        string? played = null;
        var board = RenderComponent<ChessBoard>(p => p
            .Add(b => b.Fen, fen)
            .Add(b => b.LegalMoves, moves)
            .Add(b => b.Interactive, true)
            .Add(b => b.OnMove, uci => played = uci));

        board.Find("[data-square=a7]").Click();
        board.Find("[data-square=a8]").Click();
        Assert.Null(played);
        Assert.Equal(4, board.FindAll(".promote").Count);

        board.Find(".promote[data-piece=n]").Click();
        Assert.Equal("a7a8n", played);
        Assert.Empty(board.FindAll(".promotion"));
    }

    [Fact]
    public void NonInteractiveBoardIgnoresClicks()
    {
        string? played = null;
        var board = RenderComponent<ChessBoard>(p => p
            .Add(b => b.Fen, Position.StartFen)
            .Add(b => b.LegalMoves, StartMoves)
            .Add(b => b.OnMove, uci => played = uci));

        board.Find("[data-square=e2]").Click();
        board.Find("[data-square=e4]").Click();

        Assert.Null(played);
        Assert.Empty(board.FindAll(".selected"));
    }

    [Fact]
    public void HighlightsLastMoveAndKingInCheck()
    {
        var board = RenderComponent<ChessBoard>(p => p
            .Add(b => b.Fen, "rnb1kbnr/pppp1ppp/8/4p3/6Pq/5P2/PPPPP2P/RNBQKBNR w KQkq - 1 3")
            .Add(b => b.LastMove, "d8h4")
            .Add(b => b.IsCheck, true));

        Assert.Contains("last", board.Find("[data-square=d8]").ClassList);
        Assert.Contains("last", board.Find("[data-square=h4]").ClassList);
        Assert.Contains("check", board.Find("[data-square=e1]").ClassList);
        Assert.DoesNotContain("check", board.Find("[data-square=e8]").ClassList);
    }

    [Fact]
    public void NewPositionClearsSelection()
    {
        var board = RenderComponent<ChessBoard>(p => p
            .Add(b => b.Fen, Position.StartFen)
            .Add(b => b.LegalMoves, StartMoves)
            .Add(b => b.Interactive, true));

        board.Find("[data-square=e2]").Click();
        board.SetParametersAndRender(p => p.Add(b => b.Fen, "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1"));

        Assert.Empty(board.FindAll(".selected"));
        Assert.Null(board.Instance.Selected);
    }
}
