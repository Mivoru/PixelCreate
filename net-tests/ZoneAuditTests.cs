using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Walks the real zone data, to catch places a player cannot reach or objects in the wrong spot.</summary>
public class ZoneAuditTests
{
    private const float Cell = 1f;
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    /// <summary>Every metre a player can walk to from a start point, stepping up no more than a real step.</summary>
    private static HashSet<(int X, int Z)> Walkable(Vector3 start)
    {
        var world = Valley.World;
        var seen = new HashSet<(int, int)>();
        var open = new Queue<(int X, int Z)>();
        var first = ((int)MathF.Round(start.X / Cell), (int)MathF.Round(start.Z / Cell));
        seen.Add(first);
        open.Enqueue(first);
        while (open.TryDequeue(out var cell))
        {
            var height = world.GroundHeight(cell.X * Cell, cell.Z * Cell);
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var next = (cell.X + dx, cell.Z + dz);
                if (seen.Contains(next) || Math.Abs(next.Item1) > 600 || Math.Abs(next.Item2) > 200)
                {
                    continue;
                }

                var x = next.Item1 * Cell;
                var z = next.Item2 * Cell;
                var ground = world.GroundHeight(x, z);
                if (ground - height <= 1.2f && !world.OverlapsPlayer(new Vector3(x, ground + 0.05f, z)))
                {
                    seen.Add(next);
                    open.Enqueue(next);
                }
            }
        }

        return seen;
    }

    private static (int, int) CellOf(Vector3 point) => ((int)MathF.Round(point.X / Cell), (int)MathF.Round(point.Z / Cell));

    private static bool ReachedNear(HashSet<(int X, int Z)> cells, Vector3 point, int range)
    {
        var (x, z) = CellOf(point);
        for (var dx = -range; dx <= range; dx++)
        {
            for (var dz = -range; dz <= range; dz++)
            {
                if (cells.Contains((x + dx, z + dz)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    [Fact]
    public void Doors_AreReachableFromTheSpawn()
    {
        var cells = Walkable(Valley.World.Spawn);
        Assert.True(ReachedNear(cells, Valley.World.Portals[0].From, 2), "the door of the Guild cannot be reached");
    }

    [Fact]
    public void EnemyCamps_AreReachableFromTheSpawn()
    {
        var cells = Walkable(Valley.World.Spawn);
        foreach (var camp in Valley.World.EnemySpawns)
        {
            Assert.True(ReachedNear(cells, camp.Center, (int)camp.Radius), $"camp at {camp.Center} cannot be reached");
        }
    }

    [Fact]
    public void BothBanks_OfEveryBridge_Connect()
    {
        var cells = Walkable(Valley.World.Spawn);
        foreach (var bridge in Valley.World.Platforms.Where(p => p.MaxX < 400))
        {
            var z = (bridge.MinZ + bridge.MaxZ) / 2;
            Assert.True(cells.Contains(CellOf(new Vector3(bridge.MinX + 1, 0, z))), $"west end of bridge at {bridge.MinX} cannot be reached");
            Assert.True(cells.Contains(CellOf(new Vector3(bridge.MaxX - 1, 0, z))), $"east end of bridge at {bridge.MaxX} cannot be reached");
        }
    }

    [Fact]
    public void GuildInterior_ConnectsTheDoorToMiraAndTheBoard()
    {
        var door = Valley.World.Portals[0].To;
        var cells = Walkable(door);
        foreach (var npc in Valley.World.Npcs.Where(n => n.Position.X > 400))
        {
            Assert.True(ReachedNear(cells, npc.Position, (int)Guild.TalkRange), $"{npc.Name} cannot be reached from the door");
        }
    }

    [Fact]
    public void PortalsAndPeople_NeverStandInsideSomething()
    {
        foreach (var portal in Valley.World.Portals)
        {
            Assert.False(Valley.World.OverlapsPlayer(portal.To + new Vector3(0, 0.05f, 0)), $"a door leads into a wall at {portal.To}");
        }

        Assert.False(Valley.World.OverlapsPlayer(Valley.World.TrialGround + new Vector3(0, 0.05f, 7)), "the trial starts inside a wall");
    }

    [Fact]
    public void Trees_StayOutOfTheWater()
    {
        foreach (var prop in Valley.Props.Where(p => p.Model.Contains("tree")))
        {
            Assert.False(Valley.IsBare(prop.Position.X, prop.Position.Z), $"a tree stands in water at {prop.Position}");
        }
    }

    [Fact]
    public void Bridges_MeetTheGroundAtBothEnds()
    {
        foreach (var bridge in Valley.World.Platforms.Where(p => p.MaxX < 400))
        {
            var z = (bridge.MinZ + bridge.MaxZ) / 2;
            foreach (var x in new[] { bridge.MinX - 0.2f, bridge.MaxX + 0.2f })
            {
                var step = bridge.Height - Valley.World.Terrain!.At(x, z);
                Assert.True(MathF.Abs(step) <= Tuning.StepHeight, $"the bridge end at x {x} is {step:F2} m off the ground");
            }
        }
    }

    [Fact]
    public void Bridges_CannotBeWalkedOffAtTheSide()
    {
        foreach (var bridge in Valley.World.Platforms.Where(p => p.MaxX < 400))
        {
            var middle = (bridge.MinX + bridge.MaxX) / 2;
            var edge = new Vector3(middle, bridge.Height + 0.05f, bridge.MinZ);
            Assert.True(Valley.World.OverlapsPlayer(edge), $"the bridge at {bridge.MinX} has no rail");
            Assert.False(Valley.World.OverlapsPlayer(new Vector3(middle, bridge.Height - 2.6f, bridge.MinZ)), "a rail blocks the way under the bridge");
        }
    }
}
