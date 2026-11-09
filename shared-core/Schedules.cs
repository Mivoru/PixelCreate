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
public sealed record ScheduleBlock(int FromHour, Vector3? At, float Yaw, Vector3? Home, ActorPose Pose, Condition When);

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
        return new ScheduleBlock(
            b.GetProperty("from").GetInt32(), Point(b, "at"),
            b.TryGetProperty("yaw", out var yaw) ? yaw.GetSingle() * MathF.PI / 180f : 0f,
            Point(b, "home"), pose, Condition.Parse(b.TryGetProperty("when", out var when) ? when : (JsonElement?)null));
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

    /// <summary>
    /// The way from one place to another: straight when they are close or no road is near, else to the
    /// nearest road, along the roads, and off again. The last point is the goal itself.
    /// </summary>
    public List<Vector3> Route(Vector3 from, Vector3 to, World world)
    {
        var start = new Vector2(from.X, from.Z);
        var goal = new Vector2(to.X, to.Z);
        var way = new List<Vector3>();
        var direct = Vector2.Distance(start, goal);
        var first = Nearest(start);
        var last = Nearest(goal);
        if (direct > 10f && first >= 0 && last >= 0 && first != last &&
            Vector2.Distance(start, _nodes[first]) < 14f && Vector2.Distance(goal, _nodes[last]) < 14f)
        {
            var path = Shortest(first, last);
            // Skip road points that only lead back: begin at the first one that brings the walker nearer the goal's road.
            while (path.Count > 1 && Vector2.Distance(start, _nodes[path[1]]) < Vector2.Distance(_nodes[path[0]], _nodes[path[1]]))
            {
                path.RemoveAt(0);
            }

            while (path.Count > 1 && Vector2.Distance(goal, _nodes[path[^2]]) < Vector2.Distance(_nodes[path[^1]], _nodes[path[^2]]))
            {
                path.RemoveAt(path.Count - 1);
            }

            foreach (var node in path)
            {
                var point = _nodes[node];
                way.Add(new Vector3(point.X, world.GroundHeight(point.X, point.Y), point.Y));
            }
        }

        way.Add(to with { Y = world.GroundHeight(to.X, to.Z) });
        return way;
    }

    private int Nearest(Vector2 point)
    {
        var best = -1;
        var nearest = float.MaxValue;
        for (var i = 0; i < _nodes.Count; i++)
        {
            var distance = Vector2.DistanceSquared(point, _nodes[i]);
            if (distance < nearest)
            {
                nearest = distance;
                best = i;
            }
        }

        return best;
    }

    private List<int> Shortest(int from, int to)
    {
        var cost = Enumerable.Repeat(float.MaxValue, _nodes.Count).ToArray();
        var before = Enumerable.Repeat(-1, _nodes.Count).ToArray();
        var open = new PriorityQueue<int, float>();
        cost[from] = 0;
        open.Enqueue(from, 0);
        while (open.TryDequeue(out var node, out var reached))
        {
            if (node == to)
            {
                break;
            }

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
                    open.Enqueue(next, cost[next]);
                }
            }
        }

        var path = new List<int>();
        if (cost[to] == float.MaxValue)
        {
            return path;
        }

        for (var node = to; node >= 0; node = before[node])
        {
            path.Add(node);
        }

        path.Reverse();
        return path;
    }
}
