namespace Checkers.Models;

public interface IPlayer
{
    int Id { get; }
    string Name { get; }
    PieceColor Color { get; }
}
