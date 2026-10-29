using Checkers.Contracts.Responses;
using Checkers.Mapping;
using Checkers.Models;

namespace Checkers.Tests;

/// <summary>
/// PHASE 10 — Mapping (IMPLEMENTATION_PLAN Phase 7 + phase 7 pseudocode).
/// Decisions encoded:
///   D1 — Squares in the response is the FULL 100-square list (not just the 50 dark).
///   D2 — GameId is NOT the mapper's job: ToResponse leaves it empty and the
///        controller fills it in Phase 8 (asserted here and in GamesApiTests).
///   WinnerId comes from GetWinner() (needs Phase 5's fix to return a player).
/// All tests are expected to FAIL until Phase 7 (and Phase 5's GetWinner) are implemented.
/// </summary>
[TestFixture]
public class GameStateMapperTests
{
    [Test]
    public void FreshGame_MapsFullState()
    {
        var (game, white, black) = TestHelpers.CreateGame();

        var response = game.ToResponse();

        Assert.That(response.Turn, Is.EqualTo(1));
        Assert.That(response.TurnColor, Is.EqualTo(PieceColor.White));
        Assert.That(response.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(response.WinnerId, Is.Null);
        Assert.That(response.History, Is.Empty);

        // Players, in seated order.
        Assert.That(response.Players.Count, Is.EqualTo(2));
        Assert.That(response.Players[0].Id, Is.EqualTo(white.Id));
        Assert.That(response.Players[0].Name, Is.EqualTo(white.Name));
        Assert.That(response.Players[0].Color, Is.EqualTo(PieceColor.White));
        Assert.That(response.Players[1].Id, Is.EqualTo(black.Id));
        Assert.That(response.Players[1].Name, Is.EqualTo(black.Name));
        Assert.That(response.Players[1].Color, Is.EqualTo(PieceColor.Black));

        // D1: the full 100-square board, not just playable squares.
        Assert.That(response.Squares.Count, Is.EqualTo(100));

        // D2: the controller fills GameId in Phase 8, never the mapper.
        Assert.That(response.GameId, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ToResponse_DoesNotMutateTheGame()
    {
        var (game, _, _) = TestHelpers.CreateGame();
        var before = TestHelpers.Snapshot(game);

        var first = game.ToResponse();
        var second = game.ToResponse();

        Assert.That(TestHelpers.Snapshot(game), Is.EqualTo(before));
        Assert.That(second.Turn, Is.EqualTo(first.Turn));
        Assert.That(second.Squares.Count, Is.EqualTo(first.Squares.Count));
        Assert.That(second.History.Count, Is.EqualTo(first.History.Count));
    }

    [Test]
    public void Square_LightSquare_NumberAndOccupantAllNull()
    {
        var (game, _, _) = TestHelpers.CreateGame();
        ISquare light = game.GetSquare(new Position(0, 0))!;
        Assert.That(light.IsDark, Is.False);

        SquareResponse response = light.ToResponse();

        Assert.That(response.Position, Is.EqualTo(new Position(0, 0)));
        Assert.That(response.Number, Is.Null);
        Assert.That(response.IsDark, Is.False);
        Assert.That(response.OccupantId, Is.Null);
        Assert.That(response.OccupantColor, Is.Null);
        Assert.That(response.OccupantType, Is.Null);
    }

    [Test]
    public void Square_DarkEmpty_NumberSet_OccupantTrioNull()
    {
        var (game, _, _) = TestHelpers.CreateGame();
        ISquare darkEmpty = game.GetSquare(new Position(4, 1))!;
        Assert.That(darkEmpty.IsDark, Is.True);
        Assert.That(darkEmpty.Occupant, Is.Null);

        SquareResponse response = darkEmpty.ToResponse();

        // Number computed independently: row * 5 + col/2 + 1 = 4 * 5 + 0 + 1 = 21.
        Assert.That(response.Number, Is.EqualTo((SquareNumber)21));
        Assert.That(response.IsDark, Is.True);
        Assert.That(response.OccupantId, Is.Null);
        Assert.That(response.OccupantColor, Is.Null);
        Assert.That(response.OccupantType, Is.Null);
    }

    [Test]
    public void Square_Occupied_MapsIdColorAndType()
    {
        var (game, _, _) = TestHelpers.CreateGame();

        SquareResponse whiteMan = game.GetSquare(new Position(6, 1))!.ToResponse();
        Assert.That(whiteMan.OccupantId, Is.EqualTo(21));
        Assert.That(whiteMan.OccupantColor, Is.EqualTo(PieceColor.White));
        Assert.That(whiteMan.OccupantType, Is.EqualTo(PieceType.Man));
        Assert.That(whiteMan.Number, Is.EqualTo((SquareNumber)31));

        SquareResponse blackMan = game.GetSquare(new Position(3, 8))!.ToResponse();
        Assert.That(blackMan.OccupantId, Is.EqualTo(20));
        Assert.That(blackMan.OccupantColor, Is.EqualTo(PieceColor.Black));
        Assert.That(blackMan.OccupantType, Is.EqualTo(PieceType.Man));
        // Number: row * 5 + col/2 + 1 = 3 * 5 + 4 + 1 = 20.
        Assert.That(blackMan.Number, Is.EqualTo((SquareNumber)20));
    }

    [Test]
    public void MoveResponse_Capture_MapsIdsPathAndNotation()
    {
        var (game, _, _) = TestHelpers.CreateGame();
        Move capture = new(
            new Position(6, 1), new Position(2, 5),
            new[] { new Position(4, 3) },
            new IPiece[]
            {
                new Piece(200, PieceColor.Black, PieceType.Man),
                new Piece(201, PieceColor.Black, PieceType.Man),
            },
            MoveType.Capture, PieceColor.White, PieceType.Man);
        game.History.Add(capture);

        Assert.That(game.ToResponse().History, Has.Count.EqualTo(1));

        MoveResponse response = game.ToResponse().History[0];

        Assert.That(response.From, Is.EqualTo(new Position(6, 1)));
        Assert.That(response.To, Is.EqualTo(new Position(2, 5)));
        Assert.That(response.Path, Is.EqualTo(new[] { new Position(4, 3) }));
        Assert.That(response.CapturedPieceIds, Is.EqualTo(new[] { 200, 201 }));
        Assert.That(response.Type, Is.EqualTo(MoveType.Capture));
        Assert.That(response.Color, Is.EqualTo(PieceColor.White));
        // Notation: [31] = (6,1), [22] = (4,3) intermediate, [13] = (2,5).
        Assert.That(response.Notation, Is.EqualTo("[31]x[22]x[13]"));
    }

    [Test]
    public void MoveResponse_Quiet_UsesColonNotation_EmptyPathAndIds()
    {
        var (game, _, _) = TestHelpers.CreateGame();
        Move quiet = new(
            new Position(6, 1), new Position(5, 0),
            null, null, MoveType.Quiet, PieceColor.White, PieceType.Man);
        game.History.Add(quiet);

        Assert.That(game.ToResponse().History, Has.Count.EqualTo(1));

        MoveResponse response = game.ToResponse().History[0];

        Assert.That(response.Type, Is.EqualTo(MoveType.Quiet));
        Assert.That(response.Path, Is.Empty);
        Assert.That(response.CapturedPieceIds, Is.Empty);
        // [31] = (6,1), [26] = (5,0).
        Assert.That(response.Notation, Is.EqualTo("[31]:[26]"));
    }

    [Test]
    public void History_MapsInPlayOrder()
    {
        var (game, _, _) = TestHelpers.CreateGame();
        game.History.Add(new Move(new Position(6, 1), new Position(5, 0), null, null, MoveType.Quiet, PieceColor.White, PieceType.Man));
        game.History.Add(new Move(new Position(3, 0), new Position(4, 1), null, null, MoveType.Quiet, PieceColor.Black, PieceType.Man));

        var history = game.ToResponse().History;

        Assert.That(history.Count, Is.EqualTo(2));
        Assert.That(history[0].Notation, Is.EqualTo("[31]:[26]"));
        Assert.That(history[1].Notation, Is.EqualTo("[16]:[21]"));
        Assert.That(history[0].Color, Is.EqualTo(PieceColor.White));
        Assert.That(history[1].Color, Is.EqualTo(PieceColor.Black));
    }

    [Test]
    public void WinnerId_OnlyForWonStatuss_NullForOngoingAndDraw()
    {
        // Ongoing -> null.
        var (ongoing, _, _) = TestHelpers.CreateGame();
        Assert.That(ongoing.ToResponse().WinnerId, Is.Null);

        // WhiteWon (black resigned) -> white's id.
        var (whiteWon, _, black) = TestHelpers.CreateGame();
        whiteWon.Resign(black);
        Assert.That(whiteWon.Status, Is.EqualTo(GameStatus.WhiteWon));
        Assert.That(whiteWon.ToResponse().WinnerId, Is.EqualTo(1));
        Assert.That(whiteWon.ToResponse().Status, Is.EqualTo(GameStatus.WhiteWon));

        // BlackWon (white resigned) -> black's id.
        var (blackWon, whiteB, _) = TestHelpers.CreateGame();
        blackWon.Resign(whiteB);
        Assert.That(blackWon.Status, Is.EqualTo(GameStatus.BlackWon));
        Assert.That(blackWon.ToResponse().WinnerId, Is.EqualTo(2));

        // Draw -> null.
        var (drawn, whiteC, blackC) = TestHelpers.CreateGame();
        drawn.OfferDraw(whiteC);
        drawn.OfferDraw(blackC);
        Assert.That(drawn.Status, Is.EqualTo(GameStatus.Draw));
        Assert.That(drawn.ToResponse().WinnerId, Is.Null);
    }
}

