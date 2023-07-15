using System.Text;

namespace ChessArena.Engine;

/// <summary>Standard Algebraic Notation (as used in PGN) for legal moves.</summary>
public static class San
{
    public static string ToSan(Position position, Move move)
    {
        var legalMoves = position.GetLegalMoves();
        if (!legalMoves.Contains(move))
        {
            throw new ArgumentException($"{move} is not legal in {position.ToFen()}.", nameof(move));
        }

        return Format(position, move, legalMoves);
    }

    public static Move Parse(Position position, string san) =>
        TryParse(position, san, out var move)
            ? move
            : throw new FormatException($"'{san}' is not a legal move in {position.ToFen()}.");

    /// <summary>
    /// Resolves a SAN string by matching it against the SAN of every legal move. Check/mate suffixes and
    /// annotation glyphs (!, ?) are ignored, castling may be written with zeros, and the promotion '=' is optional.
    /// </summary>
    public static bool TryParse(Position position, string san, out Move move)
    {
        move = default;
        if (string.IsNullOrWhiteSpace(san))
        {
            return false;
        }

        var wanted = Normalize(san);
        var legalMoves = position.GetLegalMoves();
        foreach (var candidate in legalMoves)
        {
            if (Normalize(Format(position, candidate, legalMoves, includeCheck: false)) == wanted)
            {
                move = candidate;
                return true;
            }
        }

        return false;
    }

    private static string Format(Position position, Move move, IReadOnlyList<Move> legalMoves, bool includeCheck = true)
    {
        var piece = position[move.From];
        var type = piece.Type();
        var sb = new StringBuilder(8);

        if (type == PieceType.King && Math.Abs(move.To.Index - move.From.Index) == 2)
        {
            sb.Append(move.To.File == 6 ? "O-O" : "O-O-O");
        }
        else
        {
            var isCapture = position[move.To] != Piece.None
                || (type == PieceType.Pawn && move.From.File != move.To.File);

            if (type == PieceType.Pawn)
            {
                if (isCapture)
                {
                    sb.Append((char)('a' + move.From.File));
                }
            }
            else
            {
                sb.Append(type.ToSanChar());
                AppendDisambiguation(sb, position, move, type, legalMoves);
            }

            if (isCapture)
            {
                sb.Append('x');
            }

            sb.Append(move.To);

            if (move.IsPromotion)
            {
                sb.Append('=').Append(move.Promotion.ToSanChar());
            }
        }

        if (includeCheck)
        {
            var undo = position.MakeMove(move);
            if (position.IsInCheck)
            {
                sb.Append(position.HasLegalMoves() ? '+' : '#');
            }

            position.UnmakeMove(move, undo);
        }

        return sb.ToString();
    }

    private static void AppendDisambiguation(StringBuilder sb, Position position, Move move, PieceType type, IReadOnlyList<Move> legalMoves)
    {
        bool ambiguous = false, sameFile = false, sameRank = false;
        foreach (var other in legalMoves)
        {
            if (other.To != move.To || other.From == move.From || position[other.From].Type() != type)
            {
                continue;
            }

            ambiguous = true;
            sameFile |= other.From.File == move.From.File;
            sameRank |= other.From.Rank == move.From.Rank;
        }

        if (!ambiguous)
        {
            return;
        }

        if (!sameFile)
        {
            sb.Append((char)('a' + move.From.File));
        }
        else if (!sameRank)
        {
            sb.Append((char)('1' + move.From.Rank));
        }
        else
        {
            sb.Append(move.From);
        }
    }

    private static string Normalize(string san)
    {
        var text = san.Trim().TrimEnd('+', '#', '!', '?').Replace("0", "O", StringComparison.Ordinal);
        // Accept "e8Q" as well as "e8=Q".
        if (text.Length >= 3 && char.IsAsciiLetterUpper(text[^1]) && char.IsAsciiDigit(text[^2]))
        {
            text = string.Concat(text.AsSpan(0, text.Length - 1), "=", text.AsSpan(text.Length - 1));
        }

        return text;
    }
}
