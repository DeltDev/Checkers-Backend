namespace Checkers.Contracts.Responses;

/// <summary>Body of every 400/404/500 error response.</summary>
public class ErrorResponse
{
    /// <summary>Human-readable reason, e.g. "game not found".</summary>
    public string Message { get; set; } = string.Empty;
}