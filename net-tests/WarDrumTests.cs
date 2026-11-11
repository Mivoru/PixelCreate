using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>
/// The Ironjaw war drum: an orc that sees the player runs to the drum and beats it, and when it finishes
/// every camp that answers the drum comes for the player. A blow on the drummer stops the alarm.
/// </summary>
public class WarDrumTests
{
    private static readonly Vector3 Drum = new(0, 0, 20);

    /// <summary>A gate guard twelve metres from the player, and a camp forty metres off that cannot see the player.</summary>
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start()
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var gate = new EnemySpawn(new Vector3(0, 0, 12), 0.5f, 1, Types: new[] { "orc_warrior" }, Drum: Drum);
        var camp = new EnemySpawn(new Vector3(0, 0, 40), 1f, 2, Types: new[] { "orc_warrior" }, Drum: Drum);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { gate, camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 5 });
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
    public void GateGuard_BeatsTheDrum_AndTheWholeCampComes()
    {
        var (server, client, network) = Start();
        var guard = server.Enemies[0];
        var drummed = false;
        for (var i = 0; i < Tuning.TickRate * 15 && !drummed; i++)
        {
            Play(server, client, network, 1);
            drummed = guard.Action == EnemyAction.Drum;
        }

        Assert.True(drummed, "the guard never went to the drum after seeing the player");
        Assert.True(Vector3.Distance(guard.Position with { Y = 0 }, Drum) < 3f, "the guard beats the drum from where it stands");
        Assert.All(server.Enemies.Skip(1), e => Assert.True(e.TargetId < 0, "the far camp came before the drum sounded"));

        Play(server, client, network, Enemy.DrumTicks + 30);
        Assert.Equal(1, server.Stats.Alarms);
        Assert.All(server.Enemies.Skip(1), e => Assert.True(e.TargetId >= 0, "the far camp did not answer the drum"));
    }

    [Fact]
    public void ABlowOnTheDrummer_StopsTheAlarm()
    {
        var (server, client, network) = Start();
        var guard = server.Enemies[0];
        for (var i = 0; i < Tuning.TickRate * 15 && guard.Action != EnemyAction.Drum; i++)
        {
            Play(server, client, network, 1);
        }

        Assert.Equal(EnemyAction.Drum, guard.Action);
        guard.TakeHit(1, Vector3.Zero, 0);
        Play(server, client, network, Enemy.DrumTicks + 60);
        Assert.Equal(0, server.Stats.Alarms);
        Assert.All(server.Enemies.Skip(1), e => Assert.True(e.TargetId < 0, "the far camp came although the drum was stopped"));
    }
}
