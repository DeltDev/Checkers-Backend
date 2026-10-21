using Checkers.Contracts.Requests;
using Checkers.Contracts.Responses;
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
        throw new NotImplementedException();
    }

    [HttpGet("{gameId}")]
    public ActionResult<GameStateResponse> GetGame(string gameId)
    {
        throw new NotImplementedException();
    }

    [HttpGet("{gameId}/moves")]
    public ActionResult<LegalMovesResponse> GetLegalMoves(string gameId, [FromQuery] int playerId)
    {
        throw new NotImplementedException();
    }

    [HttpPost("{gameId}/moves")]
    public ActionResult<ActionResultResponse> MakeMove(string gameId, [FromBody] MakeMoveRequest request)
    {
        throw new NotImplementedException();
    }

    [HttpPost("{gameId}/draw")]
    public ActionResult<ActionResultResponse> OfferDraw(string gameId, [FromBody] OfferDrawRequest request)
    {
        throw new NotImplementedException();
    }

    [HttpPost("{gameId}/resign")]
    public ActionResult<ActionResultResponse> Resign(string gameId, [FromBody] ResignRequest request)
    {
        throw new NotImplementedException();
    }
}
