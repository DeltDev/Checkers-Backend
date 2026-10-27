using Checkers.Models;

namespace Checkers.Services;


public sealed class GameProvider : IGameProvider
{
    private readonly IGameService _games;

    public GameProvider(IGameService games)
    {
        _games = games;
    }

    public string CreateGame(string? whitePlayerName, string? blackPlayerName)
    {
        return _games.CreateGame(whitePlayerName, blackPlayerName);
    }

    public GameAccess OpenGame(string gameId)
    {
        if (string.IsNullOrEmpty(gameId))
        {
            return new GameAccess(null, "game not found");
        }

        GameController? game = _games.GetGame(gameId);
        return game is null
            ? new GameAccess(null, "game not found")
            : new GameAccess(game, null);
    }

    public PlayerAccess FindPlayer(GameController game, int playerId)
    {
        IPlayer? player = game.GetPlayers().FirstOrDefault(p => p.Id == playerId);
        return player is null
            ? new PlayerAccess(null, "player not in this game")
            : new PlayerAccess(player, null);
    }
}
