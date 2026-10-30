using System.Text.RegularExpressions;
using Checkers.Models;
using Checkers.Services;

namespace Checkers.Tests;

/// <summary>
/// PHASE 10 — Services layer (IMPLEMENTATION_PLAN Phase 6 + phase 6 pseudocode).
/// Decisions encoded:
///   D1 — game ids are 12-char URL-safe tokens [A-Za-z0-9_-].
///   D2 — the store must be safe under concurrent CreateGame (lock or equivalent);
///        all 200 parallel ids must be distinct and retrievable.
///   D3 — IGameStore.Add with a duplicate id throws ArgumentException.
///   Naming — a missing or blank player name defaults to "Player 1" / "Player 2";
///        provided names are trimmed.
/// All tests are expected to FAIL until Phase 6 is implemented.
/// </summary>
[TestFixture]
public class GameServiceTests
{
    private static GameService CreateService()
    {
        return new GameService(new InMemoryGameStore(), new GameFactory(), new GameIdGenerator());
    }

    // ------------------------------------------------------- GameIdGenerator

    [Test]
    public void NextId_IsUrlSafe_TwelveChars()
    {
        GameIdGenerator generator = new();
        var regex = new Regex("^[A-Za-z0-9_-]{12}$", RegexOptions.Compiled);

        for (int i = 0; i < 100; i++)
        {
            string id = generator.NextId();
            Assert.That(string.IsNullOrWhiteSpace(id), Is.False);
            Assert.That(id, Does.Match(regex));
        }
    }

    // ----------------------------------------------------------- GameService

    [Test]
    public void CreateGame_ReturnsId_AndGameIsRetrievable()
    {
        GameService service = CreateService();

        string id = service.CreateGame("Alice", "Bob");

        Assert.That(string.IsNullOrWhiteSpace(id), Is.False);
        GameController? game = service.GetGame(id);
        Assert.That(game, Is.Not.Null);
        var players = game!.GetPlayers();
        Assert.That(players.Count, Is.EqualTo(2));
        Assert.That(players[0].Name, Is.EqualTo("Alice"));
        Assert.That(players[1].Name, Is.EqualTo("Bob"));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    [Test]
    public void CreateGame_Twice_DistinctIds_IndependentGames()
    {
        GameService service = CreateService();

        string id1 = service.CreateGame("Alice", "Bob");
        string id2 = service.CreateGame("Carol", "Dave");

        Assert.That(id2, Is.Not.EqualTo(id1));
        GameController? game1 = service.GetGame(id1);
        GameController? game2 = service.GetGame(id2);
        Assert.That(game1, Is.Not.Null);
        Assert.That(game2, Is.Not.Null);
        Assert.That(game2, Is.Not.SameAs(game1));

        // Isolation: finishing game1 must not touch game2.
        game1!.Resign(game1.GetPlayers()[0]);
        Assert.That(game1.Status, Is.EqualTo(GameStatus.BlackWon));
        Assert.That(game2!.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game2.GetWinner(), Is.Null);
    }

    [Test]
    public void GetGame_UnknownEmptyOrNull_ReturnsNull()
    {
        GameService service = CreateService();

        Assert.That(service.GetGame("no-such-id"), Is.Null);
        Assert.That(service.GetGame(""), Is.Null);
        Assert.That(service.GetGame(null!), Is.Null);
    }

    [Test]
    public void CreateGame_200ParallelGames_AllDistinctAndStored()
    {
        GameService service = CreateService();
        string[] ids = new string[200];

        Parallel.For(0, 200, i => ids[i] = service.CreateGame($"White{i}", $"Black{i}"));

        // D2: no lost updates or id collisions under concurrency.
        Assert.That(ids.Distinct().Count(), Is.EqualTo(200));
        foreach (string id in ids)
        {
            Assert.That(service.GetGame(id), Is.Not.Null);
        }
    }

    // ------------------------------------------------------------ GameFactory

    [TestCase(null, null)]
    [TestCase("", "")]
    [TestCase("   ", "  ")]
    public void Factory_MissingOrBlankNames_DefaultToPlayerOneAndTwo(string? whiteName, string? blackName)
    {
        GameController game = new GameFactory().Create(whiteName, blackName);
        var players = game.GetPlayers();

        Assert.That(players[0].Name, Is.EqualTo("Player 1"));
        Assert.That(players[1].Name, Is.EqualTo("Player 2"));
    }

    [Test]
    public void Factory_ProvidedNames_AreTrimmed()
    {
        GameController game = new GameFactory().Create("  Alice  ", "Bob ");

        var players = game.GetPlayers();
        Assert.That(players[0].Name, Is.EqualTo("Alice"));
        Assert.That(players[1].Name, Is.EqualTo("Bob"));
    }

    [Test]
    public void Factory_PlayerIdsAndColors_AreFixed()
    {
        GameController game = new GameFactory().Create(null, null);
        var players = game.GetPlayers();

        Assert.That(players[0].Id, Is.EqualTo(1));
        Assert.That(players[0].Color, Is.EqualTo(PieceColor.White));
        Assert.That(players[1].Id, Is.EqualTo(2));
        Assert.That(players[1].Color, Is.EqualTo(PieceColor.Black));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.Turn, Is.EqualTo(1));
        Assert.That(game.TurnColor, Is.EqualTo(PieceColor.White));
    }

    [Test]
    public void Factory_CreatesIndependentGames()
    {
        GameFactory factory = new();
        GameController first = factory.Create("Alice", "Bob");
        GameController second = factory.Create("Alice", "Bob");

        Assert.That(second, Is.Not.SameAs(first));
        first.Resign(first.GetPlayers()[0]);
        Assert.That(first.Status, Is.EqualTo(GameStatus.BlackWon));
        Assert.That(second.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    // ---------------------------------------------------------- InMemoryGameStore

    [Test]
    public void Store_AddAndGet_ReturnsSameInstance()
    {
        InMemoryGameStore store = new();
        GameController game = new GameFactory().Create(null, null);

        store.Add("game-1", game);

        Assert.That(store.Get("game-1"), Is.SameAs(game));
    }

    [Test]
    public void Store_Get_UnknownOrMissingKey_ReturnsNull()
    {
        InMemoryGameStore store = new();

        Assert.That(store.Get("never-added"), Is.Null);
        Assert.That(store.Get(""), Is.Null);
    }

    [Test]
    public void Store_Get_NullKey_IsRejectedByTheServiceNotTheStore()
    {
        InMemoryGameStore store = new();
        GameService service = new(store, new GameFactory(), new GameIdGenerator());

        // The store keeps Dictionary's null-key contract; GameService.GetGame
        // screens null/empty ids before the store is ever touched, so the REST
        // layer can never reach the dictionary with a null key.
        Assert.Throws<ArgumentNullException>(() => store.Get(null!));
        Assert.That(service.GetGame(null!), Is.Null);
    }

    [Test]
    public void Store_Add_DuplicateId_ThrowsArgumentException()
    {
        InMemoryGameStore store = new();
        GameController first = new GameFactory().Create(null, null);
        GameController second = new GameFactory().Create(null, null);
        store.Add("dup", first);

        Assert.Throws<ArgumentException>(() => store.Add("dup", second));
    }

    // ------------------------------------------- id allocation collision paths

    private sealed class SequenceIdGenerator : IGameIdGenerator
    {
        private readonly Queue<string> _ids = new();
        private readonly string _fallback;

        public SequenceIdGenerator(params string[] ids)
        {
            foreach (string id in ids)
            {
                _ids.Enqueue(id);
            }

            _fallback = ids[^1];
        }

        public string NextId() => _ids.Count > 0 ? _ids.Dequeue() : _fallback;
    }

    private sealed class ConstantIdGenerator : IGameIdGenerator
    {
        private readonly string _id;

        public ConstantIdGenerator(string id)
        {
            _id = id;
        }

        public string NextId() => _id;
    }

    [Test]
    public void CreateGame_WhenGeneratedIdIsTaken_RetriesWithAFreshId()
    {
        InMemoryGameStore store = new();
        store.Add("dup", new GameFactory().Create(null, null));
        GameService service = new(store, new GameFactory(), new SequenceIdGenerator("dup", "fresh"));

        string id = service.CreateGame("Alice", "Bob");

        Assert.That(id, Is.EqualTo("fresh"));
        Assert.That(service.GetGame(id), Is.Not.Null);
    }

    [Test]
    public void CreateGame_WhenEveryAttemptCollides_Throws()
    {
        InMemoryGameStore store = new();
        store.Add("dup", new GameFactory().Create(null, null));
        GameService service = new(store, new GameFactory(), new ConstantIdGenerator("dup"));

        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(
            () => service.CreateGame("Alice", "Bob"));

        Assert.That(ex!.Message, Is.EqualTo("cannot allocate a unique game id"));
    }
}

