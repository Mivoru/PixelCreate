using System.Numerics;
using System.Text.Json;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>The story calendar: time flows with ticks, sleep jumps to the morning, seasons turn.</summary>
public class CalendarTests
{
    [Fact]
    public void Calendar_AnHourLastsTwentySixSeconds()
    {
        var calendar = new Calendar { Now = new GameTime(1, 9 * 60) };
        for (var i = 0; i < Tuning.TickRate * 26; i++)
        {
            calendar.Step();
        }

        Assert.Equal(new GameTime(1, 10 * 60), calendar.Now);
    }

    [Fact]
    public void Calendar_StandsStillWhilePaused()
    {
        var calendar = new Calendar { Now = new GameTime(1, 9 * 60), Paused = true };
        for (var i = 0; i < Tuning.TickRate * 60; i++)
        {
            calendar.Step();
        }

        Assert.Equal(new GameTime(1, 9 * 60), calendar.Now);
    }

    [Fact]
    public void Sleep_WakesAtSixOnTheRightDay()
    {
        Assert.Equal(new GameTime(4, 360), new GameTime(3, 21 * 60).AfterSleep());
        Assert.Equal(new GameTime(4, 360), new GameTime(4, 2 * 60).AfterSleep());
        Assert.True(new GameTime(3, 21 * 60).CanSleep);
        Assert.False(new GameTime(3, 14 * 60).CanSleep);
    }

    [Fact]
    public void Time_RollsOverMidnightAndTurnsTheSeasons()
    {
        Assert.Equal(new GameTime(2, 30), new GameTime(1, 23 * 60 + 30).AddMinutes(60));
        Assert.Equal(Season.Thaw, new GameTime(20, 0).Season);
        Assert.Equal(Season.Highsun, new GameTime(21, 0).Season);
        Assert.Equal(Season.Harvest, new GameTime(58, 0).Season);
        Assert.Equal(Season.Frost, new GameTime(59, 0).Season);
        Assert.Equal(DayPart.Night, new GameTime(1, 23 * 60).Part);
        Assert.Equal(DayPart.Morning, new GameTime(1, 7 * 60).Part);
    }
}

/// <summary>A story context with nothing behind it but numbers a test sets.</summary>
internal sealed class FakeStory : IStoryActions
{
    public WorldState World { get; } = new();
    public int Rank { get; set; }
    public int Gold { get; set; }
    public int Level { get; set; } = 1;
    public int SaleValue { get; set; }
    public Dictionary<string, int> Items { get; } = new();
    public Dictionary<string, int> Attributes { get; } = new();
    public List<string> Actions { get; } = new();
    public int Xp { get; set; }

    public int ItemCount(string item) => Items.GetValueOrDefault(item);

    public int Attribute(string name) => Attributes.GetValueOrDefault(name);

    public bool Near(string role) => true;

    public void AddGold(int amount) => Gold += amount;

    public void GiveItem(string item, int count) => Items[item] = ItemCount(item) + count;

    public void TakeItem(string item, int count) => Items[item] = Math.Max(0, ItemCount(item) - count);

    public void AddXp(int amount) => Xp += amount;

    public void RunAction(string action) => Actions.Add(action);
}

/// <summary>The condition and effect language of the story data.</summary>
public class ConditionTests
{
    private static Condition C(string json) => Condition.Parse(JsonDocument.Parse(json).RootElement);

    private static Effect E(string json) => Effect.Parse(JsonDocument.Parse(json).RootElement);

    [Fact]
    public void Conditions_ReadTheWorldAndThePlayer()
    {
        var story = new FakeStory { Gold = 20, Rank = 1 };
        story.World.SetFlag("seen");
        story.World.AddReputation("company", 30);
        story.World.SetFate("hollis", NpcFate.Dead);
        story.Items["slime_gel"] = 3;
        story.Attributes["Intelligence"] = 6;
        story.World.Time = new GameTime(9, 600);

        Assert.True(C("""{ "flag": "seen" }""").Holds(story));
        Assert.False(C("""{ "notFlag": "seen" }""").Holds(story));
        Assert.True(C("""{ "gold": { "min": 15 } }""").Holds(story));
        Assert.False(C("""{ "gold": { "min": 25 } }""").Holds(story));
        Assert.True(C("""{ "rep": { "company": { "min": 20, "max": 40 } } }""").Holds(story));
        Assert.True(C("""{ "fate": { "hollis": "dead" } }""").Holds(story));
        Assert.True(C("""{ "item": { "id": "slime_gel", "count": 3 } }""").Holds(story));
        Assert.False(C("""{ "item": { "id": "slime_gel", "count": 4 } }""").Holds(story));
        Assert.True(C("""{ "attr": { "Intelligence": 6 } }""").Holds(story));
        Assert.True(C("""{ "day": { "min": 5, "max": 12 } }""").Holds(story));
        Assert.True(C("""{ "time": { "from": 540, "to": 660 } }""").Holds(story));
        Assert.True(C("""{ "rank": { "min": 1 } }""").Holds(story));
        Assert.True(C("""{ "any": [ { "flag": "nope" }, { "flag": "seen" } ] }""").Holds(story));
        Assert.False(C("""{ "all": [ { "flag": "nope" }, { "flag": "seen" } ] }""").Holds(story));
        Assert.True(C("""{ "not": { "flag": "nope" } }""").Holds(story));
        Assert.True(C("""{ "flag": "seen", "gold": { "min": 1 } }""").Holds(story));
    }

    [Fact]
    public void Conditions_RejectUnknownWords()
    {
        Assert.Throws<FormatException>(() => C("""{ "flagg": "x" }"""));
    }

    [Fact]
    public void Effects_ChangeTheWorldAndMarkWhatChanged()
    {
        var story = new FakeStory { Gold = 10 };
        E("""{ "setFlag": "hollis.warned" }""").Apply(story);
        E("""{ "rep": { "company": -10 } }""").Apply(story);
        E("""{ "trust": { "mira": 5 } }""").Apply(story);
        E("""{ "fate": { "hollis": "dead" } }""").Apply(story);
        E("""{ "member": { "company": 1 } }""").Apply(story);
        E("""{ "suspicion": { "company": 15 } }""").Apply(story);
        E("""{ "gold": -15 }""").Apply(story);
        E("""{ "give": { "item": "rusty_dagger" } }""").Apply(story);
        E("""{ "action": "sellCrystals" }""").Apply(story);

        Assert.True(story.World.Has("hollis.warned"));
        Assert.Equal(-10, story.World.Rep("company"));
        Assert.Equal(5, story.World.TrustOf("mira"));
        Assert.Equal(NpcFate.Dead, story.World.Fate("hollis"));
        Assert.Equal(1, story.World.Member("company").Rank);
        Assert.Equal(15, story.World.Member("company").Suspicion);
        Assert.Equal(-5, story.Gold);
        Assert.Equal(1, story.ItemCount("rusty_dagger"));
        Assert.Equal(new[] { "sellCrystals" }, story.Actions);
        Assert.Contains((StoryKind.Flag, "hollis.warned"), story.World.Changed);
        Assert.Contains((StoryKind.Fate, "hollis"), story.World.Changed);
    }

    [Fact]
    public void Reputation_StaysBetweenMinusAndPlusHundred()
    {
        var story = new FakeStory();
        E("""{ "rep": { "church": 250 } }""").Apply(story);
        Assert.Equal(100, story.World.Rep("church"));
    }
}

/// <summary>Quests from world/quests.json: steps, kills, deliveries and repeatable jobs.</summary>
public class QuestDataTests
{
    [Fact]
    public void OldJobs_PayWhatTheyPaidBefore()
    {
        var pay = new Dictionary<string, (int Gold, int Xp)>
        {
            ["slime_cull"] = (40, 60), ["goblin_bounty"] = (120, 180), ["gel_for_alchemist"] = (45, 50),
            ["hides_for_tanner"] = (60, 70), ["proof_of_hunt"] = (90, 120),
        };
        foreach (var (id, (gold, xp)) in pay)
        {
            var quest = Story.Quest(id)!;
            Assert.Equal(gold, quest.Gold);
            Assert.Equal(xp, quest.Xp);
            Assert.True(quest.Repeatable);
            Assert.Equal("job", quest.Kind);
        }
    }

    [Fact]
    public void KillJob_MovesToTheReportStepAndPays()
    {
        var story = new FakeStory();
        Story.StartQuest(story, "goblin_bounty");
        Story.OnKill(story.World, "slime");
        Story.OnKill(story.World, "goblin_brute");
        Story.OnKill(story.World, "goblin_skirmisher");
        Assert.Equal("active", Story.QuestStatusName(story, "goblin_bounty"));
        Story.OnKill(story.World, "goblin_shaman");
        Assert.Equal(1, story.World.Quest("goblin_bounty").Step);
        Assert.Equal("ready", Story.QuestStatusName(story, "goblin_bounty"));
        Story.CompleteQuest(story, "goblin_bounty");
        Assert.Equal(120, story.Gold);
        Assert.Equal(180, story.Xp);
        Assert.Equal("none", Story.QuestStatusName(story, "goblin_bounty"));
        Assert.Equal(1, story.World.Quest("goblin_bounty").Completions);
    }

    [Fact]
    public void DeliveryJob_IsReadyOnlyWithEverythingInTheBag()
    {
        var story = new FakeStory();
        Story.StartQuest(story, "hides_for_tanner");
        story.Items["deer_hide"] = 1;
        Assert.Equal("active", Story.QuestStatusName(story, "hides_for_tanner"));
        Story.CompleteQuest(story, "hides_for_tanner");
        Assert.Equal(0, story.Gold);
        story.Items["deer_hide"] = 3;
        Assert.Equal("ready", Story.QuestStatusName(story, "hides_for_tanner"));
        Story.CompleteQuest(story, "hides_for_tanner");
        Assert.Equal(60, story.Gold);
        Assert.Equal(1, story.ItemCount("deer_hide"));
    }

    [Fact]
    public void ActiveJobs_CountsOnlyJobsInProgress()
    {
        var story = new FakeStory();
        Assert.Equal(0, Story.ActiveJobs(story.World));
        Story.StartQuest(story, "slime_cull");
        Assert.Equal(1, Story.ActiveJobs(story.World));
    }
}

/// <summary>Every name in the story data points at something that exists.</summary>
public class DialogueDataTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    [Fact]
    public void StoryData_RefersOnlyToThingsThatExist()
    {
        var npcs = Valley.World.Npcs.Select(n => n.Id).Concat(Actors.Defs.Select(a => a.Id)).ToHashSet();
        var enemies = EnemyDef.All.Select(e => e.Id).ToArray();
        var regions = Valley.Places.Select(r => r.Name).ToHashSet();
        var problems = new List<string>();
        foreach (var (kind, name, where) in Story.References())
        {
            var ok = kind switch
            {
                "npc" => npcs.Contains(name),
                "item" => ItemDef.TryIndex(name, out _),
                "enemy" => name.EndsWith('*') ? enemies.Any(e => e.StartsWith(name[..^1], StringComparison.Ordinal)) : enemies.Contains(name),
                "region" => regions.Contains(name),
                "quest" => Story.Quest(name) is not null,
                "action" => Effect.IsAction(name),
                "scene" => Story.Scene(name) is not null,
                "attribute" => Enum.TryParse<PlayerAttribute>(name, true, out _),
                _ => true,
            };
            if (!ok)
            {
                problems.Add($"{where}: unknown {kind} '{name}'");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Dialogues_PointAtTheirOwnNodes()
    {
        foreach (var dialogue in Story.Dialogues.Values)
        {
            Assert.NotEmpty(dialogue.Entries);
            Assert.All(dialogue.Entries, e => Assert.True(dialogue.Nodes.ContainsKey(e.Node), $"{dialogue.Npc}: no node {e.Node}"));
            foreach (var option in dialogue.Nodes.Values.SelectMany(n => n.Options).Where(o => o.Next is not null))
            {
                Assert.True(dialogue.Nodes.ContainsKey(option.Next!), $"{dialogue.Npc}: no node {option.Next}");
            }

            // The last entry has no condition, so everybody always has something to say.
            Assert.Same(Condition.Always, dialogue.Entries[^1].When);
        }
    }

    /// <summary>
    /// Runs every condition and effect in the data once. They must hold no reference to the JSON they
    /// came from, which is disposed after loading (a choice once crashed the server that way).
    /// </summary>
    [Fact]
    public void EveryChoice_CanBeEvaluatedAndApplied()
    {
        GC.Collect();
        var story = new FakeStory { Gold = 500, Rank = 1 };
        foreach (var dialogue in Story.Dialogues.Values)
        {
            _ = dialogue.Opening(story);
            foreach (var option in dialogue.Nodes.Values.SelectMany(n => n.Options))
            {
                _ = option.When.Holds(story);
                Effect.ApplyAll(option.Effects, story);
            }
        }

        foreach (var quest in Story.Quests)
        {
            _ = quest.Available.Holds(story);
            Effect.ApplyAll(quest.Rewards, story);
        }
    }

    [Fact]
    public void DialogueFaces_AreExpressionsThatExist()
    {
        foreach (var node in Story.Dialogues.Values.SelectMany(d => d.Nodes.Values).Where(n => n.Face is not null))
        {
            Assert.True(Faces.All.ContainsKey(node.Face!), $"node {node.Id} asks for the unknown face {node.Face}");
        }

        Assert.All(Faces.All.Values, f => Assert.Contains(f.Mouth, Faces.Mouths));
    }

    [Fact]
    public void EveryPersonInTheValley_HasSomethingToSay()
    {
        foreach (var npc in Valley.World.Npcs.Where(n => n.Role != "storage"))
        {
            Assert.True(Story.Dialogues.ContainsKey(npc.Id), $"{npc.Id} has no dialogue in world/dialogue");
        }
    }

    [Fact]
    public void Registry_NumbersEveryKeyOnce()
    {
        foreach (var (kind, names) in Story.Keys)
        {
            Assert.Equal(names.Length, names.Distinct().Count());
            for (var i = 0; i < names.Length; i++)
            {
                Assert.True(Story.TryKey(kind, names[i], out var index));
                Assert.Equal(i, index);
            }
        }

        Assert.Contains("guild.registered", Story.Keys[StoryKind.Flag]);
        Assert.Contains("slime_cull", Story.Keys[StoryKind.Quest]);
    }
}

/// <summary>Faction plans run on their days, unless foiled, and without the player the world goes its own way.</summary>
public class AgendaTests
{
    /// <summary>
    /// A test timeline after design/timeline.md: the company, the church and the crown follow their
    /// plans while nobody stops them. Effects only set flags and fates.
    /// </summary>
    private const string Timeline = """
    [
      { "id": "t.d5.raid_farm", "day": 5, "effects": [ { "setFlag": "t.maddy_taken" } ] },
      { "id": "t.d8.hollis_sees", "day": 8, "effects": [ { "setFlag": "t.hollis_saw_mark" } ] },
      { "id": "t.d9.poison_hollis", "day": 9, "when": { "flag": "t.hollis_saw_mark" },
        "foiledBy": { "flag": "t.hollis_protected" }, "effects": [ { "fate": { "hollis": "dead" } } ] },
      { "id": "t.d11.take_nell", "day": 11, "foiledBy": { "flag": "t.nell_free" }, "effects": [ { "setFlag": "t.nell_sold" } ] },
      { "id": "t.d26.farm_lost", "day": 26, "effects": [ { "setFlag": "t.farm_lost" } ] },
      { "id": "t.d33.hang_wren", "day": 33, "effects": [ { "fate": { "wren": "dead" } } ] },
      { "id": "t.d36.purge_moonfell", "day": 36, "effects": [ { "setFlag": "t.moonfell_burned" } ] },
      { "id": "t.d50.battle", "day": 50, "effects": [ { "setFlag": "t.orcs_enslaved" } ] },
      { "id": "t.d52.bram", "day": 52, "effects": [ { "fate": { "elder": "dead" } } ] },
      { "id": "t.d70.levy", "day": 70, "effects": [ { "setFlag": "t.act1_over" } ] }
    ]
    """;

    private static List<AgendaStep> Steps() => Story.ParseAgendas(JsonDocument.Parse(Timeline).RootElement);

    private static void LiveDays(FakeStory story, List<AgendaStep> steps, int days)
    {
        for (var hour = 0; hour < days * 24; hour++)
        {
            story.World.Time = story.World.Time.AddMinutes(60);
            Story.RunAgendas(story, steps);
        }
    }

    [Fact]
    public void WithoutThePlayer_ActOneEndsDark()
    {
        var story = new FakeStory();
        var steps = Steps();
        LiveDays(story, steps, 70);
        Assert.Equal(NpcFate.Dead, story.World.Fate("hollis"));
        Assert.Equal(NpcFate.Dead, story.World.Fate("wren"));
        Assert.Equal(NpcFate.Dead, story.World.Fate("elder"));
        Assert.True(story.World.Has("t.nell_sold"));
        Assert.True(story.World.Has("t.moonfell_burned"));
        Assert.True(story.World.Has("t.orcs_enslaved"));
        Assert.True(story.World.Has("t.act1_over"));
        Assert.Equal(steps.Count, story.World.AgendaDone.Count);
    }

    [Fact]
    public void AStepRunsOnItsDayAndOnlyOnce()
    {
        var story = new FakeStory();
        var steps = Steps();
        // The game starts on day 1 at nine; the step of day 8 runs at six that morning.
        LiveDays(story, steps, 6);
        Assert.False(story.World.Has("t.hollis_saw_mark"));
        LiveDays(story, steps, 1);
        Assert.True(story.World.Has("t.hollis_saw_mark"));
        Assert.Equal(NpcFate.Alive, story.World.Fate("hollis"));
        LiveDays(story, steps, 1);
        Assert.Equal(NpcFate.Dead, story.World.Fate("hollis"));
    }

    [Fact]
    public void AFoiledStepNeverRuns()
    {
        var story = new FakeStory();
        story.World.SetFlag("t.hollis_protected");
        story.World.SetFlag("t.nell_free");
        LiveDays(story, Steps(), 70);
        Assert.Equal(NpcFate.Alive, story.World.Fate("hollis"));
        Assert.False(story.World.Has("t.nell_sold"));
        Assert.Contains("t.d9.poison_hollis", story.World.AgendaFoiled);
        Assert.Contains("t.d11.take_nell", story.World.AgendaFoiled);
    }
}

/// <summary>The client builds the same story state as the server, even when packets are lost.</summary>
public class StorySyncTests
{
    [Fact]
    public void Client_RebuildsTheStoryOverALossyNetwork()
    {
        var network = new SimNetwork(7, 40, 10, 10);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, Array.Empty<EnemySpawn>());
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 1 });
        var client = new ClientPrediction(network.Connect(1), world);
        for (var i = 0; i < 30; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None);
        }

        var story = server.StoryFor(1);
        story.World.SetFlag("guild.registered");
        Story.StartQuest(story, "slime_cull");
        Story.OnKill(story.World, "slime");
        Story.OnKill(story.World, "slime");
        story.World.SetFate("hollis", NpcFate.Dead);
        story.World.AddReputation(Story.Keys[StoryKind.Reputation].FirstOrDefault() ?? "guild", 0);

        for (var i = 0; i < 60 * 20; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None);
        }

        Assert.True(client.Story.Has("guild.registered"));
        Assert.Equal(QuestStatus.Active, client.Story.Quest("slime_cull").Status);
        Assert.Equal(2, client.Story.Quest("slime_cull").Progress);
        Assert.Equal(NpcFate.Dead, client.Story.Fate("hollis"));
        Assert.Equal(server.Calendar.Now.Day, client.Story.Time.Day);
    }

    [Fact]
    public void Server_TimeFlowsAndSleepJumpsToMorning()
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, Array.Empty<EnemySpawn>());
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 1 });
        var client = new ClientPrediction(network.Connect(1), world);
        var start = server.Calendar.Now;
        void Run(int ticks, Command command = Command.None)
        {
            for (var i = 0; i < ticks; i++)
            {
                network.Advance(1000.0 / Tuning.TickRate);
                server.Step();
                client.Step(0, 0, Buttons.None, 0, i == 0 ? command : Command.None);
            }
        }

        Run(Tuning.TickRate * 26);
        Assert.Equal(start.AddMinutes(60), server.Calendar.Now);

        // Reading a dialogue stops the clock.
        var reading = server.Calendar.Now;
        for (var i = 0; i < Tuning.TickRate * 10; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.Reading);
        }

        Assert.Equal(reading, server.Calendar.Now);
        start = server.Calendar.Now.AddMinutes(-60);

        // Sleeping in the morning does nothing; in the evening it wakes the player at six.
        Run(12, Command.Sleep);
        Assert.Equal(start.Day, server.Calendar.Now.Day);
        server.Calendar.Now = new GameTime(start.Day, 21 * 60);
        Run(12, Command.Sleep);
        Assert.Equal(new GameTime(start.Day + 1, GameTime.DayStartMinute).Day, server.Calendar.Now.Day);
        Assert.True(server.Calendar.Now.Minute < GameTime.DayStartMinute + 5);
    }
}
