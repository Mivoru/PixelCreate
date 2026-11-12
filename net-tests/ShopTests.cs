using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Buying healing potions from the alchemist and drinking them.</summary>
public class ShopTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) AtTheAlchemist()
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var wren = Valley.World.Npcs.Single(n => n.Role == "alchemist");
        var server = new GameServer(network.Connect(0), Valley.World, new ServerOptions { Seed = 1, SpawnOverride = wren.Position + new Vector3(-1.6f, 0, 0) });
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
    public void Alchemist_SellsPotionsForGold()
    {
        var (server, client, network) = AtTheAlchemist();
        var potion = ItemDef.Index("minor_healing_potion");
        Run(server, client, network, 12, ShopCommands.Buy(potion));
        Assert.Equal(0, server.GetBagCount(1, potion));

        server.GiveGold(1, 50);
        Run(server, client, network, 12, ShopCommands.Buy(potion));
        Assert.Equal(1, server.GetBagCount(1, potion));
        Assert.Equal(50 - ItemDef.All[potion].Price, server.GetGold(1));
    }

    [Fact]
    public void Dorran_SellsFoodAtTheBentNail_AndOnlyThere()
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var dorran = Valley.World.Npcs.Single(n => n.Id == "dorran");
        // The spawn spreads a player three metres from this point, which puts them on the floor before the bar.
        var server = new GameServer(network.Connect(0), Valley.World, new ServerOptions { Seed = 1, SpawnOverride = dorran.Position + new Vector3(1.51f, 0, 0.3f) });
        var client = new ClientPrediction(network.Connect(1), Valley.World);
        Run(server, client, network, 10);
        Assert.True(Vector3.Distance(client.State.Position with { Y = 0 }, dorran.Position with { Y = 0 }) < Guild.TalkRange,
            $"the test player stands at {client.State.Position}, out of Dorran's reach");

        var stew = ItemDef.Index("hearty_stew");
        server.GiveGold(1, 20);
        Run(server, client, network, 12, ShopCommands.Buy(stew));
        Assert.Equal(1, server.GetBagCount(1, stew));
        Assert.Equal(20 - ItemDef.All[stew].Price, server.GetGold(1));

        // Wren does not sell stew.
        var (atWren, wrenClient, wrenNetwork) = AtTheAlchemist();
        atWren.GiveGold(1, 20);
        Run(atWren, wrenClient, wrenNetwork, 12, ShopCommands.Buy(stew));
        Assert.Equal(0, atWren.GetBagCount(1, stew));
    }

    [Fact]
    public void Potions_HealAndShareACooldown()
    {
        var (server, client, network) = AtTheAlchemist();
        var potion = ItemDef.Index("minor_healing_potion");
        server.GiveGold(1, 100);
        for (var i = 0; i < 3; i++)
        {
            Run(server, client, network, 12, ShopCommands.Buy(potion));
        }

        server.SetHealth(1, 20);
        Run(server, client, network, 12, Command.UseItem, potion);
        Assert.Equal(20 + ItemDef.All[potion].Heal, server.GetPlayer(1)!.Value.Health);

        // A second one straight away does nothing; after the cooldown it heals again.
        Run(server, client, network, 12, Command.UseItem, potion);
        Assert.Equal(2, server.GetBagCount(1, potion));
        Run(server, client, network, GameServer.PotionCooldownTicks);
        Run(server, client, network, 12, Command.UseItem, potion);
        Assert.Equal(1, server.GetBagCount(1, potion));
    }
}
