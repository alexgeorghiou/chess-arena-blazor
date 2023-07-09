namespace ChessArena.Engine;

public sealed partial class Position
{
    private static readonly PieceType[] PromotionPieces =
        [PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight];

    public IReadOnlyList<Move> GetLegalMoves()
    {
        var moves = new List<Move>(64);
        GenerateLegalMoves(moves);
        return moves;
    }

    /// <summary>
    /// Fills <paramref name="moves"/> with every legal move. Pseudo-legal moves are generated first and
    /// each is verified by playing it and testing whether the mover's king is left attacked; simple,
    /// obviously correct, and fast enough for perft-validated online play.
    /// </summary>
    public void GenerateLegalMoves(List<Move> moves)
    {
        moves.Clear();
        GeneratePseudoLegalMoves(moves);

        var us = SideToMove;
        var them = us.Opponent();
        var write = 0;
        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var undo = MakeMove(move);
            var legal = !IsSquareAttacked(_kingSquare[(int)us], them);
            UnmakeMove(move, undo);
            if (legal)
            {
                moves[write++] = move;
            }
        }

        moves.RemoveRange(write, moves.Count - write);
    }

    public bool HasLegalMoves()
    {
        var moves = new List<Move>(64);
        GeneratePseudoLegalMoves(moves);
        var us = SideToMove;
        foreach (var move in moves)
        {
            var undo = MakeMove(move);
            var legal = !IsSquareAttacked(_kingSquare[(int)us], us.Opponent());
            UnmakeMove(move, undo);
            if (legal)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsLegal(Move move)
    {
        foreach (var candidate in GetLegalMoves())
        {
            if (candidate == move)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsCheckmate => IsInCheck && !HasLegalMoves();

    public bool IsStalemate => !IsInCheck && !HasLegalMoves();

    internal void GeneratePseudoLegalMoves(List<Move> moves)
    {
        var us = SideToMove;
        for (var sq = 0; sq < 64; sq++)
        {
            var piece = _board[sq];
            if (piece == Piece.None || piece.Color() != us)
            {
                continue;
            }

            switch (piece.Type())
            {
                case PieceType.Pawn:
                    GeneratePawnMoves(sq, us, moves);
                    break;
                case PieceType.Knight:
                    GenerateLeaperMoves(sq, us, AttackTables.Knight[sq], moves);
                    break;
                case PieceType.Bishop:
                    GenerateSliderMoves(sq, us, 4, 8, moves);
                    break;
                case PieceType.Rook:
                    GenerateSliderMoves(sq, us, 0, 4, moves);
                    break;
                case PieceType.Queen:
                    GenerateSliderMoves(sq, us, 0, 8, moves);
                    break;
                case PieceType.King:
                    GenerateLeaperMoves(sq, us, AttackTables.King[sq], moves);
                    GenerateCastling(sq, us, moves);
                    break;
            }
        }
    }

    private void GeneratePawnMoves(int from, Color us, List<Move> moves)
    {
        var forward = us == Color.White ? 8 : -8;
        var startRank = us == Color.White ? 1 : 6;
        var promotionRank = us == Color.White ? 7 : 0;

        var one = from + forward;
        if (_board[one] == Piece.None)
        {
            AddPawnMove(from, one, promotionRank, moves);
            var two = one + forward;
            if (from >> 3 == startRank && _board[two] == Piece.None)
            {
                moves.Add(new Move(from, two));
            }
        }

        foreach (var target in AttackTables.Pawn[(int)us][from])
        {
            var occupant = _board[target];
            if ((occupant != Piece.None && occupant.Color() != us) || target == _enPassant)
            {
                AddPawnMove(from, target, promotionRank, moves);
            }
        }
    }

    private static void AddPawnMove(int from, int to, int promotionRank, List<Move> moves)
    {
        if (to >> 3 == promotionRank)
        {
            foreach (var promotion in PromotionPieces)
            {
                moves.Add(new Move(from, to, promotion));
            }
        }
        else
        {
            moves.Add(new Move(from, to));
        }
    }

    private void GenerateLeaperMoves(int from, Color us, int[] targets, List<Move> moves)
    {
        foreach (var target in targets)
        {
            var occupant = _board[target];
            if (occupant == Piece.None || occupant.Color() != us)
            {
                moves.Add(new Move(from, target));
            }
        }
    }

    private void GenerateSliderMoves(int from, Color us, int firstDirection, int lastDirection, List<Move> moves)
    {
        for (var dir = firstDirection; dir < lastDirection; dir++)
        {
            foreach (var target in AttackTables.Rays[dir][from])
            {
                var occupant = _board[target];
                if (occupant == Piece.None)
                {
                    moves.Add(new Move(from, target));
                    continue;
                }

                if (occupant.Color() != us)
                {
                    moves.Add(new Move(from, target));
                }

                break;
            }
        }
    }

    private void GenerateCastling(int from, Color us, List<Move> moves)
    {
        var (kingSide, queenSide, home) = us == Color.White
            ? (CastlingRights.WhiteKingSide, CastlingRights.WhiteQueenSide, 4)
            : (CastlingRights.BlackKingSide, CastlingRights.BlackQueenSide, 60);

        if (from != home || (Castling & (kingSide | queenSide)) == 0)
        {
            return;
        }

        var them = us.Opponent();
        if (IsSquareAttacked(home, them))
        {
            return;
        }

        // The destination square is verified by the regular legality test; only the transit square is checked here.
        if ((Castling & kingSide) != 0
            && _board[home + 1] == Piece.None
            && _board[home + 2] == Piece.None
            && !IsSquareAttacked(home + 1, them))
        {
            moves.Add(new Move(home, home + 2));
        }

        if ((Castling & queenSide) != 0
            && _board[home - 1] == Piece.None
            && _board[home - 2] == Piece.None
            && _board[home - 3] == Piece.None
            && !IsSquareAttacked(home - 1, them))
        {
            moves.Add(new Move(home, home - 2));
        }
    }
}
