using Checkers.Models;

namespace Checkers.Services;

public class InMemoryGameStore : IGameStore
{
    private readonly Dictionary<string, GameController> _games = new();

    public void Add(string gameId, GameController game)
    {
        throw new NotImplementedException();
    }

    public GameController? Get(string gameId)
    {
        throw new NotImplementedException();
    }
}
