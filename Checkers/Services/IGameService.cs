using Checkers.Models;

namespace Checkers.Services;

public interface IGameService
{
    string CreateGame(string? whitePlayerName, string? blackPlayerName);

    GameController? GetGame(string gameId);
}
