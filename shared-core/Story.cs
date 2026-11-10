using System.Reflection;
using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>What finishes a quest step.</summary>
/// <param name="Kind">kill, bring, talk or reach.</param>
/// <param name="Target">An enemy id or a prefix ending in *, an item id, an NPC id, or a region name.</param>
public sealed record QuestGoal(string Kind, string Target, int Count)
{
    /// <summary>Steps that end in a conversation rather than by themselves: talking to someone, or handing items over.</summary>
    public bool Terminal => Kind is "talk" or "bring";

    public bool MatchesKill(string enemyId) => Kind == "kill" &&
        (Target.EndsWith('*') ? enemyId.StartsWith(Target[..^1], StringComparison.Ordinal) : enemyId == Target);
}

/// <param name="At">Where the step leads, when not worked out from its goal (metres; y is ignored).</param>
/// <param name="Route">False for a step the player cannot walk anywhere for (locked in the cage, a fight in place).</param>
public sealed record QuestStep(string Text, QuestGoal Goal, System.Numerics.Vector3? At = null, bool Route = true);

/// <summary>A quest from world/quests.json.</summary>
/// <param name="Kind">job, story, personal, faction or contract.</param>
public sealed record StoryQuest(
    string Id, string Name, string Kind, string Giver, bool Repeatable, string Brief, string Journal,
    QuestStep[] Steps, Effect[] Rewards, int Gold, int Xp, Condition Available, int DeadlineDay, Effect[] OnExpire);

/// <summary>A choice in a dialogue node. Its place in <see cref="Story.Options"/> is what a choice command sends.</summary>
public sealed record DialogueOption(int Index, string Npc, string Node, string Text, string Tone, Condition When, Effect[] Effects, string? Next);

/// <param name="Shop">A shopkeeper role whose wares the client lists as buy options before the node's own options.</param>
public sealed record DialogueNode(string Id, string Text, string? Face, DialogueOption[] Options, string? Shop = null);

public sealed record DialogueEntry(Condition When, string Node);

/// <summary>Everything one NPC can say, from world/dialogue/&lt;npc&gt;.json.</summary>
public sealed record DialogueDef(string Npc, DialogueEntry[] Entries, Dictionary<string, DialogueNode> Nodes)
{
    /// <summary>The node to open with: the first entry whose condition holds.</summary>
    public DialogueNode? Opening(IStoryContext context) =>
        Entries.FirstOrDefault(e => e.When.Holds(context)) is { } entry ? Nodes[entry.Node] : null;

    /// <summary>
    /// Whether a node can be reached now: it opens the conversation, or an option that holds leads to
    /// it from there. The server uses it so that a choice from a closed node cannot be sent.
    /// </summary>
    public bool Reachable(IStoryContext context, string node, string? from = null)
    {
        var start = from is not null && Nodes.TryGetValue(from, out var given) ? given : Opening(context);
        if (start is null)
        {
            return false;
        }

        var seen = new HashSet<string> { start.Id };
        var open = new Queue<DialogueNode>(new[] { start });
        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (current.Id == node)
            {
                return true;
            }

            foreach (var option in current.Options)
            {
                if (option.Next is { } next && Nodes.TryGetValue(next, out var target) && seen.Add(next) && option.When.Holds(context))
                {
                    open.Enqueue(target);
                }
            }
        }

        return false;
    }
}

/// <summary>One step of a faction's plan, from world/agendas.json. It runs once, on its day, when its condition holds.</summary>
public sealed record AgendaStep(string Id, string Faction, int Day, int Minute, Condition When, Effect[] Effects, Condition? FoiledBy, string? Rumor);

/// <summary>
/// Talk of the world, from world/rumors.json. People tell it for some days while its condition holds:
/// those named in <paramref name="From"/>, anyone sitting at the tavern if it names "tavern", and the
/// Guild's board if it names "board". The same news sounds different from different mouths.
/// </summary>
/// <param name="Text">How most people tell it.</param>
/// <param name="By">How a given person tells it instead: Lina's version is not Pip's.</param>
/// <param name="Note">What the journal keeps once the player has heard it.</param>
public sealed record Rumor(int Index, string Id, string Text, IReadOnlyDictionary<string, string> By, Condition When, int FromDay, int ToDay, string[] From, string Note)
{
    /// <summary>The flag set when the player has heard this rumour from anyone.</summary>
    public string HeardFlag => $"heard.{Id}";

    public string TextFor(string npc) => By.GetValueOrDefault(npc, Text);

    /// <summary>Whether this person would tell it now: it is still talked of, and it is theirs to tell.</summary>
    public bool ToldBy(string npc, IStoryContext context)
    {
        var day = context.World.Time.Day;
        if (day < FromDay || day > ToDay || !When.Holds(context))
        {
            return false;
        }

        return From.Contains(npc) || By.ContainsKey(npc) ||
            (From.Contains("tavern") && Schedules.Now(npc, context, context.World.Time.Hour)?.Place == "tavern");
    }
}

/// <summary>
/// The story data: quests, dialogues, faction plans, rumours and journal notes, loaded once from
/// world/. Client and server read the same files, so the name registries match on both sides.
/// </summary>
public static class Story
{
    private static readonly JsonDocumentOptions JsonOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>Flags set by code rather than by data. They must be known to the registry like any other.</summary>
    public static readonly string[] CodeFlags =
    {
        "guild.registered", "prologue.new", "look.chosen", "hud.mark", "trial.round2", "trial.round3",
        "with.nell",
    };

    public static IReadOnlyList<StoryQuest> Quests { get; }

    public static IReadOnlyDictionary<string, DialogueDef> Dialogues { get; }

    public static IReadOnlyList<DialogueOption> Options { get; }

    public static IReadOnlyList<AgendaStep> Agendas { get; }

    public static IReadOnlyList<Rumor> Rumors { get; }

    /// <summary>Scenes from world/scenes, numbered in file order for the snapshot's scene cue.</summary>
    public static IReadOnlyList<SceneDef> Scenes { get; }

    public static SceneDef? Scene(string id) => Scenes.FirstOrDefault(s => s.Id == id);

    /// <summary>Journal notes: a line for each flag the player can learn.</summary>
    public static IReadOnlyDictionary<string, string> Notes { get; }

    /// <summary>Every key a story entry can name, by kind, sorted so client and server number them alike.</summary>
    public static IReadOnlyDictionary<StoryKind, string[]> Keys { get; }

    private static readonly Dictionary<string, StoryQuest> QuestById;
    private static readonly Dictionary<(StoryKind, string), ushort> KeyIndex = new();

    static Story()
    {
        Quests = LoadQuests();
        QuestById = Quests.ToDictionary(q => q.Id);
        var options = new List<DialogueOption>();
        Dialogues = LoadDialogues(options);
        Options = options;
        Agendas = LoadAgendas();
        Rumors = LoadRumors();
        Scenes = LoadScenes();
        var notes = Load("journal") is { } journal
            ? journal.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!)
            : new Dictionary<string, string>();
        // A rumour heard is a note kept.
        foreach (var rumor in Rumors)
        {
            notes[rumor.HeardFlag] = rumor.Note;
        }

        Notes = notes;
        Keys = BuildKeys();
        foreach (var (kind, names) in Keys)
        {
            for (var i = 0; i < names.Length; i++)
            {
                KeyIndex[(kind, names[i])] = (ushort)i;
            }
        }
    }

    public static StoryQuest? Quest(string id) => QuestById.GetValueOrDefault(id);

    /// <summary>What someone has to tell now that the player has not heard, the newest first.</summary>
    public static IEnumerable<Rumor> NewsFrom(string npc, IStoryContext context) => Rumors
        .Where(r => !context.World.Has(r.HeardFlag) && r.ToldBy(npc, context))
        .OrderByDescending(r => r.FromDay);

    public static bool TryKey(StoryKind kind, string name, out ushort index) => KeyIndex.TryGetValue((kind, name), out index);

    public static string? KeyName(StoryKind kind, ushort index) =>
        Keys.TryGetValue(kind, out var names) && index < names.Length ? names[index] : null;

    /// <summary>Every reference in the story data, by kind, for tests that check it against the world.</summary>
    public static IEnumerable<(string Kind, string Name, string Where)> References()
    {
        foreach (var quest in Quests)
        {
            foreach (var r in quest.Available.References().Concat(quest.Rewards.SelectMany(e => e.References())).Concat(quest.OnExpire.SelectMany(e => e.References())))
            {
                yield return (r.Kind, r.Name, $"quest {quest.Id}");
            }

            foreach (var step in quest.Steps)
            {
                yield return (step.Goal.Kind switch { "kill" => "enemy", "bring" => "item", "talk" => "npc", _ => "region" }, step.Goal.Target, $"quest {quest.Id}");
            }
        }

        foreach (var option in Options)
        {
            foreach (var r in option.When.References().Concat(option.Effects.SelectMany(e => e.References())))
            {
                yield return (r.Kind, r.Name, $"dialogue {option.Npc}/{option.Node}");
            }
        }

        foreach (var dialogue in Dialogues.Values)
        {
            foreach (var entry in dialogue.Entries)
            {
                foreach (var r in entry.When.References())
                {
                    yield return (r.Kind, r.Name, $"dialogue {dialogue.Npc}");
                }
            }
        }

        foreach (var step in Agendas)
        {
            foreach (var r in step.When.References().Concat(step.FoiledBy?.References() ?? Array.Empty<(string, string)>()).Concat(step.Effects.SelectMany(e => e.References())))
            {
                yield return (r.Kind, r.Name, $"agenda {step.Id}");
            }
        }

        foreach (var rumor in Rumors)
        {
            foreach (var r in rumor.When.References())
            {
                yield return (r.Kind, r.Name, $"rumor {rumor.Id}");
            }

            foreach (var teller in rumor.From.Where(f => f != "tavern").Concat(rumor.By.Keys))
            {
                yield return ("npc", teller, $"rumor {rumor.Id}");
            }
        }

        foreach (var scene in Scenes)
        {
            var where = $"scene {scene.Id}";
            foreach (var r in scene.Trigger?.When.References() ?? Array.Empty<(string, string)>())
            {
                yield return (r.Kind, r.Name, where);
            }

            foreach (var step in scene.Steps)
            {
                foreach (var r in step.When.References().Concat(step.Effects.SelectMany(e => e.References())))
                {
                    yield return (r.Kind, r.Name, where);
                }

                if (step.Actor.Length > 0)
                {
                    yield return ("npc", step.Actor, where);
                }

                if (step.Kind == "drop")
                {
                    yield return ("item", step.Item, where);
                }

                if (step.Kind == "spawnEnemies")
                {
                    yield return ("enemy", step.Item, where);
                }

                if (step.Kind == "scene")
                {
                    yield return ("scene", step.Scene, where);
                }
            }
        }

        // What people are like and how their days go read the world state too: their flags must reach the client.
        foreach (var r in People.References().Concat(Schedules.References()))
        {
            yield return r;
        }
    }

    public static int ActiveJobs(WorldState world) =>
        world.Quests.Count(q => q.Value.Status == QuestStatus.Active && QuestById.TryGetValue(q.Key, out var def) && def.Kind == "job");

    /// <summary>none, active, ready (on a closing step that can be finished now), done or failed.</summary>
    public static string QuestStatusName(IStoryContext context, string id)
    {
        var state = context.World.Quest(id);
        return state.Status switch
        {
            QuestStatus.Active when Ready(context, id, state) => "ready",
            QuestStatus.Active => "active",
            QuestStatus.Done => "done",
            QuestStatus.Failed => "failed",
            _ => "none",
        };
    }

    /// <summary>How far the player is through the current step, for the journal and the HUD: done and needed.</summary>
    public static (int Done, int Needed) StepProgress(IStoryContext context, string id)
    {
        var state = context.World.Quest(id);
        if (Quest(id) is not { } quest || state.Step >= quest.Steps.Length)
        {
            return (0, 0);
        }

        var goal = quest.Steps[state.Step].Goal;
        return goal.Kind switch
        {
            "kill" => (state.Progress, goal.Count),
            "bring" => (Math.Min(context.ItemCount(goal.Target), goal.Count), goal.Count),
            _ => (0, 0),
        };
    }

    private static bool Ready(IStoryContext context, string id, QuestState state)
    {
        if (Quest(id) is not { } quest || state.Step >= quest.Steps.Length)
        {
            return false;
        }

        var goal = quest.Steps[state.Step].Goal;
        return state.Step == quest.Steps.Length - 1 && goal.Terminal &&
               (goal.Kind != "bring" || context.ItemCount(goal.Target) >= goal.Count);
    }

    public static void StartQuest(IStoryActions actions, string id)
    {
        if (Quest(id) is not { } quest || actions.World.Quest(id).Status == QuestStatus.Active)
        {
            return;
        }

        var state = actions.World.Quest(id);
        if (state.Status == QuestStatus.Done && !quest.Repeatable)
        {
            return;
        }

        actions.World.SetQuest(id, state with { Status = QuestStatus.Active, Step = 0, Progress = 0 });
    }

    public static void CompleteQuest(IStoryActions actions, string id)
    {
        if (Quest(id) is not { } quest || !Ready(actions, id, actions.World.Quest(id)))
        {
            return;
        }

        var goal = quest.Steps[^1].Goal;
        if (goal.Kind == "bring")
        {
            actions.TakeItem(goal.Target, goal.Count);
        }

        actions.AddGold(quest.Gold);
        actions.AddXp(quest.Xp);
        Effect.ApplyAll(quest.Rewards, actions);
        var state = actions.World.Quest(id);
        actions.World.SetQuest(id, state with
        {
            Status = quest.Repeatable ? QuestStatus.None : QuestStatus.Done,
            Step = 0,
            Progress = 0,
            Completions = state.Completions + 1,
        });
    }

    /// <summary>Counts a kill toward every active quest step that asks for it, and moves finished steps on.</summary>
    public static void OnKill(WorldState world, string enemyId)
    {
        foreach (var quest in Quests)
        {
            var state = world.Quest(quest.Id);
            if (state.Status != QuestStatus.Active || state.Step >= quest.Steps.Length)
            {
                continue;
            }

            var goal = quest.Steps[state.Step].Goal;
            if (!goal.MatchesKill(enemyId) || state.Progress >= goal.Count)
            {
                continue;
            }

            state.Progress++;
            if (state.Progress >= goal.Count && state.Step < quest.Steps.Length - 1)
            {
                state.Step++;
                state.Progress = 0;
            }

            world.SetQuest(quest.Id, state);
        }
    }

    /// <summary>Runs the faction plans whose time has come. Returns the steps that ran or were foiled.</summary>
    public static List<AgendaStep> RunAgendas(IStoryActions actions, IEnumerable<AgendaStep>? steps = null)
    {
        var happened = new List<AgendaStep>();
        var world = actions.World;
        foreach (var step in steps ?? Agendas)
        {
            if (world.AgendaDone.Contains(step.Id) || world.AgendaFoiled.Contains(step.Id) ||
                world.Time.CompareTo(new GameTime(step.Day, step.Minute)) < 0)
            {
                continue;
            }

            if (step.FoiledBy is { } foiled && foiled.Holds(actions))
            {
                world.MarkAgenda(step.Id, true);
                happened.Add(step);
                continue;
            }

            if (!step.When.Holds(actions))
            {
                continue;
            }

            world.MarkAgenda(step.Id, false);
            Effect.ApplyAll(step.Effects, actions);
            happened.Add(step);
        }

        return happened;
    }

    private static Dictionary<StoryKind, string[]> BuildKeys()
    {
        var sets = Enum.GetValues<StoryKind>().ToDictionary(k => k, _ => new SortedSet<string>(StringComparer.Ordinal));
        foreach (var flag in CodeFlags.Concat(Tutorial.Flags).Concat(Notes.Keys).Concat(Scenes.Select(s => s.DoneFlag)))
        {
            sets[StoryKind.Flag].Add(flag);
        }

        foreach (var quest in Quests)
        {
            sets[StoryKind.Quest].Add(quest.Id);
        }

        // Everyone who speaks can be trusted, distrusted, or die.
        foreach (var npc in Dialogues.Keys)
        {
            sets[StoryKind.Trust].Add(npc);
            sets[StoryKind.Fate].Add(npc);
        }

        foreach (var step in Agendas)
        {
            sets[StoryKind.Agenda].Add(step.Id);
        }

        foreach (var (kind, name, _) in References())
        {
            switch (kind)
            {
                case "flag": sets[StoryKind.Flag].Add(name); break;
                case "faction": sets[StoryKind.Reputation].Add(name); sets[StoryKind.Member].Add(name); break;
                case "npc": sets[StoryKind.Trust].Add(name); sets[StoryKind.Fate].Add(name); break;
                case "quest": sets[StoryKind.Quest].Add(name); break;
                case "agenda": sets[StoryKind.Agenda].Add(name); break;
            }
        }

        return sets.ToDictionary(p => p.Key, p => p.Value.ToArray());
    }

    private static JsonDocument? Load(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        return stream is null ? null : JsonDocument.Parse(stream, JsonOptions);
    }

    private static string Str(JsonElement e, string name, string fallback = "") =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : fallback;

    private static int Int(JsonElement e, string name, int fallback = 0) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : fallback;

    private static JsonElement? Opt(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v : null;

    private static List<StoryQuest> LoadQuests()
    {
        var list = new List<StoryQuest>();
        if (Load("quests") is not { } doc)
        {
            return list;
        }

        foreach (var q in doc.RootElement.EnumerateArray())
        {
            var steps = q.GetProperty("steps").EnumerateArray().Select(s =>
            {
                var goal = s.GetProperty("goal").EnumerateObject().First();
                var target = goal.Value.ValueKind == JsonValueKind.String ? goal.Value.GetString()! : Str(goal.Value, "id");
                var count = s.GetProperty("goal").TryGetProperty("count", out var c) ? c.GetInt32()
                    : goal.Value.ValueKind == JsonValueKind.Object ? Int(goal.Value, "count", 1) : 1;
                System.Numerics.Vector3? at = s.TryGetProperty("at", out var a) ? new System.Numerics.Vector3(a[0].GetSingle(), 0, a[1].GetSingle()) : null;
                var route = !s.TryGetProperty("route", out var r) || r.GetBoolean();
                return new QuestStep(Str(s, "text"), new QuestGoal(goal.Name, target, count), at, route);
            }).ToArray();
            var deadline = Opt(q, "deadline");
            list.Add(new StoryQuest(
                Str(q, "id"), Str(q, "name"), Str(q, "kind", "job"), Str(q, "giver"),
                q.TryGetProperty("repeatable", out var rep) && rep.GetBoolean(), Str(q, "brief"), Str(q, "journal"),
                steps, Effect.ParseList(Opt(q, "rewards")), Int(q, "gold"), Int(q, "xp"), Condition.Parse(Opt(q, "available")),
                deadline is { ValueKind: JsonValueKind.Object } d ? Int(d, "day") : 0,
                deadline is { ValueKind: JsonValueKind.Object } d2 ? Effect.ParseList(Opt(d2, "onExpire")) : Array.Empty<Effect>()));
        }

        return list;
    }

    private static Dictionary<string, DialogueDef> LoadDialogues(List<DialogueOption> options)
    {
        var dialogues = new Dictionary<string, DialogueDef>(StringComparer.Ordinal);
        var names = Assembly.GetExecutingAssembly().GetManifestResourceNames()
            .Where(n => n.StartsWith("dialogue.", StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal);
        foreach (var name in names)
        {
            using var doc = Load(name)!;
            var root = doc.RootElement;
            var npc = Str(root, "npc");
            var nodes = new Dictionary<string, DialogueNode>(StringComparer.Ordinal);
            foreach (var node in root.GetProperty("nodes").EnumerateObject())
            {
                var nodeOptions = new List<DialogueOption>();
                if (node.Value.TryGetProperty("options", out var list))
                {
                    foreach (var o in list.EnumerateArray())
                    {
                        var option = new DialogueOption(options.Count, npc, node.Name, Str(o, "text"), Str(o, "tone", "neutral"),
                            Condition.Parse(Opt(o, "when")), Effect.ParseList(Opt(o, "effects")),
                            o.TryGetProperty("next", out var next) ? next.GetString() : null);
                        options.Add(option);
                        nodeOptions.Add(option);
                    }
                }

                nodes[node.Name] = new DialogueNode(node.Name, Str(node.Value, "text"),
                    node.Value.TryGetProperty("face", out var face) ? face.GetString() : null, nodeOptions.ToArray(),
                    node.Value.TryGetProperty("shop", out var shop) ? shop.GetString() : null);
            }

            var entries = root.GetProperty("entries").EnumerateArray()
                .Select(e => new DialogueEntry(Condition.Parse(Opt(e, "when")), Str(e, "node"))).ToArray();
            dialogues[npc] = new DialogueDef(npc, entries, nodes);
        }

        return dialogues;
    }

    /// <summary>Reads agenda steps from JSON text, for the game data and for tests with their own timeline.</summary>
    public static List<AgendaStep> ParseAgendas(JsonElement root)
    {
        var list = new List<AgendaStep>();
        foreach (var a in root.EnumerateArray())
        {
            list.Add(new AgendaStep(Str(a, "id"), Str(a, "faction"), Int(a, "day", 1), Int(a, "minute", GameTime.DayStartMinute),
                Condition.Parse(Opt(a, "when")), Effect.ParseList(Opt(a, "effects")),
                a.TryGetProperty("foiledBy", out var foiled) ? Condition.Parse(foiled) : null,
                a.TryGetProperty("rumor", out var rumor) ? rumor.GetString() : null));
        }

        // Steps run in calendar order, so a later step can depend on an earlier one of the same moment.
        list.Sort((x, y) => new GameTime(x.Day, x.Minute).CompareTo(new GameTime(y.Day, y.Minute)));
        return list;
    }

    private static List<AgendaStep> LoadAgendas() =>
        Load("agendas") is { } doc ? ParseAgendas(doc.RootElement) : new List<AgendaStep>();

    private static List<SceneDef> LoadScenes()
    {
        var scenes = new List<SceneDef>();
        var names = Assembly.GetExecutingAssembly().GetManifestResourceNames()
            .Where(n => n.StartsWith("scene.", StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal);
        foreach (var name in names)
        {
            using var doc = Load(name)!;
            scenes.Add(SceneData.Parse(doc.RootElement, scenes.Count));
        }

        return scenes;
    }

    private static List<Rumor> LoadRumors()
    {
        var list = new List<Rumor>();
        if (Load("rumors") is not { } doc)
        {
            return list;
        }

        foreach (var r in doc.RootElement.EnumerateArray())
        {
            var from = Int(r, "fromDay", 1);
            var by = r.TryGetProperty("by", out var tellers)
                ? tellers.EnumerateObject().ToDictionary(t => t.Name, t => t.Value.GetString()!)
                : new Dictionary<string, string>();
            list.Add(new Rumor(list.Count, Str(r, "id"), Str(r, "text"), by, Condition.Parse(Opt(r, "when")), from, from + Int(r, "days", 7) - 1,
                r.TryGetProperty("from", out var where) ? where.EnumerateArray().Select(w => w.GetString()!).ToArray() : new[] { "tavern" },
                Str(r, "note", Str(r, "text"))));
        }

        return list;
    }
}
