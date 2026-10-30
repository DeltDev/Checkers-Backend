using Checkers.Models;

namespace Checkers.Tests;

/// <summary>
/// The remaining GameController branches: a man-jump whose landing square is
/// occupied, observer isolation for the turn/ended events, the EndGame re-entry
/// guard, both outcomes of the two end-condition ternaries, and IsPromotion's
/// defensive fallback for an unrecognised colour.
/// </summary>
[TestFixture]
public class GameControllerCoverageTests
{
    // ---------------------------------------------------- move generation

    [Test]
    public void ManJump_WithOccupiedLanding_IsNotGenerated()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 6, 1, PieceColor.White, PieceType.Man, 300);
        TestHelpers.Place(game, 5, 2, PieceColor.Black, PieceType.Man, 400);
        TestHelpers.Place(game, 4, 3, PieceColor.White, PieceType.Man, 500); // blocks the landing square

        List<Move> moves = game.GetLegalMoves(white);

        // The blocked jump is filtered out; the quiet move survives.
        Assert.That(moves.Any(m => m.From == new Position(6, 1) && m.To == new Position(4, 3)), Is.False);
        Assert.That(moves.Any(m => m.From == new Position(6, 1) && m.To == new Position(5, 0)), Is.True);
    }

    // ---------------------------------------------------- observer isolation

    [Test]
    public void ThrowingTurnChangedObserver_DoesNotBreakTheMove()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        game.OnTurnChanged += _ => throw new InvalidOperationException("observer bug");

        Move move = TestHelpers.GetLegal(game, white, new Position(6, 1), new Position(5, 0));

        Assert.That(game.MakeMove(white, move), Is.True);
        Assert.That(game.TurnColor, Is.EqualTo(PieceColor.Black));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.History, Has.Count.EqualTo(1));
    }

    [Test]
    public void ThrowingGameEndedObserver_DoesNotUndoTheResult()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        game.OnGameEnded += _ => throw new InvalidOperationException("observer bug");

        game.Resign(white);

        Assert.That(game.Status, Is.EqualTo(GameStatus.BlackWon));
        Assert.That(game.GetWinner()!.Id, Is.EqualTo(2));
    }

    [Test]
    public void EndGame_ReenteredWhileAlreadyFinished_IsIgnored()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        int endedCount = 0;
        game.OnGameEnded += _ => endedCount++;
        // Ending the game from inside the move event leaves CheckEndConditions
        // evaluating a finished game; its EndGame call must be a no-op and the
        // ended event must fire exactly once.
        game.OnMoveMade += (_, _) => game.Resign(black);

        Move move = TestHelpers.GetLegal(game, white, new Position(6, 1), new Position(5, 0));

        Assert.That(game.MakeMove(white, move), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.WhiteWon));
        Assert.That(endedCount, Is.EqualTo(1));
    }

    // ------------------------------------------------ end-condition ternaries

    [Test]
    public void LastPieceCapturedByBlack_WhiteHasNoPiecesLeft_BlackWins()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 5, 0, PieceColor.White, PieceType.Man, 300);
        TestHelpers.Place(game, 3, 2, PieceColor.Black, PieceType.Man, 400);

        // White's only legal move is the quiet step that walks into the jump.
        Move whiteMove = TestHelpers.GetLegal(game, white, new Position(5, 0), new Position(4, 1));
        Assert.That(game.MakeMove(white, whiteMove), Is.True);

        Move blackJump = TestHelpers.GetLegal(game, black, new Position(3, 2), new Position(5, 0));
        Assert.That(game.MakeMove(black, blackJump), Is.True);

        // White has zero pieces: the piece-count arm of CheckEndConditions ends
        // the game with the mover (Black) as winner.
        Assert.That(game.Status, Is.EqualTo(GameStatus.BlackWon));
        Assert.That(game.GetWinner(), Is.SameAs(black));
    }

    [Test]
    public void WhiteLockedOnItsBackRank_BlackWins()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 0, 1, PieceColor.White, PieceType.Man, 300); // back rank: no forward square
        TestHelpers.Place(game, 5, 0, PieceColor.White, PieceType.Man, 310); // gets captured
        TestHelpers.Place(game, 3, 2, PieceColor.Black, PieceType.Man, 400);

        Move whiteMove = TestHelpers.GetLegal(game, white, new Position(5, 0), new Position(4, 1));
        Assert.That(game.MakeMove(white, whiteMove), Is.True);

        Move blackJump = TestHelpers.GetLegal(game, black, new Position(3, 2), new Position(5, 0));
        Assert.That(game.MakeMove(black, blackJump), Is.True);

        // White still has one man, so the piece-count arm skips; the legal-move
        // arm finds no move for the back-rank man and Black wins.
        Assert.That(game.GetSquare(new Position(0, 1))!.Occupant, Is.Not.Null);
        Assert.That(game.Status, Is.EqualTo(GameStatus.BlackWon));
        Assert.That(game.GetWinner(), Is.SameAs(black));
    }

    // ------------------------------------------------------- IsPromotion guard

    [Test]
    public void IsPromotion_WithAnUnrecognisedColour_IsFalse()
    {
        // Destination sits on a promotion row; only the unknown colour's
        // fall-through keeps it from counting.
        Move move = new(new Position(5, 2), new Position(0, 2), null, null,
            MoveType.Quiet, (PieceColor)99, PieceType.Man);

        Assert.That(GameController.IsPromotion(move), Is.False);
    }
}
