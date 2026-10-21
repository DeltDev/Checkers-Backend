using Checkers.Models;

namespace Checkers.Contracts.Responses;

public class ActionResultResponse
{
    public bool Success { get; set; }

    public string? Message { get; set; }

    public GameStatus Status { get; set; }
}
