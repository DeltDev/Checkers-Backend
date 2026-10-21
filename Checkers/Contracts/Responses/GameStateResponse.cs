using Checkers.Models;

namespace Checkers.Contracts.Responses;

public class GameStateResponse
{
    public int Turn { get; set; }

    public PieceColor TurnColor { get; set; }

    public GameStatus Status { get; set; }

    public int? WinnerId { get; set; }

    public List<PlayerResponse> Players { get; set; } = new();

    public List<SquareResponse> Squares { get; set; } = new();

    public List<MoveResponse> History { get; set; } = new();
}
