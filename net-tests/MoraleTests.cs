using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Morale: a goblin near death breaks off and runs for its camp once, then fights again cornered.</summary>
public class MoraleTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(string id)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, 6f), 0.5f, 1, Types: new[] { id });
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 5 });
        var client = new ClientPrediction(network.Connect(1), world);
        return (server, client, network);
    }

    private static void Play(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Action? each = null)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None);
            each?.Invoke();
        }
    }

    private static float Apart(GameServer server, Enemy enemy) =>
        Vector2.Distance(new Vector2(enemy.Position.X, enemy.Position.Z), new Vector2(server.GetPlayer(1)!.Value.Position.X, server.GetPlayer(1)!.Value.Position.Z));

    /// <summary>Plays until the enemy has noticed the player and stands close, then wounds it.</summary>
    private static Enemy Engage(GameServer server, ClientPrediction client, SimNetwork network)
    {
        var enemy = server.Enemies[0];
        for (var i = 0; i < Tuning.TickRate * 10 && !(enemy.TargetId == 1 && Apart(server, enemy) < 4f); i++)
        {
            Play(server, client, network, 1);
        }

        Assert.Equal(1, enemy.TargetId);
        enemy.Health = (short)(enemy.MaxHealth * 0.2f);
        return enemy;
    }

    [Fact]
    public void WoundedGoblin_RunsOnce_ThenFightsAgain()
    {
        var (server, client, network) = Start("goblin_skirmisher");
        var goblin = Engage(server, client, network);
        var start = Apart(server, goblin);
        var farthest = start;
        var heldTurn = false;
        Play(server, client, network, Enemy.RoutDuration - 10, () =>
        {
            farthest = MathF.Max(farthest, Apart(server, goblin));
            heldTurn |= goblin.HasTurn && goblin.RoutTicks > 0;
        });

        Assert.True(goblin.Routed, "a goblin at a fifth of its health must lose heart");
        Assert.True(farthest > start + 6f, $"the routed goblin must run off; it got from {start:F1} m to only {farthest:F1} m away");
        Assert.False(heldTurn, "a fleeing goblin must give up its turn at the player");

        // Cornered, it turns and comes back to fight; it does not run a second time.
        var struck = false;
        Play(server, client, network, Tuning.TickRate * 12, () => struck |= goblin.Action is EnemyAction.Cast or EnemyAction.Leap or EnemyAction.Strike);
        Assert.True(struck, "after its flight the goblin must fight again");
        Assert.Equal(0, goblin.RoutTicks);
    }

    [Fact]
    public void Pack_LosesHeart_WhenItsLeaderFalls()
    {
        // The camp draws each member from its types by chance: take the first seed that gives one leader and a wolf.
        var camp = new EnemySpawn(new Vector3(0, 0, 7f), 1f, 3, Types: new[] { "alpha_wolf", "wolf", "wolf" });
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { camp });
        SimNetwork network = null!;
        GameServer server = null!;
        for (var seed = 1; seed < 40; seed++)
        {
            network = new SimNetwork(1, 0, 0, 0);
            server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = seed });
            if (server.Enemies.Count(e => e.Def.Leader) == 1 && server.Enemies.Any(e => !e.Def.Leader))
            {
                break;
            }
        }

        var client = new ClientPrediction(network.Connect(1), world);
        var leader = server.Enemies.FirstOrDefault(e => e.Def.Leader);
        var wolf = server.Enemies.FirstOrDefault(e => !e.Def.Leader);
        Assert.True(leader is not null && wolf is not null, "the camp needs a leader and a wolf");
        for (var i = 0; i < Tuning.TickRate * 10 && wolf!.TargetId != 1; i++)
        {
            Play(server, client, network, 1);
        }

        // Hurt but led: it fights on.
        wolf!.Health = (short)(wolf.MaxHealth * 0.4f);
        Play(server, client, network, Tuning.TickRate * 2);
        Assert.False(wolf.Routed, "a wounded wolf stays while its leader lives");

        leader!.Health = 0;
        leader.Action = EnemyAction.Dead;
        Play(server, client, network, Tuning.TickRate * 2);
        Assert.True(wolf.Routed, $"with its leader dead, a wounded wolf must run (target {wolf.TargetId}, {wolf.Action}, led {wolf.PackLed}, health {wolf.Health}/{wolf.MaxHealth})");
    }

    [Theory]
    [InlineData("goblin_brute")]
    [InlineData("orc_warrior")]
    [InlineData("wolf")]
    public void Brutes_Orcs_AndWolves_StandTheirGround(string id)
    {
        var (server, client, network) = Start(id);
        var enemy = Engage(server, client, network);
        Play(server, client, network, Tuning.TickRate * 3);
        Assert.False(enemy.Routed, $"{id} must not flee");
    }
}
