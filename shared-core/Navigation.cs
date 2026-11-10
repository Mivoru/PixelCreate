using System.Numerics;

namespace PixelCreate.Core;

/// <summary>A place to be led to, with what to call it on the map. <paramref name="Radius"/> above 0 makes it an area (where monsters live): inside it the way ends.</summary>
public readonly record struct NavTarget(Vector3 At, string Label, float Radius = 0f, int Camp = -1);

/// <summary>
/// Where the player should go: the place the active quest step points to (the person to talk to,
/// the camp of the monsters to kill, where an item to bring drops), or a point the player chose on
/// the map. A place inside a building is reached through its door. Client and tests share this, so
/// the marker on the map and the route on the ground always agree with the quest.
/// </summary>
public static class Navigation
{
    /// <summary>The quest the player is led by: the prologue while it lasts, then the first active job or quest in data order.</summary>
    public static (StoryQuest Quest, QuestStep Step)? Tracked(IStoryContext context)
    {
        var world = context.World;
        foreach (var quest in Story.Quests.OrderBy(q => q.Kind == "story" ? 0 : 1))
        {
            if (world.Quest(quest.Id) is { Status: QuestStatus.Active } state && state.Step < quest.Steps.Length)
            {
                return (quest, quest.Steps[state.Step]);
            }
        }

        return null;
    }

    /// <summary>
    /// The place the tracked quest step leads to, or null when it leads nowhere to walk. Camps the
    /// player has found empty (<paramref name="cleared"/>, by their place in <see cref="World.EnemySpawns"/>)
    /// are passed over for the next nearest one where the creatures still live.
    /// </summary>
    public static NavTarget? QuestTarget(IStoryContext context, World world, Func<int, bool>? cleared = null)
    {
        if (Tracked(context) is not { } tracked || !tracked.Step.Route)
        {
            return null;
        }

        var (quest, step) = tracked;
        if (step.At is { } at)
        {
            return new NavTarget(at, quest.Name);
        }

        var goal = step.Goal;
        switch (goal.Kind)
        {
            case "talk":
                return PersonAt(world, goal.Target) is { } person ? new NavTarget(person.Position, person.Name) : null;
            case "bring" when context.ItemCount(goal.Target) >= goal.Count:
                // Everything is in the bag: the objective says to return to the Guild; no way is drawn.
                return null;
            case "bring":
                return NearestCampArea(world, context.Position, def => def.Loot?.Any(l => l.Item == goal.Target) == true, cleared) is { } source
                    ? new NavTarget(source.Center, ItemDef.TryIndex(goal.Target, out var item) ? ItemDef.All[item].Name : goal.Target, source.Radius, source.Index)
                    : null;
            case "kill":
                return NearestCampArea(world, context.Position, def => goal.MatchesKill(def.Id), cleared) is { } camp
                    ? new NavTarget(camp.Center, quest.Name, camp.Radius, camp.Index)
                    : null;
            default:
                return null;
        }
    }

    private static Npc? PersonAt(World world, string id) =>
        world.Npcs.FirstOrDefault(n => n.Id == id) is { Id: not null } npc ? npc : null;

    /// <summary>The middle of the nearest camp where an enemy of the kind lives.</summary>
    public static Vector3? NearestCamp(World world, Vector3 from, Func<EnemyDef, bool> kind) => NearestCampArea(world, from, kind)?.Center;

    /// <summary>What the tracked step hunts, to tell whether a camp still holds any: the kill target, or the creatures that drop the item.</summary>
    public static Func<EnemyDef, bool>? Hunted(IStoryContext context) =>
        Tracked(context) is { Step.Goal: var goal } ? goal.Kind switch
        {
            "kill" => def => goal.MatchesKill(def.Id),
            "bring" => def => def.Loot?.Any(l => l.Item == goal.Target) == true,
            _ => null,
        } : null;

    /// <summary>How far round a camp's middle its creatures roam: the area the map marks for a hunt.</summary>
    public const float CampAreaMargin = 10f;

    /// <summary>The nearest camp of the kind as an area: its middle and the radius its creatures roam in.</summary>
    public static (Vector3 Center, float Radius, int Index)? NearestCampArea(World world, Vector3 from, Func<EnemyDef, bool> kind, Func<int, bool>? cleared = null)
    {
        (Vector3, float, int)? best = null;
        var bestDistance = float.MaxValue;
        for (var index = 0; index < world.EnemySpawns.Count; index++)
        {
            var spawn = world.EnemySpawns[index];
            if (cleared?.Invoke(index) == true)
            {
                continue;
            }

            var types = spawn.Types ?? new[] { EnemyDef.All[0].Id };
            if (!types.Any(t => EnemyDef.All.FirstOrDefault(d => d.Id == t) is { } def && kind(def)))
            {
                continue;
            }

            var distance = Vector2.Distance(new Vector2(spawn.Center.X, spawn.Center.Z), new Vector2(from.X, from.Z));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = (spawn.Center, spawn.Radius + CampAreaMargin, index);
            }
        }

        return best;
    }

    /// <summary>
    /// Where a player off the way should rejoin it: the point on the way (or its end) that makes the
    /// whole trip shortest, the straight walk there plus the rest of the way from there, reachable by a
    /// clear line. A player who cuts across a bend is led on ahead, never back to a point behind.
    /// Returns the index of the leg the point lies on (the point leads into path[leg + 1]) and the point,
    /// or null when no point within reach can be walked to straight (then the way is planned again).
    /// </summary>
    public static (int Leg, Vector3 Point)? Rejoin(IReadOnlyList<Vector3> path, Vector3 player, World world, float reach = 60f)
    {
        if (path.Count < 2)
        {
            return null;
        }

        // What is left of the way from the end of each leg.
        var after = new float[path.Count];
        for (var i = path.Count - 2; i >= 0; i--)
        {
            after[i] = after[i + 1] + Flat(path[i], path[i + 1]);
        }

        var from = new Vector2(player.X, player.Z);
        (int, Vector3)? best = null;
        var bestCost = float.MaxValue;
        for (var leg = 0; leg < path.Count - 1; leg++)
        {
            var a = path[leg];
            var b = path[leg + 1];
            var length = Flat(a, b);
            var samples = Math.Max(1, (int)(length / 2f));
            for (var s = 0; s <= samples; s++)
            {
                var t = (float)s / samples;
                var point = Vector3.Lerp(a, b, t);
                var straight = Vector2.Distance(from, new Vector2(point.X, point.Z));
                var cost = straight + (1f - t) * length + after[leg + 1];
                if (straight > reach || cost >= bestCost || !RoadMap.Clear(from, new Vector2(point.X, point.Z), world))
                {
                    continue;
                }

                bestCost = cost;
                best = (leg, point);
            }
        }

        return best;
    }

    private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

    /// <summary>
    /// Where to walk to reach a place: the place itself, or, when it lies on the other side of a door
    /// (inside a building while the player is out, or the other way round), that door.
    /// </summary>
    public static Vector3 Reachable(World world, Vector3 player, Vector3 place)
    {
        var playerInside = player.X >= Guild.InteriorX;
        var placeInside = place.X >= Guild.InteriorX;
        if (playerInside == placeInside)
        {
            return place;
        }

        // The door on the player's side whose other end lies nearest the place.
        var doors = world.Portals.Where(p => (p.From.X >= Guild.InteriorX) == playerInside && (p.To.X >= Guild.InteriorX) == placeInside);
        var door = doors.OrderBy(p => Vector3.DistanceSquared(p.To with { Y = 0 }, place with { Y = 0 })).Cast<Portal?>().FirstOrDefault();
        return door?.From ?? place;
    }
}
