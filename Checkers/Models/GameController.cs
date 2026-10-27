using System.Diagnostics.CodeAnalysis;


namespace Checkers.Models;

public class GameController
{
    private int [][] _diagonalPairs =
    {
        new int[] {1,-1},
        new int[] {1,1},
        new int[] {-1,1},
        new int[] {-1,-1}
    };
    public static int MaxKingOnlyStreak = 25;

    private List<IPlayer> _players;
    private readonly IBoardFactory _boardFactory;
    private readonly IPieceFactory _pieceFactory;
    private Dictionary<string, int> _positionCounts = new();
    private int _kingOnlyStreak;
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
        : this(white, black, new BoardFactory(new PieceFactory()), new PieceFactory())
    {
    }

    public GameController(
        IPlayer white,
        IPlayer black,
        IBoardFactory boardFactory,
        IPieceFactory pieceFactory)
    {
        ArgumentNullException.ThrowIfNull(white);
        ArgumentNullException.ThrowIfNull(black);
        ArgumentNullException.ThrowIfNull(boardFactory);
        ArgumentNullException.ThrowIfNull(pieceFactory);

        _boardFactory = boardFactory;
        _pieceFactory = pieceFactory;

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

    public void Reset()
    {
        // Board comes from outside: the factory builds the grid AND places
        // the starting pieces (via IPieceFactory), never "new Board" here.
        Board = _boardFactory.CreateBoard();
        Turn = 1;
        TurnColor = PieceColor.White;
        Status = GameStatus.Ongoing;
        
        History.Clear();
        DrawOfferedBy.Clear();
        _positionCounts.Clear();
        _drawOffers.Clear();
        _kingOnlyStreak = 0;

 
        _positionCounts[BuildPositionKey()] = 1;
    }

    public bool MakeMove(IPlayer player, Move move)
    {
        if (Status != GameStatus.Ongoing)
        {
            return false;
        }

        if (!HasPlayer(player))
        {
            return false;
        }

        if (player.Color != TurnColor)
        {
            return false;
        }

        List<Move> legalMoves = GetLegalMoves(player);
        Move? canonicalMove = FindLegalMove(legalMoves, move);

        if (canonicalMove == null)
        {
            return false;
        }

        ApplyMove(canonicalMove.Value);
        UpdateBookkeeping(canonicalMove.Value);

        RaiseOnMoveMade(player, canonicalMove.Value);
        RaiseOnTurnChanged(Turn);


        CheckEndConditions();

        return true;
    }

    public void PlacePiece(Position pos, IPiece piece)
    {
        ArgumentNullException.ThrowIfNull(piece);

        if (!IsPlayable(pos))
        {
            return;
        }

        ISquare square = Board.Squares[pos.Row, pos.Col];
        if (square.Occupant is not null)
        {
            return;
        }

        square.Occupant = piece;
        return;
    }

    public void RemovePiece(Position pos)
    {
        if (!IsPlayable(pos))
        {
            return;
        }
        
        ISquare square = Board.Squares[pos.Row, pos.Col];
        square.Occupant = null;
    }

    public void Resign(IPlayer player)
    {
        if (Status!= GameStatus.Ongoing)
        {
            return;
        }

        if (!HasPlayer(player))
        {
            return;
        }

        if (player.Color == PieceColor.White)
        {
            EndGame(GameStatus.BlackWon);
        }
        else
        {
            EndGame(GameStatus.WhiteWon);
        }
    }

    public bool OfferDraw(IPlayer player)
    {
        if (Status != GameStatus.Ongoing)
        {
            return false;
        }

        if (!HasPlayer(player))
        {
            return false;
        }

        if (DrawOfferedBy.Any(offered => offered.Id == player.Id))
        {
            return false;
        }

        DrawOfferedBy.Add(player);
        _drawOffers[player.Id] = true;

        if (_drawOffers.Count == 2)
        {
            EndGame(GameStatus.Draw);
        }

        return true;
    }

    // getters
    public List<Move> GetLegalMoves(IPlayer player)
    {
        if (!HasPlayer(player) || Status != GameStatus.Ongoing || player.Color != TurnColor)
        {
            return new List<Move>();
        }

        return GenerateMoves(player.Color);
    }

    public ISquare[,] GetSquares()
    {
        return (ISquare[,])Board.Squares.Clone();
    }

    public List<IPlayer> GetPlayers()
    {
        return (_players);
    }

    public ISquare? GetSquare(Position pos)
    {
        if (pos.Row < 0 || pos.Row >= Models.Board.Size || pos.Col < 0 || pos.Col >= Models.Board.Size)
        {
            return null;
        }
        return Board.Squares[pos.Row, pos.Col];
    }

    /// <summary>True when the position is a playable square with no piece on
    /// it (plan phase-5 API used by the tests; bounds-safe).</summary>
    public bool IsSquareEmpty(Position pos)
    {
        return IsPlayable(pos) && Board.Squares[pos.Row, pos.Col].Occupant is null;
    }

    public IPlayer? GetWinner()
    {
        if (Status == GameStatus.WhiteWon)
        {
            return _players.FirstOrDefault(player => player.Color == PieceColor.White);
        }

        if (Status == GameStatus.BlackWon)
        {
            return _players.FirstOrDefault(player => player.Color == PieceColor.Black);
        }

        return null;
    }

    public static bool IsPlayable(Position p)
    {
        return p.Row >= 0 && p.Row < Models.Board.Size  
                          && p.Col >= 0 && p.Col < Models.Board.Size
                          && (p.Row + p.Col) % 2 == 1;
    }
    

    private bool HasPlayer(IPlayer? player)
    {
        return player is not null && _players.Exists(p => p.Id == player.Id);
    }

    // helpers
    public static SquareNumber ToSquareNumber(Position p)
    {
        return (SquareNumber)(p.Row * (Models.Board.Size / 2) + p.Col / 2 + 1);
    }
    
    private List<Move> GenerateMoves(PieceColor pieceColor)
    {
        List<Move> captures = GenerateCaptureMoves(pieceColor);

        if (captures.Count > 0)
        {
            int maxCount = 0;
            foreach (Move move in captures)
            {
                maxCount = Math.Max(maxCount, move.Captured.Length);
            }
            return captures.Where(capture => capture.Captured.Length == maxCount).ToList();
        }

        return GenerateQuietMoves(pieceColor);
    }

    private  List<Move> GenerateQuietMoves(PieceColor pieceColor)
    {
        List<Move> moves = new();

        foreach (Position p in Models.Board.AllPositions())
        {
            if (!IsPlayable(p))
            {
                continue;
            }
            
            IPiece? piece = Board.Squares[p.Row, p.Col].Occupant;

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

                    if (IsPlayable(to) && Board.Squares[to.Row, to.Col].Occupant is null)
                    {
                        moves.Add(new Move(p,to,null,null,MoveType.Quiet, pieceColor, piece.Type));
                    }
                }
            }
            else
            {
                foreach (int[] diagonalPair in _diagonalPairs)
                {
                    Position q = new Position(p.Row + diagonalPair[0], p.Col + diagonalPair[1]);

                    while (IsPlayable(q) && Board.Squares[q.Row, q.Col].Occupant is null)
                    {
                        moves.Add(new Move(p,q,null,null,MoveType.Quiet, pieceColor, piece.Type));
                        q = new Position(q.Row + diagonalPair[0], q.Col + diagonalPair[1]);
                    }
                }
            }
        }

        return moves;
    }

    private List<Move> GenerateCaptureMoves(PieceColor pieceColor)
    {
        List<Move> result = new();
        IPiece?[,] pieces = ClonePieces(Board);
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
        List<CaptureJump> jumps = GetJumps(pieces, flags, current, piece);

        foreach (CaptureJump jump in jumps)
        {

            pieces[current.Row, current.Col] = null;
            pieces[jump.Landing.Row, jump.Landing.Col] = piece;
            flags[jump.Victim.Row, jump.Victim.Col] = true; 
            captured.Add(pieces[jump.Victim.Row, jump.Victim.Col]!);
            landings.Add(jump.Landing);


            SearchCaptures(pieces, flags, from, jump.Landing, piece, landings, captured, result);

            captured.RemoveAt(captured.Count - 1);
            landings.RemoveAt(landings.Count - 1);
            flags[jump.Victim.Row, jump.Victim.Col] = false;
            pieces[jump.Landing.Row, jump.Landing.Col] = null;
            pieces[current.Row, current.Col] = piece;
        }

        if (jumps.Count == 0 && captured.Count > 0)
        {
            Position to = landings[^1];
            Position[] path = landings.Take(landings.Count - 1).ToArray();


            result.Add(new Move(from, to, path, captured.ToArray(), MoveType.Capture, piece.Color, piece.Type));
        }
    }

    private List<CaptureJump> GetJumps(IPiece?[,] pieces, bool[,] flags, Position current, IPiece piece)
    {
        List<CaptureJump> jumps = new();

        foreach (int[] diagonalPair in _diagonalPairs)
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
        int rowStep, int colStep, List<CaptureJump> jumps) //todo: buat class baru
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

        jumps.Add(new CaptureJump{Landing = landing, Victim = victim});
    }

    private static void AddKingJumps(IPiece?[,] pieces, bool[,] flags, Position current, IPiece piece,
        int rowStep, int colStep, List<CaptureJump> jumps)
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
                    jumps.Add(new CaptureJump{Landing = landing, Victim = square});
                    landing = new Position(landing.Row + rowStep, landing.Col + colStep);
                }

                return;
            }

            square = new Position(square.Row + rowStep, square.Col + colStep);
        }
    }


    private Move? FindLegalMove(List<Move> legalMoves, Move reqMove)
    {
        foreach (Move candidate in legalMoves)
        {
            if (candidate.From != reqMove.From)
            {
                continue;
            }

            if (candidate.To != reqMove.To)
            {
                continue;
            }


            if (reqMove.Path.Length > 0 && !candidate.Path.SequenceEqual(reqMove.Path))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private void ApplyMove(Move move)
    {
        
        IPiece movingPiece = GetSquare(move.From)!.Occupant!;

        RemovePiece(move.From);

        if (move.Type == MoveType.Capture)
        {
           
            Position victimPosition = default;
            foreach (IPiece victim in move.Captured)
            {

                foreach (ISquare square in Board.Squares)
                {

                    if (square.Occupant != null && square.Occupant.Id == victim.Id) 
                    {
                        victimPosition = square.Position;
                        break;
                    }
                }
                
                RemovePiece(victimPosition);
            }
        }

        IPiece? placed;
        if (IsPromotion(move))
        {
            // Crown created through the injected piece factory - the only
            // place "new Piece" may exist is PieceFactory itself.
            placed = _pieceFactory.CreateKing(movingPiece.Id, movingPiece.Color);
        }
        else
        {
            placed = movingPiece;
        }

        PlacePiece(move.To, placed);
    }

    private void UpdateBookkeeping(Move move)
    {
        History.Add(move);
        Turn++;
        TurnColor = TurnColor == PieceColor.Black ? PieceColor.White : PieceColor.Black;

        string key = BuildPositionKey();
        
        _positionCounts[key] = _positionCounts.GetValueOrDefault(key) + 1;

        if (move.Type == MoveType.Capture || move.PieceType == PieceType.Man)
        {
            _kingOnlyStreak = 0;
        }
        else
        {
            _kingOnlyStreak++;
        }
        
        DrawOfferedBy.Clear();
        _drawOffers.Clear();
    }

    private string BuildPositionKey()
    {
        List<string> parts = new List<string>();
        
        IPiece? occupant;
        foreach (Position p in Models.Board.AllPositions())
        {
            if (!IsPlayable(p))
            {
                continue;
            }

            occupant = Board.Squares[p.Row, p.Col].Occupant;
            if (occupant == null)
            {
                parts.Add("e");
            }
            else
            {
                parts.Add(occupant.Id.ToString());
            }
        }

        
        parts.Add(TurnColor.ToString());

        return string.Join("|", parts);
    }

    private void RaiseOnMoveMade(IPlayer player, Move move)
    {
        Action<IPlayer, Move>? handlers = OnMoveMade;
        if (handlers is null)
        {
            return;
        }

        foreach (var @delegate in handlers.GetInvocationList())
        {
            var handler = (Action<IPlayer, Move>)@delegate;
            handler(player, move);
        }
    }

    private void RaiseOnTurnChanged(int turn)
    {
        Action<int>? handlers = OnTurnChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (var @delegate in handlers.GetInvocationList())
        {
            var handler = (Action<int>)@delegate;

            handler(turn);
        }
    }

    private void EndGame(GameStatus finalStatus)
    {
        if (Status != GameStatus.Ongoing )
        {
            return;
        }

        Status = finalStatus;
        Action<GameStatus>? handlers = OnGameEnded;
        if (handlers is null)
        {
            return;
        }
        foreach (var @delegate in handlers.GetInvocationList())
        {
            var handler = (Action<GameStatus>)@delegate;
            handler(finalStatus);
        }
    }

    private void CheckEndConditions()
    {

        IPlayer playerToMove = _players.First(player => player.Color == TurnColor);
        IPlayer winner = _players.First(player => player.Color != TurnColor);

        if (CountPieces(TurnColor) == 0)
        {
            EndGame(winner.Color == PieceColor.Black ? GameStatus.BlackWon : GameStatus.WhiteWon);
            return;
        }

        List<Move> legalMoves = GetLegalMoves(playerToMove);
        if (legalMoves.Count == 0)
        {
            EndGame(winner.Color == PieceColor.Black ? GameStatus.BlackWon : GameStatus.WhiteWon);
            return;
        }

        if (_positionCounts.GetValueOrDefault(BuildPositionKey()) >= 3)
        {
            EndGame(GameStatus.Draw);
            return;
        }

        if (_kingOnlyStreak >= 2 * MaxKingOnlyStreak)
        {
            EndGame(GameStatus.Draw);
        }
    }


    private int CountPieces(PieceColor color)
    {
        int count = 0;

        foreach (Position p in Models.Board.AllPositions())
        {
            if (!IsPlayable(p))
            {
                continue;
            }

            IPiece? occupant = Board.Squares[p.Row, p.Col].Occupant;

            if (occupant is not null && occupant.Color == color)
            {
                count++;
            }
        }

        return count;
    }
    
    private static bool IsCapture(Move move)
    {
        return move.Type == MoveType.Capture;
    }

    private static bool IsPromotion(Move move)
    {

        if (move.PieceType != PieceType.Man)
        {
            return false;
        }

        if (move.Color == PieceColor.White)
        {
            return move.To.Row == 0;
        }

        if (move.Color == PieceColor.Black)
        {
            return move.To.Row == Models.Board.Size-1;
        }
        return false;
    }

    public static string ToNotation(Move move)
    {
        bool isCapture = IsCapture(move);
        string separator = isCapture ? "x" : ":";

        List<int> outputSquares = new(){(int) ToSquareNumber(move.From)};

        if (isCapture)
        {
            foreach (Position step in move.Path)
            {
                if (step == move.From || step == move.To)
                {
                    continue;
                }
                outputSquares.Add((int) ToSquareNumber(step));
            }
            
            
        }
        
        outputSquares.Add((int) ToSquareNumber(move.To));
            
        return string.Join(separator, outputSquares.Select(number => $"[{number}]"));

    } }
