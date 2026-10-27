using Checkers.Models;

namespace Checkers.Contracts.Responses;

/// <summary>Outcome of a move, draw offer or resignation. Always HTTP 2xx unless the request itself was invalid.</summary>
public class ActionResultResponse
{
    /// <summary>True when the action was accepted; false when rejected (e.g. duplicate draw offer).</summary>
    public bool Success { get; set; }

    /// <summary>Rejection reason when <see cref="Success"/> is false; otherwise null.</summary>
    public string? Message { get; set; }

    /// <summary>The game status after the action: Ongoing, WhiteWon, BlackWon or Draw.</summary>
    public GameStatus Status { get; set; }
}