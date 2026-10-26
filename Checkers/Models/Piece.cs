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
}
