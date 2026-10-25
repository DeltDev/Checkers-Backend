using System.ComponentModel.DataAnnotations;

namespace Checkers.Contracts.Requests;

public class CreateGameRequest
{

    [Required]
    public string? WhitePlayerName { get; set; }

    public string? BlackPlayerName { get; set; }
}

