namespace Checkers.Models;

public class Piece : IPiece
{
    public int Id { get; }
    public PieceColor Color { get; }
    public PieceType Type { get; }

    public Piece(int id, PieceColor color, PieceType type)
    {
        Id = id;
        Color = color;
        Type = type;
    }

    public Piece(Piece other)
    {
        Id = other.Id;
        Color = other.Color;
        Type = other.Type;
    }

    public Piece Copy()
    {
        Piece pieceCopy = new Piece(this.Id, this.Color, this.Type);
        return pieceCopy;
    }
}
