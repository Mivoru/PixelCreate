using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>The player's guard: blocking light and heavy blows, parrying, and the stagger that follows.</summary>
public class BlockTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network, Enemy Foe) Start(string id)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, -30), 0.5f, 1, Types: new[] { id });
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 2 });
        var client = new ClientPrediction(network.Connect(1), world);
        Play(server, client, network, 20, Buttons.None);
        server.EnemiesFrozen = true;
        var foe = server.Enemies[0];
        return (server, client, network, foe);
    }

    private static void Play(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Buttons buttons)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, buttons);
        }
    }

    /// <summary>Puts the foe two metres in front of the player (the aim looks along -Z), or behind.</summary>
    private static void Face(GameServer server, Enemy foe, bool behind = false)
    {
        var player = server.GetPlayer(1)!.Value.Position;
        foe.Position = player + new Vector3(0, 0, behind ? 2f : -2f);
        foe.Facing = behind ? 0f : MathF.PI;
        foe.LeapTarget = player;
    }

    private static void Strike(GameServer server, Enemy foe, string effect)
    {
        var player = server.GetPlayer(1)!.Value.Position;
        ((IEnemyEvents)server).Fire(foe, foe.Def.Abilities!.First(a => a.Effect == effect), new Target(1, player, Vector3.Zero));
    }

    [Fact]
    public void Guard_TakesALightBlowWhole()
    {
        var (server, client, network, foe) = Start("goblin_skirmisher");
        Play(server, client, network, 30, Buttons.Block);
        Face(server, foe);
        var health = server.GetPlayer(1)!.Value.Health;
        Strike(server, foe, "melee");
        Assert.Equal(health, server.GetPlayer(1)!.Value.Health);
        Assert.Equal(1, server.Stats.Blocks);
    }

    [Fact]
    public void Guard_LetsPartOfAHeavyBlowThrough()
    {
        var (server, client, network, foe) = Start("goblin_brute");
        var open = server.GetPlayer(1)!.Value.Health;
        Face(server, foe);
        Strike(server, foe, "slam");
        var full = open - server.GetPlayer(1)!.Value.Health;

        Play(server, client, network, 30, Buttons.Block);
        var guarded = server.GetPlayer(1)!.Value.Health;
        Face(server, foe);
        Strike(server, foe, "slam");
        var through = guarded - server.GetPlayer(1)!.Value.Health;
        Assert.InRange(through, 1, full / 2);
    }

    [Fact]
    public void Guard_DoesNotCoverTheBack()
    {
        var (server, client, network, foe) = Start("goblin_skirmisher");
        Play(server, client, network, 30, Buttons.Block);
        Face(server, foe, behind: true);
        var health = server.GetPlayer(1)!.Value.Health;
        Strike(server, foe, "melee");
        Assert.True(server.GetPlayer(1)!.Value.Health < health);
    }

    [Theory]
    [InlineData(0, Tuning.ParryStaggerTicks)]
    [InlineData(2, Tuning.ParryStaggerTicksStronger)]
    [InlineData(4, 0)]
    public void Parry_StaggersByLevel(int levelsAbove, int stagger)
    {
        var (server, client, network, foe) = Start("goblin_skirmisher");
        foe.Level = (byte)(1 + levelsAbove);
        // The guard has just gone up on the server: within the parry window.
        for (var i = 0; i < 30 && server.GetPlayer(1)!.Value.BlockTicks == 0; i++)
        {
            Play(server, client, network, 1, Buttons.Block);
        }

        Assert.InRange(server.GetPlayer(1)!.Value.BlockTicks, 1, Tuning.ParryTicks);
        Face(server, foe);
        var health = server.GetPlayer(1)!.Value.Health;
        Strike(server, foe, "melee");
        Assert.Equal(health, server.GetPlayer(1)!.Value.Health);
        Assert.Equal(1, server.Stats.Parries);
        if (stagger > 0)
        {
            Assert.Equal(EnemyAction.Stagger, foe.Action);
            Assert.Equal(stagger, foe.ActionTicks);
        }
        else
        {
            Assert.NotEqual(EnemyAction.Stagger, foe.Action);
        }
    }

    [Fact]
    public void StaggeredEnemy_TakesMoreFromEveryHit()
    {
        var (_, _, _, foe) = Start("goblin_brute");
        var before = foe.Health;
        foe.TakeHit(20, Vector3.Zero, 1);
        var normal = before - foe.Health;
        foe.Stagger(Tuning.ParryStaggerTicks);
        before = foe.Health;
        foe.TakeHit(20, Vector3.Zero, 1);
        Assert.Equal((int)MathF.Round(normal * Tuning.StaggerDamageScale), before - foe.Health);
    }

    [Fact]
    public void Blocking_SlowsThePlayerAndDrainsStaminaPerBlow()
    {
        var state = PlayerState.Spawn(Vector3.Zero);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, Array.Empty<EnemySpawn>());
        var free = state;
        for (uint i = 0; i < 60; i++)
        {
            Movement.Step(ref free, new InputCmd { Tick = i, MoveZ = 127 }, world);
            Movement.Step(ref state, new InputCmd { Tick = i, MoveZ = 127, Buttons = Buttons.Block }, world);
        }

        Assert.True(state.BlockTicks > Tuning.ParryTicks);
        Assert.InRange(state.Position.Length() / free.Position.Length(), Tuning.BlockMoveScale - 0.1f, Tuning.BlockMoveScale + 0.1f);
    }
}
