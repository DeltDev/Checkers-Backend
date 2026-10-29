using Checkers.Models;

namespace Checkers.Tests;

[TestFixture]
public class GameControllerTests
{
    [Test]
    public void Constructor_CreatesTheInitialState()
    {
        var (game, _, _) = CreateGame();

        Assert.That(game.Board, Is.Not.Null);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.Turn, Is.EqualTo(1));
        Assert.That(game.TurnColor, Is.EqualTo(PieceColor.White));
        Assert.That(game.History, Is.Empty);
        Assert.That(game.DrawOfferedBy, Is.Empty);
    }

    [Test]
    public void Constructor_KeepsTheProvidedPlayersInOrder()
    {
        var (game, white, black) = CreateGame();

        List<IPlayer> players = game.GetPlayers();

        Assert.That(players.Count, Is.EqualTo(2));
        Assert.That(players[0], Is.SameAs(white));
        Assert.That(players[1], Is.SameAs(black));
    }

    [Test]
    public void Constructor_ThrowsWhenAPlayerIsNull()
    {
        var white = new Player(1, "White", PieceColor.White);
        var black = new Player(2, "Black", PieceColor.Black);

        Assert.Throws<ArgumentNullException>(() => new GameController(null!, black));
        Assert.Throws<ArgumentNullException>(() => new GameController(white, null!));
    }

    [Test]
    public void Constructor_ThrowsWhenTheFirstPlayerIsNotWhite()
    {
        var wrongWhite = new Player(1, "One", PieceColor.Black);
        var black = new Player(2, "Black", PieceColor.Black);

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => new GameController(wrongWhite, black))!;

        Assert.That(error.ParamName, Is.EqualTo("white"));
    }

    [Test]
    public void Constructor_ThrowsWhenTheSecondPlayerIsNotBlack()
    {
        var white = new Player(1, "White", PieceColor.White);
        var wrongBlack = new Player(2, "Two", PieceColor.White);

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => new GameController(white, wrongBlack))!;

        Assert.That(error.ParamName, Is.EqualTo("black"));
    }

    [Test]
    public void Constructor_ThrowsWhenBothPlayersShareAnId()
    {
        var white = new Player(7, "White", PieceColor.White);
        var black = new Player(7, "Black", PieceColor.Black);

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => new GameController(white, black))!;

        Assert.That(error.ParamName, Is.EqualTo("black"));
    }

    [Test]
    public void Constructor_PlacesTwentyPiecesPerColor()
    {
        var (game, _, _) = CreateGame();

        IPiece[] pieces = Board.AllPositions()
            .Select(position => game.GetSquare(position)!.Occupant)
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
    public void Constructor_PlacesBlackOnRowsZeroToThreeAndWhiteOnRowsSixToNine()
    {
        var (game, _, _) = CreateGame();

        for (int row = 0; row < Board.Size; row++)
        {
            for (int col = 0; col < Board.Size; col++)
            {
                ISquare square = game.GetSquare(new Position(row, col))!;
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
    public void Reset_RestoresTheInitialPositionAndClearsEverything()
    {
        var (game, white, black) = CreateGame();

        game.RemovePiece(new Position(0, 1));
        game.PlacePiece(
            new Position(4, 1),
            new Piece(99, PieceColor.White, PieceType.King));
        game.History.Add(new Move(
            new Position(6, 1),
            new Position(5, 0),
            null,
            null,
            MoveType.Quiet,
            PieceColor.White,
            PieceType.Man));
        game.DrawOfferedBy.Add(white);

        game.Reset();

        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.Turn, Is.EqualTo(1));
        Assert.That(game.TurnColor, Is.EqualTo(PieceColor.White));
        Assert.That(game.History, Is.Empty);
        Assert.That(game.DrawOfferedBy, Is.Empty);

        // Back to a black man on (0,1) and an empty (4,1).
        Assert.That(game.GetSquare(new Position(0, 1))!.Occupant!.Color, Is.EqualTo(PieceColor.Black));
        Assert.That(game.GetSquare(new Position(0, 1))!.Occupant!.Type, Is.EqualTo(PieceType.Man));
        Assert.That(game.GetSquare(new Position(4, 1))!.Occupant, Is.Null);

        // Same player objects, same order.
        Assert.That(game.GetPlayers()[0], Is.SameAs(white));
        Assert.That(game.GetPlayers()[1], Is.SameAs(black));
    }

    [Test]
    public void Reset_LeavesTheOldBoardUnreachableFromTheGame()
    {
        var (game, _, _) = CreateGame();

        // Reset builds a fresh Board, so squares captured beforehand no longer
        // belong to the game and mutating them must not affect it.
        ISquare staleSquare = game.GetSquare(new Position(0, 1))!;

        game.Reset();

        staleSquare.Occupant = null;

        Assert.That(game.GetSquare(new Position(0, 1))!.Occupant, Is.Not.Null);
    }

    [Test]
    public void GetPlayers_ReturnsTheLiveListInConstructorOrder()
    {
        var (game, white, black) = CreateGame();

        List<IPlayer> players = game.GetPlayers();

        // Final API: the controller hands out its own list (no defensive copy),
        // so the mapping path allocates nothing. Callers treat it read-only.
        Assert.That(game.GetPlayers(), Is.SameAs(players));
        Assert.That(players, Is.EqualTo(new IPlayer[] { white, black }));
    }

    [Test]
    public void GetSquares_ReturnsACopyOfTheSquareArray()
    {
        var (game, _, _) = CreateGame();

        ISquare[,] squares = game.GetSquares();

        Assert.That(squares, Is.Not.SameAs(game.Board.Squares));
        Assert.That(squares.GetLength(0), Is.EqualTo(Board.Size));
        Assert.That(squares.GetLength(1), Is.EqualTo(Board.Size));

        // Replacing an array element in the copy must not replace the
        // controller's square. (The Square objects themselves are shared
        // references; mutating Occupant through them does affect the game.)
        squares[0, 1] = new Square(new Position(0, 1), SquareNumber.Square1, true);

        Assert.That(game.GetSquare(new Position(0, 1)), Is.SameAs(game.Board.Squares[0, 1]));
    }

    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    [TestCase(10, 0)]
    [TestCase(0, 10)]
    public void GetSquare_ReturnsNullOutsideTheBoard(int row, int col)
    {
        var (game, _, _) = CreateGame();

        Assert.That(game.GetSquare(new Position(row, col)), Is.Null);
    }

    [Test]
    public void GetSquare_ReturnsLightAndDarkSquares()
    {
        var (game, _, _) = CreateGame();

        ISquare? light = game.GetSquare(new Position(0, 0));
        ISquare? dark = game.GetSquare(new Position(0, 1));

        Assert.That(light, Is.Not.Null);
        Assert.That(light!.IsDark, Is.False);

        Assert.That(dark, Is.Not.Null);
        Assert.That(dark!.IsDark, Is.True);
    }

    [TestCase(4, 1, true)]   // empty dark square
    [TestCase(0, 1, false)]  // occupied dark square
    [TestCase(0, 0, false)]  // not playable, so never "empty"
    [TestCase(-1, 1, false)] // outside the board counts as not empty
    [TestCase(10, 1, false)]
    public void IsSquareEmpty_ReflectsOccupancyAndBounds(int row, int col, bool expected)
    {
        var (game, _, _) = CreateGame();

        Assert.That(game.IsSquareEmpty(new Position(row, col)), Is.EqualTo(expected));
    }

    [TestCase(0, 1, true)]
    [TestCase(0, 0, false)]
    [TestCase(9, 8, true)]
    [TestCase(9, 9, false)]
    [TestCase(-1, 1, false)]
    [TestCase(10, 1, false)]
    [TestCase(0, -1, false)]
    [TestCase(0, 10, false)]
    public void IsPlayable_RequiresAnInBoundsDarkSquare(int row, int col, bool expected)
    {
        Assert.That(GameController.IsPlayable(new Position(row, col)), Is.EqualTo(expected));
    }

    [Test]
    public void PlacePiece_PutsAPieceOnAnEmptyPlayableSquare()
    {
        var (game, _, _) = CreateGame();
        var piece = new Piece(99, PieceColor.White, PieceType.King);

        game.PlacePiece(new Position(4, 1), piece);
        Assert.That(game.GetSquare(new Position(4, 1))!.Occupant, Is.SameAs(piece));
    }

    [Test]
    public void PlacePiece_RejectsOccupiedSquares()
    {
        var (game, _, _) = CreateGame();
        IPiece? original = game.GetSquare(new Position(0, 1))!.Occupant;

        game.PlacePiece(
            new Position(0, 1),
            new Piece(99, PieceColor.White, PieceType.King));

       
        Assert.That(game.GetSquare(new Position(0, 1))!.Occupant, Is.SameAs(original));
    }

    [TestCase(0, 0)]   // light square
    [TestCase(-1, 1)]  // row out of bounds
    [TestCase(10, 1)]  // row out of bounds
    public void PlacePiece_RejectsNonPlayableSquares(int row, int col)
    {
        var (game, _, _) = CreateGame();
        var piece = new Piece(99, PieceColor.White, PieceType.King);

        game.PlacePiece(new Position(row, col), piece);

        // The piece is dropped: an in-bounds light square stays empty, and
        // out-of-bounds coordinates stay unreachable through GetSquare.
        Assert.That(game.GetSquare(new Position(row, col))?.Occupant, Is.Null);
    }

    [Test]
    public void PlacePiece_ThrowsWhenThePieceIsNull()
    {
        var (game, _, _) = CreateGame();

        Assert.Throws<ArgumentNullException>(
            () => game.PlacePiece(new Position(4, 1), null!));
    }

    [Test]
    public void RemovePiece_ClearsTheSquare()
    {
        var (game, _, _) = CreateGame();

        game.RemovePiece(new Position(0, 1));

        Assert.That(game.GetSquare(new Position(0, 1))!.Occupant, Is.Null);
    }

    [TestCase(4, 1)]   // empty dark square
    [TestCase(0, 0)]   // light square
    [TestCase(-1, 1)]  // out of bounds
    [TestCase(10, 1)]  // out of bounds
    public void RemovePiece_IsANoOpWhenThereIsNothingToRemove(int row, int col)
    {
        var (game, _, _) = CreateGame();
        int piecesBefore = CountPieces(game);

        game.RemovePiece(new Position(row, col));

        Assert.That(CountPieces(game), Is.EqualTo(piecesBefore));
        Assert.That(game.GetSquare(new Position(row, col))?.Occupant, Is.Null);
    }

    [TestCase(0, 1, SquareNumber.Square1)]
    [TestCase(0, 9, SquareNumber.Square5)]
    [TestCase(1, 0, SquareNumber.Square6)]
    [TestCase(6, 1, SquareNumber.Square31)]
    [TestCase(9, 8, SquareNumber.Square50)]
    public void ToSquareNumber_ReturnsTheBoardNumber(int row, int col, SquareNumber expected)
    {
        Assert.That(GameController.ToSquareNumber(new Position(row, col)), Is.EqualTo(expected));
    }


    private static int CountPieces(GameController game)
    {
        return Board.AllPositions()
            .Count(position => game.GetSquare(position)!.Occupant is not null);
    }

    private static (GameController Game, Player White, Player Black) CreateGame()
    {
        var white = new Player(1, "White", PieceColor.White);
        var black = new Player(2, "Black", PieceColor.Black);

        return (new GameController(white, black), white, black);
    }
}
