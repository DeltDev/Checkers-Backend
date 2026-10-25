using Checkers.Contracts.Responses;
using Checkers.Models;

namespace Checkers.Mapping;

public static class GameStateMapper
{
    public static GameStateResponse ToResponse(this GameController game)
    {
        GameStateResponse response = new()
        {
            Turn = game.Turn,
            TurnColor = game.TurnColor,
            Status = game.Status,
            WinnerId = game.GetWinner()?.Id,
          
        };

        foreach (IPlayer player in game.GetPlayers())
        {
            response.Players.Add(player.ToResponse());
        }

        ISquare[,] squares = game.GetSquares();
        for (int row = 0; row < squares.GetLength(0); row++)
        {
            for (int col = 0; col < squares.GetLength(1); col++)
            {
                response.Squares.Add(squares[row, col].ToResponse());
            }
        }

        foreach (Move move in game.History)
        {
            response.History.Add(move.ToResponse());
        }

        return response;
    }

    public static MoveResponse ToResponse(this Move move)
    {
        return new MoveResponse
        {
            From = move.From,
            To = move.To,
            Path = move.Path,
            CapturedPieceIds = move.Captured.Select(victim => victim.Id).ToArray(),
            Type = move.Type,
            Color = move.Color,
            Notation = move.ToNotation(),
        };
    }

    public static PlayerResponse ToResponse(this IPlayer player)
    {
        return new PlayerResponse
        {
            Id = player.Id,
            Name = player.Name,
            Color = player.Color,
        };
    }

    public static SquareResponse ToResponse(this ISquare square)
    {
        SquareResponse response = new()
        {
            Position = square.Position,
            Number = square.Number,
            IsDark = square.IsDark,
        };

        IPiece? occupant = square.Occupant;
        if (occupant is not null)
        {
            response.OccupantId = occupant.Id;
            response.OccupantColor = occupant.Color;
            response.OccupantType = occupant.Type;
        }

        return response;
    }
}

