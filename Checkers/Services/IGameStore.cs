using Checkers.Models;

namespace Checkers.Services;

public interface IGameStore
{
    void Add(string gameId, GameController game);

    GameController? Get(string gameId);
}
