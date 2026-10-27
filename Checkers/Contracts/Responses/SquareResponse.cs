using Checkers.Models;

namespace Checkers.Contracts.Responses;

/// <summary>One board square of the 10x10 grid (100 entries per game state).</summary>
public class SquareResponse
{
    /// <summary>0-based {row, col}; row 0 is Black's back rank, row 9 White's.</summary>
    public Position Position { get; set; }

    /// <summary>Board-square number 1-50 for dark squares (1 = top-left dark), null for light squares.</summary>
    public SquareNumber? Number { get; set; }

    /// <summary>True for the 50 playable dark squares.</summary>
    public bool IsDark { get; set; }

    /// <summary>Id of the piece standing here, or null when empty.</summary>
    public int? OccupantId { get; set; }

    /// <summary>Color of the occupant, or null when empty.</summary>
    public PieceColor? OccupantColor { get; set; }

    /// <summary>Man or King, or null when empty.</summary>
    public PieceType? OccupantType { get; set; }
}