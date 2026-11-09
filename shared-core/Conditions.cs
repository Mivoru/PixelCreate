using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>
/// What a condition may ask about one player in one world. The server answers from its own state;
/// the client answers from its copy, to show dialogue options at once.
/// </summary>
public interface IStoryContext
{
    WorldState World { get; }

    /// <summary>0 before registering with the Guild, 1 for rank F and up from there.</summary>
    int Rank { get; }

    int Gold { get; }

    int Level { get; }

    int ItemCount(string item);

    /// <summary>What the Guild appraiser would pay for the materials and food in the bag.</summary>
    int SaleValue { get; }

    /// <summary>Points in an attribute, by its name (Strength, Dexterity, Intelligence, Vitality, Endurance).</summary>
    int Attribute(string name);

    /// <summary>Whether the player stands by an NPC of this role. The client always says yes; the server checks.</summary>
    bool Near(string role);

    /// <summary>Where the player stands, for conditions about places.</summary>
    System.Numerics.Vector3 Position { get; }
}

/// <summary>A test over the story state, read from the small condition language in the story data.</summary>
public abstract class Condition
{
    public static readonly Condition Always = new AllOf(Array.Empty<Condition>());

    public abstract bool Holds(IStoryContext context);

    /// <summary>Every name the condition refers to, by kind, so tests can check them against the data.</summary>
    public virtual IEnumerable<(string Kind, string Name)> References() => Array.Empty<(string, string)>();

    public static Condition Parse(JsonElement? element) => element is { } e && e.ValueKind != JsonValueKind.Null ? Parse(e) : Always;

    public static Condition Parse(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return new AllOf(element.EnumerateArray().Select(Parse).ToArray());
        }

        var parts = new List<Condition>();
        foreach (var property in element.EnumerateObject())
        {
            parts.Add(ParseOne(property.Name, property.Value));
        }

        return parts.Count == 1 ? parts[0] : new AllOf(parts.ToArray());
    }

    private static Condition ParseOne(string key, JsonElement value) => key switch
    {
        "all" => new AllOf(value.EnumerateArray().Select(Parse).ToArray()),
        "any" => new AnyOf(value.EnumerateArray().Select(Parse).ToArray()),
        "not" => new Not(Parse(value)),
        "flag" => new FlagIs(value.GetString()!, true),
        "notFlag" => new FlagIs(value.GetString()!, false),
        "day" => new Range("day", null, Min(value), Max(value), c => c.World.Time.Day),
        "time" => new Range("time", null, value.GetProperty("from").GetInt32(), value.GetProperty("to").GetInt32(), c => c.World.Time.Minute),
        "rank" => new Range("rank", null, Min(value), Max(value), c => c.Rank),
        "gold" => new Range("gold", null, Min(value), Max(value), c => c.Gold),
        "level" => new Range("level", null, Min(value), Max(value), c => c.Level),
        "rep" => Each(value, (name, v) => new Range("faction", name, Min(v), Max(v), c => c.World.Rep(name))),
        "trust" => Each(value, (name, v) => new Range("npc", name, Min(v), Max(v), c => c.World.TrustOf(name))),
        "member" => Each(value, (name, v) => new Range("faction", name, Min(v), Max(v), c => c.World.Member(name).Rank)),
        "suspicion" => Each(value, (name, v) => new Range("faction", name, Min(v), Max(v), c => c.World.Member(name).Suspicion)),
        "attr" => Each(value, (name, v) => new Range("attribute", name, v.GetInt32(), int.MaxValue, c => c.Attribute(name))),
        "item" => Item(value.GetProperty("id").GetString()!, value.TryGetProperty("count", out var n) ? n.GetInt32() : 1),
        "fate" => Each(value, (name, v) => new FateIs(name, Enum.Parse<NpcFate>(v.GetString()!, true))),
        "quest" => Each(value, (name, v) => new QuestIs(name, v.GetString()!)),
        "questStep" => Each(value, (name, v) => new QuestStepIs(name, v.GetInt32())),
        "agenda" => Each(value, (name, v) => new AgendaIs(name, v.GetString()!)),
        "saleValue" => new Range("saleValue", null, Min(value), Max(value), c => c.SaleValue),
        "activeJobs" => new Range("activeJobs", null, Min(value), Max(value), c => Story.ActiveJobs(c.World)),
        "near" => new NearRole(value.GetString()!),
        "at" => new AtPlace(new System.Numerics.Vector3(value.GetProperty("x").GetSingle(), 0, value.GetProperty("z").GetSingle()),
            value.TryGetProperty("r", out var radius) ? radius.GetSingle() : 6f),
        _ => throw new FormatException($"Unknown condition '{key}'."),
    };

    private static Condition Item(string id, int count) => new Range("item", id, count, int.MaxValue, c => c.ItemCount(id));

    private static int Min(JsonElement value) => value.ValueKind == JsonValueKind.Number ? value.GetInt32()
        : value.TryGetProperty("min", out var min) ? min.GetInt32() : int.MinValue;

    private static int Max(JsonElement value) => value.ValueKind == JsonValueKind.Number ? int.MaxValue
        : value.TryGetProperty("max", out var max) ? max.GetInt32() : int.MaxValue;

    private static Condition Each(JsonElement value, Func<string, JsonElement, Condition> make)
    {
        var parts = value.EnumerateObject().Select(p => make(p.Name, p.Value)).ToArray();
        return parts.Length == 1 ? parts[0] : new AllOf(parts);
    }

    private sealed class AllOf(Condition[] parts) : Condition
    {
        public override bool Holds(IStoryContext context) => parts.All(p => p.Holds(context));

        public override IEnumerable<(string, string)> References() => parts.SelectMany(p => p.References());
    }

    private sealed class AnyOf(Condition[] parts) : Condition
    {
        public override bool Holds(IStoryContext context) => parts.Any(p => p.Holds(context));

        public override IEnumerable<(string, string)> References() => parts.SelectMany(p => p.References());
    }

    private sealed class Not(Condition inner) : Condition
    {
        public override bool Holds(IStoryContext context) => !inner.Holds(context);

        public override IEnumerable<(string, string)> References() => inner.References();
    }

    private sealed class FlagIs(string flag, bool set) : Condition
    {
        public override bool Holds(IStoryContext context) => context.World.Has(flag) == set;

        public override IEnumerable<(string, string)> References() => new[] { ("flag", flag) };
    }

    private sealed class Range(string kind, string? name, int min, int max, Func<IStoryContext, int> read) : Condition
    {
        public override bool Holds(IStoryContext context)
        {
            var value = read(context);
            return value >= min && value <= max;
        }

        public override IEnumerable<(string, string)> References() => name is null ? Array.Empty<(string, string)>() : new[] { (kind, name) };
    }

    private sealed class FateIs(string npc, NpcFate fate) : Condition
    {
        public override bool Holds(IStoryContext context) => context.World.Fate(npc) == fate;

        public override IEnumerable<(string, string)> References() => new[] { ("npc", npc) };
    }

    private sealed class QuestIs(string quest, string status) : Condition
    {
        public override bool Holds(IStoryContext context) => Story.QuestStatusName(context, quest) == status;

        public override IEnumerable<(string, string)> References() => new[] { ("quest", quest) };
    }

    private sealed class QuestStepIs(string quest, int step) : Condition
    {
        public override bool Holds(IStoryContext context) =>
            context.World.Quest(quest) is { Status: QuestStatus.Active } state && state.Step == step;

        public override IEnumerable<(string, string)> References() => new[] { ("quest", quest) };
    }

    private sealed class AgendaIs(string step, string status) : Condition
    {
        public override bool Holds(IStoryContext context) => status switch
        {
            "done" => context.World.AgendaDone.Contains(step),
            "foiled" => context.World.AgendaFoiled.Contains(step),
            "pending" => !context.World.AgendaDone.Contains(step) && !context.World.AgendaFoiled.Contains(step),
            _ => throw new FormatException($"Unknown agenda status '{status}'."),
        };

        public override IEnumerable<(string, string)> References() => new[] { ("agenda", step) };
    }

    private sealed class AtPlace(System.Numerics.Vector3 place, float radius) : Condition
    {
        public override bool Holds(IStoryContext context) =>
            System.Numerics.Vector3.Distance(context.Position with { Y = 0 }, place) < radius;
    }

    private sealed class NearRole(string role) : Condition
    {
        public override bool Holds(IStoryContext context) => context.Near(role);
    }
}
