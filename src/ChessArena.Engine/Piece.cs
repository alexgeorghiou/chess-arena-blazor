namespace ChessArena.Engine;

public enum Color : byte
{
    White = 0,
    Black = 1,
}

public enum PieceType : byte
{
    None = 0,
    Pawn = 1,
    Knight = 2,
    Bishop = 3,
    Rook = 4,
    Queen = 5,
    King = 6,
}

/// <summary>
/// A coloured piece packed into a byte: the low three bits hold the <see cref="PieceType"/>,
/// bit 3 holds the <see cref="Color"/>. Keeping it a plain enum makes the board array cheap to copy.
/// </summary>
public enum Piece : byte
{
    None = 0,
    WhitePawn = 1,
    WhiteKnight = 2,
    WhiteBishop = 3,
    WhiteRook = 4,
    WhiteQueen = 5,
    WhiteKing = 6,
    BlackPawn = 9,
    BlackKnight = 10,
    BlackBishop = 11,
    BlackRook = 12,
    BlackQueen = 13,
    BlackKing = 14,
}

public static class PieceExtensions
{
    private const string FenLetters = " PNBRQK";

    public static PieceType Type(this Piece piece) => (PieceType)((byte)piece & 7);

    public static Color Color(this Piece piece) => (Color)((byte)piece >> 3);

    public static bool IsNone(this Piece piece) => piece == Piece.None;

    public static Color Opponent(this Color color) => color == Engine.Color.White ? Engine.Color.Black : Engine.Color.White;

    public static Piece Of(this PieceType type, Color color) =>
        type == PieceType.None ? Piece.None : (Piece)((byte)type | ((byte)color << 3));

    /// <summary>FEN letter: upper case for white, lower case for black.</summary>
    public static char ToFenChar(this Piece piece)
    {
        if (piece == Piece.None)
        {
            throw new ArgumentException("An empty square has no FEN letter.", nameof(piece));
        }

        var letter = FenLetters[(int)piece.Type()];
        return piece.Color() == Engine.Color.White ? letter : char.ToLowerInvariant(letter);
    }

    /// <summary>Upper-case letter used in SAN (e.g. N, B, R, Q, K). Pawns have no letter.</summary>
    public static char ToSanChar(this PieceType type) => type switch
    {
        PieceType.Knight => 'N',
        PieceType.Bishop => 'B',
        PieceType.Rook => 'R',
        PieceType.Queen => 'Q',
        PieceType.King => 'K',
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Only officers have a SAN letter."),
    };

    public static bool TryParseFenChar(char c, out Piece piece)
    {
        var index = FenLetters.IndexOf(char.ToUpperInvariant(c), 1);
        if (index < 1)
        {
            piece = Piece.None;
            return false;
        }

        var color = char.IsUpper(c) ? Engine.Color.White : Engine.Color.Black;
        piece = ((PieceType)index).Of(color);
        return true;
    }

    public static bool TryParsePromotion(char c, out PieceType type)
    {
        type = char.ToLowerInvariant(c) switch
        {
            'n' => PieceType.Knight,
            'b' => PieceType.Bishop,
            'r' => PieceType.Rook,
            'q' => PieceType.Queen,
            _ => PieceType.None,
        };
        return type != PieceType.None;
    }
}
