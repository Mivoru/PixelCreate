using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Tess forges gear from monster materials and gold (tools/forge.py, action forge:&lt;item&gt;).</summary>
public class ForgeTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    private static void Run(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Command command = Command.None, ushort arg = 0)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None, 0, i == 0 ? command : Command.None, null, 0, i == 0 ? arg : (ushort)0);
        }
    }

    private static ushort Option(string words) => (ushort)Story.Options.First(o => o.Npc == "smith" && o.Text.Contains(words)).Index;

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(Vector3 spawn)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var server = new GameServer(network.Connect(0), Valley.World, new ServerOptions { Seed = 1, SpawnOverride = spawn });
        var client = new ClientPrediction(network.Connect(1), Valley.World);
        Run(server, client, network, 10);
        return (server, client, network);
    }

    [Fact]
    public void Tess_ForgesTwinDaggers_FromWolfFangsIronAndGold_AndOnlyWhenTheBagHoldsThem()
    {
        // Tess stands at her anvil from six in the morning, when a new game starts.
        var anvil = new Vector3(-0.6f, 0, 18.4f);
        var (server, client, network) = Start(anvil + new Vector3(1.4f, 0, 0));
        var tess = server.Actor("smith").Position;
        Assert.True(Vector3.Distance(server.GetPlayer(1)!.Value.Position with { Y = 0 }, tess with { Y = 0 }) < Guild.TalkRange + 1,
            $"the test player at {server.GetPlayer(1)!.Value.Position} is out of reach of Tess at {tess}");

        var fang = ItemDef.Index("wolf_fang");
        var iron = ItemDef.Index("iron_scrap");
        var daggers = ItemDef.Index("twin_daggers");
        bool HasDaggers() => Enumerable.Range(0, GameServer.GearSlots).Any(i => server.GetGear(1, i) is { Item: var item } && item == daggers);

        // Short of fangs: nothing happens.
        server.GiveItem(1, fang, 3);
        server.GiveItem(1, iron, 2);
        server.GiveGold(1, 100);
        Run(server, client, network, 12, Command.Choose, Option("Forge the Twin Daggers"));
        Assert.False(HasDaggers());
        Assert.Equal(3, server.GetBagCount(1, fang));
        Assert.Equal(100, server.GetGold(1));

        server.GiveItem(1, fang, 1);
        Run(server, client, network, 12, Command.Choose, Option("Forge the Twin Daggers"));
        Assert.True(HasDaggers(), "Tess forged nothing");
        Assert.Equal(0, server.GetBagCount(1, fang));
        Assert.Equal(0, server.GetBagCount(1, iron));
        Assert.Equal(60, server.GetGold(1));
        var piece = Enumerable.Range(0, GameServer.GearSlots).Select(i => server.GetGear(1, i)).First(g => g?.Item == daggers)!;
        Assert.Equal(Rarity.Rare, piece.Rarity);
    }

    [Fact]
    public void EveryRecipe_NamesRealItems_AndForgesEquipment()
    {
        foreach (var option in Story.Options.Where(o => o.Npc == "smith" && o.Text.StartsWith("Forge the", StringComparison.Ordinal)))
        {
            Assert.Contains(option.Effects, e => e.References().Any(r => r.Kind == "item"));
        }

        Assert.True(Story.Options.Count(o => o.Npc == "smith" && o.Text.StartsWith("Forge the", StringComparison.Ordinal)) >= 8);
    }
}
