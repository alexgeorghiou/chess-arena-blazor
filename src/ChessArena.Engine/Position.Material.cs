namespace ChessArena.Engine;

public sealed partial class Position
{
    /// <summary>
    /// True when neither side can possibly deliver mate: bare kings, a single minor piece,
    /// or any number of bishops that all live on squares of the same colour.
    /// </summary>
    public bool HasInsufficientMaterial()
    {
        int knights = 0, lightBishops = 0, darkBishops = 0;
        for (var sq = 0; sq < 64; sq++)
        {
            switch (_board[sq].Type())
            {
                case PieceType.Pawn:
                case PieceType.Rook:
                case PieceType.Queen:
                    return false;
                case PieceType.Knight:
                    knights++;
                    break;
                case PieceType.Bishop:
                    if (new Square(sq).IsLight)
                    {
                        lightBishops++;
                    }
                    else
                    {
                        darkBishops++;
                    }

                    break;
            }
        }

        var bishops = lightBishops + darkBishops;
        return (knights + bishops) switch
        {
            0 or 1 => true,
            _ => knights == 0 && (lightBishops == 0 || darkBishops == 0),
        };
    }

    /// <summary>
    /// True when <paramref name="color"/> has at most a king and a single minor piece. Used for the
    /// timeout rule: a player whose flag falls does not lose if the opponent could never mate.
    /// </summary>
    public bool HasInsufficientMatingMaterial(Color color)
    {
        var minors = 0;
        for (var sq = 0; sq < 64; sq++)
        {
            var piece = _board[sq];
            if (piece == Piece.None || piece.Color() != color)
            {
                continue;
            }

            switch (piece.Type())
            {
                case PieceType.Pawn:
                case PieceType.Rook:
                case PieceType.Queen:
                    return false;
                case PieceType.Knight:
                case PieceType.Bishop:
                    minors++;
                    break;
            }
        }

        return minors <= 1;
    }
}
