using System.Numerics;

namespace PixelCreate.Core;

/// <summary>A place to be led to, with what to call it on the map.</summary>
public readonly record struct NavTarget(Vector3 At, string Label);

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

    /// <summary>The place the tracked quest step leads to, or null when it leads nowhere to walk.</summary>
    public static NavTarget? QuestTarget(IStoryContext context, World world)
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
                // Everything is in the bag: hand it in to Mira.
                return PersonAt(world, "mira") is { } mira ? new NavTarget(mira.Position, mira.Name) : null;
            case "bring":
                return NearestCamp(world, context.Position, def => def.Loot?.Any(l => l.Item == goal.Target) == true) is { } source
                    ? new NavTarget(source, ItemDef.TryIndex(goal.Target, out var item) ? ItemDef.All[item].Name : goal.Target)
                    : null;
            case "kill":
                return NearestCamp(world, context.Position, def => goal.MatchesKill(def.Id)) is { } camp
                    ? new NavTarget(camp, quest.Name)
                    : null;
            default:
                return null;
        }
    }

    private static Npc? PersonAt(World world, string id) =>
        world.Npcs.FirstOrDefault(n => n.Id == id) is { Id: not null } npc ? npc : null;

    /// <summary>The middle of the nearest camp where an enemy of the kind lives.</summary>
    public static Vector3? NearestCamp(World world, Vector3 from, Func<EnemyDef, bool> kind)
    {
        Vector3? best = null;
        var bestDistance = float.MaxValue;
        foreach (var spawn in world.EnemySpawns)
        {
            var types = spawn.Types ?? new[] { EnemyDef.All[0].Id };
            if (!types.Any(t => EnemyDef.All.FirstOrDefault(d => d.Id == t) is { } def && kind(def)))
            {
                continue;
            }

            var distance = Vector2.Distance(new Vector2(spawn.Center.X, spawn.Center.Z), new Vector2(from.X, from.Z));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = spawn.Center;
            }
        }

        return best;
    }

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
