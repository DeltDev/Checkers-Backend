using Checkers.Models;

namespace Checkers.Services;

public readonly record struct GameAccess(GameController? Game, string? Error)
{
    public bool IsReady => Game is not null;
}

public readonly record struct PlayerAccess(IPlayer? Player, string? Error)
{
    public bool IsReady => Player is not null;
}
