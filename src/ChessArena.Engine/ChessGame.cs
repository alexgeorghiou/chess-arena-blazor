namespace ChessArena.Engine;

/// <summary>A move as recorded in a game's history.</summary>
public sealed record PlayedMove(int Ply, Move Move, string San, string FenAfter)
{
    public Color Mover => Ply % 2 == 1 ? Color.White : Color.Black;
}

public enum MoveRejection
{
    None = 0,
    GameOver,
    Malformed,
    Illegal,
}

public readonly record struct MoveOutcome(bool Accepted, MoveRejection Rejection, PlayedMove? Played)
{
    public static MoveOutcome Rejected(MoveRejection reason) => new(false, reason, null);
}

/// <summary>
/// A single game from a starting position to its result. Applies the automatic game-ending rules
/// (mate, stalemate, threefold repetition, fifty-move rule, dead positions) and exposes hooks for
/// the externally-driven endings: resignation, flag fall and draw agreement.
/// </summary>
public sealed class ChessGame
{
    private readonly Position _position;
    private readonly List<PlayedMove> _moves = [];
    private readonly Dictionary<ulong, int> _repetitions = [];
    private readonly int _startPlyOffset;
    private IReadOnlyList<Move>? _legalMovesCache;

    public ChessGame(string? startFen = null)
    {
        StartFen = startFen ?? Position.StartFen;
        _position = Position.FromFen(StartFen);
        _startPlyOffset = _position.SideToMove == Color.Black ? 1 : 0;
        CountRepetition();
        EvaluateAutomaticEnd();
    }

    public string StartFen { get; }

    public bool IsStandardStart => StartFen == Position.StartFen;

    public GameResult Result { get; private set; }

    public Termination Termination { get; private set; }

    public bool IsOver => Result != GameResult.Ongoing;

    public Color SideToMove => _position.SideToMove;

    public string Fen => _position.ToFen();

    public bool IsInCheck => _position.IsInCheck;

    public IReadOnlyList<PlayedMove> Moves => _moves;

    public PlayedMove? LastMove => _moves.Count == 0 ? null : _moves[^1];

    /// <summary>A defensive copy: callers can analyse it freely without corrupting the game.</summary>
    public Position GetPosition() => _position.Clone();

    public IReadOnlyList<Move> LegalMoves =>
        IsOver ? [] : _legalMovesCache ??= _position.GetLegalMoves();

    public MoveOutcome TryMove(Move move)
    {
        if (IsOver)
        {
            return MoveOutcome.Rejected(MoveRejection.GameOver);
        }

        if (!LegalMoves.Contains(move))
        {
            return MoveOutcome.Rejected(MoveRejection.Illegal);
        }

        var san = San.ToSan(_position, move);
        _position.MakeMove(move);
        _legalMovesCache = null;

        var played = new PlayedMove(_moves.Count + 1 + _startPlyOffset, move, san, _position.ToFen());
        _moves.Add(played);
        CountRepetition();
        EvaluateAutomaticEnd();
        return new MoveOutcome(true, MoveRejection.None, played);
    }

    public MoveOutcome TryMoveUci(string uci) =>
        TryResolveUci(uci, out var move, out var rejection) ? TryMove(move) : MoveOutcome.Rejected(rejection);

    /// <summary>
    /// Validates a UCI string against the current position without playing it, so callers can run
    /// side effects (such as pressing a clock) between validation and execution. A promotion written
    /// without a piece ("e7e8") defaults to a queen, which is what UIs nearly always mean.
    /// </summary>
    public bool TryResolveUci(string uci, out Move move, out MoveRejection rejection)
    {
        rejection = MoveRejection.None;
        if (IsOver)
        {
            move = default;
            rejection = MoveRejection.GameOver;
            return false;
        }

        if (!Move.TryParseUci(uci, out move))
        {
            rejection = MoveRejection.Malformed;
            return false;
        }

        if (!move.IsPromotion && LegalMoves.Contains(move with { Promotion = PieceType.Queen }))
        {
            move = move with { Promotion = PieceType.Queen };
        }

        if (!LegalMoves.Contains(move))
        {
            rejection = MoveRejection.Illegal;
            return false;
        }

        return true;
    }

    public MoveOutcome TryMoveSan(string san)
    {
        if (IsOver)
        {
            return MoveOutcome.Rejected(MoveRejection.GameOver);
        }

        return San.TryParse(_position, san, out var move)
            ? TryMove(move)
            : MoveOutcome.Rejected(MoveRejection.Illegal);
    }

    public void Resign(Color loser) => Finish(GameResultExtensions.WinFor(loser.Opponent()), Termination.Resignation);

    public void AgreeDraw() => Finish(GameResult.Draw, Termination.DrawAgreement);

    public void Abandon(Color quitter) => Finish(GameResultExtensions.WinFor(quitter.Opponent()), Termination.Abandoned);

    /// <summary>
    /// The flagged side loses on time unless the opponent has insufficient material to ever mate
    /// (FIDE Laws 6.9), in which case the game is drawn.
    /// </summary>
    public void FlagFall(Color flagged)
    {
        if (_position.HasInsufficientMatingMaterial(flagged.Opponent()))
        {
            Finish(GameResult.Draw, Termination.TimeoutVsInsufficientMaterial);
        }
        else
        {
            Finish(GameResultExtensions.WinFor(flagged.Opponent()), Termination.Timeout);
        }
    }

    public string ToPgn(PgnHeaders? headers = null) => PgnWriter.Write(this, headers ?? new PgnHeaders());

    private void Finish(GameResult result, Termination termination)
    {
        if (IsOver)
        {
            throw new InvalidOperationException($"The game is already over ({Termination}).");
        }

        Result = result;
        Termination = termination;
        _legalMovesCache = null;
    }

    private void CountRepetition()
    {
        var key = _position.ComputeKey();
        _repetitions[key] = _repetitions.TryGetValue(key, out var count) ? count + 1 : 1;
    }

    private void EvaluateAutomaticEnd()
    {
        // Online play applies the claimable draws automatically, as most servers do. Mate takes
        // precedence over the fifty-move rule when the hundredth ply delivers it.
        if (!_position.HasLegalMoves())
        {
            if (_position.IsInCheck)
            {
                Finish(GameResultExtensions.WinFor(_position.SideToMove.Opponent()), Termination.Checkmate);
            }
            else
            {
                Finish(GameResult.Draw, Termination.Stalemate);
            }
        }
        else if (_position.HasInsufficientMaterial())
        {
            Finish(GameResult.Draw, Termination.InsufficientMaterial);
        }
        else if (_repetitions[_position.ComputeKey()] >= 3)
        {
            Finish(GameResult.Draw, Termination.ThreefoldRepetition);
        }
        else if (_position.HalfmoveClock >= 100)
        {
            Finish(GameResult.Draw, Termination.FiftyMoveRule);
        }
    }
}
