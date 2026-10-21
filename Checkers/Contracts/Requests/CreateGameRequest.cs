namespace Checkers.Contracts.Requests;

public class CreateGameRequest
{
    public string? WhitePlayerName { get; set; }

    public string? BlackPlayerName { get; set; }
}
