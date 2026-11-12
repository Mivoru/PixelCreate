using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Herbs and mushrooms (the zone's gathers): they grow on open ground, are picked by walking over them and grow back.</summary>
public class GatherTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    private static void Run(GameServer server, ClientPrediction client, SimNetwork network, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None, 0, Command.None);
        }
    }

    [Fact]
    public void EveryGatherItem_Grows_OnOpenDryGround()
    {
        var spots = Valley.World.Gathers;
        foreach (var item in ItemDef.All.Where(i => i.Gather))
        {
            Assert.True(spots.Count(s => ItemDef.All[s.Item] == item) >= 5, $"{item.Name} grows in fewer than five places");
        }

        foreach (var spot in spots)
        {
            Assert.False(Valley.IsWater(spot.Position.X, spot.Position.Z), $"a herb grows in water at {spot.Position}");
            Assert.False(Valley.World.OverlapsPlayer(spot.Position + Vector3.UnitY * 0.05f), $"a herb grows inside something at {spot.Position}");
        }
    }

    [Fact]
    public void AHerb_ShowsNearby_IsPickedByWalkingOverIt_AndGrowsBackLater()
    {
        var spot = Valley.World.Gathers.First(s => ItemDef.All[s.Item].Id == "meadow_sage");
        var network = new SimNetwork(1, 0, 0, 0);
        // Spawned twelve metres off: the herb shows but is not picked.
        var server = new GameServer(network.Connect(0), Valley.World, new ServerOptions { Seed = 1, SpawnOverride = spot.Position + new Vector3(12, 0, 0) });
        var client = new ClientPrediction(network.Connect(1), Valley.World);
        Run(server, client, network, 40);
        Assert.Contains(client.Latest!.Drops, d => d.Item == spot.Item && Vector3.Distance(d.Position, spot.Position) < 0.1f);
        Assert.Equal(0, server.GetBagCount(1, spot.Item));

        // Standing on it picks it, and it is gone for this player until it grows back.
        server.MoveForTest(1, spot.Position);
        Run(server, client, network, 60);
        Assert.True(server.GetBagCount(1, spot.Item) >= 1, "standing on the sage did not pick it");
        var picked = server.GetBagCount(1, spot.Item);
        Run(server, client, network, 120);
        Assert.Equal(picked, server.GetBagCount(1, spot.Item));
        Assert.DoesNotContain(client.Latest!.Drops, d => d.Item == spot.Item && Vector3.Distance(d.Position, spot.Position) < 0.1f);
    }
}
