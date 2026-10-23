using System.Diagnostics.CodeAnalysis;

namespace Checkers.Models;

public class GameController
{
    public static int MaxKingOnlyStreak = 25;

    private List<IPlayer> _players = new();
    private Dictionary<string, int> _positionCounts = new();
    private int _kingOnlyStreak;
    private int _endgameStreak;
    private Dictionary<int, bool> _drawOffers = new();

    public int Turn { get; private set; }
    public PieceColor TurnColor { get; private set; }
    public GameStatus Status { get; private set; }
    public IBoard Board { get; private set; }
    public List<Move> History { get; private set; } = new();
    public List<IPlayer> DrawOfferedBy { get; } = new();

    public Action<int>? OnTurnChanged;
    public Action<IPlayer, Move>? OnMoveMade;
    public Action<GameStatus>? OnGameEnded;

    public GameController(IPlayer white, IPlayer black)
    {
        ArgumentNullException.ThrowIfNull(white);
        ArgumentNullException.ThrowIfNull(black);

        if (white.Color != PieceColor.White)
        {
            throw new ArgumentException("The first player must have the White color.", nameof(white));
        }

        if (black.Color != PieceColor.Black)
        {
            throw new ArgumentException("The second player must have the Black color.", nameof(black));
        }

        if (white.Id == black.Id)
        {
            throw new ArgumentException("Players must have different IDs.", nameof(black));
        }

        _players = new List<IPlayer> { white, black };
        Reset();
    }

    // mutations
    [MemberNotNull(nameof(Board))]
    public void Reset()
    {
        Board = new Board();
        Turn = 1;
        TurnColor = PieceColor.White;
        Status = GameStatus.Ongoing;
        
        History.Clear();
        DrawOfferedBy.Clear();
        _positionCounts.Clear();
        _drawOffers.Clear();
        _kingOnlyStreak = 0;
        _endgameStreak = 0;
    }

    public bool MakeMove(IPlayer player, Move move)
    {
        throw new NotImplementedException();
    }

    public bool PlacePiece(Position pos, IPiece piece)
    {
        ArgumentNullException.ThrowIfNull(piece);

        if (!IsPlayable(pos))
        {
            return false;
        }

        ISquare square = Board.Squares[pos.Row, pos.Col];
        if (square.Occupant is not null)
        {
            return false;
        }

        square.Occupant = piece;
        return true;
    }

    public IPiece? RemovePiece(Position pos)
    {
        if (!IsPlayable(pos))
        {
            return null;
        }
        
        ISquare square = Board.Squares[pos.Row, pos.Col];
        IPiece? piece = square.Occupant;
        square.Occupant = null;
        return piece;
    }

    public void Resign(IPlayer player)
    {
        throw new NotImplementedException();
    }

    public bool OfferDraw(IPlayer player)
    {
        throw new NotImplementedException();
    }

    // getters
    public List<Move> GetLegalMoves(IPlayer player)
    {
        throw new NotImplementedException();
    }

    public ISquare[,] GetSquares()
    {
        return (ISquare[,])Board.Squares.Clone();
    }

    public List<IPlayer> GetPlayers()
    {
        return new(_players);
    }

    public ISquare? GetSquare(Position pos)
    {
        if (pos.Row < 0 || pos.Row >= Models.Board.Size || pos.Col < 0 || pos.Col >= Models.Board.Size)
        {
            return null;
        }
        return Board.Squares[pos.Row, pos.Col];
    }

    public IPlayer? GetWinner()
    {
        if (Status == GameStatus.BlackWon)
        {
            _players.FirstOrDefault(player => player.Color == PieceColor.White);
        } else if (Status == GameStatus.WhiteWon)
        {
            _players.FirstOrDefault(player => player.Color == PieceColor.White);
        }

        return null;
    }

    public static bool IsPlayable(Position p)
    {
        return p.Row >= 0 && p.Row < Models.Board.Size  
                          && p.Col >= 0 && p.Col < Models.Board.Size
                          && (p.Row + p.Col) % 2 == 1;
    }

    public bool IsSquareEmpty(Position pos)
    {
        ISquare? square = GetSquare(pos);
        return square is not null && square.Occupant is null;
    }

    private bool HasPlayer(IPlayer player)
    {
        return player is not null && _players.Exists(p => p.Id == player.Id);
    }

    // helpers
    public static SquareNumber ToSquareNumber(Position p)
    {
        return (SquareNumber)(p.Row * (Models.Board.Size / 2) + p.Col / 2 + 1);
    }

    public static Position? FromSquareNumber(SquareNumber n)
    {
        int value = (int)n;

        if (value < 1 || value > Models.Board.PlayableSquares)
        {
            return null;
        }

        int row = (value - 1) / (Models.Board.Size / 2);
        int indexInRow = (value - 1) % (Models.Board.Size / 2);
        int col = (indexInRow * 2) + (row % 2 == 0 ? 1 : 0);

        return new Position(row, col);
    }
}
