using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>
/// Days 3 to 10 in Hearthmere without the player doing anything: the world moves, people change
/// their days, and the same news sounds different from different mouths.
/// </summary>
public class VillageTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(Vector3 spawn, int day, int hour)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var server = new GameServer(network.Connect(0), Valley.World, new ServerOptions { Seed = 1, SpawnOverride = spawn });
        var client = new ClientPrediction(network.Connect(1), Valley.World);
        server.Calendar.Now = new GameTime(day, hour * 60);
        Run(server, client, network, 10);
        return (server, client, network);
    }

    private static void Run(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Command command = Command.None, ushort arg = 0)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None, 0, i == 0 ? command : Command.None, null, 0, i == 0 ? arg : (ushort)0);
        }
    }

    /// <summary>Lets the clock reach a day and hour, an hour at a time, so every plan on the way runs.</summary>
    private static void LiveUntil(GameServer server, ClientPrediction client, SimNetwork network, int day, int hour)
    {
        while (server.Calendar.Now.CompareTo(new GameTime(day, hour * 60)) < 0)
        {
            var next = server.Calendar.Now.AddMinutes(60 - server.Calendar.Now.Minute % 60);
            server.Calendar.Now = next.AddMinutes(-1);
            for (var tick = 0; tick < 200 && server.Calendar.Now.CompareTo(next) < 0; tick++)
            {
                Run(server, client, network, 1);
            }
        }
    }

    private static Rumor Rumour(string id) => Story.Rumors.First(r => r.Id == id);

    /// <summary>The story as someone with nothing in their pockets sees it: only the world's own state.</summary>
    private sealed class Watching(WorldState world) : IStoryContext
    {
        public WorldState World => world;
        public int Rank => 0;
        public int Gold => 0;
        public int Level => 1;
        public int SaleValue => 0;
        public int ItemCount(string item) => 0;
        public int Attribute(string name) => 0;
        public bool Near(string role) => false;
        public Vector3 Position => default;
    }

    [Fact]
    public void TheWorldMoves_WhileThePlayerOnlyWatches()
    {
        var (server, client, network) = Start(new Vector3(0, 0, 0), 3, 5);
        var story = server.StoryState;
        LiveUntil(server, client, network, 3, 7);
        Assert.True(story.Has("world.goblin_iron"), "day 3: the Company's iron reaches the goblins");
        Assert.Equal(NpcFate.Alive, story.Fate("maddy"));

        LiveUntil(server, client, network, 5, 7);
        Assert.Equal(NpcFate.Gone, story.Fate("maddy"));
        Assert.Equal(NpcFate.Gone, story.Fate("tobias"));
        Assert.True(story.Has("harrow.raided"));

        LiveUntil(server, client, network, 10, 7);
        Assert.True(story.Has("brightwater.first_job"));
        Assert.True(story.Has("council.met"));
        Assert.True(story.Has("news.saltmere"));
        Assert.Equal(Story.Agendas.Count(a => a.Day <= 10), story.AgendaDone.Count);
    }

    [Fact]
    public void AfterTheRaid_HammGoesToTheGuard_AndPipKeepsWatch()
    {
        var (server, client, network) = Start(new Vector3(0, 0, 0), 5, 1);
        var before = server.Actor("pip");
        Assert.False(before.Active, "before the raid Pip is asleep at this hour");
        LiveUntil(server, client, network, 5, 6);
        // Dawn sends them out; most of an hour later (an hour is 26 seconds) both have walked where the raid sent them.
        Run(server, client, network, 24 * Tuning.TickRate);
        var hamm = server.Actor("hamm");
        var garrick = server.Actor("garrick");
        Assert.True(Vector3.Distance(hamm.Position, garrick.Position) < 3f, $"Hamm is at {hamm.Position}, not with Garrick at {garrick.Position}");
        var pip = server.Actor("pip");
        Assert.True(Vector3.Distance(pip.Position, new Vector3(38, pip.Position.Y, -29)) < 1f, $"Pip is at {pip.Position}, not on watch by the pen");
    }

    [Fact]
    public void TheSameNews_SoundsDifferentFromPipAndFromLina()
    {
        var (server, client, network) = Start(new Vector3(0, 0, 0), 5, 1);
        LiveUntil(server, client, network, 5, 10);
        var context = new Watching(server.StoryState);
        var raid = Rumour("harrow_raid");
        Assert.True(raid.ToldBy("pip", context));
        Assert.True(raid.ToldBy("lina", context));
        Assert.Contains("didn't go out", raid.TextFor("pip"));
        Assert.Contains("Company", raid.TextFor("lina"));
        Assert.NotEqual(raid.TextFor("pip"), raid.TextFor("lina"));
        // Wren has no part in it and is not at the tavern: she has nothing to say.
        Assert.False(raid.ToldBy("wren", context));
    }

    [Fact]
    public void AtTheTavern_AnyoneTellsWhatIsGoingRound_AndOnlyThere()
    {
        var (server, client, network) = Start(new Vector3(0, 0, 0), 3, 9);
        LiveUntil(server, client, network, 3, 10);
        var context = new Watching(server.StoryState);
        var iron = Rumour("goblin_iron");
        Assert.False(iron.ToldBy("smith", context), "at her forge in the morning Tess does not gossip");
        Assert.True(iron.ToldBy("moss", context), "Moss saw it himself and tells it anywhere");
        LiveUntil(server, client, network, 3, 20);
        Assert.True(iron.ToldBy("smith", new Watching(server.StoryState)), "at the tavern table in the evening she passes it on");
    }

    [Fact]
    public void ARumour_IsTalkedOfForSomeDaysOnly()
    {
        var (server, client, network) = Start(new Vector3(0, 0, 0), 3, 9);
        LiveUntil(server, client, network, 3, 10);
        var iron = Rumour("goblin_iron");
        Assert.True(iron.ToldBy("moss", new Watching(server.StoryState)));
        LiveUntil(server, client, network, iron.ToDay + 1, 10);
        Assert.False(iron.ToldBy("moss", new Watching(server.StoryState)));
    }

    [Fact]
    public void HearingARumour_NeedsItsTellerNear_AndGoesInTheJournal()
    {
        // The player stands by Corwin's table; Corwin has his own word on the goblins' iron.
        var (server, client, network) = Start(new Vector3(25.8f, 0, 3.0f), 3, 9);
        LiveUntil(server, client, network, 3, 10);
        var iron = Rumour("goblin_iron");
        var raid = Rumour("harrow_raid");
        Run(server, client, network, 12, Command.HearRumor, (ushort)raid.Index);
        Assert.False(server.StoryState.Has(raid.HeardFlag), "the raid has not happened: nobody can tell it");
        Run(server, client, network, 12, Command.HearRumor, (ushort)iron.Index);
        Assert.True(server.StoryState.Has(iron.HeardFlag));
        Assert.True(Story.Notes.ContainsKey(iron.HeardFlag), "a heard rumour has a note for the journal");
        Assert.DoesNotContain(iron, Story.NewsFrom("corwin", new Watching(server.StoryState)));
    }

    [Fact]
    public void FarFromAnyTeller_NothingIsHeard()
    {
        var (server, client, network) = Start(new Vector3(-80, 0, -80), 3, 9);
        LiveUntil(server, client, network, 3, 10);
        var iron = Rumour("goblin_iron");
        Run(server, client, network, 12, Command.HearRumor, (ushort)iron.Index);
        Assert.False(server.StoryState.Has(iron.HeardFlag));
    }

    [Fact]
    public void EveryRumour_HasATellerWithADayOrAName()
    {
        var known = Actors.For(Valley.World).Select(a => a.Id).ToHashSet();
        foreach (var rumor in Story.Rumors)
        {
            Assert.All(rumor.From.Where(f => f != "tavern").Concat(rumor.By.Keys), teller => Assert.Contains(teller, known));
            Assert.True(rumor.FromDay <= rumor.ToDay);
        }

        Assert.Contains(Schedules.All.Values.SelectMany(b => b), b => b.Place == "tavern");
    }
}
