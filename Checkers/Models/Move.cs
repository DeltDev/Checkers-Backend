namespace Checkers.Models;

public struct Move
{
    public Position From { get; }
    public Position To { get; }
    public Position[] Path { get; }
    public IPiece[] Captured { get; }
    public MoveType Type { get; }
    public PieceColor Color { get; }

    public Move(Position from, Position to, Position[] path, IPiece[] captured, MoveType type, PieceColor color)
    {
        From = from;
        To = to;
        Path = path;
        Captured = captured;
        Type = type;
        Color = color;
    }

    public bool IsCapture()
    {
        throw new NotImplementedException();
    }

    public bool IsPromotion()
    {
        throw new NotImplementedException();
    }

    public string ToNotation()
    {
        throw new NotImplementedException();
    }
}
