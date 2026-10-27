using System.ComponentModel.DataAnnotations;

namespace Checkers.Contracts.Requests;

/// <summary>Body for POST /api/games.</summary>
public class CreateGameRequest
{
    /// <summary>Display name of the White player (id 1). Required; falls back to "Player 1" when blank.</summary>
    [Required]
    public string? WhitePlayerName { get; set; }

    /// <summary>Display name of the Black player (id 2). Optional; falls back to "Player 2".</summary>
    public string? BlackPlayerName { get; set; }
}