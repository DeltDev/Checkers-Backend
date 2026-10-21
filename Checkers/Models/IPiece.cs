namespace Checkers.Models;

public interface IPiece
{
    int Id { get; }
    PieceColor Color { get; }
    PieceType Type { get; }
}
