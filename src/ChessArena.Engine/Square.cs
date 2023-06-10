namespace ChessArena.Engine;

/// <summary>
/// A board square in little-endian rank-file order: a1 = 0, b1 = 1, ..., h8 = 63.
/// </summary>
public readonly record struct Square
{
    public Square(int index)
    {
        if ((uint)index > 63)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Square index must be within 0..63.");
        }

        Index = index;
    }

    public Square(int file, int rank)
        : this(rank * 8 + file)
    {
        if ((uint)file > 7 || (uint)rank > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(file), "File and rank must be within 0..7.");
        }
    }

    public int Index { get; }

    /// <summary>0 = a-file, 7 = h-file.</summary>
    public int File => Index & 7;

    /// <summary>0 = first rank, 7 = eighth rank.</summary>
    public int Rank => Index >> 3;

    public bool IsLight => ((File + Rank) & 1) == 1;

    public static Square Parse(string text) =>
        TryParse(text, out var square)
            ? square
            : throw new FormatException($"'{text}' is not a valid square.");

    public static bool TryParse(ReadOnlySpan<char> text, out Square square)
    {
        square = default;
        if (text.Length != 2)
        {
            return false;
        }

        var file = char.ToLowerInvariant(text[0]) - 'a';
        var rank = text[1] - '1';
        if ((uint)file > 7 || (uint)rank > 7)
        {
            return false;
        }

        square = new Square(file, rank);
        return true;
    }

    public override string ToString() => $"{(char)('a' + File)}{(char)('1' + Rank)}";
}
