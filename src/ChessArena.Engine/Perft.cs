namespace ChessArena.Engine;

/// <summary>
/// Performance test: counts leaf nodes of the legal move tree. Comparing against published
/// counts is the standard way to prove a move generator correct.
/// </summary>
public static class Perft
{
    public static long Count(Position position, int depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(depth);
        var buffers = new List<Move>[depth + 1];
        for (var i = 0; i < buffers.Length; i++)
        {
            buffers[i] = new List<Move>(64);
        }

        return Count(position, depth, buffers);
    }

    /// <summary>Per-root-move node counts, handy for bisecting a generator bug against a reference engine.</summary>
    public static IReadOnlyDictionary<string, long> Divide(Position position, int depth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(depth, 1);
        var result = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var move in position.GetLegalMoves())
        {
            var undo = position.MakeMove(move);
            result[move.ToUci()] = Count(position, depth - 1);
            position.UnmakeMove(move, undo);
        }

        return result;
    }

    private static long Count(Position position, int depth, List<Move>[] buffers)
    {
        if (depth == 0)
        {
            return 1;
        }

        var moves = buffers[depth];
        position.GenerateLegalMoves(moves);
        if (depth == 1)
        {
            return moves.Count;
        }

        long nodes = 0;
        // Each depth owns its buffer, so recursing deeper never clobbers the list being iterated.
        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var undo = position.MakeMove(move);
            nodes += Count(position, depth - 1, buffers);
            position.UnmakeMove(move, undo);
        }

        return nodes;
    }
}
