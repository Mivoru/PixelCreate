using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Saving and loading: the same player and world come back; a broken save falls back to the backup.</summary>
public class SaveGameTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(SaveData? save = null)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, Array.Empty<EnemySpawn>());
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 1, Save = save });
        var client = new ClientPrediction(network.Connect(1), world);
        Run(server, client, network, 20);
        return (server, client, network);
    }

    private static void Run(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Command command = Command.None)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None, 0, i == 0 ? command : Command.None);
        }
    }

    [Fact]
    public void SaveAndLoad_BringBackThePlayerAndTheWorld()
    {
        var (server, client, network) = Start();
        server.SetRank(1, GuildRank.Adventurer);
        server.GiveGold(1, 77);
        server.GiveItem(1, "slime_gel", 5);
        server.GiveCrystals(1, CrystalRarity.Rare, 3);
        server.SetLevel(1, 4);
        var story = server.StoryFor(1);
        story.World.SetFlag("guild.registered");
        story.World.SetFate("hollis", NpcFate.Dead);
        story.World.AddTrust("mira", 25);
        Story.StartQuest(story, "slime_cull");
        Story.OnKill(story.World, "slime");
        server.Calendar.Now = new GameTime(12, 900);
        Run(server, client, network, 2);

        var json = SaveFile.Serialize(server.Export());
        var save = SaveFile.Deserialize(json)!;
        var (loaded, _, _) = Start(save);

        Assert.Equal(GuildRank.Adventurer, loaded.GetRank(1));
        Assert.Equal(77, loaded.GetGold(1));
        Assert.Equal(5, loaded.GetItem(1, "slime_gel"));
        Assert.Equal(3, loaded.GetCrystals(1, CrystalRarity.Rare));
        Assert.Equal(4, loaded.GetLevel(1));
        Assert.Equal(12, loaded.Calendar.Now.Day);
        Assert.True(loaded.StoryState.Has("guild.registered"));
        Assert.Equal(NpcFate.Dead, loaded.StoryState.Fate("hollis"));
        Assert.Equal(25, loaded.StoryState.TrustOf("mira"));
        Assert.Equal(QuestStatus.Active, loaded.StoryState.Quest("slime_cull").Status);
        Assert.Equal(1, loaded.StoryState.Quest("slime_cull").Progress);
        Assert.Equal("wooden_sword", ItemDef.All[loaded.GetGear(1, 0)!.Item].Id);

        // Saving the loaded game gives the same file again.
        Assert.Equal(json, SaveFile.Serialize(loaded.Export()));
    }

    [Fact]
    public void SaveFile_KeepsABackupAndFallsBackToIt()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pixel-save-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("PIXEL_SAVE", folder);
        try
        {
            var first = new SaveData { World = new SavedWorld { Day = 3 } };
            var second = new SaveData { World = new SavedWorld { Day = 4 } };
            SaveFile.Write("slot", first);
            SaveFile.Write("slot", second);
            Assert.Equal(4, SaveFile.Read("slot")!.World.Day);
            Assert.True(File.Exists(SaveFile.BackupFor("slot")));

            File.WriteAllText(SaveFile.PathFor("slot"), "{ broken");
            Assert.Equal(3, SaveFile.Read("slot")!.World.Day);
            Assert.Null(SaveFile.Read("missing"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PIXEL_SAVE", null);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Fact]
    public void Autosave_RunsEachMorningAndOnQuit()
    {
        var (server, client, network) = Start();
        var saves = new List<SaveData>();
        server.Autosave = saves.Add;
        server.Calendar.Now = new GameTime(2, GameTime.DayStartMinute - 1);
        Run(server, client, network, GameTime.TicksPerMinute + 2);
        Assert.Single(saves);
        Assert.Equal(2, saves[0].World.Day);

        Run(server, client, network, 12, Command.Quit);
        Assert.Equal(2, saves.Count);
    }

    [Fact]
    public void Death_CostsATenthOfTheCrystalsAndIsWrittenDown()
    {
        var (server, client, network) = Start();
        server.SetRank(1, GuildRank.Adventurer);
        server.GiveCrystals(1, CrystalRarity.Common, 25);
        server.GiveCrystals(1, CrystalRarity.Rare, 5);
        server.HurtForTest(1, 10000);
        Run(server, client, network, 3);
        Assert.Equal(23, server.GetCrystals(1, CrystalRarity.Common));
        Assert.Equal(5, server.GetCrystals(1, CrystalRarity.Rare));
        Assert.Single(server.GetDeaths(1));
        Assert.Equal("sword", server.GetDeaths(1)[0].Weapon);
    }

    [Fact]
    public void Death_BeforeRegistering_CostsNothing()
    {
        var (server, client, network) = Start();
        server.GiveCrystals(1, CrystalRarity.Common, 25);
        server.HurtForTest(1, 10000);
        Run(server, client, network, 3);
        Assert.Equal(25, server.GetCrystals(1, CrystalRarity.Common));
        Assert.Empty(server.GetDeaths(1));
    }
}
