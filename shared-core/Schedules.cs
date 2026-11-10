using System.Numerics;
using System.Reflection;
using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>
/// One part of someone's day, from world/schedules.json: from an hour on they stand at a place, or
/// are at home behind a door (not in the world, nobody to talk to).
/// </summary>
/// <param name="At">Where they stand; null with <paramref name="Home"/> set.</param>
/// <param name="Home">The door they go in by; they are out of the world once there.</param>
/// <param name="When">A block with a condition is skipped unless it holds, so a day can change after an event.</param>
/// <param name="Via">Where they come out when they were indoors somewhere far off: the other side of a door.</param>
public sealed record ScheduleBlock(int FromHour, Vector3? At, float Yaw, Vector3? Home, ActorPose Pose, Condition When, Vector3? Via = null)
{
    /// <summary>Points walked last on the way in and first on the way out: round the table to the far bench, round the end of a fence.</summary>
    public Vector3[] By { get; init; } = Array.Empty<Vector3>();

    /// <summary>What kind of place this is, when that matters: at the "tavern" people pass on what they have heard.</summary>
    public string Place { get; init; } = "";
}

/// <summary>Where someone rests now: the place, the facing, the pose, and whether they are in the world at all.</summary>
public readonly record struct RestSpot(Vector3 At, float Yaw, ActorPose Pose, bool Present);

/// <summary>
/// The days of the people of the world. Server and client read the same file and the same world
/// state, so the client knows where everyone rests without being told: the server sends only those
/// on their way.
/// </summary>
public static class Schedules
{
    public static IReadOnlyDictionary<string, ScheduleBlock[]> All { get; } = Load();

    /// <summary>The block of someone's day that holds at an hour: the latest one begun, or last night's before the first.</summary>
    public static ScheduleBlock? Now(string id, IStoryContext context, int hour)
    {
        if (!All.TryGetValue(id, out var blocks))
        {
            return null;
        }

        ScheduleBlock? best = null;
        foreach (var block in blocks)
        {
            if (block.FromHour <= hour && (best is null || block.FromHour > best.FromHour) && block.When.Holds(context))
            {
                best = block;
            }
        }

        if (best is not null)
        {
            return best;
        }

        foreach (var block in blocks)
        {
            if ((best is null || block.FromHour > best.FromHour) && block.When.Holds(context))
            {
                best = block;
            }
        }

        return best;
    }

    /// <summary>Where an actor rests at an hour: by their day if they have one, else where the zone or the actors file puts them.</summary>
    public static RestSpot RestOf(Actor actor, IStoryContext context, int hour, World world)
    {
        if (Now(actor.Id, context, hour) is not { } block)
        {
            return new RestSpot(actor.Home, actor.HomeYaw, ActorPose.Stand, actor.Resident);
        }

        var spot = block.Home ?? block.At ?? actor.Home;
        spot.Y = world.GroundHeight(spot.X, spot.Z);
        return new RestSpot(spot, block.Yaw, block.Pose, block.Home is null);
    }

    /// <summary>
    /// The way from where someone is to a place of their day: out by the points of the place they leave,
    /// along the roads, and in by the points of the place they go to.
    /// </summary>
    public static List<Vector3> Way(RoadMap roads, World world, Vector3 from, IReadOnlyList<Vector3> leaving, IReadOnlyList<Vector3> arriving, Vector3 goal)
    {
        Vector3 Ground(Vector3 point) => point with { Y = world.GroundHeight(point.X, point.Z) };
        var way = new List<Vector3>();
        for (var i = leaving.Count - 1; i >= 0; i--)
        {
            way.Add(Ground(leaving[i]));
        }

        way.AddRange(roads.Route(way.Count > 0 ? way[^1] : from, arriving.Count > 0 ? arriving[0] : goal, world));
        for (var i = 1; i < arriving.Count; i++)
        {
            way.Add(Ground(arriving[i]));
        }

        if (arriving.Count > 0)
        {
            way.Add(Ground(goal));
        }

        return way;
    }

    /// <summary>Every condition reference in the schedules, for the tests that check names against the data.</summary>
    public static IEnumerable<(string Kind, string Name, string Where)> References() =>
        All.SelectMany(p => p.Value.SelectMany(b => b.When.References().Select(r => (r.Kind, r.Name, $"schedule {p.Key}"))));

    private static Dictionary<string, ScheduleBlock[]> Load()
    {
        var all = new Dictionary<string, ScheduleBlock[]>(StringComparer.Ordinal);
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("schedules");
        if (stream is null)
        {
            return all;
        }

        using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        foreach (var person in doc.RootElement.EnumerateObject())
        {
            all[person.Name] = person.Value.EnumerateArray().Select(Parse).ToArray();
        }

        return all;
    }

    private static ScheduleBlock Parse(JsonElement b)
    {
        static Vector3? Point(JsonElement e, string name) =>
            e.TryGetProperty(name, out var p) ? new Vector3(p[0].GetSingle(), 0, p[1].GetSingle()) : null;

        var pose = b.TryGetProperty("pose", out var po) ? Enum.Parse<ActorPose>(po.GetString()!, true) : ActorPose.Stand;
        var at = Point(b, "at");
        // "face" names a place to look at (the table from a bench); "yaw" a direction in degrees.
        var facing = at is { } stand && Point(b, "face") is { } look ? Actor.Facing(look - stand)
            : b.TryGetProperty("yaw", out var yaw) ? yaw.GetSingle() * MathF.PI / 180f : 0f;
        return new ScheduleBlock(
            b.GetProperty("from").GetInt32(), at, facing,
            Point(b, "home"), pose, Condition.Parse(b.TryGetProperty("when", out var when) ? when : (JsonElement?)null), Point(b, "via"))
        {
            By = b.TryGetProperty("by", out var by)
                ? by.EnumerateArray().Select(p => new Vector3(p[0].GetSingle(), 0, p[1].GetSingle())).ToArray() : Array.Empty<Vector3>(),
            Place = b.TryGetProperty("place", out var place) ? place.GetString()! : "",
        };
    }
}

/// <summary>
/// The roads of a zone as a net of points, so people walk along them and round the houses instead of
/// through them. Roads that meet or pass close are joined.
/// </summary>
public sealed class RoadMap
{
    private const float JoinRange = 3f;
    private readonly List<Vector2> _nodes = new();
    private readonly List<List<(int To, float Cost)>> _edges = new();

    public RoadMap(IEnumerable<Vector2[]> courses)
    {
        var roads = courses.Where(c => c.Length > 1).ToArray();
        var chains = new List<List<int>>();
        for (var r = 0; r < roads.Length; r++)
        {
            // Where another road ends on the middle of this one, the meeting point becomes a point of this road too.
            var chain = new List<int>();
            for (var i = 0; i < roads[r].Length - 1; i++)
            {
                var a = roads[r][i];
                var b = roads[r][i + 1];
                chain.Add(Add(a));
                var joins = new List<(float T, Vector2 Point)>();
                for (var other = 0; other < roads.Length; other++)
                {
                    if (other == r)
                    {
                        continue;
                    }

                    foreach (var end in new[] { roads[other][0], roads[other][^1] })
                    {
                        var t = Vector2.Dot(end - a, b - a) / Vector2.DistanceSquared(a, b);
                        var foot = a + (b - a) * Math.Clamp(t, 0f, 1f);
                        if (t is > 0.05f and < 0.95f && Vector2.Distance(foot, end) < JoinRange)
                        {
                            joins.Add((t, foot));
                        }
                    }
                }

                foreach (var (_, point) in joins.OrderBy(j => j.T))
                {
                    chain.Add(Add(point));
                }
            }

            chain.Add(Add(roads[r][^1]));
            chains.Add(chain);
        }

        foreach (var chain in chains)
        {
            for (var i = 0; i < chain.Count - 1; i++)
            {
                Link(chain[i], chain[i + 1]);
            }
        }

        // Points of different roads that lie close are one crossing.
        for (var i = 0; i < _nodes.Count; i++)
        {
            for (var j = i + 1; j < _nodes.Count; j++)
            {
                if (Vector2.Distance(_nodes[i], _nodes[j]) < JoinRange && !_edges[i].Any(e => e.To == j))
                {
                    Link(i, j);
                }
            }
        }
    }

    private int Add(Vector2 point)
    {
        _nodes.Add(point);
        _edges.Add(new List<(int, float)>());
        return _nodes.Count - 1;
    }

    private void Link(int a, int b)
    {
        if (a == b)
        {
            return;
        }

        var cost = Vector2.Distance(_nodes[a], _nodes[b]);
        _edges[a].Add((b, cost));
        _edges[b].Add((a, cost));
    }

    /// <summary>How far off a road someone may step onto it or leave it.</summary>
    private const float ReachRange = 30f;

    /// <summary>
    /// The way from one place to another, round what stands in between. Straight when the line is
    /// clear and short (or the roads would be a long way round); otherwise onto a road by a clear
    /// line, along the roads, and off again by a clear line. The last point is the goal itself.
    /// </summary>
    public List<Vector3> Route(Vector3 from, Vector3 to, World world)
    {
        var start = new Vector2(from.X, from.Z);
        var goal = new Vector2(to.X, to.Z);
        var end = to with { Y = world.GroundHeight(to.X, to.Z) };
        var direct = Vector2.Distance(start, goal);
        var open = Clear(start, goal, world);
        if (open && direct <= 12f)
        {
            return new List<Vector3> { end };
        }

        // Every road point that can be walked to in a straight line is a way on; the same for ways off.
        var on = Reachable(start, world);
        var off = Reachable(goal, world);
        if (on.Count == 0 || off.Count == 0)
        {
            return new List<Vector3> { end };
        }

        var cost = Enumerable.Repeat(float.MaxValue, _nodes.Count).ToArray();
        var before = Enumerable.Repeat(-1, _nodes.Count).ToArray();
        var queue = new PriorityQueue<int, float>();
        foreach (var (node, walk) in on)
        {
            cost[node] = walk;
            queue.Enqueue(node, walk);
        }

        while (queue.TryDequeue(out var node, out var reached))
        {
            if (reached > cost[node])
            {
                continue;
            }

            foreach (var (next, step) in _edges[node])
            {
                if (cost[node] + step < cost[next])
                {
                    cost[next] = cost[node] + step;
                    before[next] = node;
                    queue.Enqueue(next, cost[next]);
                }
            }
        }

        var last = -1;
        var total = float.MaxValue;
        foreach (var (node, walk) in off)
        {
            if (cost[node] < float.MaxValue && cost[node] + walk < total)
            {
                total = cost[node] + walk;
                last = node;
            }
        }

        // Across open ground people cut the corner when the roads are much the longer way.
        if (last < 0 || (open && total > direct * 1.7f))
        {
            return new List<Vector3> { end };
        }

        var way = new List<Vector3>();
        for (var node = last; node >= 0; node = before[node])
        {
            var point = _nodes[node];
            way.Add(new Vector3(point.X, world.GroundHeight(point.X, point.Y), point.Y));
        }

        way.Reverse();
        way.Add(end);
        return way;
    }

    /// <summary>Road points near a place with nothing solid on the line to them, with the walk to each.</summary>
    private List<(int Node, float Walk)> Reachable(Vector2 place, World world)
    {
        var near = new List<(int Node, float Walk)>();
        for (var i = 0; i < _nodes.Count; i++)
        {
            var walk = Vector2.Distance(place, _nodes[i]);
            if (walk < ReachRange)
            {
                near.Add((i, walk));
            }
        }

        var clear = near.OrderBy(n => n.Walk).Where(n => Clear(place, _nodes[n.Node], world)).Take(6).ToList();
        if (clear.Count > 0)
        {
            return clear;
        }

        // Nothing clear: the nearest road point within reach stands in. With no road near at all (indoors,
        // the wilds) there is no way on, and the walk is straight.
        return near.OrderBy(n => n.Walk).Take(1).Select(n => (n.Node, n.Walk * 3f)).ToList();
    }

    /// <summary>Whether someone can walk the straight line between two places: nothing solid stands on it.</summary>
    public static bool Clear(Vector2 a, Vector2 b, World world)
    {
        var length = Vector2.Distance(a, b);
        var steps = Math.Max(1, (int)(length / 0.4f));
        for (var i = 1; i < steps; i++)
        {
            var point = Vector2.Lerp(a, b, i / (float)steps);
            if (world.OverlapsPlayer(new Vector3(point.X, world.GroundHeight(point.X, point.Y) + 0.1f, point.Y)))
            {
                return false;
            }
        }

        return true;
    }
}
