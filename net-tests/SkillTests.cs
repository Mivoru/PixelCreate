using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Skills learned from books: reading, casting along the aim, cooldowns and what each effect hits.</summary>
public class SkillTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(params Vector3[] foes)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camps = foes.Select(f => new EnemySpawn(f, 0.2f, 1, Types: new[] { "goblin_brute" })).ToArray();
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, camps);
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 1 });
        var client = new ClientPrediction(network.Connect(1), world);
        Run(server, client, network, 10);
        server.EnemiesFrozen = true;
        // Foes stand where the test says, measured from the player, who spawns a little off the origin.
        var player = server.GetPlayer(1)!.Value.Position with { Y = 0 };
        for (var i = 0; i < foes.Length; i++)
        {
            server.Enemies[i].Position = player + foes[i];
        }

        return (server, client, network);
    }

    private static void Run(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Command command = Command.None, ushort arg = 0)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None, 0, i == 0 ? command : Command.None, 0, 0, i == 0 ? arg : (ushort)0);
        }
    }

    private static ushort Learn(GameServer server, ClientPrediction client, SimNetwork network, string id)
    {
        var skill = (ushort)SkillDef.Index(id);
        server.GiveItem(1, SkillDef.All[skill].Book, 1);
        Run(server, client, network, 12, Command.UseItem, SkillDef.All[skill].Book);
        return skill;
    }

    [Fact]
    public void OnlySlottedSkills_CanBeCast()
    {
        // Four skills learned: three fill the slots, the fourth waits until it is slotted, and then
        // starts on its cooldown.
        var (server, client, network) = Start();
        var learned = SkillDef.All.Take(Skills.Slots + 1).Select(s => Learn(server, client, network, s.Id)).ToArray();
        Assert.Equal(learned.Take(Skills.Slots).Select(s => (byte)s), client.Latest!.SkillSlots);

        var fourth = learned[^1];
        Run(server, client, network, 12, Command.CastSkill, fourth);
        Assert.Equal(0, server.Stats.SkillsCast);

        Run(server, client, network, 12, Command.SlotSkill, (ushort)(fourth | 1 << 8));
        Assert.Equal((byte)fourth, client.Latest!.SkillSlots[1]);
        Run(server, client, network, 12, Command.CastSkill, fourth);
        Assert.Equal(0, server.Stats.SkillsCast);

        var wait = Skills.Cooldown(SkillDef.All[fourth], 0);
        Run(server, client, network, wait);
        Run(server, client, network, 12, Command.CastSkill, fourth);
        Assert.Equal(1, server.Stats.SkillsCast);
    }

    [Fact]
    public void Skill_FliesThroughTheCrosshairOfTheCastingInput()
    {
        // The foe stands to the side; the cast carries a fine aim at it, while the body faces ahead.
        var (server, client, network) = Start(new Vector3(8, 0, 0));
        var foe = server.Enemies[0];
        var skill = Learn(server, client, network, "fireball");
        var health = foe.Health;
        const ushort right = 49152;
        for (var i = 0; i < 90; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None, 0, i == 0 ? Command.CastSkill : Command.None, right, 0, i == 0 ? skill : (ushort)0);
        }

        Assert.True(foe.Health < health);
    }

    [Fact]
    public void Book_TeachesItsSkillOnce()
    {
        var (server, client, network) = Start();
        var skill = Learn(server, client, network, "fireball");
        Assert.Equal(0, server.GetBagCount(1, SkillDef.All[skill].Book));
        Assert.NotEqual(0u, client.Latest!.KnownSkills & (1u << skill));
    }

    [Fact]
    public void Fireball_HitsAlongTheAim_ThenCoolsDown()
    {
        var (server, client, network) = Start(new Vector3(0, 0, -8));
        var foe = server.Enemies[0];
        var skill = Learn(server, client, network, "fireball");
        var health = foe.Health;
        var stamina = server.GetPlayer(1)!.Value.Stamina;
        Run(server, client, network, 3, Command.CastSkill, skill);
        var flying = server.ProjectileCount;
        Run(server, client, network, 57);
        Assert.True(foe.Health < health, $"The fireball must hit the enemy in front; shots in flight {flying}, casts {server.Stats.SkillsCast}, stamina {server.GetPlayer(1)!.Value.Stamina}.");
        Assert.True(server.GetPlayer(1)!.Value.Stamina < stamina);

        health = foe.Health;
        Run(server, client, network, 60, Command.CastSkill, skill);
        Assert.Equal(health, foe.Health);
    }

    [Fact]
    public void AirSlash_CutsEveryEnemyInItsPath()
    {
        var (server, client, network) = Start(new Vector3(0, 0, -3), new Vector3(0, 0, -6));
        var skill = Learn(server, client, network, "air_slash");
        var before = server.Enemies.Select(e => e.Health).ToArray();
        Run(server, client, network, 60, Command.CastSkill, skill);
        Assert.All(server.Enemies.Select((e, i) => (e, i)), p => Assert.True(p.e.Health < before[p.i]));
    }

    [Fact]
    public void EarthCrack_HitsInsideTheV()
    {
        var (server, client, network) = Start(new Vector3(0, 0, -4), new Vector3(4, 0, 0));
        var skill = Learn(server, client, network, "earth_crack");
        var ahead = server.Enemies[0].Health;
        var aside = server.Enemies[1].Health;
        Run(server, client, network, 60, Command.CastSkill, skill);
        Assert.True(server.Enemies[0].Health < ahead);
        Assert.Equal(aside, server.Enemies[1].Health);
    }

    [Fact]
    public void SummonedSpears_SeekTheNearestEnemy()
    {
        var (server, client, network) = Start(new Vector3(6, 0, 3));
        var skill = Learn(server, client, network, "summon_spears");
        var health = server.Enemies[0].Health;
        Run(server, client, network, 150, Command.CastSkill, skill);
        Assert.True(health - server.Enemies[0].Health > SkillDef.All[skill].Damage * 2, "All three spears must find the enemy.");
    }

    [Fact]
    public void Books_DropOnlyFromTheirTeachers()
    {
        var fireball = SkillDef.All[SkillDef.Index("fireball")];
        Assert.True(fireball.ChanceFrom(EnemyDef.All.First(d => d.Id == "goblin_shaman")) > 0);
        Assert.Equal(0, fireball.ChanceFrom(EnemyDef.All.First(d => d.Id == "goblin_brute")));
        Assert.All(SkillDef.All, s => Assert.Equal("skillbook", ItemDef.All[s.Book].Kind));
    }
}
