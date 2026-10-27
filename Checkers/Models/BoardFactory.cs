namespace Checkers.Models;


public sealed class BoardFactory : IBoardFactory
{
    private readonly IPieceFactory _pieces;

    public BoardFactory(IPieceFactory pieces)
    {
        _pieces = pieces;
    }

    public IBoard CreateBoard()
    {
        IBoard board = new Board();
        PlaceStandardPosition(board);
        return board;
    }


    private void PlaceStandardPosition(IBoard board)
    {
        int pieceId = 1;

        for (int row = 0; row < Board.Size; row++)
        {
            for (int col = 0; col < Board.Size; col++)
            {
                ISquare square = board.Squares[row, col];
                if (!square.IsDark)
                {
                    continue;
                }

                if (row < 4)
                {
                    square.Occupant = _pieces.CreateMan(pieceId++, PieceColor.Black);
                }
                else if (row > 5)
                {
                    square.Occupant = _pieces.CreateMan(pieceId++, PieceColor.White);
                }
            }
        }
    }
}
