using Checkers.Models;

namespace Checkers.Services;

public class GameFactory : IGameFactory
{
    public GameController Create(string? whitePlayerName, string? blackPlayerName)
    {
        Player white = new(1, NormalizeName(whitePlayerName, "Player 1"), PieceColor.White);
        Player black = new(2, NormalizeName(blackPlayerName, "Player 2"), PieceColor.Black);

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

