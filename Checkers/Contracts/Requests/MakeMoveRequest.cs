using Checkers.Models;

namespace Checkers.Contracts.Requests;

/// <summary>Body for POST /api/games/{gameId}/moves.</summary>
public class MakeMoveRequest
{
    /// <summary>The moving player's id: 1 = White, 2 = Black. Must be that player's turn.</summary>
    public int PlayerId { get; set; }

    /// <summary>Source square as 0-based {row, col}; row 0 is Black's back rank.</summary>
    public Position From { get; set; }

    /// <summary>Destination square as 0-based {row, col}. For multi-jumps this is the final landing square.</summary>
    public Position To { get; set; }

    /// <summary>Optional. For multi-jumps, the intermediate landing squares; must equal one of GET /moves' path values.</summary>
    public Position[]? Path { get; set; }
}