using Checkers.Models;

namespace Checkers.Tests;

[TestFixture]
public class BoardTests
{
    [Test]
    public void Board_IsTenByTen()
    {
        var board = new Board();

        Assert.That(Board.Size, Is.EqualTo(10));
        Assert.That(board.Squares.GetLength(0), Is.EqualTo(Board.Size));
        Assert.That(board.Squares.GetLength(1), Is.EqualTo(Board.Size));
    }

    [Test]
    public void Board_HasNoNullSquares()
    {
        var board = new Board();

        foreach (Position position in Board.AllPositions())
        {
            Assert.That(board.Squares[position.Row, position.Col], Is.Not.Null);
        }
    }

    [Test]
    public void Board_HasFiftyDarkAndFiftyLightSquares()
    {
        var board = new Board();
        int dark = 0;

        foreach (Position position in Board.AllPositions())
        {
            if (board.Squares[position.Row, position.Col].IsDark)
            {
                dark++;
            }
        }

        Assert.That(dark, Is.EqualTo(Board.PlayableSquares));
        Assert.That(dark, Is.EqualTo(50));
        Assert.That(Board.AllPositions().Count() - dark, Is.EqualTo(50));
    }

    [Test]
    public void Board_DarknessIsRowPlusColumnParity()
    {
        var board = new Board();

        foreach (Position position in Board.AllPositions())
        {
            ISquare square = board.Squares[position.Row, position.Col];
            bool expectedDark = (position.Row + position.Col) % 2 == 1;

            Assert.That(square.IsDark, Is.EqualTo(expectedDark));
            Assert.That(square.Number.HasValue, Is.EqualTo(expectedDark));
        }
    }

    [Test]
    public void Board_SquareNumbersAreUniqueAndCoverOneToFifty()
    {
        var board = new Board();

        int[] numbers = Board.AllPositions()
            .Select(position => board.Squares[position.Row, position.Col])
            .Where(square => square.IsDark)
            .Select(square => (int)square.Number!.Value)
            .OrderBy(number => number)
            .ToArray();

        Assert.That(numbers.Length, Is.EqualTo(50));
        Assert.That(numbers, Is.EqualTo(Enumerable.Range(1, 50)));
    }

    [Test]
    public void Board_SquaresKnowTheirOwnPosition()
    {
        var board = new Board();

        foreach (Position position in Board.AllPositions())
        {
            Assert.That(board.Squares[position.Row, position.Col].Position, Is.EqualTo(position));
        }
    }

    // Standard international draughts numbering: row 0 at the top, left to right.
    [TestCase(0, 1, SquareNumber.Square1)]
    [TestCase(0, 9, SquareNumber.Square5)]
    [TestCase(1, 0, SquareNumber.Square6)]
    [TestCase(1, 8, SquareNumber.Square10)]
    [TestCase(8, 1, SquareNumber.Square41)]
    [TestCase(9, 0, SquareNumber.Square46)]
    [TestCase(9, 8, SquareNumber.Square50)]
    public void Board_NumberingIsRowMajorFromTopLeft(int row, int col, SquareNumber expected)
    {
        var board = new Board();

        Assert.That(board.Squares[row, col].Number, Is.EqualTo(expected));
    }

    [Test]
    public void Board_InitialSetupHasTwentyPiecesPerColor()
    {
        // Pieces are initialized OUTSIDE the board now: BoardFactory places
        // them through IPieceFactory (Board itself only builds the grid).
        var board = new BoardFactory(new PieceFactory()).CreateBoard();

        IPiece[] pieces = Board.AllPositions()
            .Select(position => board.Squares[position.Row, position.Col].Occupant)
            .Where(piece => piece is not null)
            .Select(piece => piece!)
            .ToArray();

        Assert.That(pieces.Length, Is.EqualTo(40));
        Assert.That(pieces.Count(piece => piece.Color == PieceColor.Black), Is.EqualTo(20));
        Assert.That(pieces.Count(piece => piece.Color == PieceColor.White), Is.EqualTo(20));
        Assert.That(pieces, Is.All.Property("Type").EqualTo(PieceType.Man));
        Assert.That(pieces.Select(piece => piece.Id).Distinct().Count(), Is.EqualTo(40));
    }

    [Test]
    public void Board_InitialSetupPlacesBlackOnTopAndWhiteOnBottom()
    {
        // The Board builds the grid only; the standard starting position is
        // applied by BoardFactory, which is the sole owner of piece creation.
        var board = new BoardFactory(new PieceFactory()).CreateBoard();

        for (int row = 0; row < Board.Size; row++)
        {
            for (int col = 0; col < Board.Size; col++)
            {
                ISquare square = board.Squares[row, col];
                IPiece? occupant = square.Occupant;

                if (!square.IsDark)
                {
                    Assert.That(occupant, Is.Null);
                }
                else if (row <= 3)
                {
                    Assert.That(occupant, Is.Not.Null);
                    Assert.That(occupant!.Color, Is.EqualTo(PieceColor.Black));
                }
                else if (row >= 6)
                {
                    Assert.That(occupant, Is.Not.Null);
                    Assert.That(occupant!.Color, Is.EqualTo(PieceColor.White));
                }
                else
                {
                    Assert.That(occupant, Is.Null);
                }
            }
        }
    }

    [Test]
    public void Board_InitialPieceIdsAreUniqueAndSplitIntoColorRanges()
    {
        var board = new BoardFactory(new PieceFactory()).CreateBoard();

        int[] blackIds = GetPieceIds(board, PieceColor.Black);
        int[] whiteIds = GetPieceIds(board, PieceColor.White);

        // Contract: black owns 1-20, white owns 21-40, so CapturedPieceIds is unambiguous.
        Assert.That(blackIds, Is.EqualTo(Enumerable.Range(1, 20)));
        Assert.That(whiteIds, Is.EqualTo(Enumerable.Range(21, 20)));
    }

    [Test]
    public void Board_InstancesDoNotShareSquares()
    {
        var first = new Board();
        var second = new Board();

        first.Squares[4, 1].Occupant = new Piece(99, PieceColor.White, PieceType.King);

        Assert.That(second.Squares[4, 1].Occupant, Is.Null);
    }

    [Test]
    public void Board_AllPositionsReturnsEverySquareOnce()
    {
        Position[] positions = Board.AllPositions().ToArray();

        Assert.That(positions.Length, Is.EqualTo(100));
        Assert.That(positions.Distinct().Count(), Is.EqualTo(100));

        for (int row = 0; row < Board.Size; row++)
        {
            for (int col = 0; col < Board.Size; col++)
            {
                Assert.That(positions, Does.Contain(new Position(row, col)));
            }
        }
    }

    [Test]
    public void Board_NumberingMatchesGameControllerSquareNumbers()
    {
        var board = new Board();

        foreach (Position position in Board.AllPositions().Where(p => (p.Row + p.Col) % 2 == 1))
        {
            Assert.That(GameController.ToSquareNumber(position), Is.EqualTo(board.Squares[position.Row, position.Col].Number));
        }
    }

    private static int[] GetPieceIds(IBoard board, PieceColor color)
    {
        return Board.AllPositions()
            .Select(position => board.Squares[position.Row, position.Col].Occupant)
            .Where(piece => piece is not null && piece.Color == color)
            .Select(piece => piece!.Id)
            .OrderBy(id => id)
            .ToArray();
    }
}
