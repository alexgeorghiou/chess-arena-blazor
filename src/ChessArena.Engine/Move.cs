namespace ChessArena.Engine;

/// <summary>
/// A move expressed as origin, destination and optional promotion piece. Special moves
/// (castling, en passant, double pawn pushes) are inferred from the position, which keeps
/// the type trivially constructible from UCI strings and user clicks alike.
/// </summary>
public readonly record struct Move(Square From, Square To, PieceType Promotion = PieceType.None)
{
    internal Move(int from, int to, PieceType promotion = PieceType.None)
        : this(new Square(from), new Square(to), promotion)
    {
    }

    public bool IsPromotion => Promotion != PieceType.None;

    /// <summary>Long algebraic (UCI) notation, e.g. <c>e2e4</c> or <c>e7e8q</c>.</summary>
    public string ToUci() => IsPromotion
        ? $"{From}{To}{char.ToLowerInvariant(Promotion.ToSanChar())}"
        : $"{From}{To}";

    public static bool TryParseUci(ReadOnlySpan<char> text, out Move move)
    {
        move = default;
        if (text.Length is not (4 or 5))
        {
            return false;
        }

        if (!Square.TryParse(text[..2], out var from) || !Square.TryParse(text[2..4], out var to))
        {
            return false;
        }

        var promotion = PieceType.None;
        if (text.Length == 5 && !PieceExtensions.TryParsePromotion(text[4], out promotion))
        {
            return false;
        }

        move = new Move(from, to, promotion);
        return true;
    }

    public override string ToString() => ToUci();
}
