using Checkers.Models;

namespace Checkers.Services;

public class GameService : IGameService
{
    private readonly IGameStore _store;
    private readonly IGameFactory _factory;
    private readonly IGameIdGenerator _idGenerator;

    public GameService(IGameStore store, IGameFactory factory, IGameIdGenerator idGenerator)
    {
        _store = store;
        _factory = factory;
        _idGenerator = idGenerator;
    }

    public string CreateGame(string? whitePlayerName, string? blackPlayerName)
    {
        const int maxAttempts = 10;
        string gameId = string.Empty;
        bool allocated = false;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            gameId = _idGenerator.NextId();
            if (_store.Get(gameId) is null)
            {
                allocated = true;
                break;
            }
        }

        if (!allocated)
        {
            throw new InvalidOperationException("cannot allocate a unique game id");
        }

        GameController game = _factory.Create(whitePlayerName, blackPlayerName);
        _store.Add(gameId, game); 
        return gameId;
    }

    public GameController? GetGame(string gameId)
    {
        if (string.IsNullOrEmpty(gameId))
        {
            return null; 
        }

        return _store.Get(gameId);
    }
}
