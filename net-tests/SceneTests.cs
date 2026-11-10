using System.Numerics;
using System.Text.Json;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>The scene runner: steps run in order, waiting steps hold, actors move and follow.</summary>
public class SceneTests
{
    private const string Walk = """
    {
      "id": "test_walk",
      "steps": [
        { "lockInput": true },
        { "spawn": { "actor": "nell", "at": [10, 0], "yaw": 90 } },
        { "say": { "actor": "nell", "text": "Hello." } },
        { "move": { "actor": "nell", "to": [[14, 0]], "speed": 4 }, "wait": true },
        { "unlockInput": true },
        { "follow": "nell" },
        { "waitFor": { "flag": "test.ready" } },
        { "effects": [ { "setFlag": "test.done" } ] }
      ]
    }
    """;

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start()
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, Array.Empty<EnemySpawn>());
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 1 });
        var client = new ClientPrediction(network.Connect(1), world);
        Run(server, client, network, 10);
        return (server, client, network);
    }

    private static void Run(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Command command = Command.None, ushort arg = 0, sbyte moveZ = 0)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, moveZ, Buttons.None, 0, i == 0 ? command : Command.None, null, 0, i == 0 ? arg : (ushort)0);
        }
    }

    [Fact]
    public void Scene_RunsItsStepsInOrder()
    {
        var (server, client, network) = Start();
        server.StartScene(SceneData.Parse(JsonDocument.Parse(Walk).RootElement, 999));
        Run(server, client, network, 5);
        var nell = server.Actor("nell");
        Assert.True(nell.Active);
        Assert.Equal(("test_walk", 2), server.SceneState);

        // The line waits until the player has read it.
        Run(server, client, network, 12, Command.SceneNext, 2);
        Assert.Equal(3, server.SceneState.Step);

        // Locked: walking does nothing.
        var before = server.GetPlayer(1)!.Value.Position;
        Run(server, client, network, 30, moveZ: 127);
        Assert.Equal(before.Z, server.GetPlayer(1)!.Value.Position.Z, 2);

        // The walk of 4 m at 4 m/s takes a second; halfway the scene still waits for it.
        Run(server, client, network, 20);
        Assert.Equal(3, server.SceneState.Step);
        Assert.True(nell.Position.X > 10.5f);
        Run(server, client, network, 70);
        Assert.Equal(6, server.SceneState.Step);
        Assert.Equal(1, nell.Follows);

        // Now the player walks, and Nell comes along.
        Run(server, client, network, 120, moveZ: 127);
        Assert.True(Vector3.Distance(nell.Position, server.GetPlayer(1)!.Value.Position) < 4f);

        server.StoryState.SetFlag("test.ready");
        Run(server, client, network, 5);
        Assert.True(server.StoryState.Has("test.done"));
        Assert.Null(server.SceneState.Scene);
    }

    private const string Guarded = """
    {
      "id": "test_guarded",
      "steps": [
        { "waitFor": { "flag": "test.never" }, "timeout": 1 },
        { "effects": [ { "setFlag": "test.skipped" } ], "if": { "flag": "test.never" } },
        { "effects": [ { "setFlag": "test.ran" } ], "if": { "notFlag": "test.never" } },
        { "spawn": { "actor": "nell", "at": [3, 0] } },
        { "follow": "nell" },
        { "waitFor": { "flag": "test.go" } },
        { "despawn": "nell" }
      ]
    }
    """;

    [Fact]
    public void Wait_GivesUpAfterItsTimeout_AndGuardedStepsRunOnlyWhenTheirConditionHolds()
    {
        var (server, client, network) = Start();
        server.StartScene(SceneData.Parse(JsonDocument.Parse(Guarded).RootElement, 999));
        Run(server, client, network, Tuning.TickRate / 2);
        Assert.Equal(0, server.SceneState.Step);

        Run(server, client, network, Tuning.TickRate);
        Assert.False(server.StoryState.Has("test.skipped"));
        Assert.True(server.StoryState.Has("test.ran"));
    }

    [Fact]
    public void Follow_SaysWhoWalksWithThePlayer_UntilTheyAreGone()
    {
        var (server, client, network) = Start();
        server.StartScene(SceneData.Parse(JsonDocument.Parse(Guarded).RootElement, 999));
        Run(server, client, network, Tuning.TickRate * 2);
        Assert.True(server.StoryState.Has(GameServer.WithFlag("nell")));

        server.StoryState.SetFlag("test.go");
        Run(server, client, network, 5);
        Assert.False(server.StoryState.Has(GameServer.WithFlag("nell")));
    }

    [Fact]
    public void ActorsAwayFromHome_ReachTheClient()
    {
        var (server, client, network) = Start();
        server.StartScene(SceneData.Parse(JsonDocument.Parse(Walk).RootElement, 999));
        Run(server, client, network, 10);
        var index = server.Actor("nell").Index;
        Assert.Contains(client.Latest!.Actors, a => a.Index == index && a.Active);
    }
}
