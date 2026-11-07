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
        foreach (var bridge in Valley.Bridges)
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

    private static bool IsTree(string model) => model.Contains("tree") || model is "pine" or "pine_b" or "birch" or "willow" or "blossom" or "apple";

    [Fact]
    public void Roads_CrossWaterOnlyOnBridges()
    {
        foreach (var (x, z) in RoadPoints())
        {
            var onBridge = Valley.World.Platforms.Any(p => x >= p.MinX - 3 && x <= p.MaxX + 3 && z >= p.MinZ - 3 && z <= p.MaxZ + 3);
            Assert.True(onBridge || !Valley.IsBare(x, z), $"a road runs through water at {x:F0}, {z:F0}");
        }
    }

    [Fact]
    public void Roads_AreNotBlockedBySolidThings()
    {
        foreach (var (x, z) in RoadPoints())
        {
            var y = Valley.World.GroundHeight(x, z) + 0.05f;
            Assert.False(Valley.World.OverlapsPlayer(new Vector3(x, y, z)), $"something stands on the road at {x:F0}, {z:F0}");
        }
    }

    [Fact]
    public void Roads_LeadFromPlaceToPlace()
    {
        foreach (var end in Valley.RoadEnds)
        {
            var known = Valley.RegionAt(end.X, end.Y).Length > 0 || Valley.PathDistanceExcept(end) < 3f ||
                Valley.Props.Any(p => Vector2.Distance(new Vector2(p.Position.X, p.Position.Z), end) < 8f && p.Model is not ("rock" or "bush" or "flowers" or "fern"));
            Assert.True(known, $"a road ends at nothing at {end}");
        }
    }

    [Fact]
    public void Trees_KeepClearOfBridgesRoadsAndBuildings()
    {
        var built = Valley.Props.Where(p => p.Model is "bridge" or "cottage" or "cottage_b" or "tavern" or "guild_hall" or "windmill" or "watchtower" or "well" or "stall" or "field").ToArray();
        foreach (var tree in Valley.Props.Where(p => IsTree(p.Model) && p.Scale > 0f && !Authored(p)))
        {
            Assert.True(Valley.PathDistance(tree.Position.X, tree.Position.Z) > 2.5f, $"a tree stands on the road at {tree.Position}");
            foreach (var thing in built)
            {
                var gap = Vector2.Distance(new Vector2(tree.Position.X, tree.Position.Z), new Vector2(thing.Position.X, thing.Position.Z));
                Assert.True(gap > 5f, $"a tree at {tree.Position} grows into the {thing.Model} at {thing.Position}");
            }
        }
    }

    [Fact]
    public void BuiltThings_StandBesideTheRoads()
    {
        foreach (var prop in Valley.Props.Where(p => Authored(p) && p.Model is "lantern_post" or "signpost" or "barrel" or "crate" or "bench" or "well" or "cart" or "fence"))
        {
            Assert.True(Valley.PathDistance(prop.Position.X, prop.Position.Z) > 0.3f, $"the {prop.Model} at {prop.Position} stands in the road");
        }
    }

    private static bool Authored(PlacedProp prop) => prop.Scale == 1f;

    private static IEnumerable<(float X, float Z)> RoadPoints()
    {
        foreach (var points in Valley.RoadCourses)
        {
            for (var i = 0; i + 1 < points.Length; i++)
            {
                for (var t = 0f; t < 1f; t += 0.1f)
                {
                    var point = Vector2.Lerp(points[i], points[i + 1], t);
                    yield return (point.X, point.Y);
                }
            }
        }
    }

    [Fact]
    public void Bridges_MeetTheGroundAtBothEnds()
    {
        foreach (var bridge in Valley.Bridges)
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
        foreach (var bridge in Valley.Bridges)
        {
            var middle = (bridge.MinX + bridge.MaxX) / 2;
            var edge = new Vector3(middle, bridge.Height + 0.05f, bridge.MinZ);
            Assert.True(Valley.World.OverlapsPlayer(edge), $"the bridge at {bridge.MinX} has no rail");
            Assert.False(Valley.World.OverlapsPlayer(new Vector3(middle, bridge.Height - 2.6f, bridge.MinZ)), "a rail blocks the way under the bridge");
        }
    }

    /// <summary>
    /// Walks across every bridge with the real movement code, along the middle and along both
    /// sides, in both directions, without jumping. A snag anywhere stops the walker short.
    /// </summary>
    [Fact]
    public void Bridges_CanBeWalkedAcrossWithoutJumping()
    {
        var world = Valley.World;
        foreach (var bridge in Valley.Bridges)
        {
            var middle = (bridge.MinZ + bridge.MaxZ) / 2;
            // Also come at each end from an angle, the way a road bends onto a bridge.
            foreach (var (endX, outward) in new[] { (bridge.MinX, -1f), (bridge.MaxX, 1f) })
            {
                foreach (var angle in new[] { -0.7f, -0.35f, 0.35f, 0.7f })
                {
                    var start = new Vector3(endX + outward * MathF.Cos(angle) * 9f, 0, middle + MathF.Sin(angle) * 9f);
                    start.Y = world.GroundHeight(start.X, start.Z);
                    var goal = new Vector3((bridge.MinX + bridge.MaxX) / 2, 0, middle);
                    var state = PlayerState.Spawn(start);
                    for (var tick = 0; tick < Tuning.TickRate * 12; tick++)
                    {
                        var heading = Vector3.Normalize(new Vector3(goal.X - state.Position.X, 0, goal.Z - state.Position.Z));
                        if (new Vector2(goal.X - state.Position.X, goal.Z - state.Position.Z).Length() < 1f)
                        {
                            break;
                        }

                        Movement.Step(ref state, new InputCmd { Tick = (uint)tick, MoveX = (sbyte)(heading.X * 127), MoveZ = (sbyte)(heading.Z * 127) }, world);
                    }

                    Assert.True(new Vector2(goal.X - state.Position.X, goal.Z - state.Position.Z).Length() < 1.5f,
                        $"coming onto the bridge at x {endX} from angle {angle} stopped at {state.Position.X:F1}, {state.Position.Y:F2}, {state.Position.Z:F1}");
                }
            }

            foreach (var z in new[] { middle, bridge.MinZ + 0.9f, bridge.MaxZ - 0.9f })
            {
                foreach (var (from, to) in new[] { (bridge.MinX - 4f, bridge.MaxX + 4f), (bridge.MaxX + 4f, bridge.MinX - 4f) })
                {
                    var state = PlayerState.Spawn(new Vector3(from, world.GroundHeight(from, z), z));
                    var input = new InputCmd { MoveX = (sbyte)(to > from ? 127 : -127) };
                    for (var tick = 0; tick < Tuning.TickRate * 12 && MathF.Abs(state.Position.X - to) > 0.5f; tick++)
                    {
                        input.Tick = (uint)tick;
                        Movement.Step(ref state, input, world);
                    }

                    Assert.True(MathF.Abs(state.Position.X - to) < 1f,
                        $"walking across the bridge at x {bridge.MinX}..{bridge.MaxX}, z {z:F1} stopped at x {state.Position.X:F1}, y {state.Position.Y:F2}");
                }
            }
        }
    }
}
