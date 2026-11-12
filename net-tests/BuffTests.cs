using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>What cooked food and the warding draught are for: Well Fed and Warded (design/dungeon-archive.md, part 5).</summary>
public class BuffTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start()
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var server = new GameServer(network.Connect(0), Valley.World, new ServerOptions { Seed = 1, SpawnOverride = new Vector3(40, 0, 40) });
        var client = new ClientPrediction(network.Connect(1), Valley.World);
        server.EnemiesFrozen = true;
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
    public void CookedFood_MakesThePlayerWellFed_AndRawFoodDoesNot()
    {
        var (server, client, network) = Start();
        var plain = server.MaxHealthOf(1);
        var beef = ItemDef.Index("raw_beef");
        var steak = ItemDef.Index("seared_steak");
        server.GiveItem(1, beef, 1);
        server.GiveItem(1, steak, 1);

        Run(server, client, network, 12, Command.UseItem, beef);
        Assert.Equal(plain, server.MaxHealthOf(1));
        Assert.Equal(0, client.Latest!.WellFedSeconds);

        Run(server, client, network, 12, Command.UseItem, steak);
        Assert.True(server.MaxHealthOf(1) > plain, "Well Fed gives more maximum health");
        Assert.InRange(client.Latest!.WellFedSeconds, 590, 600);

        // It heals slowly while it lasts, faster than nothing.
        server.SetHealth(1, 30);
        Run(server, client, network, 60 * 4);
        Assert.True(server.GetPlayer(1)!.Value.Health > 30);
    }

    [Fact]
    public void EveryCookedDish_IsWellFed_AndNoRawFoodIs()
    {
        foreach (var recipe in new[] { "roast_chicken", "game_skewer", "roast_mutton", "boar_roast", "venison_steak", "seared_steak", "grilled_fillet", "fried_perch", "baked_trout" })
        {
            Assert.Equal("wellFed", ItemDef.All[ItemDef.Index(recipe)].Buff);
        }

        foreach (var raw in new[] { "raw_beef", "venison", "river_perch", "silver_trout", "mirefin_fillet", "raw_chicken" })
        {
            Assert.Equal("", ItemDef.All[ItemDef.Index(raw)].Buff);
        }
    }

    [Fact]
    public void TheWardingDraught_SoftensBlows_ForAMinute()
    {
        var (server, client, network) = Start();
        var draught = ItemDef.Index("warding_draught");
        server.GiveItem(1, draught, 1);
        Run(server, client, network, 12, Command.UseItem, draught);
        Assert.InRange(client.Latest!.WardedSeconds, 55, 60);
        Run(server, client, network, (int)GameServer.WardedTicks);
        Assert.Equal(0, client.Latest!.WardedSeconds);
    }

    [Fact]
    public void ItemsNobodyDrops_StillShowTheWay_ToWhereTheyGrowOrBite()
    {
        Assert.Contains(Valley.World.Gathers, g => ItemDef.All[g.Item].Id == "meadow_sage");
        foreach (var job in new[] { "sage_for_wren", "fish_for_the_nail", "glowcaps_for_the_guild" })
        {
            Assert.NotNull(Story.Quest(job));
        }
    }
}
