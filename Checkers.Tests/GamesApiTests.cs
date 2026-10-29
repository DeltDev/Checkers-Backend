using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Checkers.Tests;

/// <summary>
/// PHASE 8/10 — The six /api/games endpoints over in-memory HTTP
/// (IMPLEMENTATION_PLAN Phase 8 + phase 8 pseudocode; Phase 10 Area 7).
/// Decisions encoded:
///   D1 — POST /api/games answers 201 with a Location header; the body's
///        gameId equals the id in Location.
///   D2 — unknown game id -> 404 ErrorResponse; unknown player id -> 404;
///        never a 500 for client behavior.
///   D3 — GET moves for a player who is not to move -> 200 with an empty list.
///   D4 — missing playerId query -> 400; missing required body fields -> 400.
///   D5 — draw offer answers 200 with Success true/false (duplicate = false).
///   D6 — conflict situations (wrong turn, illegal move, finished game) -> 409.
///   D7 — error bodies are ErrorResponse { message }, non-empty.
/// All tests are expected to FAIL until Phases 4-8 are implemented.
/// </summary>
[TestFixture]
public class GamesApiTests
{
    private WebApplicationFactory<Program> _factory = null!;

    [OneTimeSetUp]
    public void CreateFactory()
    {
        _factory = new WebApplicationFactory<Program>();
    }

    [OneTimeTearDown]
    public void DisposeFactory()
    {
        _factory.Dispose();
    }

    // ---- helpers ------------------------------------------------------------

    private static StringContent Json(string body)
    {
        return new StringContent(body, Encoding.UTF8, "application/json");
    }

    private static async Task<JsonNode> ReadJson(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(body) ?? throw new InvalidOperationException($"No JSON in {(int)response.StatusCode}: {body}");
    }

    private async Task<string> CreateGameAsync(HttpClient client, string white = "Alice", string black = "Bob")
    {
        HttpResponseMessage response = await client.PostAsync(
            "/api/games",
            Json($$"""{"whitePlayerName":"{{white}}","blackPlayerName":"{{black}}"}"""));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        JsonNode body = await ReadJson(response);
        return body["gameId"]!.GetValue<string>();
    }

    private async Task<JsonNode> GetGameAsync(HttpClient client, string gameId)
    {
        HttpResponseMessage response = await client.GetAsync($"/api/games/{gameId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return await ReadJson(response);
    }

    private async Task AssertUnchangedAfterRejectedMove(HttpClient client, string gameId)
    {
        JsonNode state = await GetGameAsync(client, gameId);
        Assert.That(state["turn"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(state["turnColor"]!.GetValue<string>(), Is.EqualTo("White"));
        Assert.That(state["status"]!.GetValue<string>(), Is.EqualTo("Ongoing"));
        Assert.That(state["history"]!.AsArray(), Is.Empty);
    }

    // ---- POST /api/games ----------------------------------------------------

    [Test]
    public async Task CreateGame_Returns201WithLocationAndFullGameState()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            "/api/games",
            Json("""{"whitePlayerName":"Alice","blackPlayerName":"Bob"}"""));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created)); // D1
        Assert.That(response.Headers.Location, Is.Not.Null);
        string location = response.Headers.Location!.ToString();
        JsonNode body = await ReadJson(response);

        string gameId = location.Split('/').Last();
        Assert.That(string.IsNullOrWhiteSpace(gameId), Is.False);
        Assert.That(body["gameId"]!.GetValue<string>(), Is.EqualTo(gameId));
        Assert.That(body["turn"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(body["turnColor"]!.GetValue<string>(), Is.EqualTo("White")); // D7 phase 9: enums as strings
        Assert.That(body["status"]!.GetValue<string>(), Is.EqualTo("Ongoing"));
        Assert.That(body["winnerId"], Is.Null);
        Assert.That(body["history"]!.AsArray(), Is.Empty);

        Assert.That(body["players"]!.AsArray().Count, Is.EqualTo(2));
        Assert.That(body["players"]![0]!["name"]!.GetValue<string>(), Is.EqualTo("Alice"));
        Assert.That(body["players"]![0]!["color"]!.GetValue<string>(), Is.EqualTo("White"));
        Assert.That(body["players"]![1]!["name"]!.GetValue<string>(), Is.EqualTo("Bob"));
        Assert.That(body["players"]![1]!["color"]!.GetValue<string>(), Is.EqualTo("Black"));

        Assert.That(body["squares"]!.AsArray().Count, Is.EqualTo(100)); // phase 7 D1: full board
    }

    [Test]
    public async Task CreateGame_MissingWhitePlayerName_400()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync("/api/games", Json("{}"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest)); // D4: [Required] on WhitePlayerName
        JsonNode body = await ReadJson(response);
        Assert.That(body["errors"], Is.Not.Null);
        Assert.That(body["errors"]!["WhitePlayerName"], Is.Not.Null);
    }

    // ---- GET /api/games/{id} ------------------------------------------------

    [Test]
    public async Task GetGame_Unknown_404_WithErrorResponseMessage()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/games/no-such-game");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound)); // D2
        JsonNode body = await ReadJson(response);
        Assert.That(string.IsNullOrWhiteSpace(body["message"]!.GetValue<string>()), Is.False); // D7
    }

    [Test]
    public async Task GetGame_Known_200_EchoesGameId()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        JsonNode body = await GetGameAsync(client, gameId);

        Assert.That(body["gameId"]!.GetValue<string>(), Is.EqualTo(gameId));
        Assert.That(body["turnColor"]!.GetValue<string>(), Is.EqualTo("White"));
        Assert.That(body["status"]!.GetValue<string>(), Is.EqualTo("Ongoing"));
    }

    // ---- GET /api/games/{id}/moves -----------------------------------------

    [Test]
    public async Task GetLegalMoves_UnknownGame_404()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/games/no-such-game/moves?playerId=1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        JsonNode body = await ReadJson(response);
        Assert.That(string.IsNullOrWhiteSpace(body["message"]!.GetValue<string>()), Is.False);
    }

    [Test]
    public async Task GetLegalMoves_UnknownPlayer_404()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        HttpResponseMessage response = await client.GetAsync($"/api/games/{gameId}/moves?playerId=999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound)); // D2: TryFindPlayer scoped per game
        JsonNode body = await ReadJson(response);
        Assert.That(string.IsNullOrWhiteSpace(body["message"]!.GetValue<string>()), Is.False);
    }

    [Test]
    public async Task GetLegalMoves_MissingPlayerId_400()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        HttpResponseMessage response = await client.GetAsync($"/api/games/{gameId}/moves");

        // D4: playerId is a required query parameter. Implement with int? + explicit
        // check (or [BindRequired]) if model binding does not produce the 400 itself.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetLegalMoves_ReturnsNineOpeningMoves_WrongTurnYieldsEmptyList()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        HttpResponseMessage movesResponse = await client.GetAsync($"/api/games/{gameId}/moves?playerId=1");
        Assert.That(movesResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        JsonNode body = await ReadJson(movesResponse);
        JsonArray moves = body["moves"]!.AsArray();

        // Phase 3 verified: 9 quiet opening moves for the side to move.
        Assert.That(moves.Count, Is.EqualTo(9));
        JsonNode first = moves[0]!;
        Assert.That(first["from"]!["row"], Is.Not.Null);
        Assert.That(first["to"]!["col"], Is.Not.Null);
        Assert.That(first["path"], Is.Not.Null);
        Assert.That(first["capturedPieceIds"], Is.Not.Null);
        Assert.That(first["type"]!.GetValue<string>(), Is.EqualTo("Quiet"));   // D3 phase 9: enum strings
        Assert.That(first["color"]!.GetValue<string>(), Is.EqualTo("White"));
        Assert.That(string.IsNullOrEmpty(first["notation"]!.GetValue<string>()), Is.False);

        // D3: a member who is not to move gets 200 with an empty list, not 404/409.
        HttpResponseMessage wrongTurn = await client.GetAsync($"/api/games/{gameId}/moves?playerId=2");
        Assert.That(wrongTurn.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        JsonNode wrongTurnBody = await ReadJson(wrongTurn);
        Assert.That(wrongTurnBody["moves"]!.AsArray(), Is.Empty);
    }

    // ---- POST /api/games/{id}/moves ----------------------------------------

    [Test]
    public async Task MakeMove_Quiet_200_Success_AdvancesState()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/games/{gameId}/moves",
            Json("""{"playerId":1,"from":{"row":6,"col":1},"to":{"row":5,"col":0},"path":null}"""));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        JsonNode body = await ReadJson(response);
        Assert.That(body["success"]!.GetValue<bool>(), Is.True);
        Assert.That(body["status"]!.GetValue<string>(), Is.EqualTo("Ongoing"));
        Assert.That(body["message"], Is.Null);

        JsonNode state = await GetGameAsync(client, gameId);
        Assert.That(state["turn"]!.GetValue<int>(), Is.EqualTo(2));
        Assert.That(state["turnColor"]!.GetValue<string>(), Is.EqualTo("Black"));
        JsonNode recorded = state["history"]![0]!;
        Assert.That(recorded["from"]!["row"]!.GetValue<int>(), Is.EqualTo(6));
        Assert.That(recorded["from"]!["col"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(recorded["notation"]!.GetValue<string>(), Is.EqualTo("[31]:[26]"));
    }

    [Test]
    public async Task MakeMove_WrongTurn_409_StateUnchanged()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        // Player 2 is a member, but White is to move (D6 conflict, not 404).
        HttpResponseMessage response = await client.PostAsync(
            $"/api/games/{gameId}/moves",
            Json("""{"playerId":2,"from":{"row":3,"col":0},"to":{"row":4,"col":1},"path":null}"""));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        JsonNode body = await ReadJson(response);
        Assert.That(body["success"]!.GetValue<bool>(), Is.False);
        Assert.That(string.IsNullOrWhiteSpace(body["message"]!.GetValue<string>()), Is.False);
        await AssertUnchangedAfterRejectedMove(client, gameId);
    }

    [Test]
    public async Task MakeMove_IllegalFromTo_409_StateUnchanged()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/games/{gameId}/moves",
            Json("""{"playerId":1,"from":{"row":6,"col":1},"to":{"row":3,"col":2},"path":null}"""));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        JsonNode body = await ReadJson(response);
        Assert.That(body["success"]!.GetValue<bool>(), Is.False);
        Assert.That(string.IsNullOrWhiteSpace(body["message"]!.GetValue<string>()), Is.False);
        await AssertUnchangedAfterRejectedMove(client, gameId);
    }

    [Test]
    public async Task MakeMove_FabricatedPath_409_StateUnchanged()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        // Correct From/To of a legal quiet move, but the client smuggles a path
        // (plan walkthrough C): D1 requires an exact canonical match.
        HttpResponseMessage response = await client.PostAsync(
            $"/api/games/{gameId}/moves",
            Json("""{"playerId":1,"from":{"row":6,"col":1},"to":{"row":5,"col":0},"path":[{"row":4,"col":1}]}"""));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        JsonNode body = await ReadJson(response);
        Assert.That(body["success"]!.GetValue<bool>(), Is.False);
        await AssertUnchangedAfterRejectedMove(client, gameId);
    }

    [Test]
    public async Task MakeMove_UnknownGame_404()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            "/api/games/no-such-game/moves",
            Json("""{"playerId":1,"from":{"row":6,"col":1},"to":{"row":5,"col":0},"path":null}"""));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        JsonNode body = await ReadJson(response);
        Assert.That(string.IsNullOrWhiteSpace(body["message"]!.GetValue<string>()), Is.False);
    }

    [Test]
    public async Task MakeMove_UnknownPlayer_404()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/games/{gameId}/moves",
            Json("""{"playerId":999,"from":{"row":6,"col":1},"to":{"row":5,"col":0},"path":null}"""));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        await AssertUnchangedAfterRejectedMove(client, gameId);
    }

    // ---- POST /api/games/{id}/draw -----------------------------------------

    [Test]
    public async Task OfferDraw_FirstOfferTrue_DuplicateFalse_200()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        HttpResponseMessage first = await client.PostAsync(
            $"/api/games/{gameId}/draw", Json("""{"playerId":1}"""));
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK)); // D5: 200 with Success flag
        JsonNode firstBody = await ReadJson(first);
        Assert.That(firstBody["success"]!.GetValue<bool>(), Is.True);
        Assert.That(firstBody["status"]!.GetValue<string>(), Is.EqualTo("Ongoing"));

        HttpResponseMessage duplicate = await client.PostAsync(
            $"/api/games/{gameId}/draw", Json("""{"playerId":1}"""));
        Assert.That(duplicate.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        JsonNode duplicateBody = await ReadJson(duplicate);
        Assert.That(duplicateBody["success"]!.GetValue<bool>(), Is.False);
        Assert.That(string.IsNullOrWhiteSpace(duplicateBody["message"]!.GetValue<string>()), Is.False);
        Assert.That(duplicateBody["status"]!.GetValue<string>(), Is.EqualTo("Ongoing"));
    }

    [Test]
    public async Task OfferDraw_Mutual_SecondOfferEndsInDraw_200()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        HttpResponseMessage first = await client.PostAsync(
            $"/api/games/{gameId}/draw", Json("""{"playerId":1}"""));
        Assert.That((await ReadJson(first))["success"]!.GetValue<bool>(), Is.True);

        HttpResponseMessage second = await client.PostAsync(
            $"/api/games/{gameId}/draw", Json("""{"playerId":2}"""));
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        JsonNode body = await ReadJson(second);
        Assert.That(body["success"]!.GetValue<bool>(), Is.True);
        Assert.That(body["status"]!.GetValue<string>(), Is.EqualTo("Draw"));

        JsonNode state = await GetGameAsync(client, gameId);
        Assert.That(state["status"]!.GetValue<string>(), Is.EqualTo("Draw"));
        Assert.That(state["winnerId"], Is.Null);
    }

    [Test]
    public async Task OfferDraw_UnknownGameOrPlayer_404()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        HttpResponseMessage unknownGame = await client.PostAsync(
            "/api/games/no-such-game/draw", Json("""{"playerId":1}"""));
        Assert.That(unknownGame.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        HttpResponseMessage unknownPlayer = await client.PostAsync(
            $"/api/games/{gameId}/draw", Json("""{"playerId":999}"""));
        Assert.That(unknownPlayer.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ---- POST /api/games/{id}/resign ---------------------------------------

    [Test]
    public async Task Resign_200_EndsGameWithWinner()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/games/{gameId}/resign", Json("""{"playerId":1}"""));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        JsonNode body = await ReadJson(response);
        Assert.That(body["success"]!.GetValue<bool>(), Is.True);
        Assert.That(body["status"]!.GetValue<string>(), Is.EqualTo("BlackWon"));

        JsonNode state = await GetGameAsync(client, gameId);
        Assert.That(state["status"]!.GetValue<string>(), Is.EqualTo("BlackWon"));
        Assert.That(state["winnerId"]!.GetValue<int>(), Is.EqualTo(2));
    }

    [Test]
    public async Task Resign_AfterFinished_409()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        HttpResponseMessage first = await client.PostAsync(
            $"/api/games/{gameId}/resign", Json("""{"playerId":1}"""));
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        HttpResponseMessage second = await client.PostAsync(
            $"/api/games/{gameId}/resign", Json("""{"playerId":2}"""));

        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Conflict)); // D6: finished game
        JsonNode body = await ReadJson(second);
        Assert.That(string.IsNullOrWhiteSpace(body["message"]!.GetValue<string>()), Is.False);

        JsonNode state = await GetGameAsync(client, gameId);
        Assert.That(state["status"]!.GetValue<string>(), Is.EqualTo("BlackWon")); // still the first result
    }

    // ---- finished-game gates ------------------------------------------------

    [Test]
    public async Task MakeMove_AfterFinished_409()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);
        (await client.PostAsync($"/api/games/{gameId}/resign", Json("""{"playerId":1}""")))
            .EnsureSuccessStatusCode();

        HttpResponseMessage response = await client.PostAsync(
            $"/api/games/{gameId}/moves",
            Json("""{"playerId":2,"from":{"row":3,"col":0},"to":{"row":4,"col":1},"path":null}"""));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict)); // D6
        JsonNode body = await ReadJson(response);
        Assert.That(body["success"]!.GetValue<bool>(), Is.False);
        Assert.That(string.IsNullOrWhiteSpace(body["message"]!.GetValue<string>()), Is.False);

        JsonNode state = await GetGameAsync(client, gameId);
        Assert.That(state["status"]!.GetValue<string>(), Is.EqualTo("BlackWon"));
        Assert.That(state["history"]!.AsArray(), Is.Empty);
    }

    [Test]
    public async Task GetLegalMoves_AfterFinished_200_EmptyList()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);
        (await client.PostAsync($"/api/games/{gameId}/resign", Json("""{"playerId":1}""")))
            .EnsureSuccessStatusCode();

        HttpResponseMessage response = await client.GetAsync($"/api/games/{gameId}/moves?playerId=1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK)); // D6: empty list, not an error
        JsonNode body = await ReadJson(response);
        Assert.That(body["moves"]!.AsArray(), Is.Empty);
    }
}

