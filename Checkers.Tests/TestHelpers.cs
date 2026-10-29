using System.Reflection;
using Checkers.Models;

namespace Checkers.Tests;


public static class TestHelpers
{
    public static (GameController Game, Player White, Player Black) CreateGame(string whiteName = "Alice", string blackName = "Bob")
    {
        Player white = new(1, whiteName, PieceColor.White);
        Player black = new(2, blackName, PieceColor.Black);
        GameController game = new(white, black);
        return (game, white, black);
    }

    public static void ClearBoard(GameController game)
    {
        foreach (Position pos in Board.AllPositions())
        {
            game.RemovePiece(pos);
        }
    }

    public static void Place(GameController game, int row, int col, PieceColor color, PieceType type, int id)
    {
        Position pos = new(row, col);
        Assert.That(GameController.IsPlayable(pos), Is.True, $"Test setup: square {pos} is not playable.");
        game.PlacePiece(pos, new Piece(id, color, type));
       
    }

    /// <summary>Full 100-square occupant snapshot, for "state unchanged" assertions.</summary>
    public static (int? Id, PieceColor? Color, PieceType? Type)[,] Snapshot(GameController game)
    {
        var snapshot = new (int?, PieceColor?, PieceType?)[Board.Size, Board.Size];
        for (int row = 0; row < Board.Size; row++)
        {
            for (int col = 0; col < Board.Size; col++)
            {
                IPiece? occupant = game.Board.Squares[row, col].Occupant;
                snapshot[row, col] = occupant is null ? (null, null, null) : (occupant.Id, occupant.Color, occupant.Type);
            }
        }

        return snapshot;
    }

    /// <summary>Reads a private instance field (bookkeeping seams like _kingOnlyStreak).</summary>
    public static T GetPrivate<T>(object target, string fieldName)
    {
        FieldInfo? field = target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No private instance field '{fieldName}' on {target.GetType().Name}.");
        return (T)field.GetValue(target)!;
    }

    /// <summary>Writes a private instance field (used only to reach far-away thresholds such as the king-only streak).</summary>
    public static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo? field = target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No private instance field '{fieldName}' on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    /// <summary>Finds the canonical legal move for From/To, or fails with a diagnostic.</summary>
    public static Move GetLegal(GameController game, IPlayer player, Position from, Position to)
    {
        Assert.That(
            TryGetLegal(game, player, from, to, out Move move),
            Is.True,
            $"No legal move {from}->{to} for player {player.Id} ({player.Color}); " +
            $"legal moves: [{string.Join(", ", game.GetLegalMoves(player).Select(m => GameController.ToNotation(m)))}].");
        return move;
    }

    public static bool TryGetLegal(GameController game, IPlayer player, Position from, Position to, out Move move)
    {
        List<Move> matches = game.GetLegalMoves(player)
            .Where(m => m.From == from && m.To == to)
            .ToList();

        if (matches.Count == 0)
        {
            move = default;
            return false;
        }

        move = matches[0];
        return true;
    }
}
