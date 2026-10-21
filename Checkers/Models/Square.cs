namespace Checkers.Models;

public class Square : ISquare
{
    public Position Position { get; }
    public SquareNumber? Number { get; }
    public bool IsDark { get; }
    public IPiece? Occupant { get; set; }

    public Square(Position position, SquareNumber? number, bool isDark)
    {
        Position = position;
        Number = number;
        IsDark = isDark;
    }
}
