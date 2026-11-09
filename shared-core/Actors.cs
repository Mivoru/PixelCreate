using System.Numerics;
using System.Reflection;
using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>Someone who appears only when a scene brings them: from world/actors.json.</summary>
/// <param name="Role">Picks the look on the client and the line under the name in dialogue.</param>
public sealed record ActorDef(string Id, string Name, string Role);

/// <summary>What an actor is doing, for the client's animation.</summary>
public enum ActorPose : byte
{
    Stand = 0,
    Fallen = 1,
    Sit = 2,
}

/// <summary>An actor's state as the client hears it. Only actors away from their resting state are sent.</summary>
public readonly record struct ActorView(ushort Index, Vector3 Position, float Yaw, ActorPose Pose, bool Active);

/// <summary>
/// A person in the world who can move: every NPC of the zone (resting where the zone puts them) and
/// everyone from world/actors.json (away until a scene brings them). The server moves them along
/// paths, lets one follow a player, and sends those not at rest. Both sides number them alike:
/// the zone's NPCs first, then the actors file.
/// </summary>
public sealed class Actor
{
    public required string Id;
    public required string Name;
    public required string Role;
    public required ushort Index;
    public required Vector3 Home;
    public required float HomeYaw;

    /// <summary>True for the zone's NPCs, who are there unless moved; false for scene actors.</summary>
    public required bool Resident;

    public Vector3 Position;
    public float Yaw;
    public ActorPose Pose;
    public bool Active;

    /// <summary>Points still to walk, and how fast.</summary>
    public readonly Queue<Vector3> Path = new();
    public float Speed = 3.5f;

    /// <summary>The player this actor walks behind, or -1.</summary>
    public int Follows = -1;

    /// <summary>Whether the client needs to hear about this actor: moved, posed, or a scene actor in the world.</summary>
    public bool Away => Active != Resident || Pose != ActorPose.Stand || Vector3.DistanceSquared(Position, Home) > 0.01f || MathF.Abs(Yaw - HomeYaw) > 0.01f;

    public void Rest()
    {
        Position = Home;
        Yaw = HomeYaw;
        Pose = ActorPose.Stand;
        Active = Resident;
        Path.Clear();
        Follows = -1;
    }

    /// <summary>Walks one tick along the path, or after the followed player.</summary>
    public void Step(World world, Vector3? leader)
    {
        var step = Speed * Tuning.TickSeconds;
        Vector3? goal = Path.Count > 0 ? Path.Peek() : null;
        if (leader is { } lead && Path.Count == 0)
        {
            var offset = lead - Position;
            offset.Y = 0;
            var distance = offset.Length();
            if (distance > 30f)
            {
                // Left far behind (a door, a fall): step up beside the leader.
                Position = lead + new Vector3(1.2f, 0, 1.2f);
                Position.Y = world.GroundHeight(Position.X, Position.Z);
                return;
            }

            if (distance > 2.6f)
            {
                goal = lead - Vector3.Normalize(offset) * 2.2f;
                step = Math.Min(Tuning.MoveSpeed * 1.15f, MathF.Max(Speed, distance * 2f)) * Tuning.TickSeconds;
            }
            else
            {
                Yaw = MathF.Atan2(offset.X, offset.Z);
            }
        }

        if (goal is not { } target)
        {
            return;
        }

        var to = target - Position;
        to.Y = 0;
        if (to.Length() <= step)
        {
            Position = target with { Y = world.GroundHeight(target.X, target.Z) };
            if (Path.Count > 0)
            {
                Path.Dequeue();
            }

            return;
        }

        var heading = Vector3.Normalize(to);
        Position += heading * step;
        Position.Y = world.GroundHeight(Position.X, Position.Z);
        Yaw = MathF.Atan2(heading.X, heading.Z);
    }
}

public static class Actors
{
    public static IReadOnlyList<ActorDef> Defs { get; } = Load();

    /// <summary>Every actor of a world in the shared order: the zone's NPCs, then the actors file.</summary>
    public static List<Actor> For(World world)
    {
        var list = new List<Actor>();
        foreach (var npc in world.Npcs)
        {
            list.Add(Make(npc.Id, npc.Name, npc.Role, (ushort)list.Count, npc.Position, npc.Yaw, true));
        }

        foreach (var def in Defs)
        {
            list.Add(Make(def.Id, def.Name, def.Role, (ushort)list.Count, world.Spawn, 0f, false));
        }

        return list;
    }

    private static Actor Make(string id, string name, string role, ushort index, Vector3 home, float yaw, bool resident)
    {
        var actor = new Actor { Id = id, Name = name, Role = role, Index = index, Home = home, HomeYaw = yaw, Resident = resident };
        actor.Rest();
        return actor;
    }

    private static List<ActorDef> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("actors");
        return stream is null ? new List<ActorDef>()
            : JsonSerializer.Deserialize<List<ActorDef>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
