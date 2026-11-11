using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>
/// The barrow wisps on an empty field: out only in the dark, a slow bolt that turns after the player,
/// put out while the player stands by a standing stone, and the Lantern of the Barrow's three lights.
/// </summary>
public class WispTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    private static readonly GameTime Midnight = new(1, 0);
    private static readonly GameTime Noon = new(1, 12 * 60);

    /// <summary>Where the player of a fresh server on an empty field stands after the first tick.</summary>
    private static Vector3 PlayerSpot()
    {
        var (server, client, network) = Start("barrow_wisp", 30f, Midnight, null);
        Play(server, client, network, Tuning.TickRate);
        return server.GetPlayer(1)!.Value.Position;
    }

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(string id, float distance, GameTime time, Vector3[]? wards)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, distance), 0.3f, 1, Types: new[] { id }, Night: true, Wards: wards);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 7 });
        server.Calendar.Now = time;
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
    public void Wisp_IsOutOnlyInTheDark()
    {
        var (server, client, network) = Start("barrow_wisp", 9f, Noon, null);
        Play(server, client, network, Tuning.TickRate * 6);
        var wisp = server.Enemies[0];
        Assert.False(wisp.Alive, "a barrow wisp was out at noon");
        Assert.Equal(0, server.Stats.PlayerHits);

        server.Calendar.Now = Midnight;
        Play(server, client, network, Tuning.TickRate * 6);
        Assert.True(wisp.Alive, "the barrow wisp did not rise when night fell");

        server.Calendar.Now = Noon;
        Play(server, client, network, 3);
        Assert.False(wisp.Alive, "the barrow wisp stayed out after dawn");
        Assert.Equal(0, server.Stats.EnemiesKilled);
    }

    [Fact]
    public void Wisp_KeepsItsDistance_AndHitsWithItsBolt()
    {
        var (server, client, network) = Start("barrow_wisp", 9f, Midnight, null);
        var wisp = server.Enemies[0];
        var closest = float.MaxValue;
        for (var i = 0; i < Tuning.TickRate * 12; i++)
        {
            Play(server, client, network, 1);
            if (i > Tuning.TickRate * 2)
            {
                closest = MathF.Min(closest, Vector3.Distance(wisp.Position with { Y = 0 }, client.State.Position with { Y = 0 }));
            }
        }

        Assert.True(wisp.TargetId >= 0, "the wisp never noticed the player nine metres off");
        Assert.True(server.Stats.PlayerHits > 0, "the wisp's bolts never reached a player standing still");
        Assert.True(closest > 5f, $"a wisp keeps its distance, but it came within {closest:0.0} m");
    }

    [Fact]
    public void Wisp_GoesOut_WhileThePlayerStandsByAStandingStone()
    {
        var stone = PlayerSpot();
        var (server, client, network) = Start("barrow_wisp", 9f, Midnight, new[] { stone with { Y = 0 } });
        var wisp = server.Enemies[0];
        var start = Vector3.Distance(wisp.Position, stone);
        Play(server, client, network, Tuning.TickRate * 10);

        Assert.True(wisp.TargetId >= 0, "the wisp never noticed the player");
        Assert.Equal(EnemyAction.Dim, wisp.Action);
        Assert.Equal(0, server.Stats.PlayerHits);
        var now = Vector3.Distance(wisp.Position with { Y = 0 }, stone with { Y = 0 });
        Assert.True(now < 3f && now < start, $"a put-out wisp drifts in to the stone, but it is {now:0.0} m off");
    }

    [Fact]
    public void PutOutWisp_TakesMoreFromABlow()
    {
        var (server, _, _) = Start("barrow_wisp", 9f, Midnight, null);
        var wisp = server.Enemies[0];
        var full = wisp.Health;
        wisp.TakeHit(20, wisp.Position + Vector3.UnitX);
        var lit = full - wisp.Health;
        wisp.Health = full;
        wisp.Action = EnemyAction.Dim;
        wisp.TakeHit(20, wisp.Position + Vector3.UnitX);
        var dim = full - wisp.Health;
        Assert.Equal(20, lit);
        Assert.Equal((int)MathF.Round(20 * Enemy.DimTaken), dim);
    }

    [Fact]
    public void Lantern_SplitsIntoThreeLights_OnlyOneOfThemTrue()
    {
        var (server, client, network) = Start("lantern_of_the_barrow", 10f, Midnight, null);
        var lantern = server.Enemies[0];
        var split = false;
        for (var i = 0; i < Tuning.TickRate * 30 && !split; i++)
        {
            Play(server, client, network, 1);
            split = server.Enemies.Count == 3;
        }

        Assert.True(split, "the Lantern of the Barrow never split into three");
        var decoys = server.Enemies.Where(e => e.Def.Decoy).ToList();
        Assert.Equal(2, decoys.Count);
        Assert.All(decoys, d => Assert.Equal(lantern.Health, d.Health));
        Assert.All(decoys, d => Assert.Equal(lantern.Def.Name, d.Def.Name));

        // A false light bursts at the lightest touch and leaves nothing.
        var experience = server.GetXp(1);
        Assert.True(decoys[0].TakeHit(1, decoys[0].Position + Vector3.UnitX), "a false light outlasted a touch");
        Play(server, client, network, 2);
        Assert.Equal(2, server.Enemies.Count);
        Assert.Equal(experience, server.GetXp(1));

        // The true lantern goes on, and its other false light goes with it when it falls.
        lantern.Health = 1;
        lantern.TakeHit(5, lantern.Position + Vector3.UnitX);
        Play(server, client, network, 2);
        Assert.DoesNotContain(server.Enemies, e => e.Def.Decoy && e.Alive);
    }

    [Fact]
    public void EveryWispCamp_IsOutAtNight_WithItsStandingStones()
    {
        var camps = Valley.World.EnemySpawns.Where(s => s.Types?.Any(t => EnemyDef.All.First(d => d.Id == t).Kind == "wisp") == true).ToList();
        Assert.NotEmpty(camps);
        Assert.All(camps, c => Assert.True(c.Night, "a wisp camp that is out by day"));
        Assert.All(camps, c => Assert.True(c.Wards is { Length: >= 3 }, "a wisp camp without its standing stones"));
        foreach (var camp in camps)
        {
            foreach (var ward in camp.Wards!)
            {
                Assert.Contains(Valley.Props, p => p.Model == "standing_stone" && Vector2.Distance(new(p.Position.X, p.Position.Z), new(ward.X, ward.Z)) < 0.5f);
            }
        }
    }
}
