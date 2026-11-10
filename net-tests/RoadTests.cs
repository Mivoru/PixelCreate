using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>People and bots walk by the road map: along the roads, and never through what stands in the way.</summary>
public class RoadTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");
    private static readonly RoadMap Roads = new(Valley.World.Roads);

    private static Vector3 At(float x, float z) => new(x, Valley.World.GroundHeight(x, z), z);

    [Theory]
    [InlineData(-2f, 12.5f, -36f, 25f)]     // Bram's well to the Guild door: the hall's corner once stopped the bot
    [InlineData(19.5f, 6f, -2f, 12.5f)]     // the bakery to the well
    [InlineData(30f, 34f, 18f, 5.5f)]       // Pip's field to the bakery
    [InlineData(-36f, 26f, 11f, -27f)]      // the Guild to the south gate
    [InlineData(71.5f, 76f, 52f, 44f)]      // the ambush on the ruins road to the road west
    public void Route_GoesRoundWhatStandsInTheWay(float fromX, float fromZ, float toX, float toZ)
    {
        var from = At(fromX, fromZ);
        var way = Roads.Route(from, At(toX, toZ), Valley.World);
        Assert.Equal(toX, way[^1].X, 2);
        Assert.Equal(toZ, way[^1].Z, 2);
        var at = new Vector2(from.X, from.Z);
        foreach (var point in way)
        {
            var next = new Vector2(point.X, point.Z);
            Assert.True(RoadMap.Clear(at, next, Valley.World), $"something stands between {at} and {next}");
            at = next;
        }
    }

    [Fact]
    public void Route_IsStraightWhenShortAndClear()
    {
        var way = Roads.Route(At(-2, 12), At(1, 15), Valley.World);
        Assert.Single(way);
    }

    [Fact]
    public void Route_IsStraightWhereNoRoadIsNear()
    {
        // Inside the Guild hall, far from every road: once the bot was sent to a road hundreds of metres away.
        var way = Roads.Route(At(420, 6.5f), At(428.2f, 6.6f), Valley.World);
        Assert.Single(way);
        Assert.Single(Roads.Route(At(420, 6.5f), At(409, -6), Valley.World));
    }

    [Fact]
    public void Route_FollowsTheRoadOnALongWalk()
    {
        // From the well to the Guild door the road bends round the hall: more than the goal alone.
        var way = Roads.Route(At(-2, 12.5f), At(-36, 25), Valley.World);
        Assert.True(way.Count >= 3, $"only {way.Count} points");
        // Every point but the last lies on a road.
        foreach (var point in way.Take(way.Count - 1))
        {
            Assert.True(Valley.PathDistance(point.X, point.Z) < 1.5f, $"{point} is off the roads");
        }
    }
}
