using Checkers.Models;

namespace Checkers.Contracts.Responses;

/// <summary>Full snapshot of a game.</summary>
public class GameStateResponse
{
    /// <summary>The game id (route key for every other endpoint).</summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary>Ply counter: 1 after creation, +1 per applied move.</summary>
    public int Turn { get; set; }

    /// <summary>Side to move now: White or Black.</summary>
    public PieceColor TurnColor { get; set; }

    /// <summary>Ongoing, WhiteWon, BlackWon or Draw.</summary>
    public GameStatus Status { get; set; }

    /// <summary>Winning player's id when the game is decided; null while ongoing or on a draw.</summary>
    public int? WinnerId { get; set; }

    /// <summary>The two players (id 1 White, id 2 Black).</summary>
    public List<PlayerResponse> Players { get; set; } = new();

    /// <summary>All 100 squares (light ones carry no occupant fields).</summary>
    public List<SquareResponse> Squares { get; set; } = new();

    /// <summary>Every applied move in order, with algebraic-style notation.</summary>
    public List<MoveResponse> History { get; set; } = new();
}