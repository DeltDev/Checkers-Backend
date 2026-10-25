using Checkers.Models;

namespace Checkers.Services;

public class GameFactory : IGameFactory
{
    public GameController Create(string? whitePlayerName, string? blackPlayerName)
    {
        Player white = new Player(1, NormalizeName(whitePlayerName, "White"), PieceColor.White);
        Player black = new Player(2, NormalizeName(blackPlayerName, "Black"), PieceColor.Black);
        
        return new GameController(white, black);
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
