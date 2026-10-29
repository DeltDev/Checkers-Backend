using Checkers.Models;

namespace Checkers.Tests;

[TestFixture]
public class SquareTests
{
    [Test]
    public void Constructor_SetsPositionNumberAndDarkness()
    {
        var square = new Square(new Position(0, 1), SquareNumber.Square1, true);

        Assert.That(square.Position, Is.EqualTo(new Position(0, 1)));
        Assert.That(square.Number, Is.EqualTo(SquareNumber.Square1));
        Assert.That(square.IsDark, Is.True);
        Assert.That(square.Occupant, Is.Null);
    }

    [Test]
    public void LightSquare_HasNoNumber()
    {
        var square = new Square(new Position(0, 0), null, false);

        Assert.That(square.Number, Is.Null);
        Assert.That(square.IsDark, Is.False);
    }

    [Test]
    public void Occupant_CanBeAssignedAndCleared()
    {
        var square = new Square(new Position(0, 1), SquareNumber.Square1, true);
        var piece = new Piece(1, PieceColor.Black, PieceType.Man);

        square.Occupant = piece;
        Assert.That(square.Occupant, Is.SameAs(piece));

        square.Occupant = null;
        Assert.That(square.Occupant, Is.Null);
    }
}
