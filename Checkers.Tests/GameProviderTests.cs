using Checkers.Services;

namespace Checkers.Tests;

/// <summary>
/// GameProvider's short-circuit path: a null/empty game id must be reported as
/// "game not found" without ever touching the underlying IGameService.
/// </summary>
[TestFixture]
public class GameProviderTests
{
    [Test]
    public void OpenGame_NullOrEmptyId_IsNotFoundWithoutTouchingTheService()
    {
        GameProvider provider = new(new GameService(
            new InMemoryGameStore(), new GameFactory(), new GameIdGenerator()));

        foreach (string? id in new[] { null, "" })
        {
            GameAccess access = provider.OpenGame(id!);

            Assert.That(access.IsReady, Is.False);
            Assert.That(access.Game, Is.Null);
            Assert.That(access.Error, Is.EqualTo("game not found"));
        }
    }
}
