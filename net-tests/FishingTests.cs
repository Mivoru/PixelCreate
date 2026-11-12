using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Fishing off the piers of Mirror Lake (tools/fishing.py, action fish, GameServer.StepFishing).</summary>
public class FishingTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");
    private static readonly string[] Catch = { "river_perch", "silver_trout", "mirefin_fillet", "drowned_key" };

    private static void Run(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Command command = Command.None, ushort arg = 0)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None, 0, i == 0 ? command : Command.None, null, 0, i == 0 ? arg : (ushort)0);
        }
    }

    private static int Fish(GameServer server) => Catch.Sum(id => server.GetBagCount(1, ItemDef.Index(id)));

    [Fact]
    public void ALineCastFromThePier_Bites_IfThePlayerKeepsStill_AndNotIfTheyWalkOff()
    {
        var spot = Valley.World.Npcs.Single(n => n.Id == "fish_landing");
        var network = new SimNetwork(1, 0, 0, 0);
        var server = new GameServer(network.Connect(0), Valley.World, new ServerOptions { Seed = 1, SpawnOverride = spot.Position });
        var client = new ClientPrediction(network.Connect(1), Valley.World);
        // The lake's mirefins would pounce at a player on the pier and knock the line slack.
        server.EnemiesFrozen = true;
        Run(server, client, network, 10);
        server.MoveForTest(1, spot.Position + new Vector3(0.6f, 0, 0));
        Run(server, client, network, 10);
        var cast = (ushort)Story.Options.First(o => o.Npc == spot.Id && o.Text == "Cast a line.").Index;

        var before = server.GetPlayer(1)!.Value.Position;
        Run(server, client, network, 12, Command.Choose, cast);
        Assert.Equal(0, Fish(server));
        Run(server, client, network, GameServer.FishBiteMaxTicks + 10);
        Assert.True(Fish(server) == 1, $"no bite: the player stood at {before}, then {server.GetPlayer(1)!.Value.Position}; the spot is at {spot.Position}");

        // Cast again and walk off: the line goes slack and nothing bites.
        Run(server, client, network, 12, Command.Choose, cast);
        server.MoveForTest(1, spot.Position + new Vector3(6f, 0, 0));
        Run(server, client, network, GameServer.FishBiteMaxTicks + 10);
        Assert.Equal(1, Fish(server));
    }

    [Fact]
    public void EveryFishingSpot_IsOnAPierEnd_OverWater()
    {
        foreach (var spot in Valley.World.Npcs.Where(n => n.Role == "fishing"))
        {
            Assert.Contains(Valley.World.Platforms, p => spot.Position.X >= p.MinX && spot.Position.X <= p.MaxX && spot.Position.Z >= p.MinZ && spot.Position.Z <= p.MaxZ);
            var near = new[] { new Vector2(2, 0), new Vector2(-2, 0), new Vector2(0, 2), new Vector2(0, -2) };
            Assert.True(near.Count(d => Valley.IsWater(spot.Position.X + d.X, spot.Position.Z + d.Y)) >= 2, $"{spot.Id} is not over water");
        }
    }
}
