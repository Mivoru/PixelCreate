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
            // A door within reach takes the walker through to its other side.
            foreach (var portal in world.Portals)
            {
                var through = CellOf(portal.To);
                if (Vector2.Distance(new Vector2(cell.X * Cell, cell.Z * Cell), new Vector2(portal.From.X, portal.From.Z)) < Guild.DoorRange &&
                    seen.Add(through))
                {
                    open.Enqueue(through);
                }
            }

            var height = world.GroundHeight(cell.X * Cell, cell.Z * Cell);
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var next = (cell.X + dx, cell.Z + dz);
                if (seen.Contains(next) || Math.Abs(next.Item1) > 800 || Math.Abs(next.Item2) > 300)
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
            foreach (var point in camp.Route ?? Array.Empty<Vector3>())
            {
                Assert.True(ReachedNear(cells, point, 3), $"patrol point {point} of the camp at {camp.Center} cannot be reached");
            }
        }
    }

    [Fact]
    public void Patrols_AndWildCamps_KeepAwayFromTheVillage()
    {
        // The starting village stays safe: nothing hostile lives or walks within 50 m of its middle.
        var village = new Vector3(2, 0, 4);
        foreach (var camp in Valley.World.EnemySpawns.Where(c => c.Types?.Any(t => t.StartsWith("goblin_") || t.StartsWith("orc_")) == true))
        {
            foreach (var point in new[] { camp.Center }.Concat(camp.Route ?? Array.Empty<Vector3>()))
            {
                Assert.True(Vector3.Distance(point with { Y = 0 }, village) > 50f, $"hostile camp or patrol point {point} is too close to the village");
            }
        }
    }

    [Fact]
    public void BothBanks_OfEveryBridge_Connect()
    {
        var cells = Walkable(Valley.World.Spawn);
        foreach (var deck in Valley.Bridges.Select(b => new Deck(b)))
        {
            Assert.True(cells.Contains(CellOf(deck.World(deck.AlongMin + 1, deck.AcrossMiddle))), $"the near end of the bridge at {deck.AlongMin} cannot be reached");
            Assert.True(cells.Contains(CellOf(deck.World(deck.AlongMax - 1, deck.AcrossMiddle))), $"the far end of the bridge at {deck.AlongMax} cannot be reached");
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
            Assert.False(Valley.IsWater(prop.Position.X, prop.Position.Z), $"a tree stands in water at {prop.Position}");
        }
    }

    private static bool IsTree(string model) => model.Contains("tree") || model is "pine" or "pine_b" or "birch" or "willow" or "blossom" or "apple";

    [Fact]
    public void Roads_CrossWaterOnlyOnBridges()
    {
        foreach (var (x, z) in RoadPoints())
        {
            var onBridge = Valley.World.Platforms.Any(p => x >= p.MinX - 3 && x <= p.MaxX + 3 && z >= p.MinZ - 3 && z <= p.MaxZ + 3);
            Assert.True(onBridge || !Valley.IsWater(x, z), $"a road runs through water at {x:F0}, {z:F0}");
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
        var built = Valley.Props.Where(p => p.Model is "bridge" or "bridge_stone" or "cottage" or "cottage_b" or "tavern" or "guild_hall" or "windmill" or "watchtower" or "well" or "stall" or "field").ToArray();
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
        foreach (var deck in Valley.Bridges.Select(b => new Deck(b)))
        {
            foreach (var along in new[] { deck.AlongMin - 0.2f, deck.AlongMax + 0.2f })
            {
                var end = deck.World(along, deck.AcrossMiddle);
                var step = deck.Bridge.Height - Valley.World.Terrain!.At(end.X, end.Z);
                Assert.True(MathF.Abs(step) <= Tuning.StepHeight, $"the bridge end at {end.X:F1}, {end.Z:F1} is {step:F2} m off the ground");
            }
        }
    }

    [Fact]
    public void Bridges_CannotBeWalkedOffAtTheSide()
    {
        foreach (var deck in Valley.Bridges.Select(b => new Deck(b)))
        {
            var edge = deck.World(deck.AlongMiddle, deck.AcrossMin, deck.Bridge.Height + 0.05f);
            Assert.True(Valley.World.OverlapsPlayer(edge), $"the bridge at {deck.Bridge.MinX}, {deck.Bridge.MinZ} has no rail");
            Assert.False(Valley.World.OverlapsPlayer(deck.World(deck.AlongMiddle, deck.AcrossMin, deck.Bridge.Height - 2.6f)), "a rail blocks the way under the bridge");
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
        foreach (var deck in Valley.Bridges.Select(b => new Deck(b)))
        {
            // Also come at each end from an angle, the way a road bends onto a bridge.
            foreach (var (end, outward) in new[] { (deck.AlongMin, -1f), (deck.AlongMax, 1f) })
            {
                foreach (var angle in new[] { -0.7f, -0.35f, 0.35f, 0.7f })
                {
                    var start = deck.World(end + outward * MathF.Cos(angle) * 9f, deck.AcrossMiddle + MathF.Sin(angle) * 9f);
                    start.Y = world.GroundHeight(start.X, start.Z);
                    if (world.OverlapsPlayer(start))
                    {
                        continue; // a building stands there (the guild beside the west bridge)
                    }

                    var goal = deck.World(deck.AlongMiddle, deck.AcrossMiddle);
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
                        $"coming onto the bridge at {end} from angle {angle} stopped at {state.Position.X:F1}, {state.Position.Y:F2}, {state.Position.Z:F1}");
                }
            }

            foreach (var across in new[] { deck.AcrossMiddle, deck.AcrossMin + 0.9f, deck.AcrossMax - 0.9f })
            {
                foreach (var (from, to) in new[] { (deck.AlongMin - 4f, deck.AlongMax + 4f), (deck.AlongMax + 4f, deck.AlongMin - 4f) })
                {
                    var spot = deck.World(from, across);
                    var state = PlayerState.Spawn(spot with { Y = world.GroundHeight(spot.X, spot.Z) });
                    var input = deck.Move(to > from ? 1 : -1, 0);
                    for (var tick = 0; tick < Tuning.TickRate * 12 && MathF.Abs(deck.Along(state.Position) - to) > 0.5f; tick++)
                    {
                        input.Tick = (uint)tick;
                        Movement.Step(ref state, input, world);
                    }

                    Assert.True(MathF.Abs(deck.Along(state.Position) - to) < 1f,
                        $"walking across the bridge {deck.AlongMin}..{deck.AlongMax} at {across:F1} stopped at {state.Position.X:F1}, {state.Position.Y:F2}, {state.Position.Z:F1}");
                }
            }
        }
    }

    /// <summary>
    /// Steps off each bridge end at every point across the deck. The ground mesh never meets the deck
    /// exactly, and a lip there once held a player in place at the east end of the river bridge.
    /// </summary>
    [Fact]
    public void BridgeEnds_CanBeLeftAnywhereAcrossTheDeck()
    {
        var world = Valley.World;
        foreach (var deck in Valley.Bridges.Select(b => new Deck(b)))
        {
            for (var across = deck.AcrossMin + 0.6f; across <= deck.AcrossMax - 0.6f; across += 0.4f)
            {
                foreach (var (end, outward) in new[] { (deck.AlongMin, -1f), (deck.AlongMax, 1f) })
                {
                    var state = PlayerState.Spawn(deck.World(end, across, deck.Bridge.Height));
                    var input = deck.Move(outward, 0);
                    for (var tick = 0; tick < Tuning.TickRate; tick++)
                    {
                        input.Tick = (uint)tick;
                        Movement.Step(ref state, input, world);
                    }

                    Assert.True(MathF.Abs(deck.Along(state.Position) - end) > 2f,
                        $"leaving the bridge at {end}, across {across:F1} stopped at {state.Position.X:F2}, {state.Position.Z:F2}");
                }
            }
        }
    }

    /// <summary>
    /// Walks straight at every fence from both sides. A fence on a slope once sank so far into the
    /// uphill ground that a player could step onto its top and snag along it.
    /// </summary>
    [Fact]
    public void Fences_CannotBeSteppedOver()
    {
        var world = Valley.World;
        foreach (var fence in Valley.Props.Where(p => p.Model == "fence"))
        {
            var (sin, cos) = MathF.SinCos(fence.Yaw);
            var across = new Vector2(sin, cos);
            foreach (var side in new[] { -1f, 1f })
            {
                var start = new Vector2(fence.Position.X, fence.Position.Z) + across * side * 1.5f;
                var state = PlayerState.Spawn(new Vector3(start.X, world.GroundHeight(start.X, start.Y), start.Y));
                if (world.OverlapsPlayer(state.Position))
                {
                    continue;
                }

                var input = new InputCmd { MoveX = (sbyte)(-across.X * side * 127), MoveZ = (sbyte)(-across.Y * side * 127) };
                for (var tick = 0; tick < Tuning.TickRate; tick++)
                {
                    input.Tick = (uint)tick;
                    Movement.Step(ref state, input, world);
                }

                var past = Vector2.Dot(new Vector2(state.Position.X - fence.Position.X, state.Position.Z - fence.Position.Z), across) * side;
                Assert.True(past > 0f, $"walked over the fence at {fence.Position.X}, {fence.Position.Z} from side {side}");
            }
        }
    }

    /// <summary>Every solid thing in the zone has collision measured from its model.</summary>
    [Fact]
    public void SolidModels_HaveMeasuredCollision()
    {
        foreach (var model in Valley.Props.Select(p => p.Model).Distinct())
        {
            Assert.False(Zone.MissingShape(model), $"{model} has no measured collision; rebuild it with scripts/build_assets.ps1");
        }
    }

    /// <summary>
    /// A stockade or fence turned off the grid blocks only along its own line: a walker can stand
    /// close beside it on either side, and cannot stand on it. Its box once covered it from corner
    /// to corner, two metres out from one side of a turned palisade.
    /// </summary>
    [Fact]
    public void TurnedRuns_BlockOnlyAlongTheirLine()
    {
        var world = Valley.World;
        foreach (var run in Valley.Props.Where(p => p.Model is "palisade" or "fence" or "spike_wall" or "stone_wall"))
        {
            var (sin, cos) = MathF.SinCos(run.Yaw);
            var across = new Vector2(sin, cos);
            var half = Zone.CollisionOf(run.Model)!.Value.Y;
            var centre = new Vector2(run.Position.X, run.Position.Z);
            Vector3 At(Vector2 point) => new(point.X, world.GroundHeight(point.X, point.Y) + 0.05f, point.Y);
            Assert.True(world.OverlapsPlayer(At(centre)), $"nothing blocks the middle of the {run.Model} at {centre}");
            foreach (var side in new[] { -1f, 1f })
            {
                var beside = centre + across * side * (half + Tuning.PlayerHalfSize.X + 0.6f);
                var blocked = world.OverlapsPlayer(At(beside));
                // Something else may stand there; only this run's own boxes matter.
                if (blocked && !Valley.Props.Any(p => p != run && p.Model != "fence" && Vector2.Distance(new(p.Position.X, p.Position.Z), beside) < 4f))
                {
                    Assert.Fail($"the {run.Model} at {centre} turned {run.Yaw:F2} blocks {half + 1:F1} m out from its line");
                }
            }
        }
    }

    /// <summary>The dark crags either side of the waterfall are solid all through.</summary>
    [Fact]
    public void FallsCrags_AreSolid()
    {
        var cliff = Valley.Props.Single(p => p.Model == "falls_cliff");
        foreach (var (x, z) in new[] { (-19f, -2f), (20f, -2f), (-15f, -6f), (16f, -6f), (0f, -4f) })
        {
            var point = new Vector3(cliff.Position.X + x, 0, cliff.Position.Z + z);
            point.Y = Valley.World.GroundHeight(point.X, point.Z) + 0.05f;
            Assert.True(Valley.World.OverlapsPlayer(point), $"a walker can stand inside the falls cliff at {point}");
        }
    }

    /// <summary>
    /// A bridge seen along its own deck: it runs along the longer side of its platform. Along is the
    /// way across the water, across is from rail to rail. World turns deck coordinates into a point.
    /// </summary>
    private readonly record struct Deck(Platform Bridge)
    {
        public bool AlongX => Bridge.MaxX - Bridge.MinX >= Bridge.MaxZ - Bridge.MinZ;
        public float AlongMin => AlongX ? Bridge.MinX : Bridge.MinZ;
        public float AlongMax => AlongX ? Bridge.MaxX : Bridge.MaxZ;
        public float AcrossMin => AlongX ? Bridge.MinZ : Bridge.MinX;
        public float AcrossMax => AlongX ? Bridge.MaxZ : Bridge.MaxX;
        public float AcrossMiddle => (AcrossMin + AcrossMax) / 2;
        public float AlongMiddle => (AlongMin + AlongMax) / 2;

        public Vector3 World(float along, float across, float y = 0) => AlongX ? new Vector3(along, y, across) : new Vector3(across, y, along);

        public float Along(Vector3 p) => AlongX ? p.X : p.Z;

        public float Across(Vector3 p) => AlongX ? p.Z : p.X;

        public InputCmd Move(float along, float across) =>
            AlongX ? new InputCmd { MoveX = (sbyte)(along * 127), MoveZ = (sbyte)(across * 127) } : new InputCmd { MoveX = (sbyte)(across * 127), MoveZ = (sbyte)(along * 127) };
    }
}
