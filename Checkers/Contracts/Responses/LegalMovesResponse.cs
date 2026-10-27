namespace Checkers.Contracts.Responses;

/// <summary>Body of GET /api/games/{gameId}/moves.</summary>
public class LegalMovesResponse
{
    /// <summary>The legal moves for the queried player; empty when it is not their turn or the game is over.</summary>
    public List<MoveResponse> Moves { get; set; } = new();
}