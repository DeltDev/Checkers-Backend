using System.Diagnostics.CodeAnalysis;

namespace Checkers.Models;

public class GameController
{
    private int [][] diagonalPairs =
    {
        new int[] {1,-1},
        new int[] {1,1},
        new int[] {-1,1},
        new int[] {-1,-1}
    };
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
        if (!HasPlayer(player) || Status != GameStatus.Ongoing || player.Color != TurnColor)
        {
            return new List<Move>();
        }

        return Generate(Board, player.Color);
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
    
    private List<Move> Generate(IBoard board, PieceColor pieceColor)
    {
        List<Move> captures = GenerateCaptureMoves(board, pieceColor);

        if (captures.Count > 0)
        {
            int maxCount = 0;
            foreach (Move move in captures)
            {
                maxCount = Math.Max(maxCount, move.Captured.Length);
            }
            return captures.Where(capture => capture.Captured.Length == maxCount).ToList();
        }

        return GenerateQuietMoves(board, pieceColor);
    }

    private  List<Move> GenerateQuietMoves(IBoard board, PieceColor pieceColor)
    {
        List<Move> moves = new();

        foreach (Position p in Models.Board.AllPositions())
        {
            if (!IsPlayable(p))
            {
                continue;
            }
            
            IPiece? piece = board.Squares[p.Row, p.Col].Occupant;

            if (piece == null || piece.Color != pieceColor)
            {
                continue;
            }

            if (piece.Type == PieceType.Man)
            {
                int rowStep = piece.Color == PieceColor.White ? -1 : 1;

                foreach (int colStep in (int[])[-1,1])
                {
                    Position to = new Position(p.Row + rowStep, p.Col + colStep);

                    if (IsPlayable(to) && board.Squares[to.Row, to.Col].Occupant is null)
                    {
                        moves.Add(new Move(p,to,null,null,MoveType.Quiet, pieceColor, piece.Type));
                    }
                }
            }
            else
            {
                foreach (int[] diagonalPair in diagonalPairs)
                {
                    Position q = new Position(p.Row + diagonalPair[0], p.Col + diagonalPair[1]);

                    while (IsPlayable(q) && board.Squares[q.Row, q.Col].Occupant is null)
                    {
                        moves.Add(new Move(p,q,null,null,MoveType.Quiet, pieceColor, piece.Type));
                        q = new Position(q.Row + diagonalPair[0], q.Col + diagonalPair[1]);
                    }
                }
            }
        }

        return moves;
    }

    private List<Move> GenerateCaptureMoves(IBoard board, PieceColor pieceColor)
    {
        List<Move> result = new();
        IPiece?[,] pieces = ClonePieces(board);
        bool[,] flags = new bool[Models.Board.Size, Models.Board.Size];

        foreach (Position p in Models.Board.AllPositions())
        {
            if (!IsPlayable(p))
            {
                continue;
            }

            IPiece? piece = pieces[p.Row, p.Col];

            if (piece is null || piece.Color != pieceColor)
            {
                continue;
            }


            SearchCaptures(pieces, flags, p, p, piece, new List<Position>(), new List<IPiece>(), result);
        }

        return result;
    }

    private static IPiece?[,] ClonePieces(IBoard board)
    {
        IPiece? [,] result = new IPiece?[Models.Board.Size, Models.Board.Size];
        for (int i = 0; i < Models.Board.Size; i++)
        {
            for (int j = 0; j < Models.Board.Size; j++)
            {
                result[i, j] = board.Squares[i, j].Occupant;
            }
        }

        return result;
    }

    private void SearchCaptures(IPiece?[,] pieces, bool[,] flags, Position from, Position current,
        IPiece piece, List<Position> landings, List<IPiece> captured, List<Move> result)
    {
        List<(Position Victim, Position Landing)> jumps = GetJumps(pieces, flags, current, piece);

        foreach ((Position victim, Position landing) in jumps)
        {

            pieces[current.Row, current.Col] = null;
            pieces[landing.Row, landing.Col] = piece;
            flags[victim.Row, victim.Col] = true; 
            captured.Add(pieces[victim.Row, victim.Col]!);
            landings.Add(landing);


            SearchCaptures(pieces, flags, from, landing, piece, landings, captured, result);

            captured.RemoveAt(captured.Count - 1);
            landings.RemoveAt(landings.Count - 1);
            flags[victim.Row, victim.Col] = false;
            pieces[landing.Row, landing.Col] = null;
            pieces[current.Row, current.Col] = piece;
        }

        if (jumps.Count == 0 && captured.Count > 0)
        {
            Position to = landings[^1];
            Position[] path = landings.Take(landings.Count - 1).ToArray();


            result.Add(new Move(from, to, path, captured.ToArray(), MoveType.Capture, piece.Color, piece.Type));
        }
    }

    private List<(Position Victim, Position Landing)> GetJumps(IPiece?[,] pieces, bool[,] flags, Position current, IPiece piece)
    {
        List<(Position Victim, Position Landing)> jumps = new();

        foreach (int[] diagonalPair in diagonalPairs)
        {
            int rowStep = diagonalPair[0];
            int colStep = diagonalPair[1];

            if (piece.Type == PieceType.Man)
            {
                AddManJumps(pieces, flags, current, piece, rowStep, colStep, jumps);
            }
            else
            {
                AddKingJumps(pieces, flags, current, piece, rowStep, colStep, jumps);
            }
        }

        return jumps;
    }

    private static void AddManJumps(IPiece?[,] pieces, bool[,] flags, Position current, IPiece piece,
        int rowStep, int colStep, List<(Position Victim, Position Landing)> jumps)
    {
        Position victim = new Position(current.Row + rowStep, current.Col + colStep);
        Position landing = new Position(current.Row + (2 * rowStep), current.Col + (2 * colStep));

        if (!IsPlayable(victim) || !IsPlayable(landing))
        {
            return;
        }

        IPiece? target = pieces[victim.Row, victim.Col];

        if (target is null || target.Color == piece.Color || flags[victim.Row, victim.Col])
        {
            return;
        }

        if (pieces[landing.Row, landing.Col] is not null)
        {
            return;
        }

        jumps.Add((victim, landing));
    }

    private static void AddKingJumps(IPiece?[,] pieces, bool[,] flags, Position current, IPiece piece,
        int rowStep, int colStep, List<(Position Victim, Position Landing)> jumps)
    {
        Position square = new Position(current.Row + rowStep, current.Col + colStep);

        while (IsPlayable(square))
        {
            IPiece? occupant = pieces[square.Row, square.Col];

            if (occupant is not null)
            {
                if (occupant.Color == piece.Color || flags[square.Row, square.Col])
                {
                    return;
                }

                Position landing = new Position(square.Row + rowStep, square.Col + colStep);

                while (IsPlayable(landing) && pieces[landing.Row, landing.Col] is null)
                {
                    jumps.Add((square, landing));
                    landing = new Position(landing.Row + rowStep, landing.Col + colStep);
                }

                return;
            }

            square = new Position(square.Row + rowStep, square.Col + colStep);
        }
    }
}
