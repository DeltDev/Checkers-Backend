using Checkers.Contracts.Requests;
using Checkers.Contracts.Responses;
using Checkers.Controllers;
using Checkers.Models;
using Checkers.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Checkers.Tests;

/// <summary>
/// Every failure branch of GamesController that real traffic cannot reach: a
/// stubbed IGameProvider forces each access check, each action's catch block,
/// and both arms of ExceptionResult's type switch (input error -> 400 with the
/// exception message, anything else -> 500 without leaking internals).
/// </summary>
[TestFixture]
public class ControllerFailurePathTests
{
    private sealed class StubProvider : IGameProvider
    {
        public Exception? ThrowOnOpen;
        public GameAccess OpenResult = new(null, "game not found");
        public PlayerAccess FindResult = new(null, "player not in this game");

        public string CreateGame(string? whitePlayerName, string? blackPlayerName)
        {
            return "stub-game-id";
        }

        public GameAccess OpenGame(string gameId)
        {
            return ThrowOnOpen is null ? OpenResult : throw ThrowOnOpen;
        }

        public PlayerAccess FindPlayer(GameController game, int playerId)
        {
            return FindResult;
        }
    }

    private static GamesController CreateController(StubProvider provider)
    {
        return new GamesController(provider, NullLogger<GamesController>.Instance);
    }

    private static int StatusCodeOf<T>(ActionResult<T> result)
    {
        Assert.That(result.Result, Is.InstanceOf<ObjectResult>());
        return ((ObjectResult)result.Result!).StatusCode!.Value;
    }

    /// <summary>Every failure answer carries an ErrorResponse body.</summary>
    private static ErrorResponse ErrorBodyOf<T>(ActionResult<T> result)
    {
        Assert.That(result.Result, Is.InstanceOf<ObjectResult>());
        Assert.That(((ObjectResult)result.Result!).Value, Is.InstanceOf<ErrorResponse>());
        return (ErrorResponse)((ObjectResult)result.Result!).Value!;
    }

    // ------------------------------------------------------------- CreateGame

    [Test]
    public void CreateGame_WhenTheCreatedGameCannotBeOpened_Returns500()
    {
        StubProvider provider = new() { OpenResult = new GameAccess(null, "store unavailable") };
        GamesController controller = CreateController(provider);

        ActionResult<GameStateResponse> result =
            controller.CreateGame(new CreateGameRequest { WhitePlayerName = "Alice" });

        Assert.That(StatusCodeOf(result), Is.EqualTo(500));
        Assert.That(ErrorBodyOf(result).Message, Is.EqualTo("store unavailable"));
    }

    // ----------------------------------------------------------------- GetGame

    [Test]
    public void GetGame_WhenTheProviderThrowsAnInputError_Returns400WithTheMessage()
    {
        // ArgumentException maps to 400 with the exception message (the switch's
        // input-error arm); the default arm is covered by the DI-sabotage test
        // in ApiConfigurationTests.
        StubProvider provider = new() { ThrowOnOpen = new ArgumentException("bad game id") };
        GamesController controller = CreateController(provider);

        ActionResult<GameStateResponse> result = controller.GetGame("x");

        Assert.That(StatusCodeOf(result), Is.EqualTo(400));
        Assert.That(ErrorBodyOf(result).Message, Is.EqualTo("bad game id"));
    }

    // ----------------------------------------------------------- GetLegalMoves

    [Test]
    public void GetLegalMoves_WhenTheProviderThrows_Returns500WithoutLeaking()
    {
        StubProvider provider = new() { ThrowOnOpen = new InvalidOperationException("boom") };
        GamesController controller = CreateController(provider);

        ActionResult<LegalMovesResponse> result = controller.GetLegalMoves("x", 1);

        Assert.That(StatusCodeOf(result), Is.EqualTo(500));
        Assert.That(ErrorBodyOf(result).Message, Is.EqualTo("internal server error"));
    }

    // --------------------------------------------------------------- MakeMove

    [Test]
    public void MakeMove_WhenTheProviderThrows_Returns500WithoutLeaking()
    {
        StubProvider provider = new() { ThrowOnOpen = new InvalidOperationException("boom") };
        GamesController controller = CreateController(provider);

        ActionResult<ActionResultResponse> result = controller.MakeMove("x", new MakeMoveRequest
        {
            PlayerId = 1,
            From = new Position(6, 1),
            To = new Position(5, 0),
        });

        Assert.That(StatusCodeOf(result), Is.EqualTo(500));
        Assert.That(ErrorBodyOf(result).Message, Is.EqualTo("internal server error"));
    }

    // --------------------------------------------------------------- OfferDraw

    [Test]
    public void OfferDraw_WhenTheProviderThrows_Returns500WithoutLeaking()
    {
        StubProvider provider = new() { ThrowOnOpen = new InvalidOperationException("boom") };
        GamesController controller = CreateController(provider);

        ActionResult<ActionResultResponse> result = controller.OfferDraw("x", new OfferDrawRequest { PlayerId = 1 });

        Assert.That(StatusCodeOf(result), Is.EqualTo(500));
        Assert.That(ErrorBodyOf(result).Message, Is.EqualTo("internal server error"));
    }

    // ------------------------------------------------------------------ Resign

    [Test]
    public void Resign_UnknownGame_Returns404()
    {
        StubProvider provider = new();
        GamesController controller = CreateController(provider);

        ActionResult<ActionResultResponse> result = controller.Resign("x", new ResignRequest { PlayerId = 1 });

        Assert.That(StatusCodeOf(result), Is.EqualTo(404));
        Assert.That(ErrorBodyOf(result).Message, Is.EqualTo("game not found"));
    }

    [Test]
    public void Resign_UnknownPlayer_Returns404()
    {
        var (game, _, _) = TestHelpers.CreateGame();
        StubProvider provider = new() { OpenResult = new GameAccess(game, null) };
        GamesController controller = CreateController(provider);

        ActionResult<ActionResultResponse> result = controller.Resign("x", new ResignRequest { PlayerId = 1 });

        Assert.That(StatusCodeOf(result), Is.EqualTo(404));
        Assert.That(ErrorBodyOf(result).Message, Is.EqualTo("player not in this game"));
    }

    [Test]
    public void Resign_WhenTheProviderThrows_Returns500WithoutLeaking()
    {
        StubProvider provider = new() { ThrowOnOpen = new InvalidOperationException("boom") };
        GamesController controller = CreateController(provider);

        ActionResult<ActionResultResponse> result = controller.Resign("x", new ResignRequest { PlayerId = 1 });

        Assert.That(StatusCodeOf(result), Is.EqualTo(500));
        Assert.That(ErrorBodyOf(result).Message, Is.EqualTo("internal server error"));
    }
}
