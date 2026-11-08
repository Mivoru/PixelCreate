using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Goblins out hunting on an empty field, with no player near unless a test brings one.</summary>
public class HuntTests
{
    private static World _world = null!;

    private static GameServer Start(string[] hunters, string prey, float apart, out SimNetwork network, Vector3? spawn = null)
    {
        network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(Vector3.Zero, 2f, hunters.Length, Types: hunters, Hunts: true);
        var herd = new EnemySpawn(new Vector3(apart, 0, 0), 1f, 1, Types: new[] { prey });
        var world = new World(Array.Empty<Aabb>(), null, spawn ?? new Vector3(0, 0, -400), new[] { camp, herd });
        _world = world;
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 7 });
        server.Hunts[0].Now();
        return server;
    }

    private static Enemy Prey(GameServer server, string id) => server.Enemies.First(e => e.Def.Id == id);

    [Fact]
    public void Archers_HuntDownADeer()
    {
        var server = Start(new[] { "goblin_rapid_archer", "goblin_rapid_archer" }, "deer", 25f, out _);
        var deer = Prey(server, "deer");
        var chased = false;
        for (var i = 0; i < 60 * 60 && deer.Alive; i++)
        {
            server.Step();
            chased |= server.Hunts[0].Quarry == deer;
        }

        Assert.True(chased, "The archers must go after the deer.");
        Assert.False(deer.Alive, "The archers must bring the deer down.");
    }

    [Fact]
    public void FightersLeaveDeerAlone_AndTakeOnABoar()
    {
        var deerServer = Start(new[] { "goblin_skirmisher", "goblin_brute" }, "deer", 25f, out _);
        deerServer.Step();
        Assert.Null(deerServer.Hunts[0].Quarry);

        var server = Start(new[] { "goblin_skirmisher", "goblin_brute" }, "pig", 25f, out _);
        var boar = Prey(server, "pig");
        var hunters = server.Enemies.Where(e => e.Def.Kind == "goblin").ToList();
        var fought = false;
        for (var i = 0; i < 60 * 60 && boar.Alive; i++)
        {
            server.Step();
            fought |= boar.TargetId >= Enemy.CreatureBase;
        }

        Assert.True(fought, "A hunted boar must turn on the hunters.");
        Assert.False(boar.Alive, "Two goblins must kill a boar.");
        Assert.All(hunters.Where(h => h.Alive), h => Assert.True(h.Hunting, "After the kill the hunters eat where it fell."));
    }

    [Fact]
    public void Hunters_DropThePreyForAPlayer()
    {
        var server = Start(new[] { "goblin_rapid_archer", "goblin_skirmisher" }, "pig", 25f, out var network, new Vector3(-6, 0, 0));
        server.Step();
        Assert.NotNull(server.Hunts[0].Quarry);
        var client = new ClientPrediction(network.Connect(1), _world);
        for (var i = 0; i < 60 * 4; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None);
        }

        Assert.Contains(server.Enemies, e => e.Def.Kind == "goblin" && e.TargetId == 1);
        Assert.Null(server.Hunts[0].Quarry);
    }
}
