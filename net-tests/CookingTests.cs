using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Cooking at a fire (tools/cooking.py): raw meat from the bag in, cooked food out, only beside a fire.</summary>
public class CookingTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(Vector3 spawn)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var server = new GameServer(network.Connect(0), Valley.World, new ServerOptions { Seed = 1, SpawnOverride = spawn });
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

    [Fact]
    public void EveryFire_StandsOnAFireOrHearth_AndHasItsRecipes()
    {
        var fires = Valley.World.Npcs.Where(n => n.Role == "hearth").ToList();
        Assert.True(fires.Count >= 4, "the valley has its cooking fires");
        foreach (var fire in fires)
        {
            Assert.True(Story.Dialogues.ContainsKey(fire.Id), $"{fire.Id} has no dialogue: run python tools/cooking.py");
            var outside = fire.Position.X < Guild.InteriorX;
            if (outside)
            {
                Assert.Contains(Valley.Props, p => p.Model == "camp_fire" && Vector2.Distance(new(p.Position.X, p.Position.Z), new(fire.Position.X, fire.Position.Z)) < 0.5f);
            }
        }
    }

    [Fact]
    public void RawBeef_BecomesASearedSteak_AtTheWoodcuttersFire_AndNotAwayFromIt()
    {
        var fire = Valley.World.Npcs.Single(n => n.Id == "fire_woodcutters");
        var beef = ItemDef.Index("raw_beef");
        var steak = ItemDef.Index("seared_steak");

        var (server, client, network) = Start(fire.Position);
        Assert.True(Vector3.Distance(server.GetPlayer(1)!.Value.Position with { Y = 0 }, fire.Position with { Y = 0 }) < Guild.TalkRange + 1,
            "the test player spawned out of the fire's reach");
        // Nothing to cook: the choice is refused.
        Run(server, client, network, 12, Command.Choose, Option(fire.Id, "Sear the beef"));
        Assert.Equal(0, server.GetBagCount(1, steak));

        server.GiveItem(1, beef, 2);
        Run(server, client, network, 12, Command.Choose, Option(fire.Id, "Sear the beef"));
        Assert.Equal(1, server.GetBagCount(1, beef));
        Assert.Equal(1, server.GetBagCount(1, steak));
        Run(server, client, network, 12, Command.Choose, Option(fire.Id, "Sear the beef"));
        Assert.Equal(0, server.GetBagCount(1, beef));
        Assert.Equal(2, server.GetBagCount(1, steak));
        Assert.True(ItemDef.All[steak].Heal > ItemDef.All[beef].Heal, "cooking is worth it");

        // Far from any fire the same choice does nothing.
        var (away, awayClient, awayNetwork) = Start(fire.Position + new Vector3(30, 0, 0));
        away.GiveItem(1, beef, 1);
        Run(away, awayClient, awayNetwork, 12, Command.Choose, Option(fire.Id, "Sear the beef"));
        Assert.Equal(1, away.GetBagCount(1, beef));
        Assert.Equal(0, away.GetBagCount(1, steak));
    }
}
