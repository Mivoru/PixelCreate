using System.Numerics;
using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>
/// One step of a scene. The server runs the steps in order: most happen at once, a few wait (a line
/// to read, a choice to make, a walk to finish, a fade, a condition, enemies to fall). The client
/// shows what the current step and the ones before it ask: camera, fade, slides, a line.
/// </summary>
/// <param name="Kind">
/// lockInput, unlockInput, spawn, despawn, place, move, follow, unfollow, pose, camera, playerCamera,
/// fade, slides, say, talk, teleport, time, effects, wait, waitFor, spawnEnemies, waitForKills, drop, checkpoint, scene.
/// </param>
public sealed record SceneStep(string Kind)
{
    public string Actor { get; init; } = "";
    public string Text { get; init; } = "";
    public string? Face { get; init; }
    public string Node { get; init; } = "";
    public Vector3[] Points { get; init; } = Array.Empty<Vector3>();
    public float Yaw { get; init; }
    public float Speed { get; init; } = 3.5f;
    public bool Wait { get; init; }
    public float Seconds { get; init; }
    public ActorPose Pose { get; init; }
    public Vector3 From { get; init; }
    public Vector3 To { get; init; }
    public string[] Slides { get; init; } = Array.Empty<string>();
    public Condition When { get; init; } = Condition.Always;
    public Effect[] Effects { get; init; } = Array.Empty<Effect>();
    public string Item { get; init; } = "";
    public int Count { get; init; } = 1;
    public string Tag { get; init; } = "";
    public string Scene { get; init; } = "";
    public bool Out { get; init; }
    public int Day { get; init; }
    public int Hour { get; init; }
    public int Level { get; init; } = 1;

    /// <summary>The share of their full health that spawned enemies start with: a first fight can be a gentle one.</summary>
    public float Health { get; init; } = 1f;

    /// <summary>A speaker's name for someone who is not an actor (Rhea in the arena).</summary>
    public string Name { get; init; } = "";

    /// <summary>The speaker walks up to the player before a line or a talk, instead of waiting to be walked up to.</summary>
    public bool Approach { get; init; }

    /// <summary>A line called out while the player keeps playing: shown as a banner, never holding the controls.</summary>
    public bool Bark { get; init; }

    /// <summary>How long a line stays when nobody presses on: long enough to read.</summary>
    public float ReadSeconds => 1.6f + Text.Length * 0.055f;
}

/// <summary>What starts a scene by itself: a condition and, if given, a place the player must be near.</summary>
public sealed record SceneTrigger(Condition When, Vector3? Near, float Radius);

public sealed record SceneDef(string Id, int Index, SceneStep[] Steps, SceneTrigger? Trigger)
{
    /// <summary>Ends the scene at once when it holds: the fight it comments on is over, the player has left.</summary>
    public Condition? Until { get; init; }

    /// <summary>The flag set when the scene has played to its end.</summary>
    public string DoneFlag => $"scene.{Id}";
}

public static class SceneData
{
    /// <summary>Reads one scene file. Values are read now; the document is disposed after.</summary>
    public static SceneDef Parse(JsonElement root, int index)
    {
        var steps = root.GetProperty("steps").EnumerateArray().Select(ParseStep).ToArray();
        SceneTrigger? trigger = null;
        if (root.TryGetProperty("trigger", out var t))
        {
            Vector3? near = t.TryGetProperty("near", out var n) ? Ground(n) : null;
            trigger = new SceneTrigger(Condition.Parse(t.TryGetProperty("when", out var w) ? w : null), near,
                t.TryGetProperty("radius", out var r) ? r.GetSingle() : 6f);
        }

        return new SceneDef(root.GetProperty("id").GetString()!, index, steps, trigger)
        {
            Until = root.TryGetProperty("until", out var u) ? Condition.Parse(u) : null,
        };
    }

    private static Vector3 Ground(JsonElement point) => new(point[0].GetSingle(), 0, point[1].GetSingle());

    private static Vector3 Space(JsonElement point) => new(point[0].GetSingle(), point[1].GetSingle(), point[2].GetSingle());

    private static float Degrees(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetSingle() * MathF.PI / 180f : 0f;

    private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetString()! : "";

    private static SceneStep ParseStep(JsonElement element)
    {
        var property = element.EnumerateObject().First();
        var v = property.Value;
        var wait = element.TryGetProperty("wait", out var w) && w.ValueKind == JsonValueKind.True;
        return property.Name switch
        {
            "lockInput" => new SceneStep("lockInput"),
            "unlockInput" => new SceneStep("unlockInput"),
            "spawn" => new SceneStep("spawn") { Actor = Str(v, "actor"), Points = new[] { Ground(v.GetProperty("at")) }, Yaw = Degrees(v, "yaw") },
            "place" => new SceneStep("place") { Actor = Str(v, "actor"), Points = new[] { Ground(v.GetProperty("at")) }, Yaw = Degrees(v, "yaw") },
            "despawn" => new SceneStep("despawn") { Actor = v.GetString()! },
            "move" => new SceneStep("move")
            {
                Actor = Str(v, "actor"), Points = v.GetProperty("to").EnumerateArray().Select(Ground).ToArray(),
                Speed = v.TryGetProperty("speed", out var s) ? s.GetSingle() : 3.5f, Wait = wait,
            },
            "follow" => new SceneStep("follow") { Actor = v.GetString()! },
            "unfollow" => new SceneStep("unfollow") { Actor = v.GetString()! },
            "pose" => new SceneStep("pose") { Actor = Str(v, "actor"), Pose = Enum.Parse<ActorPose>(Str(v, "pose"), true) },
            "camera" => new SceneStep("camera") { From = Space(v.GetProperty("from")), To = Space(v.GetProperty("to")) },
            "playerCamera" => new SceneStep("playerCamera"),
            "fade" => new SceneStep("fade") { Out = Str(v, "to") == "out", Seconds = v.TryGetProperty("seconds", out var fs) ? fs.GetSingle() : 1f },
            "slides" => new SceneStep("slides")
            {
                Slides = v.GetProperty("images").EnumerateArray().Select(i => i.GetString()!).ToArray(),
                Text = Str(v, "caption"), Seconds = v.TryGetProperty("seconds", out var ss) ? ss.GetSingle() : 4f,
            },
            "say" => new SceneStep("say")
            {
                Actor = Str(v, "actor"), Text = Str(v, "text"), Face = v.TryGetProperty("face", out var f) ? f.GetString() : null,
                Name = Str(v, "name"), Bark = v.TryGetProperty("bark", out var b) && b.GetBoolean(),
                Approach = v.TryGetProperty("approach", out var sa) && sa.GetBoolean(),
            },
            "talk" => new SceneStep("talk") { Actor = Str(v, "actor"), Node = Str(v, "node"), Approach = v.TryGetProperty("approach", out var ta) && ta.GetBoolean() },
            "teleport" => new SceneStep("teleport") { Points = new[] { Ground(v.GetProperty("to")) }, Yaw = Degrees(v, "yaw") },
            "time" => new SceneStep("time") { Day = v.GetProperty("day").GetInt32(), Hour = v.GetProperty("hour").GetInt32() },
            "effects" => new SceneStep("effects") { Effects = Effect.ParseList(v) },
            "wait" => new SceneStep("wait") { Seconds = v.GetSingle() },
            "waitFor" => new SceneStep("waitFor") { When = Condition.Parse(v) },
            "spawnEnemies" => new SceneStep("spawnEnemies")
            {
                Item = Str(v, "type"), Count = v.TryGetProperty("count", out var c) ? c.GetInt32() : 1, Points = new[] { Ground(v.GetProperty("at")) },
                Tag = Str(v, "tag"), Level = v.TryGetProperty("level", out var l) ? l.GetInt32() : 1,
                Health = v.TryGetProperty("health", out var h) ? h.GetSingle() : 1f,
            },
            "checkpoint" => new SceneStep("checkpoint") { Points = new[] { Ground(v) } },
            "waitForKills" => new SceneStep("waitForKills") { Tag = v.GetString()! },
            "drop" => new SceneStep("drop") { Item = Str(v, "item"), Points = new[] { Ground(v.GetProperty("at")) }, Count = v.TryGetProperty("count", out var dc) ? dc.GetInt32() : 1 },
            "scene" => new SceneStep("scene") { Scene = v.GetString()! },
            _ => throw new FormatException($"Unknown scene step '{property.Name}'."),
        };
    }

    /// <summary>How close the player and a speaker must be for a line or a talk to begin.</summary>
    public const float SpeakRange = 4.5f;

    /// <summary>
    /// Whether a line or a talk still waits for the player and the speaker to meet. People speak face
    /// to face: until then the player walks freely, and the words do not show. Barks carry any distance.
    /// </summary>
    public static bool Apart(SceneStep step, Vector3 speaker, Vector3 player) =>
        (step.Kind == "talk" || (step.Kind == "say" && !step.Bark)) &&
        Vector3.Distance(speaker with { Y = 0 }, player with { Y = 0 }) > SpeakRange;

    /// <summary>Steps that hold the scene until something happens; the rest run at once.</summary>
    public static bool Holds(SceneStep step) => step.Kind is "fade" or "slides" or "say" or "talk" or "wait" or "waitFor" or "waitForKills"
        || (step.Kind == "move" && step.Wait);
}
