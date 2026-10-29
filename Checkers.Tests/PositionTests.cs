using Checkers.Models;

namespace Checkers.Tests;

[TestFixture]
public class PositionTests
{
    [Test]
    public void Constructor_SetsRowAndCol()
    {
        var position = new Position(3, 7);

        Assert.That(position.Row, Is.EqualTo(3));
        Assert.That(position.Col, Is.EqualTo(7));
    }

    [Test]
    public void EqualPositions_CompareEqual()
    {
        var left = new Position(6, 1);
        var right = new Position(6, 1);

        Assert.That(left == right, Is.True);
        Assert.That(left != right, Is.False);
        Assert.That(left.Equals(right), Is.True);
        Assert.That(left.Equals((object)right), Is.True);
    }

    [Test]
    public void DifferentPositions_AreNotEqual()
    {
        var origin = new Position(0, 0);

        Assert.That(origin != new Position(0, 1), Is.True);
        Assert.That(origin != new Position(1, 0), Is.True);
        Assert.That(origin == new Position(0, 1), Is.False);
        Assert.That(origin.Equals(new Position(1, 0)), Is.False);
    }

    [Test]
    public void EqualsObject_ReturnsFalseForNullOrOtherTypes()
    {
        var position = new Position(2, 3);

        Assert.That(position.Equals(null), Is.False);
        Assert.That(position.Equals("2,3"), Is.False);
        Assert.That(position.Equals((object)123), Is.False);
    }

    [Test]
    public void EqualPositions_HaveSameHashCode()
    {
        var left = new Position(6, 1);
        var right = new Position(6, 1);

        Assert.That(right.GetHashCode(), Is.EqualTo(left.GetHashCode()));
    }

    [Test]
    public void Position_WorksAsDictionaryKey()
    {
        var counts = new Dictionary<Position, int>
        {
            [new Position(6, 1)] = 1,
        };

        counts[new Position(6, 1)] = counts[new Position(6, 1)] + 1;

        Assert.That(counts[new Position(6, 1)], Is.EqualTo(2));
    }

    [Test]
    public void Position_WorksInHashSet()
    {
        var seen = new HashSet<Position>
        {
            new(0, 1),
            new(0, 1),
        };

        Assert.That(seen, Has.Count.EqualTo(1));
        Assert.That(seen, Does.Contain(new Position(0, 1)));
    }

    [Test]
    public void ToString_IsPredictable()
    {
        Assert.That(new Position(6, 1).ToString(), Is.EqualTo("(6, 1)"));
        Assert.That(default(Position).ToString(), Is.EqualTo("(0, 0)"));
    }
}
