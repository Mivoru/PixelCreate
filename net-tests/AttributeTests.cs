using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>PlayerAttribute points: earned by level, spent one at a time, and what each one changes.</summary>
public class AttributeTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start()
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, Array.Empty<EnemySpawn>());
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 1 });
        var client = new ClientPrediction(network.Connect(1), world);
        Run(server, client, network, 10);
        return (server, client, network);
    }

    private static void Run(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Command command = Command.None, ushort arg = 0)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None, 0, i == 0 ? command : Command.None, null, 0, i == 0 ? arg : (ushort)0);
        }
    }

    [Fact]
    public void Points_ComeWithLevelsAndAreSpentOneByOne()
    {
        var (server, client, network) = Start();
        Run(server, client, network, 12, Command.RaiseAttribute, (ushort)PlayerAttribute.Strength);
        Assert.Equal(0, server.GetAttribute(1, PlayerAttribute.Strength));

        server.SetLevel(1, 2);
        for (var i = 0; i < Attributes.PointsPerLevel + 2; i++)
        {
            Run(server, client, network, 12, Command.RaiseAttribute, (ushort)PlayerAttribute.Strength);
        }

        Assert.Equal(Attributes.PointsPerLevel, server.GetAttribute(1, PlayerAttribute.Strength));
    }

    [Fact]
    public void Endurance_RaisesStaminaOnBothSides()
    {
        var (server, client, network) = Start();
        server.SetLevel(1, 3);
        for (var i = 0; i < 4; i++)
        {
            Run(server, client, network, 12, Command.RaiseAttribute, (ushort)PlayerAttribute.Endurance);
        }

        Run(server, client, network, 60 * 6);
        var expected = Tuning.MaxStamina + 4 * Attributes.StaminaPerEndurance;
        Assert.Equal(expected, server.GetPlayer(1)!.Value.Stamina, 2);
        Assert.Equal(expected, client.State.Stamina, 2);
    }

    [Fact]
    public void Vitality_RaisesMaximumHealth()
    {
        var (server, client, network) = Start();
        server.SetLevel(1, 2);
        Run(server, client, network, 12, Command.RaiseAttribute, (ushort)PlayerAttribute.Vitality);
        Run(server, client, network, 12);
        Assert.Equal(Progression.MaxHealth(2) + Attributes.HealthPerVitality, client.Latest!.MaxHealth);
    }
}
