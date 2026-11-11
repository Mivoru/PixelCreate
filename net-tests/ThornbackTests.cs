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

    /// <summary>
    /// From a distance a thornback lowers its head (a wind-up long enough to step aside from), runs the
    /// player down and then stands winded: the opening the player is meant to use.
    /// </summary>
    [Theory]
    [InlineData("thornback")]
    [InlineData("briarking")]
    public void Thornback_ChargesAndStandsWinded(string id)
    {
        var (server, client, network) = Start(id, 8f);
        var boar = server.Enemies[0];
        var charge = boar.Def.Abilities!.Single(a => a.Effect == "dashStrike");
        Assert.True(charge.WindupTicks >= 30, "the charge needs half a second of warning");
        Assert.True(charge.RecoverTicks >= 50, "the boar stands winded after a charge");
        Assert.Equal("line", charge.Telegraph);

        var charged = false;
        for (var i = 0; i < 60 * 20 && !charged; i++)
        {
            Play(server, client, network, 1);
            charged = boar.CurrentAbility?.Effect == "dashStrike" && boar.Action is EnemyAction.Strike or EnemyAction.Leap;
        }

        Assert.True(charged, "the thornback never charged a player standing eight metres away");
        Play(server, client, network, 60 * 4);
        Assert.True(server.Stats.PlayerHits > 0, "the charge must hurt a player who stands still");
    }

    [Fact]
    public void Briarking_ThrashesWhenCrowded()
    {
        var king = EnemyDef.All.Single(d => d.Id == "briarking");
        Assert.True(king.Rare);
        var thrash = king.Abilities!.Single(a => a.Effect == "spin");
        Assert.Equal("circle", thrash.Telegraph);
        Assert.True(thrash.WindupTicks >= 40, "the thrash hits all round, so it needs a long warning");
    }
}
