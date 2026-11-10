using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>What effects may change beyond the world state: the player's purse, bag and growth, and the server's own actions.</summary>
public interface IStoryActions : IStoryContext
{
    void AddGold(int amount);

    void GiveItem(string item, int count);

    void TakeItem(string item, int count);

    void AddXp(int amount);

    /// <summary>A named server action that the effect language cannot say by itself, such as selling crystals.</summary>
    void RunAction(string action);
}

/// <summary>One change from the story data. Only the server applies effects.</summary>
public abstract class Effect
{
    /// <summary>The server actions an effect may name; anything else is a mistake in the data.</summary>
    public static readonly string[] Actions =
    {
        "register", "beginTrial", "sellCrystals", "sellLoot", "resetAttributes", "openChest", "sleep", "openShop:alchemist",
    };

    /// <summary>Whether a name is a server action: one of <see cref="Actions"/>, or scene:&lt;id&gt; to start a scene.</summary>
    public static bool IsAction(string name) => Actions.Contains(name) || name.StartsWith("scene:", StringComparison.Ordinal);

    public abstract void Apply(IStoryActions actions);

    public virtual IEnumerable<(string Kind, string Name)> References() => Array.Empty<(string, string)>();

    public static Effect[] ParseList(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Array } e ? e.EnumerateArray().Select(Parse).ToArray() : Array.Empty<Effect>();

    public static Effect Parse(JsonElement element)
    {
        var property = element.EnumerateObject().Single();
        var value = property.Value;

        // Every value is read now: the JSON document is gone by the time an effect runs.
        switch (property.Name)
        {
            case "setFlag":
            {
                var flag = value.GetString()!;
                return new Do(a => a.World.SetFlag(flag), ("flag", flag));
            }
            case "clearFlag":
            {
                var flag = value.GetString()!;
                return new Do(a => a.World.SetFlag(flag, false), ("flag", flag));
            }
            case "rep":
                return Each(value, (name, v) =>
                {
                    var amount = v.GetInt32();
                    return new Do(a => a.World.AddReputation(name, amount), ("faction", name));
                });
            case "trust":
                return Each(value, (name, v) =>
                {
                    var amount = v.GetInt32();
                    return new Do(a => a.World.AddTrust(name, amount), ("npc", name));
                });
            case "fate":
                return Each(value, (name, v) =>
                {
                    var fate = Enum.Parse<NpcFate>(v.GetString()!, true);
                    return new Do(a => a.World.SetFate(name, fate), ("npc", name));
                });
            case "member":
                return Each(value, (name, v) =>
                {
                    var rank = (byte)Math.Clamp(v.GetInt32(), 0, 3);
                    return new Do(a =>
                    {
                        var m = a.World.Member(name);
                        m.Rank = rank;
                        a.World.SetMembership(name, m);
                    }, ("faction", name));
                });
            case "suspicion":
                return Each(value, (name, v) =>
                {
                    var amount = v.GetInt32();
                    return new Do(a =>
                    {
                        var m = a.World.Member(name);
                        m.Suspicion = (byte)Math.Clamp(m.Suspicion + amount, 0, 100);
                        a.World.SetMembership(name, m);
                    }, ("faction", name));
                });
            case "gold":
            {
                var amount = value.GetInt32();
                return new Do(a => a.AddGold(amount));
            }
            case "xp":
            {
                var amount = value.GetInt32();
                return new Do(a => a.AddXp(amount));
            }
            case "give":
            {
                var item = value.GetProperty("item").GetString()!;
                var count = Count(value);
                return new Do(a => a.GiveItem(item, count), ("item", item));
            }
            case "take":
            {
                var item = value.GetProperty("item").GetString()!;
                var count = Count(value);
                return new Do(a => a.TakeItem(item, count), ("item", item));
            }
            case "quest":
                return ParseQuest(value);
            case "agenda":
                return ParseAgenda(value);
            case "action":
            {
                var action = value.GetString()!;
                return new Do(a => a.RunAction(action), ("action", action));
            }
            default:
                throw new FormatException($"Unknown effect '{property.Name}'.");
        }
    }

    public static void ApplyAll(IEnumerable<Effect> effects, IStoryActions actions)
    {
        foreach (var effect in effects)
        {
            effect.Apply(actions);
        }
    }

    private static int Count(JsonElement value) => value.TryGetProperty("count", out var n) ? n.GetInt32() : 1;

    private static Effect ParseQuest(JsonElement value)
    {
        if (value.TryGetProperty("start", out var start))
        {
            var id = start.GetString()!;
            return new Do(a => Story.StartQuest(a, id), ("quest", id));
        }

        if (value.TryGetProperty("complete", out var complete))
        {
            var id = complete.GetString()!;
            return new Do(a => Story.CompleteQuest(a, id), ("quest", id));
        }

        if (value.TryGetProperty("fail", out var fail))
        {
            var id = fail.GetString()!;
            return new Do(a =>
            {
                var state = a.World.Quest(id);
                state.Status = QuestStatus.Failed;
                a.World.SetQuest(id, state);
            }, ("quest", id));
        }

        if (value.TryGetProperty("step", out var step))
        {
            var id = step[0].GetString()!;
            var to = (byte)step[1].GetInt32();
            return new Do(a =>
            {
                var state = a.World.Quest(id);
                state.Step = to;
                state.Progress = 0;
                a.World.SetQuest(id, state);
            }, ("quest", id));
        }

        throw new FormatException("A quest effect needs start, complete, fail or step.");
    }

    private static Effect ParseAgenda(JsonElement value)
    {
        if (value.TryGetProperty("foil", out var foil))
        {
            var id = foil.GetString()!;
            return new Do(a => a.World.MarkAgenda(id, true), ("agenda", id));
        }

        throw new FormatException("An agenda effect needs foil.");
    }

    private static Effect Each(JsonElement value, Func<string, JsonElement, Effect> make)
    {
        var parts = value.EnumerateObject().Select(p => make(p.Name, p.Value)).ToArray();
        return parts.Length == 1 ? parts[0] : new Many(parts);
    }

    private sealed class Do(Action<IStoryActions> apply, params (string Kind, string Name)[] references) : Effect
    {
        public override void Apply(IStoryActions actions) => apply(actions);

        public override IEnumerable<(string, string)> References() => references;
    }

    private sealed class Many(Effect[] parts) : Effect
    {
        public override void Apply(IStoryActions actions)
        {
            foreach (var part in parts)
            {
                part.Apply(actions);
            }
        }

        public override IEnumerable<(string, string)> References() => parts.SelectMany(p => p.References());
    }
}
