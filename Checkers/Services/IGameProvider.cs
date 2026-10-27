using Checkers.Models;

namespace Checkers.Services;

public interface IGameProvider
{

    string CreateGame(string? whitePlayerName, string? blackPlayerName);

    GameAccess OpenGame(string gameId);

    PlayerAccess FindPlayer(GameController game, int playerId);
}
