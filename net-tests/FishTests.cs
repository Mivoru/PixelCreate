using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>
/// Mirefins on an empty field, with their camp as the lake: beyond reach under the water, a leap at a player by
/// the shore that leaves the fish stranded and open to blows, and no leap at a player who keeps away.
/// </summary>
public class FishTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(string id, float lakeDistance)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var lake = new EnemySpawn(new Vector3(0, 0, lakeDistance), 15f, 1, Types: new[] { id });
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { lake });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 12 });
        var client = new ClientPrediction(network.Connect(1), world);
        return (server, client, network);
    }

    private static void Play(GameServer server, ClientPrediction client, SimNetwork network, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None);
        }
    }

    [Fact]
    public void Mirefin_UnderTheWater_IsBeyondReach()
    {
        var (server, client, network) = Start("mirefin", 40f);
        Play(server, client, network, 30);
        var fish = server.Enemies[0];
        Assert.True(fish.Submerged, "a fish in its lake swims under the water");
        var health = fish.Health;
        Assert.False(fish.TakeHit(500, fish.Position + Vector3.UnitX, 1));
        Assert.Equal(health, fish.Health);
    }

    [Fact]
    public void Mirefin_LeapsAtAPlayerByTheShore_AndLiesStranded()
    {
        // The player stands about two metres outside the lake's edge.
        var (server, client, network) = Start("mirefin", 20f);
        var fish = server.Enemies[0];
        var stranded = false;
        for (var i = 0; i < Tuning.TickRate * 25 && !stranded; i++)
        {
            Play(server, client, network, 1);
            stranded = fish.Action == EnemyAction.Stagger;
        }

        Assert.True(stranded, "the fish never leapt out at a player by the shore");
        Assert.True(server.Stats.PlayerHits > 0, "the leap must hurt the player it lands on");
        Assert.False(fish.Untouchable, "a stranded fish can be struck");
        Assert.True(Vector3.Distance(fish.Position with { Y = 0 }, new Vector3(0, 0, 20)) > 15f - 0.5f, "a stranded fish lies out of the water");

        // Then it wriggles back in.
        var back = false;
        for (var i = 0; i < Tuning.TickRate * 15 && !back; i++)
        {
            Play(server, client, network, 1);
            back = fish.Submerged;
        }

        Assert.True(back, "the stranded fish never got back into its lake");
    }

    [Fact]
    public void Mirefin_LeavesAPlayerAwayFromTheWaterAlone()
    {
        var (server, client, network) = Start("mirefin", 30f);
        Play(server, client, network, Tuning.TickRate * 15);
        Assert.Equal(0, server.Stats.PlayerHits);
    }

    [Fact]
    public void OldGulper_SpitsAJet_ThatThrowsThePlayerBack()
    {
        var gulper = EnemyDef.All.Single(d => d.Id == "old_gulper");
        Assert.True(gulper.Rare);
        Assert.Contains(gulper.Abilities!, a => a.Effect == "waterJet");
    }
}
