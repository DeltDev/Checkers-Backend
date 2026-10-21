namespace Checkers.Models;

public interface IBoard
{
    ISquare[,] Squares { get; }
}
