using Checkers.Models;

namespace Checkers.Tests;

/// <summary>
/// PHASE 10 — MakeMove (IMPLEMENTATION_PLAN Phase 4 + phase 4 pseudocode).
/// Decisions encoded:
///   D1 — a request is validated by matching it against the canonical legal move
///        (From, To, and Path when it is a capture); ALL client-supplied metadata
///        (Type/Color/PieceType/Captured) is replaced by the canonical move's values.
/// All tests are expected to FAIL until MakeMove is implemented (Phase 4).
/// </summary>
[TestFixture]
public class MakeMoveTests
{
    [Test]
    public void QuietMove_AppliesMove_AppendsCanonicalHistory_FlipsTurn_RaisesEventsInOrder()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        Move canonical = TestHelpers.GetLegal(game, white, new Position(6, 1), new Position(5, 0));
        int pieceId = game.GetSquare(new Position(6, 1))!.Occupant!.Id;

        var events = new List<string>();
        int moveEventCount = 0;
        IPlayer? moveEventPlayer = null;
        Move moveEventMove = default;
        int turnEventCount = 0;
        int? turnEventTurn = null;
        game.OnMoveMade += (player, move) =>
        {
            events.Add("move");
            moveEventCount++;
            moveEventPlayer = player;
            moveEventMove = move;
        };
        game.OnTurnChanged += turn =>
        {
            events.Add("turn");
            turnEventCount++;
            turnEventTurn = turn;
        };

        bool result = game.MakeMove(white, canonical);

        Assert.That(result, Is.True);

        // Board: piece relocated, nothing else disturbed.
        Assert.That(game.IsSquareEmpty(new Position(6, 1)), Is.True);
        IPiece moved = game.GetSquare(new Position(5, 0))!.Occupant!;
        Assert.That(moved.Id, Is.EqualTo(pieceId));
        Assert.That(moved.Color, Is.EqualTo(PieceColor.White));
        Assert.That(moved.Type, Is.EqualTo(PieceType.Man));
        Assert.That(game.IsSquareEmpty(new Position(5, 2)), Is.True); // nearby empty square untouched

        // History: exactly one entry, equal to the canonical move.
        Assert.That(game.History, Has.Count.EqualTo(1));
        Move recorded = game.History[0];
        Assert.That(recorded.From, Is.EqualTo(canonical.From));
        Assert.That(recorded.To, Is.EqualTo(canonical.To));
        Assert.That(recorded.Path, Is.Empty);
        Assert.That(recorded.Captured, Is.Empty);
        Assert.That(recorded.Type, Is.EqualTo(MoveType.Quiet));
        Assert.That(recorded.Color, Is.EqualTo(PieceColor.White));
        Assert.That(recorded.PieceType, Is.EqualTo(PieceType.Man));

        // Turn state.
        Assert.That(game.Turn, Is.EqualTo(2));
        Assert.That(game.TurnColor, Is.EqualTo(PieceColor.Black));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));

        // Events: once each, correct payloads, move-before-turn order.
        Assert.That(events, Is.EqualTo(new[] { "move", "turn" }));
        Assert.That(moveEventCount, Is.EqualTo(1));
        Assert.That(moveEventPlayer, Is.SameAs(white));
        Assert.That(moveEventMove.From, Is.EqualTo(canonical.From));
        Assert.That(moveEventMove.To, Is.EqualTo(canonical.To));
        Assert.That(turnEventCount, Is.EqualTo(1));
        Assert.That(turnEventTurn, Is.EqualTo(2));
        _ = black;
    }

    [Test]
    public void Events_FireAfterCommit_HandlersObserveNewTurnAndHistory()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 6, 1, PieceColor.White, PieceType.Man, 700);
        TestHelpers.Place(game, 3, 0, PieceColor.Black, PieceType.Man, 800);
        Move canonical = TestHelpers.GetLegal(game, white, new Position(6, 1), new Position(5, 0));

        int? turnSeenByMoveEvent = null;
        int? historySeenByMoveEvent = null;
        game.OnMoveMade += (_, _) =>
        {
            turnSeenByMoveEvent = game.Turn;
            historySeenByMoveEvent = game.History.Count;
        };
        int? turnSeenByTurnEvent = null;
        game.OnTurnChanged += turn => turnSeenByTurnEvent = turn;
        int gameEndedCount = 0;
        game.OnGameEnded += _ => gameEndedCount++;

        Assert.That(game.MakeMove(white, canonical), Is.True);

        // Handlers run after the state is committed, never before it.
        Assert.That(turnSeenByMoveEvent, Is.EqualTo(2));
        Assert.That(historySeenByMoveEvent, Is.EqualTo(1));
        Assert.That(turnSeenByTurnEvent, Is.EqualTo(2));
        Assert.That(gameEndedCount, Is.EqualTo(0)); // game continues: opponent has pieces and moves
    }

    [Test]
    public void MakeMove_OnWrongTurn_IsRejected_StateUnchanged()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        Move first = TestHelpers.GetLegal(game, white, new Position(6, 1), new Position(5, 0));
        Assert.That(game.MakeMove(white, first), Is.True);
        var before = TestHelpers.Snapshot(game);

        // White tries to move again although it is Black's turn.
        Move again = new(new Position(5, 0), new Position(4, 1), null, null, MoveType.Quiet, PieceColor.White, PieceType.Man);
        bool result = game.MakeMove(white, again);

        Assert.That(result, Is.False);
        Assert.That(TestHelpers.Snapshot(game), Is.EqualTo(before));
        Assert.That(game.Turn, Is.EqualTo(2));
        Assert.That(game.TurnColor, Is.EqualTo(PieceColor.Black));
        Assert.That(game.History, Has.Count.EqualTo(1));
    }

    [Test]
    public void MakeMove_ByPlayerNotInThisGame_IsRejected()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        Player outsider = new(99, "Eve", PieceColor.White);
        Move canonical = TestHelpers.GetLegal(game, white, new Position(6, 1), new Position(5, 0));
        var before = TestHelpers.Snapshot(game);
        int moveEvents = 0;
        game.OnMoveMade += (_, _) => moveEvents++;

        bool result = game.MakeMove(outsider, canonical);

        Assert.That(result, Is.False);
        Assert.That(TestHelpers.Snapshot(game), Is.EqualTo(before));
        Assert.That(game.Turn, Is.EqualTo(1));
        Assert.That(game.History, Is.Empty);
        Assert.That(moveEvents, Is.EqualTo(0));
    }

    [Test]
    public void MakeMove_NullPlayer_IsRejected()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        Move canonical = TestHelpers.GetLegal(game, white, new Position(6, 1), new Position(5, 0));
        var before = TestHelpers.Snapshot(game);

        bool result = game.MakeMove(null!, canonical);

        Assert.That(result, Is.False);
        Assert.That(TestHelpers.Snapshot(game), Is.EqualTo(before));
        Assert.That(game.History, Is.Empty);
    }

    [Test]
    public void MakeMove_IllegalFromTo_IsRejected()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        var before = TestHelpers.Snapshot(game);

        // (6,1) cannot reach (3,2) in one move.
        Move fabricated = new(new Position(6, 1), new Position(3, 2), null, null, MoveType.Quiet, PieceColor.White, PieceType.Man);
        bool result = game.MakeMove(white, fabricated);

        Assert.That(result, Is.False);
        Assert.That(TestHelpers.Snapshot(game), Is.EqualTo(before));
        Assert.That(game.History, Is.Empty);
        Assert.That(game.Turn, Is.EqualTo(1));
    }

    [Test]
    public void MakeMove_FabricatedQuietPathDetour_IsRejected()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        var before = TestHelpers.Snapshot(game);

        // Real From/To of a legal quiet move, but the client smuggles a "path".
        // D1: quiet moves carry an empty canonical path, so this must NOT match.
        Move fabricated = new(
            new Position(6, 1), new Position(5, 0),
            new[] { new Position(4, 1) }, null,
            MoveType.Quiet, PieceColor.White, PieceType.Man);
        bool result = game.MakeMove(white, fabricated);

        Assert.That(result, Is.False);
        Assert.That(TestHelpers.Snapshot(game), Is.EqualTo(before));
        Assert.That(game.History, Is.Empty);
    }

    [Test]
    public void MakeMove_Capture_RemovesExactlyTheJumpedPieces()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 6, 3, PieceColor.White, PieceType.Man, 300);
        TestHelpers.Place(game, 5, 2, PieceColor.Black, PieceType.Man, 400);
        TestHelpers.Place(game, 3, 2, PieceColor.Black, PieceType.Man, 401);
        TestHelpers.Place(game, 5, 4, PieceColor.Black, PieceType.Man, 402);

        Move canonical = TestHelpers.GetLegal(game, white, new Position(6, 3), new Position(2, 3));
        Assert.That(game.MakeMove(white, canonical), Is.True);

        // Mover relocated. ((6,3) jumps (5,2) -> (4,1), then (3,2) -> (2,3).)
        IPiece moved = game.GetSquare(new Position(2, 3))!.Occupant!;
        Assert.That(moved.Id, Is.EqualTo(300));
        Assert.That(game.IsSquareEmpty(new Position(6, 3)), Is.True);
        Assert.That(game.IsSquareEmpty(new Position(4, 1)), Is.True); // intermediate landing is empty after the move

        // Exactly the two jumped pieces are gone.
        Assert.That(game.IsSquareEmpty(new Position(5, 2)), Is.True);
        Assert.That(game.IsSquareEmpty(new Position(3, 2)), Is.True);
        IPiece survivor = game.GetSquare(new Position(5, 4))!.Occupant!;
        Assert.That(survivor.Id, Is.EqualTo(402)); // the piece outside the sequence is untouched

        // History records the canonical capture.
        Assert.That(game.History, Has.Count.EqualTo(1));
        Move recorded = game.History[0];
        Assert.That(recorded.Type, Is.EqualTo(MoveType.Capture));
        Assert.That(recorded.Captured.Select(p => p.Id).ToArray(), Is.EqualTo(new[] { 400, 401 }));
        Assert.That(recorded.Path, Is.EqualTo(new[] { new Position(4, 1) }));
        Assert.That(game.Turn, Is.EqualTo(2));
        _ = black;
    }

    [Test]
    public void MakeMove_Promotion_CrownsInPlace_KeepsPieceId()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 2, 1, PieceColor.White, PieceType.Man, 500);
        TestHelpers.Place(game, 1, 2, PieceColor.Black, PieceType.Man, 600);

        Move canonical = TestHelpers.GetLegal(game, white, new Position(2, 1), new Position(0, 3));
        Assert.That(game.MakeMove(white, canonical), Is.True);

        // Same piece object identity by id, crowned in place on the landing square.
        IPiece crowned = game.GetSquare(new Position(0, 3))!.Occupant!;
        Assert.That(crowned.Id, Is.EqualTo(500));
        Assert.That(crowned.Color, Is.EqualTo(PieceColor.White));
        Assert.That(crowned.Type, Is.EqualTo(PieceType.King));

        Assert.That(game.IsSquareEmpty(new Position(2, 1)), Is.True);
        Assert.That(game.IsSquareEmpty(new Position(1, 2)), Is.True); // captured piece removed

        Assert.That(game.History, Has.Count.EqualTo(1));

        Move recorded = game.History[0];
        Assert.That(recorded.Type, Is.EqualTo(MoveType.Capture));
    }

    [Test]
    public void MakeMove_FabricatedMetadata_ReplacedByCanonical()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 6, 1, PieceColor.White, PieceType.Man, 700);
        TestHelpers.Place(game, 5, 4, PieceColor.Black, PieceType.Man, 800);

        Move canonical = TestHelpers.GetLegal(game, white, new Position(6, 1), new Position(5, 0));
        Assert.That(canonical.Type, Is.EqualTo(MoveType.Quiet));

        // Client lies about everything except From/To - worked example B:
        // claims a capture with a victim, black's colour, king type.
        // Path must stay empty: §2 only reads Path when non-empty, and a claimed
        // route on a quiet move is covered by MakeMove_FabricatedQuietPathDetour.
        Move fabricated = new(
            canonical.From, canonical.To,
            null,
            new IPiece[] { new Piece(999, PieceColor.Black, PieceType.Man) },
            MoveType.Capture, PieceColor.Black, PieceType.King);

        bool result = game.MakeMove(white, fabricated);

        Assert.That(result, Is.True); // From/To match the canonical quiet move
        Assert.That(game.History, Has.Count.EqualTo(1));
        Move recorded = game.History[0];
        Assert.That(recorded.Type, Is.EqualTo(MoveType.Quiet));      // D1: canonical type wins
        Assert.That(recorded.Color, Is.EqualTo(PieceColor.White));   // canonical color wins
        Assert.That(recorded.PieceType, Is.EqualTo(PieceType.Man));  // canonical piece type wins
        Assert.That(recorded.Path, Is.Empty);                      // fabricated path dropped
        Assert.That(recorded.Captured, Is.Empty);                  // fabricated victim dropped

        // Board reflects the canonical quiet move only.
        IPiece moved = game.GetSquare(new Position(5, 0))!.Occupant!;
        Assert.That(moved.Id, Is.EqualTo(700));
        Assert.That(moved.Type, Is.EqualTo(PieceType.Man));
        IPiece untouched = game.GetSquare(new Position(5, 4))!.Occupant!;
        Assert.That(untouched.Id, Is.EqualTo(800));
    }

    [Test]
    public void MakeMove_ThrowingEventSubscriber_DoesNotFailTheMove()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 6, 1, PieceColor.White, PieceType.Man, 710);
        TestHelpers.Place(game, 3, 0, PieceColor.Black, PieceType.Man, 810);
        Move canonical = TestHelpers.GetLegal(game, white, new Position(6, 1), new Position(5, 0));

        int healthyCalls = 0;
        game.OnMoveMade += (_, _) => throw new InvalidOperationException("subscriber bug");
        game.OnMoveMade += (_, _) => healthyCalls++;

        bool result = game.MakeMove(white, canonical);

        Assert.That(result, Is.True); // a broken subscriber must not fail the move
        Assert.That(healthyCalls, Is.EqualTo(1)); // later subscribers still run
        Assert.That(game.Turn, Is.EqualTo(2));
        Assert.That(game.History, Has.Count.EqualTo(1));
    }
}
