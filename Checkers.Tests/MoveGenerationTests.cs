using System.Reflection;
using System.Text;
using Checkers.Models;

namespace Checkers.Tests;

/// <summary>
/// Phase 3 - legal move generation (GameController.GetLegalMoves and its helpers).
/// Covers the plan's "Done when" list: opening moves, forced capture, maximum
/// capture, multi-jump, flying king, blocked king, promotion, no-moves loss and
/// edge squares, plus the entry-point gates.
/// </summary>
[TestFixture]
public class MoveGenerationTests
{
    // ---- helpers -----------------------------------------------------------

    private static (GameController game, IPlayer white, IPlayer black) CreateGame()
    {
        IPlayer white = new Player(1, "White", PieceColor.White);
        IPlayer black = new Player(2, "Black", PieceColor.Black);
        return (new GameController(white, black), white, black);
    }

    private static void ClearBoard(GameController game)
    {
        foreach (Position p in Board.AllPositions())
        {
            game.RemovePiece(p);
        }
    }

    private static void Place(GameController game, int row, int col, PieceColor color, PieceType type, int id)
    {
        game.PlacePiece(new Position(row, col), new Piece(id, color, type));
    }

    /// <summary>
    /// TurnColor and Status only advance through MakeMove/Resign (Phases 4-5),
    /// so Phase 3 tests flip them directly to reach black-side generation and
    /// the game-over gate.
    /// </summary>
    private static void SetPrivate<T>(GameController game, string propertyName, T value)
    {
        PropertyInfo property = typeof(GameController).GetProperty(propertyName)!;
        MethodInfo setter = property.GetSetMethod(nonPublic: true)!;
        setter.Invoke(game, new object[] { value! });
    }

    private static string Snapshot(GameController game)
    {
        StringBuilder board = new();
        foreach (Position p in Board.AllPositions())
        {
            IPiece? piece = game.GetSquare(p)!.Occupant;
            board.Append(p).Append(':')
                 .Append(piece is null ? "-" : $"{piece.Id}/{piece.Color}/{piece.Type}")
                 .Append(';');
        }
        return board.ToString();
    }

    // ---- entry-point gates -------------------------------------------------

    [Test]
    public void Opening_WhiteHasNineQuietMoves()
    {
        var (game, white, _) = CreateGame();

        List<Move> moves = game.GetLegalMoves(white);

        Assert.That(moves.Count, Is.EqualTo(9));
        Assert.That(moves, Is.All.Property("Type").EqualTo(MoveType.Quiet));
        Assert.That(moves, Is.All.Property("From").Property("Row").EqualTo(6));
        Assert.That(moves, Is.All.Property("To").Property("Row").EqualTo(5));
        Assert.That(moves, Is.All.Property("Path").Empty);
        Assert.That(moves, Is.All.Property("Captured").Empty);
    }

    [Test]
    public void Opening_BlackHasNineQuietMoves()
    {
        var (game, _, black) = CreateGame();
        SetPrivate(game, nameof(GameController.TurnColor), PieceColor.Black);

        List<Move> moves = game.GetLegalMoves(black);

        Assert.That(moves.Count, Is.EqualTo(9));
        Assert.That(moves, Is.All.Property("Type").EqualTo(MoveType.Quiet));
        Assert.That(moves, Is.All.Property("From").Property("Row").EqualTo(3));
        Assert.That(moves, Is.All.Property("To").Property("Row").EqualTo(4));
    }

    [Test]
    public void GetLegalMoves_ReturnsEmptyWhenNotThatPlayersTurn()
    {
        var (game, _, black) = CreateGame();

        Assert.That(game.GetLegalMoves(black), Is.Empty);
    }

    [Test]
    public void GetLegalMoves_ReturnsEmptyForNonMember()
    {
        var (game, _, _) = CreateGame();
        IPlayer stranger = new Player(99, "Intruder", PieceColor.White);

        Assert.That(game.GetLegalMoves(stranger), Is.Empty);
    }

    [Test]
    public void GetLegalMoves_ReturnsEmptyForNullPlayer()
    {
        var (game, _, _) = CreateGame();

        Assert.That(game.GetLegalMoves(null!), Is.Empty);
    }

    [Test]
    public void GetLegalMoves_ReturnsEmptyWhenTheGameIsOver()
    {
        var (game, white, _) = CreateGame();
        SetPrivate(game, nameof(GameController.Status), GameStatus.WhiteWon);

        Assert.That(game.GetLegalMoves(white), Is.Empty);
    }

    [Test]
    public void GetLegalMoves_ReturnsEmptyWhenThePlayerHasNoPieces()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);

        Assert.That(game.GetLegalMoves(white), Is.Empty);
    }

    [Test]
    public void GetLegalMoves_DoesNotMutateTheBoard()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 6, 3, PieceColor.White, PieceType.Man, 100);
        Place(game, 5, 2, PieceColor.Black, PieceType.Man, 200);
        Place(game, 3, 2, PieceColor.Black, PieceType.Man, 201);
        Place(game, 5, 4, PieceColor.Black, PieceType.Man, 202);

        string before = Snapshot(game);
        _ = game.GetLegalMoves(white);
        string after = Snapshot(game);

        Assert.That(after, Is.EqualTo(before));
    }

    // ---- movement direction ------------------------------------------------

    [Test]
    public void WhiteMan_QuietMovesGoForwardOnly()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 5, 2, PieceColor.White, PieceType.Man, 100);

        List<Move> moves = game.GetLegalMoves(white);

        Assert.That(moves.Count, Is.EqualTo(2));
        Assert.That(moves.Any(m => m.To == new Position(4, 1)), Is.True);
        Assert.That(moves.Any(m => m.To == new Position(4, 3)), Is.True);
        Assert.That(moves, Is.All.Property("To").Property("Row").EqualTo(4));
    }

    [Test]
    public void BlackMan_QuietMovesGoForwardOnly()
    {
        var (game, _, black) = CreateGame();
        ClearBoard(game);
        Place(game, 4, 3, PieceColor.Black, PieceType.Man, 200);
        SetPrivate(game, nameof(GameController.TurnColor), PieceColor.Black);

        List<Move> moves = game.GetLegalMoves(black);

        Assert.That(moves.Count, Is.EqualTo(2));
        Assert.That(moves.Any(m => m.To == new Position(5, 2)), Is.True);
        Assert.That(moves.Any(m => m.To == new Position(5, 4)), Is.True);
        Assert.That(moves, Is.All.Property("To").Property("Row").EqualTo(5));
    }

    // ---- flying king, quiet moves ------------------------------------------

    [Test]
    public void FlyingKing_QuietMovesSlideAnyDistance()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 5, 4, PieceColor.White, PieceType.King, 100);

        List<Move> moves = game.GetLegalMoves(white);

        // up-left 4, up-right 5, down-left 4, down-right 4.
        Assert.That(moves.Count, Is.EqualTo(17));
        Assert.That(moves.Select(m => m.To).Distinct().Count(), Is.EqualTo(17));
        Assert.That(moves, Is.All.Property("Type").EqualTo(MoveType.Quiet));
        Assert.That(moves, Is.All.Property("From").EqualTo(new Position(5, 4)));
        Assert.That(moves.Any(m => m.To == new Position(1, 0)), Is.True);   // up-left corner
        Assert.That(moves.Any(m => m.To == new Position(0, 9)), Is.True);   // up-right corner
        Assert.That(moves.Any(m => m.To == new Position(9, 0)), Is.True);   // down-left corner
        Assert.That(moves.Any(m => m.To == new Position(9, 8)), Is.True);   // down-right corner
    }

    [Test]
    public void King_QuietSlide_StopsAtOwnPiece()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 5, 4, PieceColor.White, PieceType.King, 100);
        Place(game, 4, 3, PieceColor.White, PieceType.Man, 101);

        List<Move> moves = game.GetLegalMoves(white);

        // No enemies on the board, so only quiet moves. The king's up-left
        // diagonal is closed entirely by the friendly man on (4,3); the man
        // itself still has its two forward quiet moves.
        List<Move> kingMoves = moves.Where(m => m.From == new Position(5, 4)).ToList();
        Assert.That(kingMoves.Count, Is.EqualTo(13)); // 0 up-left + 5 up-right + 4 + 4
        Assert.That(kingMoves, Is.All.Property("Type").EqualTo(MoveType.Quiet));
        foreach (Position blocked in new[] { new Position(4, 3), new Position(3, 2), new Position(2, 1), new Position(1, 0) })
        {
            Assert.That(kingMoves.Any(m => m.To == blocked), Is.False);
        }

        Assert.That(moves.Count(m => m.From == new Position(4, 3)), Is.EqualTo(2));
    }

    // ---- blocked king ------------------------------------------------------

    [Test]
    public void King_CannotJumpOverItsOwnPiece()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 5, 4, PieceColor.White, PieceType.King, 100);
        Place(game, 4, 3, PieceColor.White, PieceType.Man, 101);
        Place(game, 3, 2, PieceColor.Black, PieceType.Man, 200);

        List<Move> moves = game.GetLegalMoves(white);

        Assert.That(moves, Has.Count.EqualTo(1));

        Move move = moves[0];
        Assert.That(move.From, Is.EqualTo(new Position(4, 3)));
        Assert.That(move.To, Is.EqualTo(new Position(2, 1)));
        Assert.That(move.Type, Is.EqualTo(MoveType.Capture));
    }

    [Test]
    public void King_CannotCaptureWhenTheLandingSquaresAreBlocked()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 5, 4, PieceColor.White, PieceType.King, 100);
        Place(game, 3, 2, PieceColor.Black, PieceType.Man, 200);
        Place(game, 2, 1, PieceColor.Black, PieceType.Man, 201); // blocks the only landing

        List<Move> moves = game.GetLegalMoves(white);

        // The jump over (3,2) needs an empty square beyond it; there is none,
        // so no capture exists and the king falls back to quiet moves.
        Assert.That(moves.Count, Is.EqualTo(14));
        Assert.That(moves, Is.All.Property("Type").EqualTo(MoveType.Quiet));
        Assert.That(moves.Any(m => m.To == new Position(4, 3)), Is.True);
        Assert.That(moves.Any(m => m.To == new Position(2, 1)), Is.False);
        Assert.That(moves.Any(m => m.To == new Position(1, 0)), Is.False);
    }

    // ---- edge squares ------------------------------------------------------

    [Test]
    public void EdgeSquare_ManOnTheRightEdge_OnlyOneQuietMove()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 6, 9, PieceColor.White, PieceType.Man, 100);

        List<Move> moves = game.GetLegalMoves(white);

        Assert.That(moves, Has.Count.EqualTo(1));

        Move move = moves[0];
        Assert.That(move.To, Is.EqualTo(new Position(5, 8)));
    }

    [Test]
    public void EdgeSquare_CaptureIsNotGeneratedWhenTheLandingWouldBeOffTheBoard()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 1, 2, PieceColor.White, PieceType.Man, 100);
        Place(game, 0, 1, PieceColor.Black, PieceType.Man, 200); // landing would be (-1,0)

        List<Move> moves = game.GetLegalMoves(white);

        // The jump over (0,1) would land on (-1,0), so it is not generated;
        // the man's only remaining quiet move is forward to (0,3).
        Assert.That(moves, Has.Count.EqualTo(1));
        Move move = moves[0];
        Assert.That(move.Type, Is.EqualTo(MoveType.Quiet));
        Assert.That(move.To, Is.EqualTo(new Position(0, 3)));
        Assert.That(moves.Any(m => m.Type == MoveType.Capture), Is.False);
    }

    // ---- mandatory and maximum capture -------------------------------------

    [Test]
    public void MandatoryCapture_QuietMovesAreSuppressed()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 4, 3, PieceColor.White, PieceType.Man, 100);
        Place(game, 5, 4, PieceColor.Black, PieceType.Man, 200);

        List<Move> moves = game.GetLegalMoves(white);

        Assert.That(moves, Has.Count.EqualTo(1));

        Move move = moves[0];
        Assert.That(move.Type, Is.EqualTo(MoveType.Capture));
        Assert.That(move.From, Is.EqualTo(new Position(4, 3)));
        Assert.That(move.To, Is.EqualTo(new Position(6, 5)));
        // the quiet moves to (3,2) and (3,4) must not be offered
        Assert.That(moves.Any(m => m.To == new Position(3, 2)), Is.False);
        Assert.That(moves.Any(m => m.To == new Position(3, 4)), Is.False);
    }

    [Test]
    public void MandatoryCapture_OnlyLongestSequenceIsReturned()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 6, 3, PieceColor.White, PieceType.Man, 100);
        Place(game, 5, 2, PieceColor.Black, PieceType.Man, 200);
        Place(game, 3, 2, PieceColor.Black, PieceType.Man, 201);
        Place(game, 5, 4, PieceColor.Black, PieceType.Man, 202);

        List<Move> moves = game.GetLegalMoves(white);

        Assert.That(moves, Has.Count.EqualTo(1));

        Move move = moves[0];
        Assert.That(move.Type, Is.EqualTo(MoveType.Capture));
        Assert.That(move.From, Is.EqualTo(new Position(6, 3)));
        Assert.That(move.To, Is.EqualTo(new Position(2, 3)));
        Assert.That(move.Captured.Length, Is.EqualTo(2));
        Assert.That(move.Captured, Is.All.Property("Color").EqualTo(PieceColor.Black));
        Assert.That(move.Path, Has.Length.EqualTo(1));
        Position landing = move.Path[0];
        Assert.That(landing, Is.EqualTo(new Position(4, 1)));
        Assert.That(move.PieceType, Is.EqualTo(PieceType.Man));
    }

    [Test]
    public void MandatoryCapture_OnlyThePieceWithTheLongestSequenceMayMove()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        // one capture available...
        Place(game, 8, 3, PieceColor.White, PieceType.Man, 100);
        Place(game, 7, 4, PieceColor.Black, PieceType.Man, 200);
        // ...and a longer one elsewhere
        Place(game, 6, 1, PieceColor.White, PieceType.Man, 101);
        Place(game, 5, 2, PieceColor.Black, PieceType.Man, 201);
        Place(game, 3, 4, PieceColor.Black, PieceType.Man, 202);

        List<Move> moves = game.GetLegalMoves(white);

        Assert.That(moves, Has.Count.EqualTo(1));

        Move move = moves[0];
        Assert.That(move.From, Is.EqualTo(new Position(6, 1)));
        Assert.That(move.To, Is.EqualTo(new Position(2, 5)));
        Assert.That(move.Captured.Length, Is.EqualTo(2));
    }

    [Test]
    public void TiedMaximumSequences_AreBothReturned()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 6, 5, PieceColor.White, PieceType.Man, 100);
        Place(game, 5, 4, PieceColor.Black, PieceType.Man, 200);
        Place(game, 3, 2, PieceColor.Black, PieceType.Man, 201);
        Place(game, 5, 6, PieceColor.Black, PieceType.Man, 202);
        Place(game, 3, 8, PieceColor.Black, PieceType.Man, 203);

        List<Move> moves = game.GetLegalMoves(white);

        Assert.That(moves.Count, Is.EqualTo(2));
        foreach (Move move in moves)
        {
            Assert.That(move.From, Is.EqualTo(new Position(6, 5)));
            Assert.That(move.Captured, Has.Length.EqualTo(2));
            Assert.That(move.Path, Has.Length.EqualTo(1));
        }
        Assert.That(moves.Any(m => m.To == new Position(2, 1)), Is.True);
        Assert.That(moves.Any(m => m.To == new Position(2, 9)), Is.True);
    }

    [Test]
    public void Man_CapturesBackward()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 4, 3, PieceColor.White, PieceType.Man, 100);
        Place(game, 5, 4, PieceColor.Black, PieceType.Man, 200);

        List<Move> moves = game.GetLegalMoves(white);

        Assert.That(moves, Has.Count.EqualTo(1));

        Move move = moves[0];
        Assert.That(move.Type, Is.EqualTo(MoveType.Capture));
        Assert.That(move.From, Is.EqualTo(new Position(4, 3)));
        Assert.That(move.To, Is.EqualTo(new Position(6, 5)));
        Assert.That(move.Captured, Has.Length.EqualTo(1));
    }

    // ---- multi-jump --------------------------------------------------------

    [Test]
    public void MultiJump_ThreeJumpSequenceIsFullyEnumerated()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 6, 1, PieceColor.White, PieceType.Man, 100);
        Place(game, 5, 2, PieceColor.Black, PieceType.Man, 200);
        Place(game, 3, 4, PieceColor.Black, PieceType.Man, 201);
        Place(game, 1, 6, PieceColor.Black, PieceType.Man, 202);

        List<Move> moves = game.GetLegalMoves(white);

        Assert.That(moves, Has.Count.EqualTo(1));

        Move move = moves[0];
        Assert.That(move.Type, Is.EqualTo(MoveType.Capture));
        Assert.That(move.From, Is.EqualTo(new Position(6, 1)));
        Assert.That(move.To, Is.EqualTo(new Position(0, 7)));
        Assert.That(move.Path.Length, Is.EqualTo(2));
        Assert.That(move.Path[0], Is.EqualTo(new Position(4, 3)));
        Assert.That(move.Path[1], Is.EqualTo(new Position(2, 5)));
        // Captured pieces are ordered by capture.
        Assert.That(move.Captured.Select(p => p.Id), Is.EqualTo(new[] { 200, 201, 202 }));
        Assert.That(move.Captured, Is.All.Property("Color").EqualTo(PieceColor.Black));
    }

    [Test]
    public void CapturedPiece_CannotBeJumpedTwice()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 6, 3, PieceColor.White, PieceType.Man, 100);
        Place(game, 5, 2, PieceColor.Black, PieceType.Man, 200);

        List<Move> moves = game.GetLegalMoves(white);

        // Without the already-captured blocker the piece could jump back over
        // (5,2) onto its now-empty start square and "capture" it a second time.
        Assert.That(moves, Has.Count.EqualTo(1));
        Move move = moves[0];
        Assert.That(move.To, Is.EqualTo(new Position(4, 1)));
        Assert.That(move.Captured, Has.Length.EqualTo(1));
        Assert.That(moves.Any(m => m.To == new Position(6, 3)), Is.False);
    }

    // ---- flying king, captures ---------------------------------------------

    [Test]
    public void FlyingKing_EveryEmptySquareBeyondTheVictimIsALanding()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 5, 4, PieceColor.White, PieceType.King, 100);
        Place(game, 3, 2, PieceColor.Black, PieceType.Man, 200);

        List<Move> moves = game.GetLegalMoves(white);

        Assert.That(moves.Count, Is.EqualTo(2));
        foreach (Move move in moves)
        {
            Assert.That(move.From, Is.EqualTo(new Position(5, 4)));
            Assert.That(move.Captured, Has.Length.EqualTo(1));
            Assert.That(move.PieceType, Is.EqualTo(PieceType.King));
        }
        Assert.That(moves.Any(m => m.To == new Position(2, 1)), Is.True);
        Assert.That(moves.Any(m => m.To == new Position(1, 0)), Is.True);
    }

    [Test]
    public void FlyingKing_MayChangeDirectionMidSequence()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 5, 4, PieceColor.White, PieceType.King, 100);
        Place(game, 3, 2, PieceColor.Black, PieceType.Man, 200);
        Place(game, 1, 2, PieceColor.Black, PieceType.Man, 201);

        List<Move> moves = game.GetLegalMoves(white);

        // (5,4) jumps (3,2) and lands on (2,1), then turns up-right to jump
        // (1,2) and land on (0,3). The one-jump alternative via (1,0) is
        // shorter and must be filtered out by the maximum-capture rule.
        Assert.That(moves, Has.Count.EqualTo(1));
        Move move = moves[0];
        Assert.That(move.From, Is.EqualTo(new Position(5, 4)));
        Assert.That(move.To, Is.EqualTo(new Position(0, 3)));
        Assert.That(move.Path, Has.Length.EqualTo(1));
        Position landing = move.Path[0];
        Assert.That(landing, Is.EqualTo(new Position(2, 1)));
        Assert.That(move.Captured.Length, Is.EqualTo(2));
    }

    // ---- promotion ---------------------------------------------------------

    [Test]
    public void QuietMove_ontoLastRow_ReportsPromotion()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 1, 2, PieceColor.White, PieceType.Man, 100);

        List<Move> moves = game.GetLegalMoves(white);

        Assert.That(moves.Count, Is.EqualTo(2));
        foreach (Move move in moves)
        {
            Assert.That(move.Type, Is.EqualTo(MoveType.Quiet));
            Assert.That(move.PieceType, Is.EqualTo(PieceType.Man));
            Assert.That(move.To.Row, Is.EqualTo(0));
            Assert.That(GameController.IsPromotion(move), Is.True);
        }
    }

    [Test]
    public void BlackMan_QuietMoveOntoLastRow_ReportsPromotion()
    {
        var (game, _, black) = CreateGame();
        ClearBoard(game);
        Place(game, 8, 1, PieceColor.Black, PieceType.Man, 200);
        SetPrivate(game, nameof(GameController.TurnColor), PieceColor.Black);

        List<Move> moves = game.GetLegalMoves(black);

        Assert.That(moves.Count, Is.EqualTo(2));
        foreach (Move move in moves)
        {
            Assert.That(move.To.Row, Is.EqualTo(Board.Size - 1));
            Assert.That(move.PieceType, Is.EqualTo(PieceType.Man));
            Assert.That(GameController.IsPromotion(move), Is.True);
        }
    }

    [Test]
    public void Man_LandingOnLastRow_ReportsPromotion()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 2, 1, PieceColor.White, PieceType.Man, 100);
        Place(game, 1, 2, PieceColor.Black, PieceType.Man, 200);

        List<Move> moves = game.GetLegalMoves(white);

        Assert.That(moves, Has.Count.EqualTo(1));

        Move move = moves[0];
        Assert.That(move.To, Is.EqualTo(new Position(0, 3)));
        Assert.That(move.Captured, Has.Length.EqualTo(1));
        Assert.That(move.PieceType, Is.EqualTo(PieceType.Man));
        Assert.That(GameController.IsPromotion(move), Is.True);
    }

    [Test]
    public void Man_PassingOverLastRow_DoesNotPromoteMidSequence()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 2, 1, PieceColor.White, PieceType.Man, 100);
        Place(game, 1, 2, PieceColor.Black, PieceType.Man, 200);
        Place(game, 1, 4, PieceColor.Black, PieceType.Man, 201);

        List<Move> moves = game.GetLegalMoves(white);

        Assert.That(moves, Has.Count.EqualTo(1));

        Move move = moves[0];
        Assert.That(move.To, Is.EqualTo(new Position(2, 5)));
        Assert.That(move.Captured.Length, Is.EqualTo(2));
        Assert.That(move.Path, Has.Length.EqualTo(1));
        Position landing = move.Path[0];
        Assert.That(landing, Is.EqualTo(new Position(0, 3)));
        Assert.That(move.PieceType, Is.EqualTo(PieceType.Man));
        Assert.That(GameController.IsPromotion(move), Is.False);
    }

    [Test]
    public void KingEndingOnLastRow_IsNotPromoted()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 2, 3, PieceColor.White, PieceType.King, 100);

        List<Move> moves = game.GetLegalMoves(white);

        Assert.That(moves, Is.All.Property("PieceType").EqualTo(PieceType.King));
        Assert.That(moves.Any(m => m.To == new Position(0, 1) && !GameController.IsPromotion(m)), Is.True);
    }

    // ---- no legal moves ----------------------------------------------------

    [Test]
    public void NoMoves_ReturnsEmptyList()
    {
        var (game, white, _) = CreateGame();
        ClearBoard(game);
        Place(game, 0, 1, PieceColor.White, PieceType.Man, 100);

        Assert.That(game.GetLegalMoves(white), Is.Empty);
    }
}
