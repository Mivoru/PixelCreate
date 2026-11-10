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
