using Checkers.Models;

namespace Checkers.Contracts.Responses;

public class MoveResponse
{
    public Position From { get; set; }

    public Position To { get; set; }

    public Position[] Path { get; set; } = Array.Empty<Position>();

    public int[] CapturedPieceIds { get; set; } = Array.Empty<int>();

    public MoveType Type { get; set; }

    public PieceColor Color { get; set; }

    public string Notation { get; set; } = string.Empty;
}
