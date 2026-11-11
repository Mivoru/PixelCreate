using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>
/// Stonebacks on an empty field: struck, they hide in the shell beyond harm and come out with a lunge once the
/// player backs off; the Mossback Elder spins in its shell, glances off walls and stands dizzy after.
/// </summary>
public class StonebackTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(string id, float distance, params Aabb[] boxes)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, distance), 0.3f, 1, Types: new[] { id });
        var world = new World(boxes, null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 4 });
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

    [Fact]
    public void Stoneback_Struck_HidesInItsShell_BeyondHarm()
    {
        var (server, _, _) = Start("stoneback", 12f);
        var turtle = server.Enemies[0];
        turtle.TakeHit(20, turtle.Position + Vector3.UnitX, 1);
        Assert.Equal(EnemyAction.Shell, turtle.Action);
        Assert.True(turtle.Untouchable);
        var health = turtle.Health;
        turtle.TakeHit(500, turtle.Position + Vector3.UnitX, 1);
        Assert.Equal(health, turtle.Health);
    }

    [Fact]
    public void Stoneback_ComesOut_AndLunges_OnceThePlayerBacksOff()
    {
        // The player (who spawns three metres off the middle) stands about five and a half metres away:
        // beyond the calm distance, inside the lunge's reach.
        var (server, client, network) = Start("stoneback", 8f);
        Play(server, client, network, 30);
        var turtle = server.Enemies[0];
        turtle.TakeHit(20, turtle.Position + Vector3.UnitX, 1);
        var lunged = false;
        for (var i = 0; i < Tuning.TickRate * 3 && !lunged; i++)
        {
            Play(server, client, network, 1);
            lunged = turtle.Action is EnemyAction.Cast or EnemyAction.Leap && turtle.CurrentAbility?.Effect == "dashStrike";
        }

        Assert.True(lunged, "the stoneback stayed in its shell, or came out without lunging, with the player backed off");
        Assert.False(turtle.Untouchable, "out of its shell it can be struck");
    }

    [Fact]
    public void Stoneback_StaysIn_WhileThePlayerStandsClose()
    {
        var (server, client, network) = Start("stoneback", 2f);
        Play(server, client, network, 30);
        var turtle = server.Enemies[0];
        turtle.TakeHit(20, turtle.Position + Vector3.UnitX, 1);
        Play(server, client, network, Tuning.TickRate * 3);
        Assert.Equal(EnemyAction.Shell, turtle.Action);
    }

    [Fact]
    public void MossbackElder_Spins_GlancesOffAWall_AndStandsDizzy()
    {
        var elder = EnemyDef.All.Single(d => d.Id == "mossback_elder");
        Assert.True(elder.Rare && elder.Shells);
        // A wall behind the player, so a spin that misses glances off it.
        var wall = new Aabb(new Vector3(-6, 0, -4), new Vector3(6, 3, -3));
        var (server, client, network) = Start("mossback_elder", 8f, wall);
        var boss = server.Enemies[0];
        var spun = false;
        var glanced = false;
        for (var i = 0; i < Tuning.TickRate * 30 && !(spun && boss.Action == EnemyAction.Stagger); i++)
        {
            Play(server, client, network, 1);
            spun |= boss.Action == EnemyAction.ShellSpin;
            glanced |= boss.Glanced;
        }

        Assert.True(spun, "the Mossback Elder never spun");
        Assert.True(glanced, "a spinning shell glances off the wall");
        Assert.Equal(EnemyAction.Stagger, boss.Action);
    }
}
