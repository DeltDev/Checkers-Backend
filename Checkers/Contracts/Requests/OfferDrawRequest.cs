namespace Checkers.Contracts.Requests;

/// <summary>Body for POST /api/games/{gameId}/draw.</summary>
public class OfferDrawRequest
{
    /// <summary>The offering player's id: 1 = White, 2 = Black.</summary>
    public int PlayerId { get; set; }
}