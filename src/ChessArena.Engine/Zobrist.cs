namespace ChessArena.Engine;

/// <summary>
/// Zobrist keys used to recognise repeated positions. Seeded deterministically so keys are
/// stable across processes, which makes stored repetition keys and test expectations reproducible.
/// </summary>
internal static class Zobrist
{
    public static readonly ulong[,] Pieces = new ulong[16, 64];
    public static readonly ulong[] Castling = new ulong[16];
    public static readonly ulong[] EnPassantFile = new ulong[8];
    public static readonly ulong BlackToMove;

    static Zobrist()
    {
        // SplitMix64: tiny, deterministic and well distributed.
        var state = 0x9E3779B97F4A7C15UL;
        ulong Next()
        {
            state += 0x9E3779B97F4A7C15UL;
            var z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        for (var piece = 0; piece < 16; piece++)
        {
            for (var sq = 0; sq < 64; sq++)
            {
                Pieces[piece, sq] = Next();
            }
        }

        for (var i = 0; i < Castling.Length; i++)
        {
            Castling[i] = Next();
        }

        for (var i = 0; i < EnPassantFile.Length; i++)
        {
            EnPassantFile[i] = Next();
        }

        BlackToMove = Next();
    }
}
