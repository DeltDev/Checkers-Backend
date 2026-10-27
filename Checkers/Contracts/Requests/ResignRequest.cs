namespace Checkers.Contracts.Requests;

/// <summary>Body for POST /api/games/{gameId}/resign.</summary>
public class ResignRequest
{
    /// <summary>The resigning player's id: 1 = White, 2 = Black. The opponent wins.</summary>
    public int PlayerId { get; set; }
}