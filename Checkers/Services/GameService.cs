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
        throw new NotImplementedException();
    }

    public GameController? GetGame(string gameId)
    {
        throw new NotImplementedException();
    }
}
