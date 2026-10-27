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
public class GamesController : ControllerBase
{
    private readonly IGameProvider _provider;
    private readonly ILogger<GamesController> _logger;

    public GamesController(IGameProvider provider, ILogger<GamesController> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    [HttpPost]
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

    [HttpGet("{gameId}")]
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

    [HttpGet("{gameId}/moves")]
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

    [HttpPost("{gameId}/moves")]
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

    [HttpPost("{gameId}/draw")]
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

    [HttpPost("{gameId}/resign")]
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
