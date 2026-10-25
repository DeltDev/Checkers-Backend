using System.Text.Json.Serialization;

namespace Checkers.Models;

public struct Position
{
    public int Row { get; }
    public int Col { get; }
    [JsonConstructor]
    public Position(int row, int col)
    {
        Row = row;
        Col = col;
    }
    public bool Equals(Position other)
    {
        return Row == other.Row && Col == other.Col;
    }

    public override bool Equals(object? obj)
    {
        return obj is Position other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Row, Col);
    }

    public static bool operator ==(Position left, Position right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Position left, Position right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"({Row}, {Col})";
    }

}
