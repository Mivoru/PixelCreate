using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>
/// A shaman's war dance hastens the goblins of its camp; a goblin brute and an orc berserker fly into a rage
/// when hurt: the brute winds up faster, the berserker recovers faster but stops guarding itself.
/// </summary>
public class RageAndDanceTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(float distance, int count, params string[] types)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, distance), 1.5f, count, Types: types);
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

    private static byte TypeOf(string id) => (byte)EnemyDef.All.ToList().FindIndex(d => d.Id == id);

    [Fact]
    public void ShamansDance_HastensTheGoblinsOfItsCamp()
    {
        var (server, client, network) = Start(12f, 3, "goblin_skirmisher");
        server.Enemies[0].Type = TypeOf("goblin_shaman");
        var hastened = false;
        for (var i = 0; i < Tuning.TickRate * 20 && !hastened; i++)
        {
            Play(server, client, network, 1);
            hastened = server.Enemies.Skip(1).Any(e => e.HasteTicks > 0);
        }

        Assert.True(hastened, "the shaman never danced for the two goblins of its camp");
        Assert.Equal(0, server.Enemies[0].HasteTicks);
    }

    [Fact]
    public void ShamanAlone_DoesNotDance()
    {
        var (server, client, network) = Start(12f, 1, "goblin_shaman");
        var shaman = server.Enemies[0];
        for (var i = 0; i < Tuning.TickRate * 15; i++)
        {
            Play(server, client, network, 1);
            Assert.False(shaman.Action == EnemyAction.Cast && shaman.CurrentAbility?.Effect == "warDance", "a shaman with nobody to stir danced");
        }
    }

    [Fact]
    public void Brute_FliesIntoARage_AndWindsUpFaster()
    {
        var (server, client, network) = Start(2.5f, 1, "goblin_brute");
        var brute = server.Enemies[0];
        brute.Health = (short)(brute.MaxHealth / 4);
        var cast = -1;
        for (var i = 0; i < Tuning.TickRate * 15 && cast < 0; i++)
        {
            var before = brute.Action;
            Play(server, client, network, 1);
            if (brute.Action == EnemyAction.Cast && before != EnemyAction.Cast && brute.CurrentAbility!.Effect == "slam")
            {
                cast = brute.ActionTicks;
            }
        }

        Assert.True(brute.Enraged);
        var slam = brute.Def.Abilities!.Single(a => a.Effect == "slam");
        Assert.InRange(cast, Enemy.MinWindupTicks - 1, (int)MathF.Round(slam.WindupTicks * brute.Def.EnrageWindup));
    }

    [Fact]
    public void Berserker_InItsFrenzy_TakesMoreFromEveryBlow()
    {
        var (server, client, network) = Start(30f, 1, "orc_berserker");
        var orc = server.Enemies[0];
        var full = orc.Health;
        orc.TakeHit(100, orc.Position + Vector3.UnitX);
        Assert.Equal(100, full - orc.Health);

        orc.Health = (short)(orc.MaxHealth * 0.4f);
        Play(server, client, network, 2);
        Assert.True(orc.Enraged);
        var before = orc.Health;
        orc.TakeHit(100, orc.Position + Vector3.UnitX);
        Assert.Equal(125, before - orc.Health);
    }
}
