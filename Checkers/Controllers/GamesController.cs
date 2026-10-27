using Checkers.Contracts.Requests;
using Checkers.Contracts.Responses;
using Checkers.Mapping;
using Checkers.Models;
using Checkers.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Checkers.Controllers;

[ApiController]
[Route("api/games")]
[Tags("Games")]
public class GamesController : ControllerBase
{
    private readonly IGameProvider _provider;
    private readonly ILogger<GamesController> _logger;

    public GamesController(IGameProvider provider, ILogger<GamesController> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    /// <summary>Starts a new game and returns its full initial state.</summary>
    /// <remarks>
    /// White is player id 1, Black is player id 2. The response carries the
    /// initial board (40 pieces), the turn counter and the Location header
    /// pointing at <c>/api/games/{gameId}</c>.
    /// </remarks>
    /// <param name="request">Player names; only <c>whitePlayerName</c> is required.</param>
    /// <response code="201">The created game state.</response>
    /// <response code="400">The request body failed validation.</response>
    /// <response code="500">The game could not be stored.</response>
    [HttpPost]
    [ProducesResponseType(typeof(GameStateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public ActionResult<GameStateResponse> CreateGame([FromBody] CreateGameRequest request)
    {
        try
        {
            string gameId = _provider.CreateGame(request.WhitePlayerName, request.BlackPlayerName);

            GameAccess access = _provider.OpenGame(gameId);
            if (!access.IsReady)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, Error(access.Error!));
            }

            GameStateResponse response = access.Game!.ToResponse();
            response.GameId = gameId;
            return Created($"/api/games/{gameId}", response);
        }
        catch (Exception ex)
        {
            return ExceptionResult("create game", ex);
        }
    }

    /// <summary>Fetches the current state of a game.</summary>
    /// <remarks>Turn counter, side to move, status, players, the 100 squares and the full move history.</remarks>
    /// <param name="gameId">The game id returned by POST /api/games.</param>
    /// <response code="200">The current game state.</response>
    /// <response code="404">No game with that id.</response>
    [HttpGet("{gameId}")]
    [ProducesResponseType(typeof(GameStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<GameStateResponse> GetGame(string gameId)
    {
        try
        {
            GameAccess access = _provider.OpenGame(gameId);
            if (!access.IsReady)
            {
                return NotFound(Error(access.Error!));
            }

            GameStateResponse response = access.Game!.ToResponse();
            response.GameId = gameId;
            return Ok(response);
        }
        catch (Exception ex)
        {
            return ExceptionResult("get game", ex);
        }
    }

    /// <summary>Lists every move the player may legally play right now.</summary>
    /// <remarks>
    /// Empty list when it is not that player's turn, the game is over, or the
    /// position has no legal moves. Captures that can be continued are returned
    /// as a single move with the full jump <c>path</c>.
    /// </remarks>
    /// <param name="gameId">The game to query.</param>
    /// <param name="playerId">1 for White, 2 for Black.</param>
    /// <response code="200">The legal moves (possibly empty).</response>
    /// <response code="400">The playerId query parameter is missing.</response>
    /// <response code="404">Unknown game, or the player is not in it.</response>
    [HttpGet("{gameId}/moves")]
    [ProducesResponseType(typeof(LegalMovesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<LegalMovesResponse> GetLegalMoves(string gameId, [FromQuery] int? playerId)
    {
        try
        {
            GameAccess access = _provider.OpenGame(gameId);
            if (!access.IsReady)
            {
                return NotFound(Error(access.Error!));
            }

            if (playerId is null)
            {
                return BadRequest(Error("playerId query parameter is required"));
            }

            PlayerAccess playerAccess = _provider.FindPlayer(access.Game!, playerId.Value);
            if (!playerAccess.IsReady)
            {
                return NotFound(Error(playerAccess.Error!));
            }

            LegalMovesResponse result = new();
            foreach (Move move in access.Game!.GetLegalMoves(playerAccess.Player!))
            {
                result.Moves.Add(move.ToResponse());
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return ExceptionResult("get legal moves", ex);
        }
    }

    /// <summary>Plays a move for the requesting player.</summary>
    /// <remarks>
    /// The move is validated against the legal-move list, so multi-jumps
    /// travel the full <c>path</c> in one request. A rejected move answers
    /// 409 with a reason instead of throwing.
    /// </remarks>
    /// <param name="gameId">The game to play in.</param>
    /// <param name="request">Player id plus from/to (and path for multi-jumps).</param>
    /// <response code="200">Move applied; body carries the new game status.</response>
    /// <response code="400">Malformed input (bad coordinates, wrong shapes).</response>
    /// <response code="404">Unknown game or player.</response>
    /// <response code="409">Rejected: illegal move, not this player's turn, or game is finished.</response>
    [HttpPost("{gameId}/moves")]
    [ProducesResponseType(typeof(ActionResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ActionResultResponse), StatusCodes.Status409Conflict)]
    public ActionResult<ActionResultResponse> MakeMove(string gameId, [FromBody] MakeMoveRequest request)
    {
        try
        {
            GameAccess access = _provider.OpenGame(gameId);
            if (!access.IsReady)
            {
                return NotFound(Error(access.Error!));
            }

            PlayerAccess playerAccess = _provider.FindPlayer(access.Game!, request.PlayerId);
            if (!playerAccess.IsReady)
            {
                return NotFound(Error(playerAccess.Error!));
            }

            Move requested = new(
                request.From,
                request.To,
                request.Path ?? Array.Empty<Position>(),
                Array.Empty<IPiece>(),
                MoveType.Quiet,
                playerAccess.Player!.Color,
                PieceType.Man);

            if (!access.Game!.MakeMove(playerAccess.Player!, requested))
            {
                return Conflict(DecideRejection(access.Game!, playerAccess.Player!));
            }

            return Ok(new ActionResultResponse
            {
                Success = true,
                Message = null,
                Status = access.Game!.Status,
            });
        }
        catch (Exception ex)
        {
            return ExceptionResult("make move", ex);
        }
    }

    /// <summary>Offers a draw on behalf of the player.</summary>
    /// <remarks>
    /// The first offer records interest and answers <c>success: true</c>;
    /// the game only ends in a draw when BOTH players have offered.
    /// A duplicate offer answers <c>success: false</c> with HTTP 200.
    /// </remarks>
    /// <param name="gameId">The game to offer in.</param>
    /// <param name="request">The offering player's id.</param>
    /// <response code="200">Offer recorded (<c>success: true</c>) or rejected as duplicate (<c>success: false</c>).</response>
    /// <response code="400">Malformed input.</response>
    /// <response code="404">Unknown game or player.</response>
    [HttpPost("{gameId}/draw")]
    [ProducesResponseType(typeof(ActionResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<ActionResultResponse> OfferDraw(string gameId, [FromBody] OfferDrawRequest request)
    {
        try
        {
            GameAccess access = _provider.OpenGame(gameId);
            if (!access.IsReady)
            {
                return NotFound(Error(access.Error!));
            }

            PlayerAccess playerAccess = _provider.FindPlayer(access.Game!, request.PlayerId);
            if (!playerAccess.IsReady)
            {
                return NotFound(Error(playerAccess.Error!));
            }

            bool accepted = access.Game!.OfferDraw(playerAccess.Player!);

            return Ok(new ActionResultResponse
            {
                Success = accepted,
                Message = accepted ? null : "draw offer rejected (finished game or duplicate)",
                Status = access.Game!.Status,
            });
        }
        catch (Exception ex)
        {
            return ExceptionResult("offer draw", ex);
        }
    }

    /// <summary>The player resigns; the opponent wins immediately.</summary>
    /// <param name="gameId">The game to resign from.</param>
    /// <param name="request">The resigning player's id.</param>
    /// <response code="200">Resignation accepted; status flips to the opponent's win.</response>
    /// <response code="400">Malformed input.</response>
    /// <response code="404">Unknown game or player.</response>
    /// <response code="409">The game is already finished.</response>
    [HttpPost("{gameId}/resign")]
    [ProducesResponseType(typeof(ActionResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ActionResultResponse), StatusCodes.Status409Conflict)]
    public ActionResult<ActionResultResponse> Resign(string gameId, [FromBody] ResignRequest request)
    {
        try
        {
            GameAccess access = _provider.OpenGame(gameId);
            if (!access.IsReady)
            {
                return NotFound(Error(access.Error!));
            }

            PlayerAccess playerAccess = _provider.FindPlayer(access.Game!, request.PlayerId);
            if (!playerAccess.IsReady)
            {
                return NotFound(Error(playerAccess.Error!));
            }

            if (access.Game!.Status != GameStatus.Ongoing)
            {
                return Conflict(new ActionResultResponse
                {
                    Success = false,
                    Message = "game already finished",
                    Status = access.Game!.Status,
                });
            }

            access.Game!.Resign(playerAccess.Player!);
            return Ok(new ActionResultResponse
            {
                Success = true,
                Message = null,
                Status = access.Game!.Status,
            });
        }
        catch (Exception ex)
        {
            return ExceptionResult("resign", ex);
        }
    }

    /// <summary>
    /// Maps an exception that escaped an action to an HTTP response: input
    /// errors (out-of-range indexes, bad arguments, malformed values) become
    /// 400 with the exception message, anything else is logged and answered
    /// with a generic 500 so internals never leak. Program.cs keeps the global
    /// exception handler as the final safety net outside the controller.
    /// </summary>
    private ActionResult ExceptionResult(string operation, Exception ex)
    {
        switch (ex)
        {
            case ArgumentException: // includes ArgumentNullException / ArgumentOutOfRange
            case IndexOutOfRangeException:
            case FormatException:
            case OverflowException:
                _logger.LogWarning(ex, "{Operation} received invalid input", operation);
                return BadRequest(Error(ex.Message));

            default:
                _logger.LogError(ex, "{Operation} failed with an unexpected exception", operation);
                return StatusCode(StatusCodes.Status500InternalServerError, Error("internal server error"));
        }
    }

    private static ActionResultResponse DecideRejection(GameController game, IPlayer player)
    {
        string reason;
        if (game.Status != GameStatus.Ongoing)
        {
            reason = "game is finished";
        }
        else if (player.Color != game.TurnColor)
        {
            reason = "not this player's turn";
        }
        else
        {
            reason = "illegal move";
        }

        return new ActionResultResponse
        {
            Success = false,
            Message = reason,
            Status = game.Status,
        };
    }

    private static ErrorResponse Error(string message)
    {
        return new ErrorResponse { Message = message };
    }
}
