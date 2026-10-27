using Checkers.Models;

namespace Checkers.Contracts.Responses;

/// <summary>A single move, used in GET /moves (candidates) and inside game-state history.</summary>
public class MoveResponse
{
    /// <summary>Source square, 0-based {row, col}.</summary>
    public Position From { get; set; }

    /// <summary>Final landing square, 0-based {row, col}.</summary>
    public Position To { get; set; }

    /// <summary>Full jump route for multi-captures (empty for quiet moves).</summary>
    public Position[] Path { get; set; } = Array.Empty<Position>();

    /// <summary>Ids of the pieces this move would capture (empty for quiet moves).</summary>
    public int[] CapturedPieceIds { get; set; } = Array.Empty<int>();

    /// <summary>Quiet or Capture.</summary>
    public MoveType Type { get; set; }

    /// <summary>Moving side: White or Black.</summary>
    public PieceColor Color { get; set; }

    /// <summary>Board-square notation, e.g. "[31]:[26]" (quiet) or "[46]x[37]x[19]" (capture).</summary>
    public string Notation { get; set; } = string.Empty;
}