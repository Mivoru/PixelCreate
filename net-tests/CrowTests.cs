using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>
/// The carrion crows on an empty field: a flock circling out of reach, dives that open a window to strike,
/// a struck crow scattering its flock, and the Gallows Rook's five crows diving one after another.
/// </summary>
public class CrowTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(string id, int count, float distance)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, distance), 2f, count, Types: new[] { id });
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 11 });
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
    public void Flock_Circles_ThenDives_AndOnlyThenCanBeStruck()
    {
        var (server, client, network) = Start("carrion_crow", 4, 8f);
        var dived = false;
        var aloftWhileCircling = true;
        for (var i = 0; i < Tuning.TickRate * 20 && !dived; i++)
        {
            Play(server, client, network, 1);
            foreach (var crow in server.Enemies)
            {
                if (crow.Action == EnemyAction.Walk && !crow.Untouchable)
                {
                    aloftWhileCircling = false;
                }

                dived |= crow.Action == EnemyAction.Strike;
            }
        }

        Assert.True(aloftWhileCircling, "a circling crow could be struck");
        Assert.True(dived, "the flock never dived at the player");
        Assert.True(server.Stats.PlayerHits > 0, "a dive came down but did not hurt the player standing under it");
        var down = server.Enemies.First(c => c.Action == EnemyAction.Strike);
        Assert.False(down.Untouchable, "a crow pecking on the ground after its dive must be open to blows");
    }

    [Fact]
    public void Flock_TakesTurns_NeverMoreThanTwoDiveAtOnce()
    {
        var (server, client, network) = Start("carrion_crow", 5, 8f);
        var most = 0;
        for (var i = 0; i < Tuning.TickRate * 25; i++)
        {
            Play(server, client, network, 1);
            most = Math.Max(most, server.Enemies.Count(c => c.Action is EnemyAction.Leap || (c.Action == EnemyAction.Cast)));
        }

        Assert.InRange(most, 1, GameServer.MaxCloseAttackers);
    }

    [Fact]
    public void AloftCrow_IsUntouched_ByABlow()
    {
        var (server, _, _) = Start("carrion_crow", 1, 8f);
        var crow = server.Enemies[0];
        crow.Action = EnemyAction.Walk;
        var full = crow.Health;
        Assert.False(crow.TakeHit(500, crow.Position + Vector3.UnitX, 1));
        Assert.Equal(full, crow.Health);
    }

    [Fact]
    public void StruckCrow_ScattersItsFlock()
    {
        var (server, client, network) = Start("carrion_crow", 4, 8f);
        Play(server, client, network, Tuning.TickRate * 2);
        var struck = server.Enemies[0];
        struck.Action = EnemyAction.Strike;
        struck.ActionTicks = 30;
        struck.TakeHit(1, struck.Position + Vector3.UnitX, 1);
        Assert.All(server.Enemies, c => Assert.True(c.ScatterTicks > 0, "a crow of the flock did not scatter"));

        var before = server.Enemies.Average(c => Vector3.Distance(c.Position with { Y = 0 }, client.State.Position with { Y = 0 }));
        Play(server, client, network, Enemy.ScatterDuration - 10);
        var after = server.Enemies.Average(c => Vector3.Distance(c.Position with { Y = 0 }, client.State.Position with { Y = 0 }));
        Assert.True(after > before + 3f, $"a scattered flock flies off, but it went from {before:0.0} m to {after:0.0} m");
        Assert.All(server.Enemies, c => Assert.True(c.Untouchable, "a scattering crow flies up out of reach"));

        // Then it comes back.
        Play(server, client, network, Tuning.TickRate * 6);
        var back = server.Enemies.Average(c => Vector3.Distance(c.Position with { Y = 0 }, client.State.Position with { Y = 0 }));
        Assert.True(back < after, $"the flock did not come back after scattering ({after:0.0} m, then {back:0.0} m)");
    }

    [Fact]
    public void GallowsRook_CallsFiveCrows_ThatDiveOneAfterAnother_AndFallToOneBlow()
    {
        var (server, client, network) = Start("gallows_rook", 1, 8f);
        var called = false;
        for (var i = 0; i < Tuning.TickRate * 30 && !called; i++)
        {
            Play(server, client, network, 1);
            called = server.Enemies.Count == 1 + GameServer.RookFlock;
        }

        Assert.True(called, "the Gallows Rook never called its crows");
        var crows = server.Enemies.Where(e => e.Def.Id == "gallows_crow").ToList();

        // They come in one after another, never all at once.
        var firstDive = new Dictionary<int, int>();
        for (var tick = 0; tick < Tuning.TickRate * 6 && firstDive.Count < crows.Count; tick++)
        {
            Play(server, client, network, 1);
            foreach (var crow in crows.Where(c => c.Action == EnemyAction.Leap && !firstDive.ContainsKey(c.Id)))
            {
                firstDive[crow.Id] = tick;
            }
        }

        Assert.Equal(crows.Count, firstDive.Count);
        var order = firstDive.Values.OrderBy(t => t).ToArray();
        Assert.True(order.Zip(order.Skip(1)).All(p => p.Second - p.First >= GameServer.RookCallGapTicks - 2), $"the crows dived at ticks {string.Join(", ", order)}");

        // One blow each, and nothing for it.
        var landed = crows.First(c => c.Alive && c.Action == EnemyAction.Strike);
        var experience = server.GetXp(1);
        Assert.True(landed.TakeHit(1, landed.Position + Vector3.UnitX, 1), "a called crow outlasted a blow");
        Play(server, client, network, 2);
        Assert.Equal(experience, server.GetXp(1));
    }

    [Fact]
    public void Crowfield_HasItsFlock_AndTheRook()
    {
        var valley = Zone.Load("meadow-valley");
        Assert.Contains(valley.World.EnemySpawns, s => s.Types?.Contains("carrion_crow") == true && s.Count is >= 3 and <= 5);
        Assert.Contains(valley.World.EnemySpawns, s => s.Types?.Contains("gallows_rook") == true && s.Rare);
        Assert.Contains(valley.Props, p => p.Model == "battle_debris");
    }
}
