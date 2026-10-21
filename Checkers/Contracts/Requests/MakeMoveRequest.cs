using Checkers.Models;

namespace Checkers.Contracts.Requests;

public class MakeMoveRequest
{
    public int PlayerId { get; set; }

    public Position From { get; set; }

    public Position To { get; set; }

    public Position[]? Path { get; set; }
}
