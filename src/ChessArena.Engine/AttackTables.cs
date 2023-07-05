namespace ChessArena.Engine;

/// <summary>
/// Pre-computed target squares for leapers and rays for sliders, built once per process.
/// Using square lists instead of on-the-fly offset arithmetic avoids all board-edge wrap checks
/// in the hot move-generation loop.
/// </summary>
internal static class AttackTables
{
    // Direction indices: the first four are orthogonal (rook-like), the last four diagonal (bishop-like).
    internal const int North = 0, South = 1, East = 2, West = 3, NorthEast = 4, NorthWest = 5, SouthEast = 6, SouthWest = 7;

    private static readonly (int File, int Rank)[] DirectionSteps =
    [
        (0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (-1, 1), (1, -1), (-1, -1),
    ];

    private static readonly (int File, int Rank)[] KnightSteps =
    [
        (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2),
    ];

    public static readonly int[][] Knight = BuildLeaper(KnightSteps);

    public static readonly int[][] King = BuildLeaper(DirectionSteps);

    /// <summary>Squares attacked by a pawn of the given colour standing on a square: [color][square].</summary>
    public static readonly int[][][] Pawn =
    [
        BuildLeaper([(-1, 1), (1, 1)]),
        BuildLeaper([(-1, -1), (1, -1)]),
    ];

    /// <summary>Ray squares ordered outward from the origin: [direction][square].</summary>
    public static readonly int[][][] Rays = BuildRays();

    private static int[][] BuildLeaper((int File, int Rank)[] steps)
    {
        var table = new int[64][];
        for (var sq = 0; sq < 64; sq++)
        {
            var targets = new List<int>(steps.Length);
            foreach (var (df, dr) in steps)
            {
                var file = (sq & 7) + df;
                var rank = (sq >> 3) + dr;
                if ((uint)file < 8 && (uint)rank < 8)
                {
                    targets.Add(rank * 8 + file);
                }
            }

            table[sq] = [.. targets];
        }

        return table;
    }

    private static int[][][] BuildRays()
    {
        var rays = new int[8][][];
        for (var dir = 0; dir < 8; dir++)
        {
            var (df, dr) = DirectionSteps[dir];
            rays[dir] = new int[64][];
            for (var sq = 0; sq < 64; sq++)
            {
                var ray = new List<int>(7);
                var file = (sq & 7) + df;
                var rank = (sq >> 3) + dr;
                while ((uint)file < 8 && (uint)rank < 8)
                {
                    ray.Add(rank * 8 + file);
                    file += df;
                    rank += dr;
                }

                rays[dir][sq] = [.. ray];
            }
        }

        return rays;
    }
}
