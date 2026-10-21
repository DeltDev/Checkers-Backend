using Checkers.Models;

namespace Checkers.Contracts.Responses;

public class PlayerResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public PieceColor Color { get; set; }
}
