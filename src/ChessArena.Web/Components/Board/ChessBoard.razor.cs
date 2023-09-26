using ChessArena.Engine;
using Microsoft.AspNetCore.Components;

namespace ChessArena.Web.Components.Board;

/// <summary>
/// Click-to-move board. It never decides legality itself: the server sends the legal moves for
/// the current position and the board only offers those, so client and server cannot disagree.
/// </summary>
public partial class ChessBoard
{
    private static readonly (char Letter, PieceType Type)[] PromotionChoices =
        [('q', PieceType.Queen), ('r', PieceType.Rook), ('b', PieceType.Bishop), ('n', PieceType.Knight)];

    private Position _position = Position.Initial();
    private string? _renderedFen;
    private Square? _selected;
    private string? _pendingPromotion;

    [Parameter, EditorRequired]
    public string Fen { get; set; } = Position.StartFen;

    [Parameter]
    public Color Orientation { get; set; } = Color.White;

    /// <summary>Moves in UCI notation that the local user may play right now.</summary>
    [Parameter]
    public IReadOnlyList<string> LegalMoves { get; set; } = [];

    [Parameter]
    public string? LastMove { get; set; }

    [Parameter]
    public bool IsCheck { get; set; }

    [Parameter]
    public bool Interactive { get; set; }

    [Parameter]
    public EventCallback<string> OnMove { get; set; }

    internal Square? Selected => _selected;

    private IEnumerable<Square> DisplaySquares
    {
        get
        {
            for (var row = 0; row < 8; row++)
            {
                for (var col = 0; col < 8; col++)
                {
                    yield return Orientation == Color.White
                        ? new Square(col, 7 - row)
                        : new Square(7 - col, row);
                }
            }
        }
    }

    protected override void OnParametersSet()
    {
        if (_renderedFen != Fen)
        {
            _position = Position.FromFen(Fen);
            _renderedFen = Fen;
            _selected = null;
            _pendingPromotion = null;
        }
    }

    internal static string Glyph(Piece piece) => piece.Type() switch
    {
        // Solid glyphs for both sides; colour comes from CSS so both sets render with equal weight.
        PieceType.King => "♚",
        PieceType.Queen => "♛",
        PieceType.Rook => "♜",
        PieceType.Bishop => "♝",
        PieceType.Knight => "♞",
        PieceType.Pawn => "♟",
        _ => string.Empty,
    };

    private string SquareClasses(Square square)
    {
        var classes = new List<string>(5) { "square", square.IsLight ? "light" : "dark" };
        if (_selected == square)
        {
            classes.Add("selected");
        }
        else if (IsTarget(square))
        {
            classes.Add(_position[square] == Piece.None ? "target" : "target capture");
        }

        if (LastMove is { Length: >= 4 } last && (last[..2] == square.ToString() || last[2..4] == square.ToString()))
        {
            classes.Add("last");
        }

        if (IsCheck && _position[square] == PieceType.King.Of(_position.SideToMove))
        {
            classes.Add("check");
        }

        return string.Join(' ', classes);
    }

    private bool IsTarget(Square square) =>
        _selected is { } from && LegalMoves.Any(m => m.StartsWith($"{from}{square}", StringComparison.Ordinal));

    private async Task OnSquareClickAsync(Square square)
    {
        if (!Interactive || _pendingPromotion is not null)
        {
            return;
        }

        if (_selected is { } from && IsTarget(square))
        {
            var prefix = $"{from}{square}";
            var candidates = LegalMoves.Where(m => m.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            if (candidates.Count > 1)
            {
                _pendingPromotion = prefix;
                return;
            }

            _selected = null;
            await OnMove.InvokeAsync(candidates[0]);
            return;
        }

        var hasMoves = LegalMoves.Any(m => m.StartsWith(square.ToString(), StringComparison.Ordinal));
        _selected = hasMoves && _selected != square ? square : null;
    }

    private async Task PromoteAsync(char letter)
    {
        var uci = _pendingPromotion + letter;
        _pendingPromotion = null;
        _selected = null;
        await OnMove.InvokeAsync(uci);
    }

    private void CancelPromotion()
    {
        _pendingPromotion = null;
        _selected = null;
    }
}
