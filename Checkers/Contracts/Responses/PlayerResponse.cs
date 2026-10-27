using Checkers.Models;

namespace Checkers.Contracts.Responses;

/// <summary>A player seated at the table.</summary>
public class PlayerResponse
{
    /// <summary>Stable player id used in every request body: 1 = White, 2 = Black.</summary>
    public int Id { get; set; }

    /// <summary>Display name chosen at game creation.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>White or Black.</summary>
    public PieceColor Color { get; set; }
}