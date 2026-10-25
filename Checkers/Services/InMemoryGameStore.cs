using Checkers.Models;

namespace Checkers.Services;

public class InMemoryGameStore : IGameStore
{
    private readonly Dictionary<string, GameController> _games = new();
    private readonly object _sync = new ();
    public void Add(string gameId, GameController game)
    {
        lock (_sync)
        {
            if (_games.ContainsKey(gameId))
            {
                throw new ArgumentException("duplicate game id", nameof(gameId));
            }

            _games[gameId] = game;
        }
    }

    public GameController? Get(string gameId)
    {
        if (gameId is null)
        {
            return null;
        }
        lock (_sync)
        {
            return _games.GetValueOrDefault(gameId);
        }
    }
}
