using ChessArena.Engine;

namespace ChessArena.Engine.Tests;

public class PgnTests
{
    [Fact]
    public void ExportsSevenTagRosterAndMovetext()
    {
        var game = new ChessGame();
        foreach (var san in new[] { "e4", "e5", "Bc4", "Nc6", "Qh5", "Nf6", "Qxf7#" })
        {
            Assert.True(game.TryMoveSan(san).Accepted);
        }

        var pgn = game.ToPgn(new PgnHeaders
        {
            Event = "Rated blitz",
            Date = new DateOnly(2026, 3, 14),
            White = "alice",
            Black = "bob",
            WhiteElo = 1500,
            BlackElo = 1480,
            TimeControl = "180+2",
        });

        const string expected = """
            [Event "Rated blitz"]
            [Site "ChessArena"]
            [Date "2026.03.14"]
            [Round "-"]
            [White "alice"]
            [Black "bob"]
            [Result "1-0"]
            [WhiteElo "1500"]
            [BlackElo "1480"]
            [TimeControl "180+2"]
            [Termination "normal"]

            1. e4 e5 2. Bc4 Nc6 3. Qh5 Nf6 4. Qxf7# 1-0

            """;
        Assert.Equal(expected.ReplaceLineEndings("\n"), pgn);
    }

    [Fact]
    public void ExportsSetUpAndBlackFirstMoveNumbering()
    {
        var game = new ChessGame("4k3/8/8/8/8/8/8/R3K3 b - - 0 12");
        game.TryMoveSan("Kd7");
        game.TryMoveSan("Ra7+");

        var pgn = game.ToPgn();
        Assert.Contains("[SetUp \"1\"]", pgn);
        Assert.Contains("[FEN \"4k3/8/8/8/8/8/8/R3K3 b - - 0 12\"]", pgn);
        Assert.Contains("12... Kd7 13. Ra7+ *", pgn);
    }

    [Fact]
    public void WrapsLongMovetextAtEightyColumns()
    {
        var game = new ChessGame();
        var shuffle = new[] { "Nf3", "Nf6", "Nc3", "Nc6", "Nb1", "Nb8", "Ng1", "Ng8" };
        foreach (var san in shuffle.Concat(new[] { "e4", "e5", "d4", "d5", "c4", "c5", "a3", "a6", "h3", "h6", "b3", "b6" }))
        {
            game.TryMoveSan(san);
        }

        var movetext = game.ToPgn().Split("\n\n")[1];
        Assert.All(movetext.Split('\n'), line => Assert.True(line.Length <= 80, line));
        Assert.Contains('\n', movetext.TrimEnd());
    }

    [Fact]
    public void EscapesQuotesInTags()
    {
        var pgn = new ChessGame().ToPgn(new PgnHeaders { White = "The \"Hammer\"" });
        Assert.Contains("[White \"The \\\"Hammer\\\"\"]", pgn);

        var (tags, _) = PgnReader.Read(pgn);
        Assert.Equal("The \"Hammer\"", tags["White"]);
    }

    [Fact]
    public void RoundTripsThroughReader()
    {
        var original = new ChessGame();
        foreach (var san in new[] { "d4", "d5", "c4", "dxc4", "e4", "b5", "a4", "c6", "axb5", "cxb5", "Qf3" })
        {
            original.TryMoveSan(san);
        }

        original.Resign(Color.Black);
        var (tags, replayed) = PgnReader.Read(original.ToPgn(new PgnHeaders { White = "w", Black = "b" }));

        Assert.Equal("1-0", tags["Result"]);
        Assert.Equal(original.Fen, replayed.Fen);
        Assert.Equal(original.Moves.Select(m => m.San), replayed.Moves.Select(m => m.San));
    }

    [Fact]
    public void ReaderSkipsCommentsVariationsAndNags()
    {
        const string pgn = """
            [Event "Annotated"]

            1. e4 {best by test} e5 2. Nf3 (2. f4 exf4 3. Bc4) Nc6 $1 3. Bb5 ; the Spanish
            a6 *
            """;

        var (_, game) = PgnReader.Read(pgn);
        Assert.Equal(["e4", "e5", "Nf3", "Nc6", "Bb5", "a6"], game.Moves.Select(m => m.San));
    }

    [Fact]
    public void ReaderRejectsIllegalMoves() =>
        Assert.Throws<FormatException>(() => PgnReader.Read("1. e4 e5 2. Ke3 Ke6 3. Kd5 *"));
}
