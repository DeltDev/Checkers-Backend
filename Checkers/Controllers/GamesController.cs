using System.ComponentModel.DataAnnotations;
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
    private readonly IGameService _gameService;
    private readonly ILogger<GamesController> _logger;

    public GamesController(IGameService gameService, ILogger<GamesController> logger)
    {
        _gameService = gameService;
        _logger = logger;
    }

    [HttpPost]
    public ActionResult<GameStateResponse> CreateGame([FromBody] CreateGameRequest request)
    {
        try
        {
            string gameId = _gameService.CreateGame(request.WhitePlayerName, request.BlackPlayerName);

            GameController? game = _gameService.GetGame(gameId);
            if (game is null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, Error("game not found after creation"));
            }

            GameStateResponse response = game.ToResponse();
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
            GameController? game = TryGetGame(gameId);
            if (game is null)
            {
                return NotFound(Error("game not found"));
            }

            GameStateResponse response = game.ToResponse();
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
            GameController? game = TryGetGame(gameId);
            if (game is null)
            {
                return NotFound(Error("game not found"));
            }

            if (playerId is null)
            {
                return BadRequest(Error("playerId query parameter is required"));
            }

            IPlayer? player = TryFindPlayer(game, playerId.Value);
            if (player is null)
            {
                return NotFound(Error("player not in this game"));
            }

            LegalMovesResponse result = new();
            foreach (Move move in game.GetLegalMoves(player))
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
            GameController? game = TryGetGame(gameId); //buat service layer baru
            if (game is null)
            {
                return NotFound(Error("game not found"));
            }

            IPlayer? player = TryFindPlayer(game, request.PlayerId);
            if (player is null)
            {
                return NotFound(Error("player not in this game"));
            }

            Move requested = new(
                request.From,
                request.To,
                request.Path ?? Array.Empty<Position>(),
                Array.Empty<IPiece>(),
                MoveType.Quiet,
                player.Color,
                PieceType.Man);

            if (!game.MakeMove(player, requested))
            {
                return Conflict(DecideRejection(game, player));
            }

            return Ok(new ActionResultResponse
            {
                Success = true,
                Message = null,
                Status = game.Status,
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
            GameController? game = TryGetGame(gameId);
            if (game is null)
            {
                return NotFound(Error("game not found"));
            }

            IPlayer? player = TryFindPlayer(game, request.PlayerId);
            if (player is null)
            {
                return NotFound(Error("player not in this game"));
            }

            bool accepted = game.OfferDraw(player);

            return Ok(new ActionResultResponse
            {
                Success = accepted,
                Message = accepted ? null : "draw offer rejected (finished game or duplicate)",
                Status = game.Status,
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
            GameController? game = TryGetGame(gameId);
            if (game is null)
            {
                return NotFound(Error("game not found"));
            }

            IPlayer? player = TryFindPlayer(game, request.PlayerId);
            if (player is null)
            {
                return NotFound(Error("player not in this game"));
            }

            if (game.Status != GameStatus.Ongoing)
            {
                return Conflict(new ActionResultResponse
                {
                    Success = false,
                    Message = "game already finished",
                    Status = game.Status,
                });
            }

            game.Resign(player);
            return Ok(new ActionResultResponse
            {
                Success = true,
                Message = null,
                Status = game.Status,
            });
        }
        catch (Exception ex)
        {
            return ExceptionResult("resign", ex);
        }
    }


    private ActionResult ExceptionResult(string operation, Exception ex)
    {
        switch (ex)
        {
            case ArgumentException: 
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

    private GameController? TryGetGame(string gameId)
    {
        if (string.IsNullOrEmpty(gameId))
        {
            return null;
        }

        return _gameService.GetGame(gameId);
    }

    private static IPlayer? TryFindPlayer(GameController game, int playerId)
    {
        return game.GetPlayers().FirstOrDefault(player => player.Id == playerId);
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
