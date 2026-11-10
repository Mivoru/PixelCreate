using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Waystones in the real zone: touch to bind, travel only from a bound stone to a bound stone, for gold.</summary>
public class WaystoneTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    private static Npc Stone(string id) => Valley.World.Npcs.Single(n => n.Id == id);

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(Vector3 spawn)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var server = new GameServer(network.Connect(0), Valley.World, new ServerOptions { Seed = 1, SpawnOverride = spawn, TestMode = true });
        var client = new ClientPrediction(network.Connect(1), Valley.World);
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

    private static ushort Option(string npc, string words) => (ushort)Story.Options.First(o => o.Npc == npc && o.Text.Contains(words)).Index;

    private static Vector3 At(GameServer server) => server.GetPlayer(1)!.Value.Position;

    [Fact]
    public void EveryWaystone_StandsClear_AndItsArrivalIsOpenGround()
    {
        var stones = Valley.World.Npcs.Where(n => n.Role == Waystones.Role).ToList();
        Assert.True(stones.Count >= 5, "the valley has its five waystones");
        foreach (var stone in stones)
        {
            Assert.Contains(Valley.Props, p => p.Model == "waystone" && Vector2.Distance(new(p.Position.X, p.Position.Z), new(stone.Position.X, stone.Position.Z)) < 0.5f);
            var arrival = Waystones.Arrival(stone);
            arrival.Y = Valley.World.GroundHeight(arrival.X, arrival.Z) + 0.05f;
            Assert.False(Valley.World.OverlapsPlayer(arrival), $"travellers to {stone.Name} would arrive inside something");
        }
    }

    [Fact]
    public void Travel_NeedsBothStonesBound_ThenCarriesThePlayerForGold()
    {
        var home = Stone("waystone_hearthmere");
        var falls = Stone("waystone_falls");
        var (server, client, network) = Start(Waystones.Arrival(falls));
        server.GiveGold(1, 100);

        // Bind the Silver Falls stone; the village stone is not bound yet, so the trip is refused.
        Run(server, client, network, 12, Command.Choose, Option(falls.Id, "Lay your hand"));
        Run(server, client, network, 12, Command.Choose, Option(falls.Id, "Hearthmere Waystone"));
        Assert.True(Vector3.Distance(At(server), Waystones.Arrival(falls)) < 3f, "travel to an unbound stone must do nothing");

        server.SetFlag("waystone." + home.Id);
        Run(server, client, network, 12, Command.Choose, Option(falls.Id, "Hearthmere Waystone"));
        var there = At(server);
        Assert.True(Vector2.Distance(new(there.X, there.Z), new(Waystones.Arrival(home).X, Waystones.Arrival(home).Z)) < 1.5f,
            $"the player should stand by the Hearthmere stone, not at {there}");
        Assert.Equal(85, server.GetGold(1));
    }
}
