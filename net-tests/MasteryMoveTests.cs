using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>The moves at the ends of the weapon mastery branches, each with and without the move taken.</summary>
public class MasteryMoveTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(string weapon, string? unlock, params Vector3[] foes)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var types = foes.Select(_ => "goblin_brute").ToArray();
        var camp = new EnemySpawn(new Vector3(0, 0, -60), 1f, foes.Length, Types: types);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 4 });
        var client = new ClientPrediction(network.Connect(1), world);
        Run(server, client, network, 10, Buttons.None);
        server.EnemiesFrozen = true;
        var piece = server.GiveGear(1, new GearPiece(ItemDef.Index(weapon), Rarity.Common, 1, Array.Empty<AffixRoll>()));
        Run(server, client, network, 12, Buttons.None, GearCommands.Toggle(piece));
        if (unlock is not null)
        {
            server.GrantMastery(1, unlock);
        }

        var feet = server.GetPlayer(1)!.Value.Position;
        for (var i = 0; i < foes.Length; i++)
        {
            server.Enemies[i].Position = feet + foes[i];
            server.Enemies[i].Facing = MathF.PI;
        }

        return (server, client, network);
    }

    private static void Run(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Buttons buttons, Command command = Command.None)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, buttons, 0, i == 0 ? command : Command.None);
        }
    }

    /// <summary>Holds a button for some ticks, lets go, and waits for what it set off to finish.</summary>
    private static void Press(GameServer server, ClientPrediction client, SimNetwork network, Buttons button, int hold)
    {
        Run(server, client, network, hold, button);
        Run(server, client, network, 120, Buttons.None);
    }

    private static int[] Health(GameServer server) => server.Enemies.Select(e => (int)e.Health).ToArray();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PiercingArrow_HitsEveryEnemyInLine(bool taken)
    {
        var (server, client, network) = Start("hunters_longbow", taken ? "piercing_arrow" : null, new Vector3(0, 0, -7), new Vector3(0, 0, -11));
        var before = Health(server);
        Press(server, client, network, Buttons.Light, Tuning.FullDraw + 4);
        var after = Health(server);
        Assert.True(after[0] < before[0], "The first enemy in line is always hit.");
        Assert.Equal(taken, after[1] < before[1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StunningBash_LeavesSpunEnemiesReeling(bool taken)
    {
        var (server, client, network) = Start("spiked_club", taken ? "stunning_bash" : null, new Vector3(0, 0, -1.5f));
        var foe = server.Enemies[0];
        var before = foe.Health;
        var spin = Tuning.Attack(AttackKind.ClubSpin);
        Run(server, client, network, 2, Buttons.Heavy);
        Run(server, client, network, spin.HitAt + 4, Buttons.None);
        Assert.True(foe.Health < before);
        Assert.Equal(taken, foe.Action == EnemyAction.Stagger);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shockwave_ReachesPastTheSpin(bool taken)
    {
        var (server, client, network) = Start("spiked_club", taken ? "shockwave" : null, new Vector3(4.5f, 0, 0));
        var before = Health(server);
        Press(server, client, network, Buttons.Heavy, 2);
        Assert.Equal(taken, Health(server)[0] < before[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChainLightning_LeapsFromTheFireballsVictim(bool taken)
    {
        var (server, client, network) = Start("bone_staff", taken ? "chain_lightning" : null, new Vector3(0, 0, -8), new Vector3(5, 0, -9));
        var before = Health(server);
        Press(server, client, network, Buttons.Light, 2);
        var after = Health(server);
        Assert.True(after[0] < before[0], "The fireball hits the enemy it was cast at.");
        Assert.Equal(taken, after[1] < before[1]);
    }
}
