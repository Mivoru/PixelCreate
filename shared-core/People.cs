using System.Reflection;
using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>A face someone wears while a condition holds: the first that holds is their mood.</summary>
public sealed record PersonMood(Condition When, string Face);

/// <summary>Something someone says to nobody in particular as the player passes, while a condition holds.</summary>
public sealed record PersonBark(Condition When, string Text);

/// <summary>
/// Who someone is, from world/people.json: not what they say (that is their dialogue) but what they
/// are like. The game reads it wherever a person should differ from the next one: how an answer's
/// tone lands with them, how they judge what the player did, what they think of their neighbours,
/// what mood events leave them in, what they mutter in passing.
/// </summary>
/// <param name="Summary">One or two lines for the journal's page on them, once the player knows them.</param>
/// <param name="Traits">Words for their temperament, shown in the journal and used by writers.</param>
/// <param name="Tones">Trust gained or lost when the player answers them in a tone, once for each answer.</param>
/// <param name="Judges">Trust gained or lost when a flag is set: what they make of the player's deeds.</param>
/// <param name="Bonds">What they feel for others, from -100 to 100.</param>
/// <param name="About">What they say when asked about someone, by that person's id.</param>
public sealed record PersonDef(
    string Id, int Age, string Summary, string[] Traits,
    IReadOnlyDictionary<string, int> Tones, IReadOnlyDictionary<string, int> Judges,
    IReadOnlyDictionary<string, int> Bonds, IReadOnlyDictionary<string, string> About,
    PersonMood[] Moods, PersonBark[] Barks)
{
    /// <summary>What they shout in a fight now and then, and their last words when they fall.</summary>
    public string[] Fight { get; init; } = Array.Empty<string>();
    public string Death { get; init; } = "";

    /// <summary>The face they wear now, or null for the one they were drawn with.</summary>
    public string? Mood(IStoryContext context) => Moods.FirstOrDefault(m => m.When.Holds(context))?.Face;

    /// <summary>The lines they could mutter now.</summary>
    public IEnumerable<string> BarksNow(IStoryContext context) => Barks.Where(b => b.When.Holds(context)).Select(b => b.Text);
}

public static class People
{
    public static IReadOnlyDictionary<string, PersonDef> All { get; } = Load();

    public static PersonDef? Of(string id) => All.GetValueOrDefault(id);

    /// <summary>How well someone knows the player, as a word: numbers are for the game, not for the journal.</summary>
    public static string TrustWord(int trust) => trust switch
    {
        <= -40 => "hostile",
        < -10 => "wary",
        < 10 => "a stranger",
        < 30 => "known",
        < 60 => "trusted",
        _ => "a friend",
    };

    /// <summary>
    /// What everyone makes of something that has just come to pass: each person who judges this flag
    /// trusts the player more or less for it. The dead and the gone judge nothing.
    /// </summary>
    public static void Judge(WorldState world, string flag)
    {
        foreach (var person in All.Values)
        {
            if (person.Judges.TryGetValue(flag, out var change) && world.Fate(person.Id) is NpcFate.Alive or NpcFate.Moved)
            {
                world.AddTrust(person.Id, change);
            }
        }
    }

    /// <summary>
    /// The trust an answer earns by its tone, once for each answer: the same kind word said twice is
    /// not kinder. Returns the change made.
    /// </summary>
    public static int HearTone(WorldState world, DialogueOption option)
    {
        if (Of(option.Npc) is not { } person || !person.Tones.TryGetValue(option.Tone, out var change) || change == 0)
        {
            return 0;
        }

        var said = $"said.{option.Npc}.{option.Node}.{Stable(option.Text):x4}";
        if (world.Has(said))
        {
            return 0;
        }

        world.SetFlag(said);
        world.AddTrust(option.Npc, change);
        return change;
    }

    /// <summary>A hash of a line that is the same on every run and every machine: it names a flag in the save.</summary>
    private static int Stable(string text)
    {
        var hash = 2166136261u;
        foreach (var ch in text)
        {
            hash = (hash ^ ch) * 16777619u;
        }

        return (int)(hash & 0xffff);
    }

    /// <summary>Every name the people file refers to, for the tests that check names against the data.</summary>
    public static IEnumerable<(string Kind, string Name, string Where)> References()
    {
        foreach (var person in All.Values)
        {
            var where = $"person {person.Id}";
            foreach (var other in person.Bonds.Keys.Concat(person.About.Keys))
            {
                yield return ("person", other, where);
            }

            foreach (var flag in person.Judges.Keys)
            {
                yield return ("flag", flag, where);
            }

            foreach (var r in person.Moods.SelectMany(m => m.When.References()).Concat(person.Barks.SelectMany(b => b.When.References())))
            {
                yield return (r.Kind, r.Name, where);
            }
        }
    }

    private static Dictionary<string, PersonDef> Load()
    {
        var all = new Dictionary<string, PersonDef>(StringComparer.Ordinal);
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("people");
        if (stream is null)
        {
            return all;
        }

        using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            var p = entry.Value;
            all[entry.Name] = new PersonDef(
                entry.Name,
                p.TryGetProperty("age", out var age) ? age.GetInt32() : 0,
                p.TryGetProperty("summary", out var summary) ? summary.GetString()! : "",
                p.TryGetProperty("traits", out var traits) ? traits.EnumerateArray().Select(t => t.GetString()!).ToArray() : Array.Empty<string>(),
                Numbers(p, "tones"), Numbers(p, "judges"), Numbers(p, "bonds"),
                p.TryGetProperty("about", out var about) ? about.EnumerateObject().ToDictionary(a => a.Name, a => a.Value.GetString()!) : new Dictionary<string, string>(),
                p.TryGetProperty("moods", out var moods)
                    ? moods.EnumerateArray().Select(m => new PersonMood(Condition.Parse(m.TryGetProperty("when", out var w) ? w : (JsonElement?)null), m.GetProperty("face").GetString()!)).ToArray()
                    : Array.Empty<PersonMood>(),
                p.TryGetProperty("barks", out var barks)
                    ? barks.EnumerateArray().Select(b => new PersonBark(Condition.Parse(b.TryGetProperty("when", out var w) ? w : (JsonElement?)null), b.GetProperty("text").GetString()!)).ToArray()
                    : Array.Empty<PersonBark>())
            {
                Fight = p.TryGetProperty("fight", out var fight) ? fight.EnumerateArray().Select(f => f.GetString()!).ToArray() : Array.Empty<string>(),
                Death = p.TryGetProperty("death", out var death) ? death.GetString()! : "",
            };
        }

        return all;
    }

    private static Dictionary<string, int> Numbers(JsonElement person, string name) =>
        person.TryGetProperty(name, out var map) ? map.EnumerateObject().ToDictionary(e => e.Name, e => e.Value.GetInt32()) : new Dictionary<string, int>();
}
