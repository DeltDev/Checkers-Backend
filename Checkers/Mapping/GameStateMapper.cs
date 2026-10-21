using Checkers.Contracts.Responses;
using Checkers.Models;

namespace Checkers.Mapping;

public static class GameStateMapper
{
    public static GameStateResponse ToResponse(this GameController game)
    {
        throw new NotImplementedException();
    }

    public static MoveResponse ToResponse(this Move move)
    {
        throw new NotImplementedException();
    }

    public static PlayerResponse ToResponse(this IPlayer player)
    {
        throw new NotImplementedException();
    }

    public static SquareResponse ToResponse(this ISquare square)
    {
        throw new NotImplementedException();
    }
}
