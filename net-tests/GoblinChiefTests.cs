using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>
/// The goblin trapper's snares and the warchief's horn and shield on an empty field.
/// </summary>
public class GoblinChiefTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(params EnemySpawn[] camps)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, camps);
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 9 });
        var client = new ClientPrediction(network.Connect(1), world);
        return (server, client, network);
    }

    private static void Play(GameServer server, ClientPrediction client, SimNetwork network, int ticks, sbyte x = 0, sbyte z = 0)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(x, z, Buttons.None);
        }
    }

    [Fact]
    public void Trapper_LaysASnare_ThatSlowsWhoeverStepsInIt()
    {
        var (server, client, network) = Start(new EnemySpawn(new Vector3(0, 0, 9), 0.5f, 1, Types: new[] { "goblin_trapper" }));
        var laid = false;
        for (var i = 0; i < Tuning.TickRate * 20 && !laid; i++)
        {
            Play(server, client, network, 1);
            laid = server.FireWalls.Any(w => w.Kind == HazardKind.Snare);
        }

        Assert.True(laid, "the trapper never laid a snare for a player nine metres off");
        var snare = server.FireWalls.First(w => w.Kind == HazardKind.Snare);

        // Walk at the snare: find which way the stick moves the player, then go.
        var before = server.GetPlayer(1)!.Value.Position;
        Play(server, client, network, 20, 0, 127);
        var after = server.GetPlayer(1)!.Value.Position;
        var stick = Vector3.Normalize((after - before) with { Y = 0 });
        var snared = false;
        for (var i = 0; i < Tuning.TickRate * 6 && !snared; i++)
        {
            var here = server.GetPlayer(1)!.Value.Position;
            var to = (snare.Origin - here) with { Y = 0 };
            if (to.Length() < 0.2f)
            {
                break;
            }

            var side = new Vector3(-stick.Z, 0, stick.X);
            var forward = Vector3.Dot(to, stick);
            var across = Vector3.Dot(to, side);
            var scale = 127f / MathF.Max(MathF.Abs(forward), MathF.Abs(across));
            Play(server, client, network, 1, (sbyte)Math.Clamp(-across * scale, -127, 127), (sbyte)Math.Clamp(forward * scale, -127, 127));
            snared = server.Stats.Snared > 0;
        }

        Assert.True(snared, "walking into the snare did not set it off");
        Assert.True(server.GetPlayer(1)!.Value.SlowTicks > 0, "a snare holds the player for a moment");
        Assert.DoesNotContain(server.FireWalls, w => w.Id == snare.Id);
    }

    [Fact]
    public void Trapper_KeepsNoMoreThanThreeSnares()
    {
        var (server, client, network) = Start(new EnemySpawn(new Vector3(0, 0, 9), 0.5f, 1, Types: new[] { "goblin_trapper" }));
        var most = 0;
        for (var i = 0; i < Tuning.TickRate * 60; i++)
        {
            Play(server, client, network, 1);
            most = Math.Max(most, server.FireWalls.Count(w => w.Kind == HazardKind.Snare));
        }

        Assert.InRange(most, 1, Snare.PerTrapper);
    }

    [Fact]
    public void Warchief_SoundsHisHorn_AndTheGoblinsNearbyCome()
    {
        // The warchief by the player, and a camp of goblins thirty metres off that cannot see the player.
        var chief = new EnemySpawn(new Vector3(0, 0, 6), 0.5f, 1, Types: new[] { "goblin_warchief" });
        var camp = new EnemySpawn(new Vector3(0, 0, 36), 1f, 2, Types: new[] { "goblin_skirmisher" });
        var (server, client, network) = Start(chief, camp);
        var called = false;
        for (var i = 0; i < Tuning.TickRate * 25 && !called; i++)
        {
            Play(server, client, network, 1);
            called = server.Stats.HornCalls > 0;
        }

        Assert.True(called, "the warchief never sounded his horn");
        Assert.All(server.Enemies.Where(e => e.Def.Id == "goblin_skirmisher"), e => Assert.True(e.TargetId >= 0, "a goblin in earshot did not come"));
    }

    [Fact]
    public void Warchief_ShieldBash_ThrowsThePlayerBack()
    {
        var warchief = EnemyDef.All.Single(d => d.Id == "goblin_warchief");
        Assert.True(warchief.Rare);
        Assert.Contains(warchief.Abilities!, a => a.Effect == "shieldBash");
        var (server, client, network) = Start(new EnemySpawn(new Vector3(0, 0, 2.5f), 0.3f, 1, Types: new[] { "goblin_warchief" }));
        var start = server.GetPlayer(1)?.Position ?? Vector3.Zero;
        var thrown = 0f;
        for (var i = 0; i < Tuning.TickRate * 25 && thrown < 1.5f; i++)
        {
            Play(server, client, network, 1);
            if (server.Enemies[0].CurrentAbility?.Effect == "shieldBash" && server.Enemies[0].Action == EnemyAction.Strike)
            {
                Play(server, client, network, 12);
                thrown = MathF.Max(thrown, Vector3.Distance(server.GetPlayer(1)!.Value.Position with { Y = 0 }, start with { Y = 0 }));
            }
        }

        Assert.True(thrown >= 1.5f, $"a shield bash throws the player back, but they moved only {thrown:0.0} m");
    }
}
