using Checkers.Models;

namespace Checkers.Services;

public class InMemoryGameStore : IGameStore
{
    private readonly Dictionary<string, GameController> _games = new();
    private readonly object _sync = new();

    public void Add(string gameId, GameController game)
    {
        lock (_sync)
        {
 
            if (!_games.TryAdd(gameId, game))
            {
                throw new ArgumentException("duplicate game id", nameof(gameId));
            }
        }
    }

    public GameController? Get(string gameId)
    {
        lock (_sync)
        {
            
            return _games.GetValueOrDefault(gameId);
        }
    }
}

