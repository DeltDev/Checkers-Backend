namespace Checkers.Models;


public class Board : IBoard
{

    public static int Size = 10;


    public static int PlayableSquares = 50;

    public ISquare[,] Squares { get; }

    public Board()
    {

        Squares = new ISquare[Size, Size];
        int playableSquareNumber = 1;
        int squareNumberReal = 1;
        for (int i = 0; i < Size; i++)
        {
            for (int j = 0; j < Size; j++)
            {
                bool isSquareDark;
                if (i % 2 == 0)
                {
                    isSquareDark = squareNumberReal % 2 == 0;
                }
                else
                {
                    isSquareDark = squareNumberReal % 2 == 1;
                }

                squareNumberReal++;

                if (isSquareDark)
                {
                    Squares[i, j] = new Square(new Position(i, j), (SquareNumber)playableSquareNumber++, isSquareDark);
                }
                else
                {
                    Squares[i, j] = new Square(new Position(i, j), null, isSquareDark);
                }



            }
        }
    }
    
    public static IEnumerable<Position> AllPositions()
    {
        List<Position> positions = new List<Position>();

        for (int i = 0; i < Size; i++)
        {
            for (int j = 0; j < Size; j++)
            {
                positions.Add(new Position(i, j));
            }
        }
        
        return positions;
    }
}
