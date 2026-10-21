using Checkers.Models;

namespace Checkers.Services;

public interface IGameFactory
{
    GameController Create(string? whitePlayerName, string? blackPlayerName);
}
