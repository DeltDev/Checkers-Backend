using System.Reflection;
using Checkers.Models;

namespace Checkers.Tests;

[TestFixture]
public class PieceTests
{
    [TestCase(PieceType.Man)]
    [TestCase(PieceType.King)]
    public void Piece_KeepsTheConstructorValues(PieceType type)
    {
        var piece = new Piece(7, PieceColor.Black, type);

        Assert.That(piece.Id, Is.EqualTo(7));
        Assert.That(piece.Color, Is.EqualTo(PieceColor.Black));
        Assert.That(piece.Type, Is.EqualTo(type));
    }

    [Test]
    public void Piece_IsUsableThroughIPiece()
    {
        IPiece piece = new Piece(21, PieceColor.White, PieceType.King);

        Assert.That(piece.Id, Is.EqualTo(21));
        Assert.That(piece.Color, Is.EqualTo(PieceColor.White));
        Assert.That(piece.Type, Is.EqualTo(PieceType.King));
    }

    [Test]
    public void Piece_HasNoPublicSetters()
    {
        // Identity, color and type are fixed at creation: CapturedPieceIds and
        // the position key both assume a piece never mutates in place.
        foreach (PropertyInfo property in typeof(IPiece).GetProperties())
        {
            Assert.That(property.CanWrite, Is.False, $"{property.Name} must stay read-only");
        }
    }
}
