namespace Checkers.Models;

public struct Move
{
    public Position From { get; }
    public Position To { get; }
    public Position[] Path { get; }
    public IPiece[] Captured { get; }
    public MoveType Type { get; }
    public PieceColor Color { get; }
    public PieceType PieceType { get; }

    public Move(Position from, Position to, Position[]? path, IPiece[]? captured, MoveType type, PieceColor color, PieceType pieceType)
    {
        From = from;
        To = to;
        Path = path?.ToArray() ?? Array.Empty<Position>();
        Captured = captured?.ToArray() ?? Array.Empty<IPiece>();
        Type = type;
        Color = color;
        PieceType = pieceType;
    }

    
    
}
