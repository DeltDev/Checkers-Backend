using Checkers.Models;

namespace Checkers.Tests;

[TestFixture]
public class MoveTests
{
    [TestCase(MoveType.Quiet, false)]
    [TestCase(MoveType.Capture, true)]
    public void IsCapture_ReflectsMoveType(MoveType type, bool expected)
    {
        Move move = CreateMove(type: type);

        Assert.That(GameController.IsCapture(move), Is.EqualTo(expected));
    }

    [TestCase(PieceColor.White, PieceType.Man, 0, true)]
    [TestCase(PieceColor.White, PieceType.Man, 1, false)]
    [TestCase(PieceColor.Black, PieceType.Man, 9, true)]
    [TestCase(PieceColor.Black, PieceType.Man, 8, false)]
    [TestCase(PieceColor.White, PieceType.Man, 9, false)]
    [TestCase(PieceColor.Black, PieceType.Man, 0, false)]
    [TestCase(PieceColor.White, PieceType.King, 0, false)]
    [TestCase(PieceColor.White, PieceType.King, 9, false)]
    [TestCase(PieceColor.Black, PieceType.King, 9, false)]
    [TestCase(PieceColor.Black, PieceType.King, 0, false)]
    public void IsPromotion_IsOnlyTrueForAManOnItsPromotionRow(
        PieceColor color,
        PieceType pieceType,
        int toRow,
        bool expected)
    {
        // Keep the destination on a dark square for the given row.
        var to = new Position(toRow, toRow % 2 == 0 ? 1 : 0);
        Move move = CreateMove(to: to, color: color, pieceType: pieceType);

        Assert.That(GameController.IsPromotion(move), Is.EqualTo(expected));
    }

    [Test]
    public void IsPromotion_IsTrueWhenACaptureEndsOnThePromotionRow()
    {
        Move move = CreateMove(
            from: new Position(2, 1),
            to: new Position(0, 1),
            type: MoveType.Capture,
            pieceType: PieceType.Man,
            captured: new IPiece[] { new Piece(1, PieceColor.Black, PieceType.Man) });

        Assert.That(GameController.IsCapture(move), Is.True);
        Assert.That(GameController.IsPromotion(move), Is.True);
    }

    [Test]
    public void ToNotation_QuietMoveUsesSquareNumbers()
    {
        Move move = CreateMove(from: new Position(6, 1), to: new Position(5, 0));

        Assert.That(GameController.ToNotation(move), Is.EqualTo("[31]:[26]"));
    }

    [Test]
    public void ToNotation_SingleCaptureUsesSquareNumbers()
    {
        Move move = CreateMove(
            from: new Position(9, 0),
            to: new Position(3, 6),
            type: MoveType.Capture);

        Assert.That(GameController.ToNotation(move), Is.EqualTo("[46]x[19]"));
    }

    [Test]
    public void ToNotation_MultiCaptureIncludesEveryLandingSquare()
    {
        Move move = CreateMove(
            from: new Position(9, 0),
            to: new Position(3, 6),
            type: MoveType.Capture,
            path: new[] { new Position(7, 2), new Position(5, 4) });

        Assert.That(GameController.ToNotation(move), Is.EqualTo("[46]x[37]x[28]x[19]"));
    }

    [Test]
    public void ToNotation_DoesNotDuplicatePathEndpoints()
    {
        Move move = CreateMove(
            from: new Position(9, 0),
            to: new Position(3, 6),
            type: MoveType.Capture,
            path: new[]
            {
                new Position(9, 0),
                new Position(7, 2),
                new Position(5, 4),
                new Position(3, 6),
            });

        Assert.That(GameController.ToNotation(move), Is.EqualTo("[46]x[37]x[28]x[19]"));
    }

    [Test]
    public void ToNotation_MatchesBoardNumberingForEveryPlayableSquare()
    {
        var board = new Board();

        foreach (Position from in Board.AllPositions().Where(p => (p.Row + p.Col) % 2 == 1))
        {
            Position to = from == new Position(0, 1)
                ? new Position(0, 3)
                : new Position(0, 1);
            Move move = CreateMove(from: from, to: to);

            string expectedFrom = ((int)board.Squares[from.Row, from.Col].Number!.Value).ToString();
            string expectedTo = ((int)board.Squares[to.Row, to.Col].Number!.Value).ToString();

            Assert.That(GameController.ToNotation(move), Is.EqualTo($"[{expectedFrom}]:[{expectedTo}]"));
        }
    }

    [Test]
    public void Move_NormalizesNullPathAndCapturedToEmptyArrays()
    {
        Move move = CreateMove(path: null, captured: null);

        Assert.That(move.Path, Is.Empty);
        Assert.That(move.Captured, Is.Empty);
    }

    [Test]
    public void Move_CopiesPathAndCapturedArrays()
    {
        var path = new[] { new Position(7, 2) };
        var captured = new IPiece[] { new Piece(1, PieceColor.Black, PieceType.Man) };

        Move move = CreateMove(type: MoveType.Capture, path: path, captured: captured);

        // Mutating the arrays after construction must not rewrite recorded history.
        path[0] = new Position(9, 8);
        captured[0] = new Piece(2, PieceColor.Black, PieceType.Man);

        Assert.That(move.Path[0], Is.EqualTo(new Position(7, 2)));
        Assert.That(move.Captured[0].Id, Is.EqualTo(1));
    }

    [Test]
    public void Move_ExposesTheMovingPieceType()
    {
        Move move = CreateMove(pieceType: PieceType.King);

        Assert.That(move.PieceType, Is.EqualTo(PieceType.King));
    }

    private static Move CreateMove(
        Position? from = null,
        Position? to = null,
        MoveType type = MoveType.Quiet,
        PieceColor color = PieceColor.White,
        PieceType pieceType = PieceType.Man,
        Position[]? path = null,
        IPiece[]? captured = null)
    {
        return new Move(
            from ?? new Position(6, 1),
            to ?? new Position(5, 0),
            path,
            captured,
            type,
            color,
            pieceType);
    }
}
