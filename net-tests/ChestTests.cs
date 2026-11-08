using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>The Guild chest: storing and taking stacks and gear, only while standing by it.</summary>
public class ChestTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(bool byTheChest)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var chest = Valley.World.Npcs.Single(n => n.Role == "storage");
        var spawn = byTheChest ? chest.Position + new Vector3(1.2f, 0, 0) : new Vector3(420, 0, 4);
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

    [Fact]
    public void Chest_KeepsAStackAndGivesItBack()
    {
        var (server, client, network) = Start(true);
        var potion = ItemDef.Index("minor_healing_potion");
        server.GiveGold(1, 100);
        server.GiveItem(1, potion, 3);
        Run(server, client, network, 12, Command.StoreItem, potion);
        Assert.Equal(0, server.GetBagCount(1, potion));
        Assert.Equal(3, server.GetStoredCount(1, potion));
        Run(server, client, network, 12, Command.TakeItem, potion);
        Assert.Equal(3, server.GetBagCount(1, potion));
        Assert.Equal(0, server.GetStoredCount(1, potion));
    }

    [Fact]
    public void Chest_KeepsGearThatIsNotWorn()
    {
        var (server, client, network) = Start(true);
        // Test mode hands out a kit; place 1 holds a piece that is not worn, place 0 the worn sword.
        var piece = server.GetGear(1, 1)!;
        Run(server, client, network, 12, Command.StoreGear, 1);
        Assert.Null(server.GetGear(1, 1));
        Assert.Same(piece, server.GetStoredGear(1, 0));
        Run(server, client, network, 12, Command.StoreGear, 0);
        Assert.NotNull(server.GetGear(1, 0));

        Run(server, client, network, 12, Command.TakeGear, 0);
        Assert.Null(server.GetStoredGear(1, 0));
        Assert.Same(piece, server.GetGear(1, 1));
    }

    [Fact]
    public void Chest_OnlyWorksStandingByIt()
    {
        var (server, client, network) = Start(false);
        var potion = ItemDef.Index("minor_healing_potion");
        server.GiveItem(1, potion, 2);
        Run(server, client, network, 12, Command.StoreItem, potion);
        Assert.Equal(2, server.GetBagCount(1, potion));
    }
}
