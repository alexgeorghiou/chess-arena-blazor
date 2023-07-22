using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ChessArena.Engine;

/// <summary>Tag pairs for PGN export. The Seven Tag Roster is always emitted, in order.</summary>
public sealed class PgnHeaders
{
    public string Event { get; init; } = "Casual game";
    public string Site { get; init; } = "ChessArena";
    public DateOnly? Date { get; init; }
    public string Round { get; init; } = "-";
    public string White { get; init; } = "?";
    public string Black { get; init; } = "?";
    public int? WhiteElo { get; init; }
    public int? BlackElo { get; init; }
    public string? TimeControl { get; init; }

    /// <summary>Any additional tags, emitted after the standard ones.</summary>
    public IReadOnlyDictionary<string, string> Extra { get; init; } = new Dictionary<string, string>();
}

public static class PgnWriter
{
    private const int LineWidth = 80;

    public static string Write(ChessGame game, PgnHeaders headers)
    {
        var sb = new StringBuilder();
        var result = game.Result.ToPgn();

        AppendTag(sb, "Event", headers.Event);
        AppendTag(sb, "Site", headers.Site);
        AppendTag(sb, "Date", headers.Date?.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture) ?? "????.??.??");
        AppendTag(sb, "Round", headers.Round);
        AppendTag(sb, "White", headers.White);
        AppendTag(sb, "Black", headers.Black);
        AppendTag(sb, "Result", result);
        if (headers.WhiteElo is { } whiteElo)
        {
            AppendTag(sb, "WhiteElo", whiteElo.ToString(CultureInfo.InvariantCulture));
        }

        if (headers.BlackElo is { } blackElo)
        {
            AppendTag(sb, "BlackElo", blackElo.ToString(CultureInfo.InvariantCulture));
        }

        if (headers.TimeControl is not null)
        {
            AppendTag(sb, "TimeControl", headers.TimeControl);
        }

        if (game.Termination != Termination.None)
        {
            AppendTag(sb, "Termination", DescribeTermination(game.Termination));
        }

        if (!game.IsStandardStart)
        {
            AppendTag(sb, "SetUp", "1");
            AppendTag(sb, "FEN", game.StartFen);
        }

        foreach (var (name, value) in headers.Extra)
        {
            AppendTag(sb, name, value);
        }

        sb.Append('\n');
        AppendMovetext(sb, game, result);
        return sb.ToString();
    }

    private static void AppendMovetext(StringBuilder sb, ChessGame game, string result)
    {
        var start = Position.FromFen(game.StartFen);
        var moveNumber = start.FullmoveNumber;
        var whiteToMove = start.SideToMove == Color.White;

        var tokens = new List<string>(game.Moves.Count * 2 + 1);
        var first = true;
        foreach (var played in game.Moves)
        {
            if (whiteToMove)
            {
                tokens.Add($"{moveNumber}.");
            }
            else if (first)
            {
                tokens.Add($"{moveNumber}...");
            }

            tokens.Add(played.San);
            if (!whiteToMove)
            {
                moveNumber++;
            }

            whiteToMove = !whiteToMove;
            first = false;
        }

        tokens.Add(result);

        var lineLength = 0;
        foreach (var token in tokens)
        {
            if (lineLength > 0 && lineLength + 1 + token.Length > LineWidth)
            {
                sb.Append('\n');
                lineLength = 0;
            }
            else if (lineLength > 0)
            {
                sb.Append(' ');
                lineLength++;
            }

            sb.Append(token);
            lineLength += token.Length;
        }

        sb.Append('\n');
    }

    private static void AppendTag(StringBuilder sb, string name, string value)
    {
        var escaped = value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        sb.Append('[').Append(name).Append(" \"").Append(escaped).Append("\"]\n");
    }

    private static string DescribeTermination(Termination termination) => termination switch
    {
        Termination.Timeout or Termination.TimeoutVsInsufficientMaterial => "time forfeit",
        Termination.Abandoned => "abandoned",
        _ => "normal",
    };
}

/// <summary>
/// Minimal PGN reader for a single game: tag pairs plus mainline SAN. Comments, variations and
/// numeric annotation glyphs are skipped. Every move is replayed through the rules engine.
/// </summary>
public static partial class PgnReader
{
    public static (IReadOnlyDictionary<string, string> Tags, ChessGame Game) Read(string pgn)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in TagPattern().Matches(pgn))
        {
            tags[match.Groups[1].Value] = match.Groups[2].Value.Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        var movetext = TagPattern().Replace(pgn, " ");
        movetext = CommentPattern().Replace(movetext, " ");
        movetext = StripVariations(movetext);

        var game = new ChessGame(tags.TryGetValue("FEN", out var fen) ? fen : null);
        foreach (var raw in movetext.Split((char[])[' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            var token = MoveNumberPattern().Replace(raw, string.Empty);
            if (token.Length == 0 || token[0] == '$')
            {
                continue;
            }

            if (token is "1-0" or "0-1" or "1/2-1/2" or "*")
            {
                break;
            }

            var outcome = game.TryMoveSan(token);
            if (!outcome.Accepted)
            {
                throw new FormatException($"Move '{token}' could not be played after {game.Moves.Count} plies ({outcome.Rejection}).");
            }
        }

        return (tags, game);
    }

    private static string StripVariations(string text)
    {
        var sb = new StringBuilder(text.Length);
        var depth = 0;
        foreach (var c in text)
        {
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (depth == 0)
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"\[(\w+)\s+""((?:[^""\\]|\\.)*)""\]")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\{[^}]*\}|;[^\n]*")]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"^\d+\.+")]
    private static partial Regex MoveNumberPattern();
}
