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
