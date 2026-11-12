using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>
/// Vinelashers on an empty field: rooted to the spot, they lash a player within reach of a vine and drag them
/// in towards the jaws; the Stranglethorn's three vines hold whoever they catch.
/// </summary>
public class VineTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(string id, float distance)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, distance), 0.3f, 1, Types: new[] { id }, Facing: 0f);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 6 });
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
    public void Vinelasher_NeverMoves()
    {
        var (server, client, network) = Start("vinelasher", 7f);
        var plant = server.Enemies[0];
        var root = plant.Position;
        Play(server, client, network, Tuning.TickRate * 10);
        plant.TakeHit(5, plant.Position + Vector3.UnitX * 2f, 1);
        Play(server, client, network, Tuning.TickRate * 2);
        Assert.True(Vector3.Distance(root with { Y = 0 }, plant.Position with { Y = 0 }) < 0.05f, $"the plant moved from {root} to {plant.Position}");
    }

    [Fact]
    public void Vinelasher_LashesAPlayerInReach_AndDragsThemIn()
    {
        // The player spawns about five metres from the plant: in reach of the vine, out of reach of the jaws.
        var (server, client, network) = Start("vinelasher", 7.5f);
        var plant = server.Enemies[0];
        Play(server, client, network, 5);
        var before = 0f;
        var after = 0f;
        for (var i = 0; i < Tuning.TickRate * 15 && server.Stats.VineLashes == 0; i++)
        {
            before = Vector3.Distance(server.GetPlayer(1)!.Value.Position with { Y = 0 }, plant.Position with { Y = 0 });
            Play(server, client, network, 1);
        }

        Assert.True(server.Stats.VineLashes > 0, "the vine never caught a player standing in its reach");
        Play(server, client, network, 15);
        after = Vector3.Distance(server.GetPlayer(1)!.Value.Position with { Y = 0 }, plant.Position with { Y = 0 });
        Assert.True(after < before - 0.8f, $"the lash drags the player in, but they went from {before:0.0} m to {after:0.0} m");
    }

    [Fact]
    public void Stranglethorn_GrabsAndHolds()
    {
        var thorn = EnemyDef.All.Single(d => d.Id == "stranglethorn");
        Assert.True(thorn.Rare && thorn.Statue);
        var grab = thorn.Abilities!.Single(a => a.Effect == "vineGrab");
        Assert.True(grab.WindupTicks >= 40, "three vines at once need a long warning");
        var (server, client, network) = Start("stranglethorn", 7.5f);
        var held = false;
        for (var i = 0; i < Tuning.TickRate * 25 && !held; i++)
        {
            Play(server, client, network, 1);
            held = server.Enemies[0].CurrentAbility?.Effect == "vineGrab" && server.Enemies[0].Action == EnemyAction.Strike
                && server.GetPlayer(1)!.Value.SlowTicks > Tuning.TickRate / 2;
        }

        Assert.True(held, "the Stranglethorn's grab never held the player");
    }
}
