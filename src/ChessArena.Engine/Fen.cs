using System.Text;

namespace ChessArena.Engine;

/// <summary>Forsyth-Edwards Notation parsing and serialisation.</summary>
public static class Fen
{
    public static Position Parse(string fen)
    {
        ArgumentNullException.ThrowIfNull(fen);
        var fields = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length is < 4 or > 6)
        {
            throw new FenException($"Expected 4 to 6 space-separated fields but found {fields.Length}.");
        }

        var position = new Position();
        ParsePlacement(fields[0], position);

        var side = fields[1] switch
        {
            "w" => Color.White,
            "b" => Color.Black,
            _ => throw new FenException($"Invalid side to move '{fields[1]}'."),
        };

        var castling = SanitizeCastling(ParseCastling(fields[2]), position);
        var enPassant = ParseEnPassant(fields[3], side);
        var halfmove = fields.Length > 4 ? ParseCounter(fields[4], "halfmove clock", 0) : 0;
        var fullmove = fields.Length > 5 ? ParseCounter(fields[5], "fullmove number", 1) : 1;

        position.SetState(side, castling, enPassant, halfmove, fullmove);

        if (position.IsSquareAttacked(position.KingSquare(side.Opponent()), side))
        {
            throw new FenException("The side not to move is in check.");
        }

        return position;
    }

    public static bool TryParse(string fen, out Position? position)
    {
        try
        {
            position = Parse(fen);
            return true;
        }
        catch (FenException)
        {
            position = null;
            return false;
        }
    }

    public static string Serialize(Position position)
    {
        var sb = new StringBuilder(90);
        for (var rank = 7; rank >= 0; rank--)
        {
            var empty = 0;
            for (var file = 0; file < 8; file++)
            {
                var piece = position[rank * 8 + file];
                if (piece == Piece.None)
                {
                    empty++;
                    continue;
                }

                if (empty > 0)
                {
                    sb.Append(empty);
                    empty = 0;
                }

                sb.Append(piece.ToFenChar());
            }

            if (empty > 0)
            {
                sb.Append(empty);
            }

            if (rank > 0)
            {
                sb.Append('/');
            }
        }

        sb.Append(position.SideToMove == Color.White ? " w " : " b ");
        sb.Append(FormatCastling(position.Castling));
        sb.Append(' ');
        sb.Append(position.EnPassant?.ToString() ?? "-");
        sb.Append(' ').Append(position.HalfmoveClock);
        sb.Append(' ').Append(position.FullmoveNumber);
        return sb.ToString();
    }

    private static void ParsePlacement(string placement, Position position)
    {
        var ranks = placement.Split('/');
        if (ranks.Length != 8)
        {
            throw new FenException($"Piece placement must describe 8 ranks but has {ranks.Length}.");
        }

        int whiteKings = 0, blackKings = 0;
        for (var i = 0; i < 8; i++)
        {
            var rank = 7 - i;
            var file = 0;
            foreach (var c in ranks[i])
            {
                if (c is >= '1' and <= '8')
                {
                    file += c - '0';
                }
                else if (PieceExtensions.TryParseFenChar(c, out var piece))
                {
                    if (file > 7)
                    {
                        throw new FenException($"Rank {rank + 1} has more than 8 squares.");
                    }

                    if (piece.Type() == PieceType.Pawn && rank is 0 or 7)
                    {
                        throw new FenException("Pawns cannot stand on the first or eighth rank.");
                    }

                    if (piece == Piece.WhiteKing)
                    {
                        whiteKings++;
                    }
                    else if (piece == Piece.BlackKing)
                    {
                        blackKings++;
                    }

                    position.Place(rank * 8 + file, piece);
                    file++;
                }
                else
                {
                    throw new FenException($"Unexpected character '{c}' in piece placement.");
                }
            }

            if (file != 8)
            {
                throw new FenException($"Rank {rank + 1} describes {file} squares instead of 8.");
            }
        }

        if (whiteKings != 1 || blackKings != 1)
        {
            throw new FenException("Each side must have exactly one king.");
        }
    }

    private static CastlingRights ParseCastling(string field)
    {
        if (field == "-")
        {
            return CastlingRights.None;
        }

        var rights = CastlingRights.None;
        foreach (var c in field)
        {
            var flag = c switch
            {
                'K' => CastlingRights.WhiteKingSide,
                'Q' => CastlingRights.WhiteQueenSide,
                'k' => CastlingRights.BlackKingSide,
                'q' => CastlingRights.BlackQueenSide,
                _ => throw new FenException($"Invalid castling flag '{c}'."),
            };

            if ((rights & flag) != 0)
            {
                throw new FenException($"Duplicate castling flag '{c}'.");
            }

            rights |= flag;
        }

        return rights;
    }

    /// <summary>
    /// Drops castling rights whose king or rook is not on its original square. Many FEN sources are
    /// sloppy about this and keeping such rights would let the generator castle with a missing rook.
    /// </summary>
    private static CastlingRights SanitizeCastling(CastlingRights rights, Position position)
    {
        if (position[4] != Piece.WhiteKing)
        {
            rights &= ~CastlingRights.White;
        }

        if (position[7] != Piece.WhiteRook)
        {
            rights &= ~CastlingRights.WhiteKingSide;
        }

        if (position[0] != Piece.WhiteRook)
        {
            rights &= ~CastlingRights.WhiteQueenSide;
        }

        if (position[60] != Piece.BlackKing)
        {
            rights &= ~CastlingRights.Black;
        }

        if (position[63] != Piece.BlackRook)
        {
            rights &= ~CastlingRights.BlackKingSide;
        }

        if (position[56] != Piece.BlackRook)
        {
            rights &= ~CastlingRights.BlackQueenSide;
        }

        return rights;
    }

    private static int ParseEnPassant(string field, Color side)
    {
        if (field == "-")
        {
            return -1;
        }

        if (!Square.TryParse(field, out var square))
        {
            throw new FenException($"Invalid en passant square '{field}'.");
        }

        var expectedRank = side == Color.White ? 5 : 2;
        if (square.Rank != expectedRank)
        {
            throw new FenException($"En passant square '{field}' is impossible with {side} to move.");
        }

        return square.Index;
    }

    private static int ParseCounter(string field, string name, int minimum)
    {
        if (!int.TryParse(field, out var value) || value < minimum)
        {
            throw new FenException($"Invalid {name} '{field}'.");
        }

        return value;
    }

    private static string FormatCastling(CastlingRights rights)
    {
        if (rights == CastlingRights.None)
        {
            return "-";
        }

        var sb = new StringBuilder(4);
        if (rights.HasFlag(CastlingRights.WhiteKingSide))
        {
            sb.Append('K');
        }

        if (rights.HasFlag(CastlingRights.WhiteQueenSide))
        {
            sb.Append('Q');
        }

        if (rights.HasFlag(CastlingRights.BlackKingSide))
        {
            sb.Append('k');
        }

        if (rights.HasFlag(CastlingRights.BlackQueenSide))
        {
            sb.Append('q');
        }

        return sb.ToString();
    }
}
