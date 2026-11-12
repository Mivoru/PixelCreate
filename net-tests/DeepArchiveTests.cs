using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>The deep Sunken Archive (design/dungeon-archive.md): the Keeper, its gate, the secret wall and the locked chest.</summary>
public class DeepArchiveTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(Vector3 spawn, bool test = false)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var server = new GameServer(network.Connect(0), Valley.World, new ServerOptions { Seed = 1, SpawnOverride = spawn, TestMode = test });
        var client = new ClientPrediction(network.Connect(1), Valley.World);
        Run(server, client, network, 10);
        return (server, client, network);
    }

    private static void Run(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Command command = Command.None, ushort arg = 0, Buttons buttons = Buttons.None)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, buttons, 0, i == 0 ? command : Command.None, null, 0, i == 0 ? arg : (ushort)0);
        }
    }

    private static ushort Option(string npc, string words) => (ushort)Story.Options.First(o => o.Npc == npc && o.Text.Contains(words)).Index;

    [Fact]
    public void TheKeeper_SleepsOnItsPlinth_UntilAPlayerComesIntoItsHall()
    {
        var (server, client, network) = Start(new Vector3(560, 0, -72));
        Run(server, client, network, 60);
        var keeper = server.Enemies.Single(e => e.Def.Id == "archive_keeper");
        Assert.True(keeper.Def.Boss);
        Assert.Equal(10, keeper.Level);
        Assert.Equal(-1, keeper.TargetId);

        server.MoveForTest(1, new Vector3(560, 0, -88));
        Run(server, client, network, 90);
        Assert.Equal(1, keeper.TargetId);
    }

    [Fact]
    public void TheKeeper_CallsUpPageWisps_AndOnlyOneLotAtATime()
    {
        var (server, client, network) = Start(new Vector3(560, 0, -88));
        server.MoveForTest(1, new Vector3(560, 0, -91));
        var called = 0;
        for (var second = 0; second < 60 && called == 0; second++)
        {
            Run(server, client, network, 60);
            server.SetHealth(1, 100);
            called = server.Enemies.Count(e => e.Def.Id == "page_wisp");
        }

        Assert.Equal(2, called);
        Run(server, client, network, 60 * 5);
        Assert.True(server.Enemies.Count(e => e.Def.Id == "page_wisp") <= 2);
    }

    [Fact]
    public void BringingDownTheKeeper_OpensItsGate_OnTheWayOut()
    {
        var (server, client, network) = Start(new Vector3(560, 0, -96), test: true);
        var gate = new Vector3(560, 1, -107.2f);
        Assert.True(Valley.World.IsBlocked(gate), "the Keeper's gate stands open before the Keeper falls");
        server.EnemiesFrozen = true;
        var keeper = server.Enemies.Single(e => e.Def.Id == "archive_keeper");
        keeper.Health = 1;
        keeper.Position = server.GetPlayer(1)!.Value.Position + new Vector3(0, 0, -2.4f);
        Run(server, client, network, 40, buttons: Buttons.Light);
        Assert.False(keeper.Alive);
        Assert.True(server.StoryState.Has("archive.keeper"));
        Valley.World.HasFlag = server.StoryState.Has;
        try
        {
            Assert.False(Valley.World.IsBlocked(gate), "the gate still stands after the Keeper fell");
        }
        finally
        {
            Valley.World.HasFlag = _ => false;
        }
    }

    [Fact]
    public void TheWarmGlyph_OpensTheSecretWall()
    {
        var glyph = Valley.World.Npcs.Single(n => n.Id == "archive_glyph");
        var (server, client, network) = Start(glyph.Position + new Vector3(-1.2f, 0, 0));
        server.EnemiesFrozen = true;
        var wall = new Vector3(578.4f, 1, -52.4f);
        Assert.True(Valley.World.IsBlocked(wall));
        Run(server, client, network, 12, Command.Choose, Option("archive_glyph", "Press the glyph"));
        Assert.True(server.StoryState.Has("archive.secret"));
        Valley.World.HasFlag = server.StoryState.Has;
        try
        {
            Assert.False(Valley.World.IsBlocked(wall), "the secret wall still stands");
        }
        finally
        {
            Valley.World.HasFlag = _ => false;
        }
    }

    [Fact]
    public void TheRuneLockedChest_OpensOnlyWithTheDrownedKey_AndKeepsIt()
    {
        var chest = Valley.World.Npcs.Single(n => n.Id == "cache_archive_vault");
        var (server, client, network) = Start(chest.Position + new Vector3(-1.4f, 0, 0));
        server.EnemiesFrozen = true;
        var open = Option("cache_archive_vault", "Open it");
        var gold = server.GetGold(1);
        Run(server, client, network, 12, Command.Choose, open);
        Assert.Equal(gold, server.GetGold(1));

        var key = ItemDef.Index("drowned_key");
        server.GiveItem(1, key, 1);
        Run(server, client, network, 12, Command.Choose, open);
        Assert.True(server.GetGold(1) > gold, "the chest did not open with its key");
        Assert.Equal(0, server.GetBagCount(1, key));
        Assert.True(server.StoryState.Has(GameServer.VaultOpenedFlag));
    }
}
