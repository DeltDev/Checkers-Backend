// ============================================================================
// Checkers.RestDemo - a full game of checkers played end to end over REST
// ----------------------------------------------------------------------------
// Talks to the Checkers backend purely over HTTP (no project reference), the
// way a frontend client would:
//
//   GET  /health                          - is the pipeline alive?
//   POST /api/games                       - 201 + Location + game id
//   GET  /api/games/{id}                  - full board, turn, status
//   GET  /api/games/{id}/moves?playerId   - legal moves for one player
//   POST /api/games/{id}/moves            - play a move (409 when illegal)
//
// It also probes the API contract twice without touching the game:
//   - an unknown game id must 404
//   - a move sent by the wrong player must 409 (and change nothing)
//
// Then it plays White vs Black automatically (captures are mandatory, so the
// policy "biggest capture, else most advanced move" is enough) until the
// game reaches a terminal status, and prints the final board and result.
//
// Usage:
//   dotnet run --project ../Checkers.RestDemo            # starts the API itself
//   dotnet run --project ../Checkers.RestDemo -- http://127.0.0.1:5199
//
// Exit code 0 = game finished cleanly through the API.
// ============================================================================

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

const int MaxPlies = 600; // safety net: a real game ends long before this

string baseUrl = (args.Length > 0 ? args[0] : "http://127.0.0.1:5199").TrimEnd('/');

JsonSerializerOptions json = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter() },
};

using HttpClient http = new() { BaseAddress = new Uri(baseUrl) };
http.Timeout = TimeSpan.FromSeconds(15);

// ---------------------------------------------------------------- step 0 ----
// Make sure the API is up. If it isn't, start it ourselves and stop it again
// when the demo finishes (only if WE started it).
Process? server = null;

if (!await HealthyAsync())
{
    Console.WriteLine($"[0] {baseUrl} is not answering - starting the Checkers API...");
    server = StartServer(baseUrl);
    if (!await WaitForHealthyAsync(TimeSpan.FromSeconds(90)))
    {
        Console.Error.WriteLine("[0] FAIL: the API did not come up within 90s.");
        StopServer(server);
        return 1;
    }
}

Console.WriteLine($"[0] OK   GET /health -> 200 ({baseUrl})");

// ---------------------------------------------------------------- step 1 ----
// Create the game.
var (createdOk, created, createRaw) = await PostAsync<GameStateDto>(
    "/api/games",
    new { whitePlayerName = "Ada", blackPlayerName = "Grace" });

if (!createdOk || created is null || createRaw.StatusCode != System.Net.HttpStatusCode.Created)
{
    Console.Error.WriteLine($"[1] FAIL: POST /api/games -> {await DescribeAsync(createRaw)}");
    StopServer(server);
    return 1;
}

string gameId = created.GameId;
Console.WriteLine($"[1] OK   POST /api/games -> 201, Location: /api/games/{gameId}");
Console.WriteLine($"     players: {created.Players[0].Name} ({created.Players[0].Color}, id {created.Players[0].Id}) vs " +
                  $"{created.Players[1].Name} ({created.Players[1].Color}, id {created.Players[1].Id})");

// ---------------------------------------------------------------- step 2 ----
// Initial state.
var (stateOk, state, stateRaw) = await GetAsync<GameStateDto>($"/api/games/{gameId}");
if (!stateOk || state is null)
{
    Console.Error.WriteLine($"[2] FAIL: GET /api/games/{gameId} -> {(int)stateRaw.StatusCode}");
    StopServer(server);
    return 1;
}

Console.WriteLine($"[2] OK   GET /api/games/{gameId} -> 200, turn {state.Turn}, {state.TurnColor}, {state.Status}");
PrintBoard(state);

// ---------------------------------------------------------------- step 3 ----
// Two contract probes that do NOT change the game.
var (missingOk, _, missingRaw) = await GetAsync<GameStateDto>("/api/games/does-not-exist");
Console.WriteLine(!missingOk && (int)missingRaw.StatusCode == 404
    ? $"[3] OK   GET /api/games/does-not-exist -> {await DescribeAsync(missingRaw)}"
    : $"[3] FAIL: GET an unknown game id should 404, got {await DescribeAsync(missingRaw)}");

int whiteId = state.Players.Single(p => p.Color == PieceColor.White).Id;
int blackId = state.Players.Single(p => p.Color == PieceColor.Black).Id;

// It is White's turn; ask for BLACK's legal moves (must be an empty list, a
// normal 200 answer), then try to send White's move as Black (must 409).
var (whiteMovesOk, whiteMoves, _) = await GetAsync<LegalMovesDto>(
    $"/api/games/{gameId}/moves?playerId={whiteId}");
if (!whiteMovesOk || whiteMoves is null || whiteMoves.Moves.Count == 0)
{
    Console.Error.WriteLine("[3] FAIL: no legal moves for the side to move");
    StopServer(server);
    return 1;
}

var (blackListOk, blackList, blackListRaw) = await GetAsync<LegalMovesDto>(
    $"/api/games/{gameId}/moves?playerId={blackId}");
Console.WriteLine(blackListOk && blackList is not null && blackList.Moves.Count == 0
    ? "[3] OK   GET moves for the player NOT to move -> 200 with an empty list"
    : $"[3] FAIL: expected an empty move list, got {await DescribeAsync(blackListRaw)} / {blackList?.Moves.Count} moves");

MoveDto wrongTurn = whiteMoves.Moves[0];
var (wrongOk, wrong, wrongRaw) = await PostAsync<ActionResultDto>(
    $"/api/games/{gameId}/moves",
    new { playerId = blackId, from = wrongTurn.From, to = wrongTurn.To, path = wrongTurn.Path });
Console.WriteLine(!wrongOk && (int)wrongRaw.StatusCode == 409
    ? $"[3] OK   POST a move by the wrong player -> {await DescribeAsync(wrongRaw)}"
    : $"[3] FAIL: expected 409, got {await DescribeAsync(wrongRaw)}");

// ---------------------------------------------------------------- step 4 ----
// Play the game out: GET state -> GET legal moves -> POST move, every ply.
Console.WriteLine();
Console.WriteLine("[4] playing the game over REST:");

int plies = 0;
while (true)
{
    var (sOk, s, sRaw) = await GetAsync<GameStateDto>($"/api/games/{gameId}");
    if (!sOk || s is null)
    {
        Console.Error.WriteLine($"     FAIL: GET state -> {await DescribeAsync(sRaw)}");
        StopServer(server);
        return 1;
    }

    if (s.Status != GameStatus.Ongoing)
    {
        break;
    }

    if (plies >= MaxPlies)
    {
        Console.Error.WriteLine($"     FAIL: still {s.Status} after {MaxPlies} plies - no natural end?");
        StopServer(server);
        return 1;
    }

    int moverId = s.TurnColor == PieceColor.White ? whiteId : blackId;

    var (mOk, legal, mRaw) = await GetAsync<LegalMovesDto>(
        $"/api/games/{gameId}/moves?playerId={moverId}");
    if (!mOk || legal is null || legal.Moves.Count == 0)
    {
        Console.Error.WriteLine($"     FAIL: legal moves -> {await DescribeAsync(mRaw)}, {legal?.Moves.Count} moves, " +
                                $"but status is {s.Status}");
        StopServer(server);
        return 1;
    }

    // Biggest capture wins (captures are mandatory); otherwise push the piece
    // closest to promotion. Ties break on the API's own ordering - deterministic.
    List<MoveDto> candidates = legal.Moves
        .OrderByDescending(m => m.CapturedPieceIds.Length)
        .ThenByDescending(m => s.TurnColor == PieceColor.White ? 9 - m.To.Row : m.To.Row)
        .ThenBy(m => m.From.Row).ThenBy(m => m.From.Col)
        .ToList();

    ActionResultDto? played = null;
    HttpResponseMessage? moveRaw = null;
    foreach (MoveDto candidate in candidates)
    {
        var (pOk, body, raw) = await PostAsync<ActionResultDto>(
            $"/api/games/{gameId}/moves",
            new { playerId = moverId, from = candidate.From, to = candidate.To, path = candidate.Path });
        moveRaw = raw;
        if (pOk && body is { Success: true })
        {
            played = body;
            string captures = candidate.CapturedPieceIds.Length > 0
                ? $" x{candidate.CapturedPieceIds.Length} (ids {string.Join(",", candidate.CapturedPieceIds)})"
                : "";
            Console.WriteLine($"     ply {plies + 1,3}  {s.TurnColor,-5} {candidate.Notation,-24} {candidate.Type}{captures}");
            break;
        }

        // The server re-derives the reason; show it and try the next candidate.
        Console.WriteLine($"     ply {plies + 1,3}  rejected: {await DescribeAsync(raw)}");
    }

    if (played is null)
    {
        Console.Error.WriteLine($"     FAIL: every legal candidate was rejected at ply {plies + 1}: {await DescribeAsync(moveRaw!)}");
        StopServer(server);
        return 1;
    }

    plies++;
}

// ---------------------------------------------------------------- step 5 ----
// Final state.
var (finalOk, final, finalRaw) = await GetAsync<GameStateDto>($"/api/games/{gameId}");
if (!finalOk || final is null)
{
    Console.Error.WriteLine($"[5] FAIL: final GET -> {await DescribeAsync(finalRaw)}");
    StopServer(server);
    return 1;
}

Console.WriteLine();
Console.WriteLine($"[5] OK   game over after {plies} plies ({final.History.Count} moves in history)");
PrintBoard(final);
Console.WriteLine($"     status : {final.Status}");
Console.WriteLine(final.WinnerId is null
    ? "     result : draw"
    : $"     result : {final.Players.Single(p => p.Id == final.WinnerId).Name} ({final.Players.Single(p => p.Id == final.WinnerId).Color}) wins");

// ---------------------------------------------------------------- checks ----
bool clean = true;
void Check(bool condition, string what)
{
    Console.WriteLine($"     {(condition ? "OK  " : "FAIL")} {what}");
    clean &= condition;
}

Check(final.Status != GameStatus.Ongoing, "terminal status is not Ongoing");
Check(final.History.Count == plies, $"history holds every played move ({final.History.Count} == {plies})");
Check(final.Turn == plies + 1, $"turn counter is 1-based ({final.Turn} == {plies} + 1)");
Check(final.Squares.Count == 100, "state still carries all 100 squares");
Check(final.WinnerId is null || final.Players.Any(p => p.Id == final.WinnerId),
      "winnerId refers to a player of this game");

StopServer(server);

Console.WriteLine();
Console.WriteLine(clean ? "DEMO COMPLETE - full game played start to end over REST." : "DEMO FINISHED WITH FAILED CHECKS.");
return clean ? 0 : 1;

// ============================================================================
// helpers
// ============================================================================

async Task<bool> HealthyAsync()
{
    try
    {
        HttpResponseMessage response = await http.GetAsync("/health");
        return response.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}

async Task<bool> WaitForHealthyAsync(TimeSpan timeout)
{
    Stopwatch clock = Stopwatch.StartNew();
    while (clock.Elapsed < timeout)
    {
        if (await HealthyAsync())
        {
            return true;
        }

        await Task.Delay(500);
    }

    return false;
}

async Task<(bool ok, T? body, HttpResponseMessage raw)> GetAsync<T>(string url)
{
    try
    {
        HttpResponseMessage response = await http.GetAsync(url);
        string text = await response.Content.ReadAsStringAsync();
        return (response.IsSuccessStatusCode,
                response.IsSuccessStatusCode ? JsonSerializer.Deserialize<T>(text, json) : default,
                response);
    }
    catch (Exception ex)
    {
        return (false, default, new HttpResponseMessage(System.Net.HttpStatusCode.RequestTimeout)
        {
            ReasonPhrase = ex.Message,
        });
    }
}

async Task<(bool ok, T? body, HttpResponseMessage raw)> PostAsync<T>(string url, object payload)
{
    try
    {
        string body = JsonSerializer.Serialize(payload, json);
        HttpResponseMessage response = await http.PostAsync(url,
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        string text = await response.Content.ReadAsStringAsync();
        return (response.IsSuccessStatusCode,
                response.IsSuccessStatusCode ? JsonSerializer.Deserialize<T>(text, json) : default,
                response);
    }
    catch (Exception ex)
    {
        return (false, default, new HttpResponseMessage(System.Net.HttpStatusCode.RequestTimeout)
        {
            ReasonPhrase = ex.Message,
        });
    }
}

static async Task<string> DescribeAsync(HttpResponseMessage response)
{
    string text;
    try
    {
        text = await response.Content.ReadAsStringAsync();
    }
    catch
    {
        text = string.Empty;
    }

    // Success bodies carry no message; failures carry { "message": "..." }.
    try
    {
        using JsonDocument document = JsonDocument.Parse(text);
        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("message", out JsonElement message))
        {
            return $"{(int)response.StatusCode} {message.GetString()}";
        }
    }
    catch
    {
        // Not JSON - fall through to the raw body.
    }

    return string.IsNullOrWhiteSpace(text)
        ? $"{(int)response.StatusCode} {response.ReasonPhrase}"
        : $"{(int)response.StatusCode} {text}";
}

Process StartServer(string url)
{
    // The API project sits next to this one: <Checkers-Backend>/Checkers/Checkers.csproj
    DirectoryInfo? dir = new(AppContext.BaseDirectory);
    string? project = null;
    while (dir is not null)
    {
        string candidate = Path.Combine(dir.FullName, "Checkers", "Checkers.csproj");
        if (File.Exists(candidate))
        {
            project = candidate;
            break;
        }

        dir = dir.Parent;
    }

    if (project is null)
    {
        throw new InvalidOperationException("Could not locate ../Checkers/Checkers.csproj next to this demo.");
    }

    ProcessStartInfo psi = new("dotnet",
        $"run --project \"{project}\" --no-launch-profile --urls {url}")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
        WorkingDirectory = Path.GetDirectoryName(project)!,
    };

    Process process = Process.Start(psi)
        ?? throw new InvalidOperationException("Failed to start the Checkers API process.");

    // Drain the child's output so it can never block on a full pipe; show it
    // only if the API fails to come up.
    _ = Task.Run(async () =>
    {
        string? line;
        while ((line = await process.StandardOutput.ReadLineAsync()) is not null)
        {
            Debug.WriteLine(line);
        }
    });
    _ = Task.Run(async () =>
    {
        string? line;
        while ((line = await process.StandardError.ReadLineAsync()) is not null)
        {
            Debug.WriteLine(line);
        }
    });

    return process;
}

void StopServer(Process? process)
{
    if (process is null || process.HasExited)
    {
        return;
    }

    try
    {
        process.Kill(entireProcessTree: true);
        process.WaitForExit(5000);
    }
    catch
    {
        // Already gone - nothing to clean up.
    }
}

static void PrintBoard(GameStateDto state)
{
    Console.WriteLine("       " + string.Join(" ", Enumerable.Range(0, 10).Select(c => c.ToString())));
    for (int row = 0; row < 10; row++)
    {
        string line = $"     {row}  ";
        for (int col = 0; col < 10; col++)
        {
            SquareDto square = state.Squares[row * 10 + col];
            string glyph = square.OccupantColor switch
            {
                PieceColor.White => square.OccupantType == PieceType.King ? "O" : "o",
                PieceColor.Black => square.OccupantType == PieceType.King ? "X" : "x",
                _ => square.IsDark ? "·" : " ",
            };
            line += glyph + (col == 9 ? "" : " ");
        }

        Console.WriteLine(line);
    }

    Console.WriteLine("        legend: o/O = white man/king, x/X = black man/king, · = empty dark square");
}

// ============================================================================
// wire DTOs - mirror the backend contracts (camelCase JSON, string enums)
// ============================================================================

sealed class GameStateDto
{
    public string GameId { get; set; } = string.Empty;
    public int Turn { get; set; }
    public PieceColor TurnColor { get; set; }
    public GameStatus Status { get; set; }
    public int? WinnerId { get; set; }
    public List<PlayerDto> Players { get; set; } = new();
    public List<SquareDto> Squares { get; set; } = new();
    public List<MoveDto> History { get; set; } = new();
}

sealed class PlayerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PieceColor Color { get; set; }
}

sealed class SquareDto
{
    public PositionDto Position { get; set; } = new();
    public bool IsDark { get; set; }
    public int? OccupantId { get; set; }
    public PieceColor? OccupantColor { get; set; }
    public PieceType? OccupantType { get; set; }
}

sealed class MoveDto
{
    public PositionDto From { get; set; } = new();
    public PositionDto To { get; set; } = new();
    public PositionDto[] Path { get; set; } = Array.Empty<PositionDto>();
    public int[] CapturedPieceIds { get; set; } = Array.Empty<int>();
    public MoveType Type { get; set; }
    public PieceColor Color { get; set; }
    public string Notation { get; set; } = string.Empty;
}

sealed class LegalMovesDto
{
    public List<MoveDto> Moves { get; set; } = new();
}

sealed class ActionResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public GameStatus Status { get; set; }
}

sealed class PositionDto
{
    public int Row { get; set; }
    public int Col { get; set; }
}

enum PieceColor { White, Black }
enum PieceType { Man, King }
enum MoveType { Quiet, Capture }
enum GameStatus { Ongoing, WhiteWon, BlackWon, Draw }
