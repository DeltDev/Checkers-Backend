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

    public bool IsCapture()
    {
        return Type == MoveType.Capture;
    }

    public bool IsPromotion()
    {

        if (PieceType != PieceType.Man)
        {
            return false;
        }

        if (Color == PieceColor.White)
        {
            return To.Row == 0;
        }

        if (Color == PieceColor.Black)
        {
            return To.Row == Board.Size-1;
        }
        return false;
    }

    public string ToNotation()
    {
        bool isCapture = IsCapture();
        string separator = isCapture ? "x" : ":";

        List<int> outputSquares = new(){(int) GameController.ToSquareNumber(From)};

        if (isCapture)
        {
            foreach (Position step in Path)
            {
                if (step == From || step == To)
                {
                    continue;
                }
                outputSquares.Add((int) GameController.ToSquareNumber(step));
            }
            
            
        }
        
        outputSquares.Add((int) GameController.ToSquareNumber(To));
            
        return string.Join(separator, outputSquares.Select(number => $"[{number}]"));

    }
    
}
