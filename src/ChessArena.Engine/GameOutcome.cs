namespace ChessArena.Engine;

public enum GameResult
{
    Ongoing = 0,
    WhiteWins,
    BlackWins,
    Draw,
}

public enum Termination
{
    None = 0,
    Checkmate,
    Resignation,
    Timeout,
    Stalemate,
    ThreefoldRepetition,
    FiftyMoveRule,
    InsufficientMaterial,
    TimeoutVsInsufficientMaterial,
    DrawAgreement,
    Abandoned,
}

public static class GameResultExtensions
{
    /// <summary>PGN result token.</summary>
    public static string ToPgn(this GameResult result) => result switch
    {
        GameResult.WhiteWins => "1-0",
        GameResult.BlackWins => "0-1",
        GameResult.Draw => "1/2-1/2",
        _ => "*",
    };

    public static GameResult FromPgn(string token) => token switch
    {
        "1-0" => GameResult.WhiteWins,
        "0-1" => GameResult.BlackWins,
        "1/2-1/2" => GameResult.Draw,
        "*" => GameResult.Ongoing,
        _ => throw new FormatException($"'{token}' is not a PGN result."),
    };

    public static GameResult WinFor(Color color) => color == Color.White ? GameResult.WhiteWins : GameResult.BlackWins;

    /// <summary>Score from white's perspective: 1, 0.5 or 0.</summary>
    public static double WhiteScore(this GameResult result) => result switch
    {
        GameResult.WhiteWins => 1.0,
        GameResult.BlackWins => 0.0,
        GameResult.Draw => 0.5,
        _ => throw new InvalidOperationException("An unfinished game has no score."),
    };
}
