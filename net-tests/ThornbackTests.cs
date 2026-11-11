using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>The thornbacks of the Emerald Jungle on an empty field: the charge and the Briarking's thrash.</summary>
public class ThornbackTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(string id, float distance)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, distance), 1f, 1, Types: new[] { id });
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { camp });
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

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) StartIn(string id, float distance, Aabb[] boxes)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, distance), 1f, 1, Types: new[] { id });
        var world = new World(boxes, null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 5 });
        var client = new ClientPrediction(network.Connect(1), world);
        return (server, client, network);
    }

    /// <summary>
    /// From a distance a thornback lowers its head (a wind-up long enough to step aside from), runs the whole
    /// way at the player, gores them on the way and runs on past, then stands winded: the opening to use.
    /// </summary>
    [Theory]
    [InlineData("thornback")]
    [InlineData("briarking")]
    public void Thornback_ChargesTheWholeWay_AndStandsWinded(string id)
    {
        var (server, client, network) = Start(id, 9f);
        var boar = server.Enemies[0];
        var charge = boar.Def.Abilities!.Single(a => a.Effect == "charge");
        Assert.True(charge.WindupTicks >= 30, "the charge needs half a second of warning");
        Assert.True(charge.RecoverTicks >= 50, "the boar stands winded after a charge");
        Assert.Equal("line", charge.Telegraph);

        var start = Vector3.Zero;
        var charging = false;
        for (var i = 0; i < 60 * 20 && !charging; i++)
        {
            Play(server, client, network, 1);
            charging = boar.CurrentAbility?.Effect == "charge" && boar.Action == EnemyAction.Leap;
            start = boar.Position;
        }

        Assert.True(charging, "the thornback never charged a player standing nine metres away");
        while (boar.Action == EnemyAction.Leap)
        {
            Play(server, client, network, 1);
        }

        var run = Vector3.Distance(start with { Y = 0 }, boar.Position with { Y = 0 });
        Assert.True(run > 7f, $"a charge runs the whole way and on past, but it ran only {run:0.0} m");
        Assert.True(server.Stats.PlayerHits > 0, "the charge must gore a player who stands still");
        Assert.Equal(EnemyAction.Strike, boar.Action);
    }

    [Fact]
    public void Thornback_RammingARock_StandsStunned()
    {
        // A boulder between the boar and the player: the charge breaks on it.
        var rock = new Aabb(new Vector3(-3, 0, 3.5f), new Vector3(3, 3, 4.5f));
        var (server, client, network) = StartIn("thornback", 10f, new[] { rock });
        var boar = server.Enemies[0];
        var rammed = false;
        for (var i = 0; i < 60 * 25 && !rammed; i++)
        {
            Play(server, client, network, 1);
            rammed = boar.Rammed;
        }

        Assert.True(rammed, "a charge into a rock never broke on it");
        Assert.Equal(EnemyAction.Stagger, boar.Action);
        Assert.True(boar.ActionTicks >= Enemy.RamStunTicks - 2);
    }

    [Fact]
    public void Thornback_WithoutATurn_BacksOffForARunUp()
    {
        var thornback = EnemyDef.All.Single(d => d.Id == "thornback");
        Assert.Equal("charger", thornback.Behavior);
        Assert.False(Enemy.IsClose(thornback.Abilities!.Single(a => a.Effect == "charge")), "a charge must not wait for a close-blow turn");
        Assert.True(Enemy.ChargeRing > thornback.Abilities!.Single(a => a.Effect == "charge").MinRange, "the run-up ring lies inside the charge's reach");
    }

    [Fact]
    public void Briarking_ShakesOffARingOfThorns()
    {
        var king = EnemyDef.All.Single(d => d.Id == "briarking");
        Assert.True(king.Rare);
        var burst = king.Abilities!.Single(a => a.Effect == "quillBurst");
        Assert.Equal("circle", burst.Telegraph);
        Assert.True(burst.WindupTicks >= 40, "the thorns fly all round, so they need a long warning");

        var (server, client, network) = Start("briarking", 5f);
        var most = 0;
        for (var i = 0; i < 60 * 30 && most < GameServer.QuillCount; i++)
        {
            Play(server, client, network, 1);
            most = Math.Max(most, server.ProjectileCount);
        }

        Assert.Equal(GameServer.QuillCount, most);
    }
}
