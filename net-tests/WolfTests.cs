using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>The wolves' moves on an empty field: the pounce, the shadow clones and the shadow run.</summary>
public class WolfTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(string id, float distance)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, distance), 1f, 1, Types: new[] { id });
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

    [Theory]
    [InlineData("wolf")]
    [InlineData("alpha_wolf")]
    public void Wolf_PouncesThroughTheAir(string id)
    {
        var (server, client, network) = Start(id, 6f);
        var wolf = server.Enemies[0];
        var highest = 0f;
        for (var i = 0; i < 60 * 20 && highest < 0.5f; i++)
        {
            Play(server, client, network, 1);
            if (wolf.Action == EnemyAction.Leap && wolf.CurrentAbility?.Effect == "pounce")
            {
                highest = MathF.Max(highest, wolf.Position.Y);
            }
        }

        Assert.True(highest >= 0.5f, $"A pounce must leave the ground; it rose {highest:F2} m.");
        Play(server, client, network, 60 * 5);
        Assert.True(server.Stats.PlayerHits > 0, "The wolf must hurt the player.");
    }

    [Fact]
    public void MirrorWolf_CallsTwoClonesThatGoWithIt()
    {
        var (server, client, network) = Start("mirror_wolf", 9f);
        for (var i = 0; i < 60 * 10 && server.Enemies.Count < 3; i++)
        {
            Play(server, client, network, 1);
        }

        Assert.Equal(3, server.Enemies.Count);
        var wolf = server.Enemies.Single(e => e.SummonedBy < 0);
        var clones = server.Enemies.Where(e => e.SummonedBy == wolf.Id).ToList();
        Assert.Equal(2, clones.Count);
        Assert.All(clones, c => Assert.Equal("shadow_clone", c.Def.Id));
        Assert.All(clones, c => Assert.True(c.MaxHealth < wolf.MaxHealth / 3, "A clone must be much weaker than its wolf."));

        // Killing one clone gives nothing and removes it; killing the wolf takes the other with it.
        var kills = server.Stats.EnemiesKilled;
        clones[0].TakeHit(short.MaxValue, Vector3.Zero);
        Play(server, client, network, 2);
        Assert.Equal(2, server.Enemies.Count);
        wolf.TakeHit(short.MaxValue, Vector3.Zero);
        Play(server, client, network, 2);
        Assert.DoesNotContain(server.Enemies, e => e.SummonedBy >= 0);
        Assert.Equal(kills, server.Stats.EnemiesKilled);
    }

    [Fact]
    public void Clones_FadeAfterAWhile()
    {
        var (server, client, network) = Start("mirror_wolf", 9f);
        for (var i = 0; i < 60 * 10 && server.Enemies.Count < 3; i++)
        {
            Play(server, client, network, 1);
        }

        Assert.Equal(3, server.Enemies.Count);
        server.Enemies.Single(e => e.SummonedBy < 0).Health = short.MaxValue;
        // Clones last 14 s; the wolf cannot call new ones until its cooldown, which is longer.
        Play(server, client, network, 60 * 14 + 10);
        Assert.Single(server.Enemies);
    }

    [Fact]
    public void ShadowWolf_ZigzagsUnharmedThenBurstsOut()
    {
        var (server, client, network) = Start("shadow_wolf", 15f);
        var wolf = server.Enemies[0];
        var risings = new List<Vector3>();
        var dodged = false;
        var lastAction = wolf.Action;
        var burst = false;
        for (var i = 0; i < 60 * 25 && !burst; i++)
        {
            Play(server, client, network, 1);
            if (wolf.Action == EnemyAction.Surface && lastAction != EnemyAction.Surface)
            {
                risings.Add(wolf.Position);
            }

            if (wolf.Action == EnemyAction.Shadow && !dodged)
            {
                var health = wolf.Health;
                Assert.False(wolf.TakeHit(50, Vector3.Zero, 1));
                Assert.Equal(health, wolf.Health);
                dodged = true;
            }

            burst = lastAction == EnemyAction.Shadow && wolf.Action == EnemyAction.Leap;
            lastAction = wolf.Action;
        }

        Assert.True(dodged, "The wolf must sink into the shadow.");
        Assert.True(burst, "The shadow must burst out onto the player.");
        Assert.True(risings.Count >= 2, $"The wolf must rise between slides; it rose {risings.Count} times.");

        // Each rising lies on the other side of the line from the wolf's camp to the player.
        var line = Vector3.Normalize(new Vector3(0, 0, 15f));
        var sides = risings.Select(p => MathF.Sign(p.X * line.Z - p.Z * line.X)).ToList();
        for (var i = 1; i < sides.Count; i++)
        {
            Assert.NotEqual(sides[i - 1], sides[i]);
        }

        Play(server, client, network, 60);
        Assert.True(server.Stats.PlayerHits > 0, "The leap out of the shadow must hurt the player.");
    }
}
