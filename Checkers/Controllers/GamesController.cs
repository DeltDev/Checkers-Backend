using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices.JavaScript;
using Checkers.Contracts.Requests;
using Checkers.Contracts.Responses;
using Checkers.Mapping;
using Checkers.Models;
using Checkers.Services;
using Microsoft.AspNetCore.Mvc;


namespace Checkers.Controllers;

[ApiController]
[Route("api/games")]
public class GamesController : ControllerBase
{
    private readonly IGameService _gameService;

    public GamesController(IGameService gameService)
    {
        _gameService = gameService;
    }

    [HttpPost]
    public ActionResult<GameStateResponse> CreateGame([FromBody] CreateGameRequest request)
    {
        string gameId = _gameService.CreateGame(request.WhitePlayerName, request.BlackPlayerName);
        GameController? game = _gameService.GetGame(gameId);
        if (game is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,Error("game not found after creation"));
        }

        GameStateResponse response = game.ToResponse();
        response.GameId = gameId;
        return Created($"/api/games/{gameId}", response);
        
    }

    [HttpGet("{gameId}")]
    public ActionResult<GameStateResponse> GetGame(string gameId)
    {
        GameController? game = TryGetGame(gameId);
        if (game is null)
        {
            return NotFound(Error("Game not found"));
        }
        
        GameStateResponse response = game.ToResponse();
        response.GameId = gameId;
        return Ok(response);
    }

    [HttpGet("{gameId}/moves")]
    public ActionResult<LegalMovesResponse> GetLegalMoves(string gameId, [FromQuery] int? playerId)
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

    [HttpPost("{gameId}/moves")]
    public ActionResult<ActionResultResponse> MakeMove(string gameId, [FromBody] MakeMoveRequest request)
    {
        GameController? game = TryGetGame(gameId);
        if (game is null)
        {
            return NotFound(Error("Game not found"));
        }

        IPlayer? player = TryFindPlayer(game, request.PlayerId);
        if (player is null)
        {
            return NotFound(Error("Player is not in this game"));
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
            return Conflict(DecideRejection(game, player)); // D6: 409, never 500
        }

        return Ok(new ActionResultResponse
        {
            Success = true,
            Message = null,
            Status = game.Status,
        });
    }

    [HttpPost("{gameId}/draw")]
    public ActionResult<ActionResultResponse> OfferDraw(string gameId, [FromBody] OfferDrawRequest request)
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
            Message = accepted ? "Draw accepted" : "Draw offer rejected",
            Status = game.Status,
        });
    }

    [HttpPost("{gameId}/resign")]
    public ActionResult<ActionResultResponse> Resign(string gameId, [FromBody] ResignRequest request)
    {
        throw new NotImplementedException();
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
