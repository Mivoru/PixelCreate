using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Rhea's training after signing up: each lesson counts what it teaches, nothing can die, and it can be skipped.</summary>
public class TutorialTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start()
    {
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { new EnemySpawn(new Vector3(300, 0, 300), 1f, 1) })
        {
            Npcs = new[] { new Npc("mira", "Mira", "receptionist", new Vector3(0, 0, 1), 0) },
            TrialGround = new Vector3(0, 0, 60),
        };
        var network = new SimNetwork(1, 0, 0, 0);
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 3, SpawnOverride = Vector3.Zero });
        var client = new ClientPrediction(network.Connect(1), world);
        Run(server, client, network, 20);
        return (server, client, network);
    }

    private static void Run(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Command command = Command.None,
        Buttons buttons = Buttons.None, ushort arg = 0, sbyte moveZ = 0)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, moveZ, buttons, 0, i == 0 ? command : Command.None, null, 0, i == 0 ? arg : (ushort)0);
        }
    }

    private static ushort Option(string words) => (ushort)Story.Options.First(o => o.Npc == "mira" && o.Text.Contains(words)).Index;

    private static void SignAndTrain(GameServer server, ClientPrediction client, SimNetwork network)
    {
        Run(server, client, network, 12, Command.Choose, Buttons.None, Option("Sign me up."));
        Run(server, client, network, 12, Command.Choose, Buttons.None, Option("Take me to her."));
    }

    [Fact]
    public void SigningUp_TakesThePlayerToRhea_WhoCannotBeHurt()
    {
        var (server, client, network) = Start();
        SignAndTrain(server, client, network);
        Assert.Equal(GuildRank.Adventurer, server.GetRank(1));
        Assert.True(server.StoryState.Has(Tutorial.Started));
        var rhea = server.Enemies.Single(e => e.TutorLesson > 0);
        Assert.False(rhea.TakeHit(500, Vector3.Zero, 1));
        Assert.Equal(rhea.MaxHealth, rhea.Health);
    }

    [Fact]
    public void LightBlows_FinishTheFirstLesson()
    {
        var (server, client, network) = Start();
        SignAndTrain(server, client, network);
        var rhea = server.Enemies.Single(e => e.TutorLesson > 0);
        server.EnemiesFrozen = true;
        rhea.Position = server.GetPlayer(1)!.Value.Position + new Vector3(0, 0, -1.5f);
        for (var i = 0; i < 4; i++)
        {
            Run(server, client, network, 40, Command.None, Buttons.Light);
            Run(server, client, network, 10);
        }

        Assert.True(server.StoryState.Has("tutorial.light"));
        Assert.Equal(2, rhea.TutorLesson);
    }

    [Fact]
    public void Slides_FinishTheSlideLesson()
    {
        var (server, client, network) = Start();
        SignAndTrain(server, client, network);
        var rhea = server.Enemies.Single(e => e.TutorLesson > 0);
        rhea.TutorLesson = 5;
        for (var i = 0; i < 3; i++)
        {
            Run(server, client, network, 30, moveZ: 127);
            Run(server, client, network, 50, Command.None, Buttons.Slide, moveZ: 127);
            Run(server, client, network, 20);
        }

        var p = server.GetPlayer(1)!.Value;
        Assert.True(server.StoryState.Has("tutorial.slide"), $"lesson {rhea.TutorLesson}, at {p.Position}, stamina {p.Stamina}, slide {p.SlideTicks}, enemies {server.Enemies.Count}");
        Assert.True(server.StoryState.Has(Tutorial.Done));
    }

    [Fact]
    public void Training_CanBeSkipped_AndNobodyDiesInIt()
    {
        var (server, client, network) = Start();
        SignAndTrain(server, client, network);
        server.HurtForTest(1, 10_000);
        Run(server, client, network, 5);
        Assert.Equal(GuildRank.Adventurer, server.GetRank(1));

        Run(server, client, network, 12, Command.SkipTutorial);
        Assert.True(server.StoryState.Has(Tutorial.Finished));
        Assert.DoesNotContain(server.Enemies, e => e.TutorLesson > 0);
    }
}
