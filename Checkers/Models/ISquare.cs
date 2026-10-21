namespace Checkers.Models;

public interface ISquare
{
    Position Position { get; }
    SquareNumber? Number { get; }
    bool IsDark { get; }
    IPiece? Occupant { get; set; }
}
