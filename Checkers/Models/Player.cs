namespace Checkers.Models;

public class Player : IPlayer
{
    public int Id { get; }
    public string Name { get; }
    public PieceColor Color { get; }

    public Player(int id, string name, PieceColor color)
    {
        Id = id;
        Name = name;
        Color = color;
    }
}
