namespace ChessArena.Engine;

/// <summary>
/// Mutable board state with make/unmake support. All rule enforcement lives here and in
/// <see cref="MoveGenerator"/>; higher layers (game, clocks, networking) only ever talk to
/// the engine through legal moves.
/// </summary>
public sealed partial class Position
{
    public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    // Rights that survive a move touching the given square; e.g. moving from or capturing on h1
    // removes white's king-side right.
    private static readonly CastlingRights[] CastlingMask = BuildCastlingMask();

    private readonly Piece[] _board = new Piece[64];
    private readonly int[] _kingSquare = [-1, -1];
    private int _enPassant = -1;

    internal Position()
    {
    }

    public Color SideToMove { get; private set; }

    public CastlingRights Castling { get; private set; }

    /// <summary>The square a pawn just skipped over, if the last move was a double push.</summary>
    public Square? EnPassant => _enPassant < 0 ? null : new Square(_enPassant);

    /// <summary>Plies since the last capture or pawn move (for the fifty-move rule).</summary>
    public int HalfmoveClock { get; private set; }

    public int FullmoveNumber { get; private set; } = 1;

    public Piece this[Square square] => _board[square.Index];

    internal Piece this[int square] => _board[square];

    public static Position Initial() => Fen.Parse(StartFen);

    public static Position FromFen(string fen) => Fen.Parse(fen);

    public string ToFen() => Fen.Serialize(this);

    public Square KingSquare(Color color) => new(_kingSquare[(int)color]);

    public bool IsInCheck => IsSquareAttacked(_kingSquare[(int)SideToMove], SideToMove.Opponent());

    public Position Clone()
    {
        var copy = new Position
        {
            SideToMove = SideToMove,
            Castling = Castling,
            HalfmoveClock = HalfmoveClock,
            FullmoveNumber = FullmoveNumber,
            _enPassant = _enPassant,
        };
        Array.Copy(_board, copy._board, 64);
        copy._kingSquare[0] = _kingSquare[0];
        copy._kingSquare[1] = _kingSquare[1];
        return copy;
    }

    public IEnumerable<(Square Square, Piece Piece)> Pieces()
    {
        for (var sq = 0; sq < 64; sq++)
        {
            if (_board[sq] != Piece.None)
            {
                yield return (new Square(sq), _board[sq]);
            }
        }
    }

    /// <summary>
    /// Applies a move that is at least pseudo-legal and returns the information needed to undo it.
    /// The caller is responsible for legality; use <see cref="GetLegalMoves"/> to obtain moves.
    /// </summary>
    public UndoInfo MakeMove(Move move)
    {
        var from = move.From.Index;
        var to = move.To.Index;
        var piece = _board[from];
        var us = SideToMove;
        var type = piece.Type();

        var captured = _board[to];
        var isEnPassant = type == PieceType.Pawn && to == _enPassant && captured == Piece.None;
        var undo = new UndoInfo(captured, Castling, _enPassant, HalfmoveClock, isEnPassant);

        if (isEnPassant)
        {
            var victim = us == Color.White ? to - 8 : to + 8;
            undo = undo with { Captured = _board[victim] };
            _board[victim] = Piece.None;
        }

        _board[to] = move.IsPromotion ? move.Promotion.Of(us) : piece;
        _board[from] = Piece.None;

        if (type == PieceType.King)
        {
            _kingSquare[(int)us] = to;
            if (to - from == 2)
            {
                _board[to - 1] = _board[to + 1];
                _board[to + 1] = Piece.None;
            }
            else if (from - to == 2)
            {
                _board[to + 1] = _board[to - 2];
                _board[to - 2] = Piece.None;
            }
        }

        _enPassant = type == PieceType.Pawn && Math.Abs(to - from) == 16 ? (from + to) / 2 : -1;
        Castling &= CastlingMask[from] & CastlingMask[to];
        HalfmoveClock = type == PieceType.Pawn || undo.Captured != Piece.None ? 0 : HalfmoveClock + 1;
        if (us == Color.Black)
        {
            FullmoveNumber++;
        }

        SideToMove = us.Opponent();
        return undo;
    }

    public void UnmakeMove(Move move, UndoInfo undo)
    {
        var from = move.From.Index;
        var to = move.To.Index;
        var us = SideToMove.Opponent();
        SideToMove = us;
        if (us == Color.Black)
        {
            FullmoveNumber--;
        }

        var moved = move.IsPromotion ? PieceType.Pawn.Of(us) : _board[to];
        _board[from] = moved;

        if (undo.WasEnPassant)
        {
            _board[to] = Piece.None;
            _board[us == Color.White ? to - 8 : to + 8] = undo.Captured;
        }
        else
        {
            _board[to] = undo.Captured;
        }

        if (moved.Type() == PieceType.King)
        {
            _kingSquare[(int)us] = from;
            if (to - from == 2)
            {
                _board[to + 1] = _board[to - 1];
                _board[to - 1] = Piece.None;
            }
            else if (from - to == 2)
            {
                _board[to - 2] = _board[to + 1];
                _board[to + 1] = Piece.None;
            }
        }

        Castling = undo.Castling;
        _enPassant = undo.EnPassant;
        HalfmoveClock = undo.HalfmoveClock;
    }

    /// <summary>Returns true if any piece of <paramref name="by"/> attacks <paramref name="square"/>.</summary>
    public bool IsSquareAttacked(Square square, Color by) => IsSquareAttacked(square.Index, by);

    internal bool IsSquareAttacked(int sq, Color by)
    {
        // A pawn of colour `by` attacks sq if it stands where an opposite-coloured pawn on sq would attack.
        var pawn = PieceType.Pawn.Of(by);
        foreach (var origin in AttackTables.Pawn[(int)by.Opponent()][sq])
        {
            if (_board[origin] == pawn)
            {
                return true;
            }
        }

        var knight = PieceType.Knight.Of(by);
        foreach (var origin in AttackTables.Knight[sq])
        {
            if (_board[origin] == knight)
            {
                return true;
            }
        }

        var king = PieceType.King.Of(by);
        foreach (var origin in AttackTables.King[sq])
        {
            if (_board[origin] == king)
            {
                return true;
            }
        }

        var rook = PieceType.Rook.Of(by);
        var bishop = PieceType.Bishop.Of(by);
        var queen = PieceType.Queen.Of(by);
        for (var dir = 0; dir < 8; dir++)
        {
            var slider = dir < 4 ? rook : bishop;
            foreach (var target in AttackTables.Rays[dir][sq])
            {
                var occupant = _board[target];
                if (occupant == Piece.None)
                {
                    continue;
                }

                if (occupant == slider || occupant == queen)
                {
                    return true;
                }

                break;
            }
        }

        return false;
    }

    /// <summary>Zobrist key identifying the position for repetition detection.</summary>
    public ulong ComputeKey()
    {
        ulong key = 0;
        for (var sq = 0; sq < 64; sq++)
        {
            if (_board[sq] != Piece.None)
            {
                key ^= Zobrist.Pieces[(int)_board[sq], sq];
            }
        }

        key ^= Zobrist.Castling[(int)Castling];
        if (SideToMove == Color.Black)
        {
            key ^= Zobrist.BlackToMove;
        }

        // FIDE only treats positions as different when an en passant capture is actually available,
        // so the file is hashed only if a capturing pawn stands next to the skipped square.
        if (_enPassant >= 0 && HasEnPassantCapturer())
        {
            key ^= Zobrist.EnPassantFile[_enPassant & 7];
        }

        return key;
    }

    private bool HasEnPassantCapturer()
    {
        var pawn = PieceType.Pawn.Of(SideToMove);
        foreach (var origin in AttackTables.Pawn[(int)SideToMove.Opponent()][_enPassant])
        {
            if (_board[origin] == pawn)
            {
                return true;
            }
        }

        return false;
    }

    internal void Place(int square, Piece piece)
    {
        _board[square] = piece;
        if (piece.Type() == PieceType.King)
        {
            _kingSquare[(int)piece.Color()] = square;
        }
    }

    internal void SetState(Color sideToMove, CastlingRights castling, int enPassant, int halfmove, int fullmove)
    {
        SideToMove = sideToMove;
        Castling = castling;
        _enPassant = enPassant;
        HalfmoveClock = halfmove;
        FullmoveNumber = fullmove;
    }

    private static CastlingRights[] BuildCastlingMask()
    {
        var mask = Enumerable.Repeat(CastlingRights.All, 64).ToArray();
        mask[4] = ~CastlingRights.White & CastlingRights.All;   // e1
        mask[0] = ~CastlingRights.WhiteQueenSide & CastlingRights.All; // a1
        mask[7] = ~CastlingRights.WhiteKingSide & CastlingRights.All;  // h1
        mask[60] = ~CastlingRights.Black & CastlingRights.All;  // e8
        mask[56] = ~CastlingRights.BlackQueenSide & CastlingRights.All; // a8
        mask[63] = ~CastlingRights.BlackKingSide & CastlingRights.All;  // h8
        return mask;
    }
}

/// <summary>State that cannot be recomputed when a move is taken back.</summary>
public readonly record struct UndoInfo(
    Piece Captured,
    CastlingRights Castling,
    int EnPassant,
    int HalfmoveClock,
    bool WasEnPassant);
