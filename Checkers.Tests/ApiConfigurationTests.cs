using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Checkers.Models;
using Checkers.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Checkers.Tests;

/// <summary>
/// PHASE 9/10 — Program.cs configuration (phase 9 pseudocode + IMPLEMENTATION_PLAN).
/// Decisions encoded:
///   D1 (route) — a health route GET /health answers "ok" (replaces MapGet("/")).
///   D2 — the four services resolve as singletons (safe only with Phase 6's lock).
///   D3 — enums serialize as STRINGS in every body ("White", "Ongoing", "Quiet").
///   D4 — Position in MakeMoveRequest deserializes from JSON (the plan's single
///        serialization risk; if this fails, add [JsonConstructor] to Position).
///   D5 — camelCase property names (ASP.NET default web options, kept).
///   D7 — an unexpected exception becomes a 500 ErrorResponse JSON, never HTML.
/// All tests are expected to FAIL until Phase 9 is implemented.
/// </summary>
[TestFixture]
public class ApiConfigurationTests
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

    private static StringContent Json(string body)
    {
        return new StringContent(body, Encoding.UTF8, "application/json");
    }

    private static async Task<JsonNode> ReadJson(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(body) ?? throw new InvalidOperationException($"No JSON in {(int)response.StatusCode}: {body}");
    }

    private async Task<string> CreateGameAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsync(
            "/api/games", Json("""{"whitePlayerName":"Alice","blackPlayerName":"Bob"}"""));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        JsonNode body = await ReadJson(response);
        return body["gameId"]!.GetValue<string>();
    }

    // ---- D1: health route ----------------------------------------------------

    [Test]
    public async Task HealthRoute_ReturnsOk()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        string body = (await response.Content.ReadAsStringAsync()).Trim();
        Assert.That(body, Is.EqualTo("ok"));
    }

    // ---- D3 + D5: raw JSON shape --------------------------------------------

    [Test]
    public async Task GameState_Json_UsesEnumStrings_AndCamelCase()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        HttpResponseMessage response = await client.GetAsync($"/api/games/{gameId}");
        string raw = await response.Content.ReadAsStringAsync();

        // D3: enums as strings, not 0/1/2.
        Assert.That(raw, Does.Contain("\"turnColor\":\"White\""));
        Assert.That(raw, Does.Contain("\"status\":\"Ongoing\""));

        // D5: camelCase names, never PascalCase.
        Assert.That(raw, Does.Contain("\"gameId\""));
        Assert.That(raw, Does.Contain("\"turnColor\""));
        Assert.That(raw, Does.Contain("\"winnerId\""));
        Assert.That(raw, Does.Not.Contain("\"GameId\""));
        Assert.That(raw, Does.Not.Contain("\"TurnColor\""));
        Assert.That(raw, Does.Not.Contain("\"Status\""));

        // A fresh game has an empty history, so capturedPieceIds only appears
        // once a move exists - play one and re-read to assert its casing.
        HttpResponseMessage move = await client.PostAsync(
            $"/api/games/{gameId}/moves",
            Json("""{"playerId":1,"from":{"row":6,"col":1},"to":{"row":5,"col":0},"path":null}"""));
        Assert.That(move.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        HttpResponseMessage afterMoveResponse = await client.GetAsync($"/api/games/{gameId}");
        string afterMove = await afterMoveResponse.Content.ReadAsStringAsync();
        Assert.That(afterMove.Contains("\"capturedPieceIds\"", StringComparison.Ordinal), Is.True);
    }

    [Test]
    public async Task LegalMoves_Json_UsesEnumStrings()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        HttpResponseMessage response = await client.GetAsync($"/api/games/{gameId}/moves?playerId=1");
        string raw = await response.Content.ReadAsStringAsync();

        Assert.That(raw, Does.Contain("\"type\":\"Quiet\""));
        Assert.That(raw, Does.Contain("\"color\":\"White\""));
        Assert.That(raw, Does.Contain("\"moves\""));
    }

    // ---- D4: Position round trip --------------------------------------------

    [Test]
    public async Task MoveRequest_Position_Deserializes_FromJsonBody()
    {
        HttpClient client = _factory.CreateClient();
        string gameId = await CreateGameAsync(client);

        // If System.Text.Json cannot populate Position's get-only properties this
        // returns 400/409 instead of 200 -> apply the [JsonConstructor] fix (D4).
        HttpResponseMessage response = await client.PostAsync(
            $"/api/games/{gameId}/moves",
            Json("""{"playerId":1,"from":{"row":6,"col":1},"to":{"row":5,"col":0},"path":null}"""));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        JsonNode body = await ReadJson(response);
        Assert.That(body["success"]!.GetValue<bool>(), Is.True);

        // The parsed From must be exactly (6,1), proving Position bound correctly.
        HttpResponseMessage stateResponse = await client.GetAsync($"/api/games/{gameId}");
        JsonNode state = await ReadJson(stateResponse);
        JsonNode recorded = state["history"]![0]!;
        Assert.That(recorded["from"]!["row"]!.GetValue<int>(), Is.EqualTo(6));
        Assert.That(recorded["from"]!["col"]!.GetValue<int>(), Is.EqualTo(1));
    }

    // ---- D2: DI lifetimes ----------------------------------------------------

    [Test]
    public void FourServices_ResolveAsSingletons()
    {
        IServiceProvider services = _factory.Services;

        Assert.That(services.GetRequiredService<IGameStore>(), Is.SameAs(services.GetRequiredService<IGameStore>()));
        Assert.That(services.GetRequiredService<IGameFactory>(), Is.SameAs(services.GetRequiredService<IGameFactory>()));
        Assert.That(services.GetRequiredService<IGameIdGenerator>(), Is.SameAs(services.GetRequiredService<IGameIdGenerator>()));
        Assert.That(services.GetRequiredService<IGameService>(), Is.SameAs(services.GetRequiredService<IGameService>()));
    }

    // ---- D7: 500 handling ----------------------------------------------------

    private sealed class ThrowingGameService : IGameService
    {
        public string CreateGame(string? whitePlayerName, string? blackPlayerName)
        {
            throw new InvalidOperationException("deliberate test failure");
        }

        public GameController? GetGame(string gameId)
        {
            throw new InvalidOperationException("deliberate test failure");
        }
    }

    [Test]
    public async Task UnexpectedException_ReturnsJson500_NotHtml()
    {
        // Swap in a throwing service: the request reaches a real endpoint and dies
        // inside the handler, exactly the case the D7 exception handler must cover.
        using WebApplicationFactory<Program> hostile = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IGameService>(new ThrowingGameService())));
        HttpClient client = hostile.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            "/api/games", Json("""{"whitePlayerName":"Alice"}"""));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));

        string raw = await response.Content.ReadAsStringAsync();
        Assert.That(raw.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase), Is.False); // never the developer page
        JsonNode body = JsonNode.Parse(raw)!;
        Assert.That(string.IsNullOrWhiteSpace(body["message"]!.GetValue<string>()), Is.False);
    }

    // ---- D7 (outer layer): an exception that escapes every controller --------

    [Test]
    public async Task ExceptionEscapingTheController_ReturnsGlobalJson500()
    {
        // Sabotage IGameProvider itself: the controller can never be constructed,
        // so the failure bypasses every controller try/catch and only the global
        // exception handler in Program.cs can answer. Its response is the bare
        // {message: "internal server error"} envelope, not MVC problem details.
        using WebApplicationFactory<Program> hostile = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IGameProvider>(_ => throw new InvalidOperationException("provider unavailable"))));
        HttpClient client = hostile.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/games/any");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));

        string raw = await response.Content.ReadAsStringAsync();
        JsonNode body = JsonNode.Parse(raw)!;
        Assert.That(body["message"]!.GetValue<string>(), Is.EqualTo("internal server error"));
    }

    // ---- CORS policy: the allow-everything branch ----------------------------

    [Test]
    public void CorsPolicy_WithoutConfiguredOrigins_AllowsAnyOrigin()
    {
        string contentRoot = Path.Combine(Path.GetTempPath(), "checkers-cors-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        string? previousOrigin = Environment.GetEnvironmentVariable("Cors__AllowedOrigins");
        try
        {
            // A content root without appsettings.json drops the configured origin
            // list, and the leftover scalar cannot bind to string[] (Get returns
            // null), so Program.cs must fall back to Array.Empty and run its
            // allow-everything branch: the policy echoes ANY origin back.
            Environment.SetEnvironmentVariable("Cors__AllowedOrigins", "http://configured.test");
            using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseSetting(WebHostDefaults.ContentRootKey, contentRoot));
            HttpClient client = factory.CreateClient();

            HttpRequestMessage request = new(HttpMethod.Get, "/health");
            request.Headers.Add("Origin", "http://any-origin.test");
            HttpResponseMessage response = client.SendAsync(request).GetAwaiter().GetResult();

            Assert.That(response.Headers.TryGetValues("Access-Control-Allow-Origin", out IEnumerable<string>? origins), Is.True);
            Assert.That(string.Join(",", origins!), Is.EqualTo("http://any-origin.test"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("Cors__AllowedOrigins", previousOrigin);
            try { Directory.Delete(contentRoot, true); } catch (IOException) { }
        }
    }

    // ---- top-level catch in Program.cs ---------------------------------------

    [Test]
    public void StartupFailure_IsSwallowedByTheTopLevelHandler()
    {
        string? previousLevel = Environment.GetEnvironmentVariable("Serilog__MinimumLevel__Default");
        try
        {
            // An unparsable minimum level blows up while the host is being built,
            // inside Program's top-level try. The catch must swallow it and exit
            // without ever building a host - which the test host reports as an
            // InvalidOperationException from CreateClient.
            Environment.SetEnvironmentVariable("Serilog__MinimumLevel__Default", "NotALevel");

            Assert.That(() =>
            {
                using WebApplicationFactory<Program> factory = new();
                factory.CreateClient();
            }, Throws.InstanceOf<InvalidOperationException>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("Serilog__MinimumLevel__Default", previousLevel);
        }
    }
}

