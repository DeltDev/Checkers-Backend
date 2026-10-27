using Checkers.Models;

namespace Checkers.Services;

public class GameFactory : IGameFactory
{
    private readonly IBoardFactory _boardFactory;
    private readonly IPieceFactory _pieceFactory;

    /// <summary>DI constructor: uses the registered singleton factories.</summary>
    public GameFactory(IBoardFactory boardFactory, IPieceFactory pieceFactory)
    {
        _boardFactory = boardFactory;
        _pieceFactory = pieceFactory;
    }

    /// <summary>Convenience overload for tests and quick setup.</summary>
    public GameFactory()
        : this(new BoardFactory(new PieceFactory()), new PieceFactory())
    {
    }

    public GameController Create(string? whitePlayerName, string? blackPlayerName)
    {
        Player white = new(1, NormalizeName(whitePlayerName, "Player 1"), PieceColor.White);
        Player black = new(2, NormalizeName(blackPlayerName, "Player 2"), PieceColor.Black);

        return new GameController(white, black, _boardFactory, _pieceFactory);
    }

    private static string NormalizeName(string? name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        return name.Trim();
    }
}
