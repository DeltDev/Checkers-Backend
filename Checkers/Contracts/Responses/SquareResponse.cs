using Checkers.Models;

namespace Checkers.Contracts.Responses;

public class SquareResponse
{
    public Position Position { get; set; }

    public SquareNumber? Number { get; set; }

    public bool IsDark { get; set; }

    public int? OccupantId { get; set; }

    public PieceColor? OccupantColor { get; set; }

    public PieceType? OccupantType { get; set; }
}
