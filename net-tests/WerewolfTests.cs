using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Werewolves on an empty field: the pounce, Redclaw's fang slash and Moonleaper's great leap.</summary>
public class WerewolfTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(string id, float distance)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, distance), 0.5f, 1, Types: new[] { id });
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 5 });
        var client = new ClientPrediction(network.Connect(1), world);
        return (server, client, network);
    }

    private static void Play(GameServer server, ClientPrediction client, SimNetwork network, int ticks, sbyte moveX = 0)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(moveX, 0, Buttons.None);
        }
    }

    [Theory]
    [InlineData("werewolf")]
    [InlineData("werewolf_alpha")]
    [InlineData("werewolf_redclaw")]
    [InlineData("werewolf_moonleaper")]
    public void Werewolves_PounceAndClaw(string id)
    {
        var (server, client, network) = Start(id, 7f);
        var wolf = server.Enemies[0];
        var pounced = false;
        for (var i = 0; i < 60 * 20 && !pounced; i++)
        {
            Play(server, client, network, 1);
            pounced = wolf.Action == EnemyAction.Leap && wolf.CurrentAbility?.Effect == "pounce" && wolf.Position.Y > 0.4f;
        }

        Assert.True(pounced, "A werewolf must leap at the player.");
        Assert.Contains(wolf.Def.Abilities!, a => a.Effect == "melee");
    }

    [Fact]
    public void Redclaw_TearsThreeSlantedSlashes()
    {
        var (server, client, network) = Start("werewolf_redclaw", 6f);
        var most = 0;
        for (var i = 0; i < 60 * 25 && most < 3; i++)
        {
            Play(server, client, network, 1);
            most = Math.Max(most, server.ProjectileCount);
        }

        Assert.Equal(3, most);
    }

    [Fact]
    public void Moonleaper_LeapsFromFarAndHits()
    {
        var (server, client, network) = Start("werewolf_moonleaper", 16f);
        var wolf = server.Enemies[0];
        var highest = 0f;
        var hit = false;
        for (var i = 0; i < 60 * 25 && !hit; i++)
        {
            Play(server, client, network, 1);
            if (wolf.Action == EnemyAction.Leap && wolf.CurrentAbility?.Effect == "superJump")
            {
                highest = MathF.Max(highest, wolf.Position.Y);
            }

            hit = wolf.CurrentAbility?.Effect == "superJump" && wolf.Action == EnemyAction.Strike;
        }

        Assert.True(highest > 2f, $"The great leap must go high; it rose {highest:F1} m.");
        Assert.True(hit, "A great leap onto a player who stands still must land as a strike.");
        Assert.True(server.Stats.PlayerHits > 0);
    }

    [Fact]
    public void Moonleaper_RollsWhenItMisses()
    {
        var (server, client, network) = Start("werewolf_moonleaper", 16f);
        var wolf = server.Enemies[0];
        var rolled = false;
        for (var i = 0; i < 60 * 30 && !rolled; i++)
        {
            // Once the leap is in the air, run sideways out of the landing spot.
            var flying = wolf.Action == EnemyAction.Leap && wolf.CurrentAbility?.Effect == "superJump";
            Play(server, client, network, 1, (sbyte)(flying ? 127 : 0));
            rolled = wolf.Action == EnemyAction.Roll;
        }

        Assert.True(rolled, "A great leap that misses must end in a roll.");
    }
}
