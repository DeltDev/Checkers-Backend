using Checkers.Models;

namespace Checkers.Tests;

/// <summary>
/// PHASE 10 — End of game (IMPLEMENTATION_PLAN Phase 5 + phase 5 pseudocode).
/// Decisions encoded:
///   D1 — the side to move with no pieces OR no legal moves loses; the mover wins.
///   D2 — threefold repetition counts the initial position (Reset records it);
///        count >= 3 is a draw; key = board layout + side to move.
///   D3 — king-only threshold = 2 * MaxKingOnlyStreak (50 plies = FMJD 25 moves each);
///        any capture or any move by a man resets the streak to 0.
///   D5 — mutual draw offer ends the game immediately; a single offer expires on
///        the next accepted move by either side; offers only count while Ongoing.
///   D6 — a finished game rejects everything: Resign = no-op, OfferDraw = false,
///        MakeMove = false, GetLegalMoves = empty.
///   GetWinner — branches select their OWN colour; null for Ongoing and Draw.
/// All tests are expected to FAIL until Phase 5 (and Phase 4's MakeMove) are implemented.
/// </summary>
[TestFixture]
public class EndOfGameTests
{
    // ---------------------------------------------------------------- Resign

    [Test]
    public void Resign_ByWhite_SetsBlackWon_FiresGameEndedOnce()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        var ended = new List<GameStatus>();
        game.OnGameEnded += s => ended.Add(s);

        game.Resign(white);

        Assert.That(game.Status, Is.EqualTo(GameStatus.BlackWon));
        Assert.That(game.GetWinner(), Is.SameAs(black));
        Assert.That(ended, Is.EqualTo(new[] { GameStatus.BlackWon }));
    }

    [Test]
    public void Resign_ByBlack_SetsWhiteWon()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        var ended = new List<GameStatus>();
        game.OnGameEnded += s => ended.Add(s);

        game.Resign(black);

        Assert.That(game.Status, Is.EqualTo(GameStatus.WhiteWon));
        Assert.That(game.GetWinner(), Is.SameAs(white));
        Assert.That(ended, Is.EqualTo(new[] { GameStatus.WhiteWon }));
    }

    [Test]
    public void Resign_ByOutsider_IsIgnored()
    {
        var (game, _, _) = TestHelpers.CreateGame();
        Player outsider = new(99, "Eve", PieceColor.White);
        var ended = new List<GameStatus>();
        game.OnGameEnded += s => ended.Add(s);

        game.Resign(outsider);
        game.Resign(null!);

        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(ended, Is.Empty);
        Assert.That(game.GetWinner(), Is.Null);
    }

    [Test]
    public void Resign_AfterFinished_IsNoOp_EventNotRaisedAgain()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        int endedCount = 0;
        game.OnGameEnded += _ => endedCount++;

        game.Resign(white); // -> BlackWon, first event
        game.Resign(black); // must not flip the result
        game.Resign(white); // must not re-raise

        Assert.That(game.Status, Is.EqualTo(GameStatus.BlackWon));
        Assert.That(endedCount, Is.EqualTo(1));
        Assert.That(game.GetWinner(), Is.SameAs(black));
    }

    // ----------------------------------------------------------- Exhaustion

    [Test]
    public void Exhaustion_LastPieceCaptured_SideToMoveLoses_MoverWins()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 6, 3, PieceColor.White, PieceType.Man, 300);
        TestHelpers.Place(game, 5, 2, PieceColor.Black, PieceType.Man, 400);

        var events = new List<string>();
        var endedPayloads = new List<GameStatus>();
        GameStatus? statusSeenInMoveEvent = null;
        PieceColor? turnColorSeenInEndEvent = null;
        game.OnMoveMade += (_, _) =>
        {
            events.Add("move");
            statusSeenInMoveEvent = game.Status;
        };
        game.OnTurnChanged += _ =>
        {
            events.Add("turn");
        };
        game.OnGameEnded += s =>
        {
            events.Add("end");
            endedPayloads.Add(s);
            turnColorSeenInEndEvent = game.TurnColor;
        };

        // (6,3) jumps the man at (5,2) and lands on (4,1).
        Move canonical = TestHelpers.GetLegal(game, white, new Position(6, 3), new Position(4, 1));
        Assert.That(game.MakeMove(white, canonical), Is.True);

        Assert.That(game.Status, Is.EqualTo(GameStatus.WhiteWon));
        Assert.That(game.GetWinner(), Is.SameAs(white));
        Assert.That(endedPayloads, Is.EqualTo(new[] { GameStatus.WhiteWon }));
        // Event order: move -> turn -> end, with state fully committed each time.
        Assert.That(events, Is.EqualTo(new[] { "move", "turn", "end" }));
        Assert.That(statusSeenInMoveEvent, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(turnColorSeenInEndEvent, Is.EqualTo(PieceColor.Black)); // turn already flipped before the end check
    }

    [Test]
    public void Exhaustion_NoLegalMoves_Loses_OpponentWins()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 6, 1, PieceColor.White, PieceType.Man, 300);
        // Black man on the last row: forward is off the board, nothing to jump -> zero moves.
        TestHelpers.Place(game, 9, 0, PieceColor.Black, PieceType.Man, 400);
        Assert.That(game.GetSquare(new Position(9, 0))!.Occupant, Is.Not.Null);

        var ended = new List<GameStatus>();
        game.OnGameEnded += s => ended.Add(s);

        Move canonical = TestHelpers.GetLegal(game, white, new Position(6, 1), new Position(5, 0));
        Assert.That(game.MakeMove(white, canonical), Is.True);

        // Black still has a piece (1), but no legal move -> Black loses, White wins.
        Assert.That(game.GetSquare(new Position(9, 0))!.Occupant, Is.Not.Null);
        Assert.That(game.Status, Is.EqualTo(GameStatus.WhiteWon));
        Assert.That(game.GetWinner(), Is.SameAs(white));
        Assert.That(ended, Is.EqualTo(new[] { GameStatus.WhiteWon }));
    }

    // ------------------------------------------------------ Repetition draw

    [Test]
    public void ThreefoldRepetition_ThirdOccurrence_EndsInDraw()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 4, 3, PieceColor.White, PieceType.King, 500);
        TestHelpers.Place(game, 6, 7, PieceColor.Black, PieceType.King, 600);

        var ended = new List<GameStatus>();
        game.OnGameEnded += s => ended.Add(s);

        Position[][] cycle =
        {
            new[] { new Position(4, 3), new Position(3, 2) }, // white out
            new[] { new Position(6, 7), new Position(5, 6) }, // black out
            new[] { new Position(3, 2), new Position(4, 3) }, // white back
            new[] { new Position(5, 6), new Position(6, 7) }, // black back
        };

        int plies = 0;
        for (; plies < 24 && game.Status == GameStatus.Ongoing; plies++)
        {
            Position[] step = cycle[plies % 4];
            IPlayer mover = plies % 2 == 0 ? white : black;
            Move move = TestHelpers.GetLegal(game, mover, step[0], step[1]);
            Assert.That(game.MakeMove(mover, move), Is.True);
        }

        // The position recurs every 4 plies; the third occurrence ends the game (D2, count >= 3).
        Assert.That(game.Status, Is.EqualTo(GameStatus.Draw));
        Assert.That(plies, Is.InRange(5, 24)); // ended within the loop, not by the cap
        Assert.That(game.GetWinner(), Is.Null);
        Assert.That(ended, Is.EqualTo(new[] { GameStatus.Draw }));

        // A finished game rejects further moves.
        Move fabricated = new(new Position(4, 3), new Position(3, 2), null, null, MoveType.Quiet, PieceColor.White, PieceType.King);
        Assert.That(game.MakeMove(white, fabricated), Is.False);
        Assert.That(ended, Has.Count.EqualTo(1));
    }

    // --------------------------------------------------- King-only streak

    [Test]
    public void KingOnlyStreak_IncrementsPerQuietKingPly()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 4, 3, PieceColor.White, PieceType.King, 500);
        TestHelpers.Place(game, 6, 7, PieceColor.Black, PieceType.King, 600);

        Move whiteMove = TestHelpers.GetLegal(game, white, new Position(4, 3), new Position(3, 2));
        Assert.That(game.MakeMove(white, whiteMove), Is.True);
        Assert.That(TestHelpers.GetPrivate<int>(game, "_kingOnlyStreak"), Is.EqualTo(1));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));

        Move blackMove = TestHelpers.GetLegal(game, black, new Position(6, 7), new Position(5, 6));
        Assert.That(game.MakeMove(black, blackMove), Is.True);
        Assert.That(TestHelpers.GetPrivate<int>(game, "_kingOnlyStreak"), Is.EqualTo(2));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    [Test]
    public void KingOnlyStreak_QuietManMove_ResetsToZero()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 4, 3, PieceColor.White, PieceType.King, 500);
        TestHelpers.Place(game, 8, 1, PieceColor.White, PieceType.Man, 510);
        TestHelpers.Place(game, 6, 7, PieceColor.Black, PieceType.King, 600);

        Assert.That(game.MakeMove(white, TestHelpers.GetLegal(game, white, new Position(4, 3), new Position(3, 2))), Is.True);
        Assert.That(TestHelpers.GetPrivate<int>(game, "_kingOnlyStreak"), Is.EqualTo(1));

        Assert.That(game.MakeMove(black, TestHelpers.GetLegal(game, black, new Position(6, 7), new Position(5, 6))), Is.True);
        Assert.That(TestHelpers.GetPrivate<int>(game, "_kingOnlyStreak"), Is.EqualTo(2));

        // A quiet move BY A MAN resets the streak (D3), even though it is quiet.
        Assert.That(game.MakeMove(white, TestHelpers.GetLegal(game, white, new Position(8, 1), new Position(7, 0))), Is.True);
        Assert.That(TestHelpers.GetPrivate<int>(game, "_kingOnlyStreak"), Is.EqualTo(0));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    [Test]
    public void KingOnlyStreak_Capture_ResetsToZero_AndDoesNotTriggerDraw()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 4, 3, PieceColor.White, PieceType.King, 500);
        TestHelpers.Place(game, 5, 4, PieceColor.Black, PieceType.Man, 620);
        TestHelpers.Place(game, 9, 8, PieceColor.Black, PieceType.King, 630);

        // Seed the streak one below the draw threshold: if a capture counted as a
        // king-only ply the game would end here, and if it did not reset, 49 would stay.
        TestHelpers.SetPrivate(game, "_kingOnlyStreak", 2 * GameController.MaxKingOnlyStreak - 1); // 49

        Move capture = TestHelpers.GetLegal(game, white, new Position(4, 3), new Position(6, 5));
        Assert.That(game.MakeMove(white, capture), Is.True);

        Assert.That(TestHelpers.GetPrivate<int>(game, "_kingOnlyStreak"), Is.EqualTo(0));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing)); // NOT the 50-ply draw
        Assert.That(game.GetWinner(), Is.Null);
        Assert.That(game.IsSquareEmpty(new Position(5, 4)), Is.True); // victim gone
        Assert.That(game.GetSquare(new Position(9, 8))!.Occupant, Is.Not.Null); // other black piece survives
    }

    [Test]
    public void KingOnlyDraw_AtTwiceMaxStreak_WithBoundary()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 4, 3, PieceColor.White, PieceType.King, 500);
        TestHelpers.Place(game, 6, 7, PieceColor.Black, PieceType.King, 600);

        var ended = new List<GameStatus>();
        game.OnGameEnded += s => ended.Add(s);

        Assert.That(game.MakeMove(white, TestHelpers.GetLegal(game, white, new Position(4, 3), new Position(3, 2))), Is.True);
        Assert.That(game.MakeMove(black, TestHelpers.GetLegal(game, black, new Position(6, 7), new Position(5, 6))), Is.True);

        // D3: threshold is 2 * MaxKingOnlyStreak = 50 plies.
        TestHelpers.SetPrivate(game, "_kingOnlyStreak", 2 * GameController.MaxKingOnlyStreak - 2); // 48

        Assert.That(game.MakeMove(white, TestHelpers.GetLegal(game, white, new Position(3, 2), new Position(4, 3))), Is.True);
        Assert.That(TestHelpers.GetPrivate<int>(game, "_kingOnlyStreak"), Is.EqualTo(2 * GameController.MaxKingOnlyStreak - 1)); // 49
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing)); // boundary: 49 must NOT end the game
        Assert.That(ended, Is.Empty);

        Assert.That(game.MakeMove(black, TestHelpers.GetLegal(game, black, new Position(5, 6), new Position(6, 7))), Is.True);
        Assert.That(TestHelpers.GetPrivate<int>(game, "_kingOnlyStreak"), Is.EqualTo(2 * GameController.MaxKingOnlyStreak)); // 50
        Assert.That(game.Status, Is.EqualTo(GameStatus.Draw)); // 50 must end the game
        Assert.That(ended, Is.EqualTo(new[] { GameStatus.Draw }));
        Assert.That(game.GetWinner(), Is.Null);

        // Finished game stays finished (D6).
        Move fabricated = new(new Position(4, 3), new Position(3, 2), null, null, MoveType.Quiet, PieceColor.White, PieceType.King);
        Assert.That(game.MakeMove(white, fabricated), Is.False);
        Assert.That(game.OfferDraw(black), Is.False);
        game.Resign(white);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Draw));
        Assert.That(ended, Has.Count.EqualTo(1));
    }

    // ---------------------------------------------------------- Draw offers

    [Test]
    public void OfferDraw_FirstOffer_ReturnsTrue_GameOngoing()
    {
        var (game, white, _) = TestHelpers.CreateGame();
        int endedCount = 0;
        game.OnGameEnded += _ => endedCount++;

        bool result = game.OfferDraw(white);

        Assert.That(result, Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.DrawOfferedBy, Has.Count.EqualTo(1));
        IPlayer recorded = game.DrawOfferedBy[0];
        Assert.That(recorded, Is.SameAs(white));
        Assert.That(endedCount, Is.EqualTo(0));
    }

    [Test]
    public void OfferDraw_Duplicate_ReturnsFalse_ListUnchanged()
    {
        var (game, white, _) = TestHelpers.CreateGame();

        Assert.That(game.OfferDraw(white), Is.True);
        bool second = game.OfferDraw(white);

        Assert.That(second, Is.False); // D5: duplicate offer
        Assert.That(game.DrawOfferedBy, Has.Count.EqualTo(1));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    [Test]
    public void OfferDraw_ByOutsiderOrNullOrEmpty_ReturnsFalse()
    {
        var (game, _, _) = TestHelpers.CreateGame();
        Player outsider = new(99, "Eve", PieceColor.White);

        Assert.That(game.OfferDraw(outsider), Is.False);
        Assert.That(game.OfferDraw(null!), Is.False);
        Assert.That(game.DrawOfferedBy, Is.Empty);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    [Test]
    public void OfferDraw_Mutual_EndsInDraw()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        var ended = new List<GameStatus>();
        game.OnGameEnded += s => ended.Add(s);

        Assert.That(game.OfferDraw(white), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(ended, Is.Empty);

        Assert.That(game.OfferDraw(black), Is.True); // second offer -> immediate draw

        Assert.That(game.Status, Is.EqualTo(GameStatus.Draw));
        Assert.That(game.DrawOfferedBy.Count, Is.EqualTo(2));
        Assert.That(game.GetWinner(), Is.Null);
        Assert.That(ended, Is.EqualTo(new[] { GameStatus.Draw }));
    }

    [Test]
    public void OfferDraw_ClearedByAnyMove_ReofferAllowed()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        TestHelpers.ClearBoard(game);
        TestHelpers.Place(game, 6, 1, PieceColor.White, PieceType.Man, 700);
        TestHelpers.Place(game, 1, 8, PieceColor.Black, PieceType.Man, 800);

        // White offers, then any accepted move expires it (D5: no decline endpoint).
        Assert.That(game.OfferDraw(white), Is.True);
        Assert.That(game.DrawOfferedBy, Has.Count.EqualTo(1));
        Assert.That(game.MakeMove(white, TestHelpers.GetLegal(game, white, new Position(6, 1), new Position(5, 0))), Is.True);
        Assert.That(game.DrawOfferedBy, Is.Empty);

        // The opponent's move expires offers too.
        Assert.That(game.MakeMove(black, TestHelpers.GetLegal(game, black, new Position(1, 8), new Position(2, 9))), Is.True);
        Assert.That(game.DrawOfferedBy, Is.Empty);

        // Re-offering is allowed while the game is still ongoing.
        Assert.That(game.OfferDraw(white), Is.True);
        Assert.That(game.DrawOfferedBy, Has.Count.EqualTo(1));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    // --------------------------------------------- Finished game gates (D6)

    [Test]
    public void FinishedGame_MakeMove_OfferDraw_Resign_AllRejectedOrIgnored()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        int endedCount = 0;
        game.OnGameEnded += _ => endedCount++;

        game.Resign(white); // -> BlackWon
        Assert.That(game.Status, Is.EqualTo(GameStatus.BlackWon));
        Assert.That(endedCount, Is.EqualTo(1));

        var before = TestHelpers.Snapshot(game);
        Move fabricated = new(new Position(6, 1), new Position(5, 0), null, null, MoveType.Quiet, PieceColor.White, PieceType.Man);
        Assert.That(game.MakeMove(black, fabricated), Is.False);
        Assert.That(game.OfferDraw(black), Is.False);
        game.Resign(black); // no-op
        game.Resign(white); // no-op

        Assert.That(TestHelpers.Snapshot(game), Is.EqualTo(before));
        Assert.That(game.Status, Is.EqualTo(GameStatus.BlackWon));
        Assert.That(endedCount, Is.EqualTo(1)); // OnGameEnded exactly once per game
    }

    [Test]
    public void GetLegalMoves_AfterFinished_IsEmpty()
    {
        var (game, white, black) = TestHelpers.CreateGame();
        game.Resign(white); // -> BlackWon

        Assert.That(game.GetLegalMoves(white), Is.Empty);
        Assert.That(game.GetLegalMoves(black), Is.Empty);
    }

    // ------------------------------------------------------------ GetWinner

    [Test]
    public void GetWinner_OnlyForWonStatuss_NullForOngoingAndDraw()
    {
        // Ongoing -> null.
        var (ongoing, _, _) = TestHelpers.CreateGame();
        Assert.That(ongoing.GetWinner(), Is.Null);

        // BlackWon (white resigned) -> the BLACK player (own-colour branch).
        var (blackWon, whiteB, blackB) = TestHelpers.CreateGame();
        blackWon.Resign(whiteB);
        Assert.That(blackWon.Status, Is.EqualTo(GameStatus.BlackWon));
        Assert.That(blackWon.GetWinner(), Is.SameAs(blackB));

        // WhiteWon (black resigned) -> the WHITE player.
        var (whiteWon, white, black2) = TestHelpers.CreateGame();
        whiteWon.Resign(black2);
        Assert.That(whiteWon.Status, Is.EqualTo(GameStatus.WhiteWon));
        Assert.That(whiteWon.GetWinner(), Is.SameAs(white));

        // Draw -> null.
        var (drawn, white3, black3) = TestHelpers.CreateGame();
        drawn.OfferDraw(white3);
        drawn.OfferDraw(black3);
        Assert.That(drawn.Status, Is.EqualTo(GameStatus.Draw));
        Assert.That(drawn.GetWinner(), Is.Null);
    }
}
