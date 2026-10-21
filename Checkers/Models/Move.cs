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
        return Type == MoveType.Capture;
    }

    public bool IsPromotion()
    {
        if (Type != MoveType.Capture)
        {
            if (Color == PieceColor.White)
            {
                return To.Row == 0;
            } 
            if(Color == PieceColor.Black)
            {
                return To.Row == 9;
            }
        }
        return false;
    }

    public string ToNotation()
    {
        
        if (Type == MoveType.Capture)
        {
            //asumsi masih 1 gerakan, path belum diimplementasi
            SquareNumber fromSquareNumber, toSquareNumber;

            Board board = new Board();
            
            fromSquareNumber = board.Squares[From.Row, From.Col].Number!.Value;
            toSquareNumber = board.Squares[To.Row, To.Col].Number!.Value;

            return $"[{(int)fromSquareNumber}]x[{(int)toSquareNumber}]";
        }
        else
        {
            SquareNumber fromSquareNumber, toSquareNumber;

            Board board = new Board();
            
            fromSquareNumber = board.Squares[From.Row, From.Col].Number!.Value;
            toSquareNumber = board.Squares[To.Row, To.Col].Number!.Value;

            return $"[{(int)fromSquareNumber}]:[{(int)toSquareNumber}]";
        }
    }
    
}
