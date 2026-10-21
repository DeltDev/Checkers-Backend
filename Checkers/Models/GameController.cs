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
        throw new NotImplementedException();
    }

    // mutations
    public void Reset()
    {
        throw new NotImplementedException();
    }

    public bool MakeMove(IPlayer player, Move move)
    {
        throw new NotImplementedException();
    }

    public bool PlacePiece(Position pos, IPiece piece)
    {
        throw new NotImplementedException();
    }

    public IPiece? RemovePiece(Position pos)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    public List<IPlayer> GetPlayers()
    {
        throw new NotImplementedException();
    }

    public ISquare? GetSquare(Position pos)
    {
        throw new NotImplementedException();
    }

    public IPlayer? GetWinner()
    {
        throw new NotImplementedException();
    }

    public static bool IsPlayable(Position p)
    {
        throw new NotImplementedException();
    }

    public bool IsSquareEmpty(Position pos)
    {
        throw new NotImplementedException();
    }

    private bool HasPlayer(IPlayer player)
    {
        throw new NotImplementedException();
    }

    // helpers
    public static SquareNumber ToSquareNumber(Position p)
    {
        throw new NotImplementedException();
    }

    public static Position? FromSquareNumber(SquareNumber n)
    {
        throw new NotImplementedException();
    }
}
