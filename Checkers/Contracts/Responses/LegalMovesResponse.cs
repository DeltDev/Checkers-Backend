namespace Checkers.Contracts.Responses;

public class LegalMovesResponse
{
    public List<MoveResponse> Moves { get; set; } = new();
}
