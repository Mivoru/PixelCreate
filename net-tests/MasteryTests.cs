using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Weapon mastery: experience from kills, points per level, and a tree taken branch by branch.</summary>
public class MasteryTests
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
            client.Step(0, 0, Buttons.None, 0, i == 0 ? command : Command.None, 0, 0, i == 0 ? arg : (ushort)0);
        }
    }

    private static MasteryNode Node(string weapon, int branch, int tier) =>
        Mastery.Nodes.Single(n => n.Weapon == weapon && n.Branch == branch && n.Tier == tier);

    [Fact]
    public void Trees_CoverEveryWeaponWithThreeBranches()
    {
        Assert.All(Mastery.Weapons, w => Assert.Equal(3, Mastery.Branches(w).Count));
        Assert.All(Mastery.Nodes.Where(n => n.Unlock is null), n => Assert.NotNull(n.Stats));
    }

    [Fact]
    public void KillExperience_LevelsTheWeaponInHand()
    {
        var (server, _, _) = Start();
        server.GiveMasteryXp(1, Mastery.XpToNext(0) + Mastery.XpToNext(1));
        Assert.Equal(2, server.GetMasteryLevel(1, "sword"));
        Assert.Equal(0, server.GetMasteryLevel(1, "bow"));
    }

    [Fact]
    public void Nodes_AreTakenInOrderWithPoints()
    {
        var (server, client, network) = Start();
        var first = Node("sword", 0, 0);
        var second = Node("sword", 0, 1);
        Run(server, client, network, 12, Command.TakeMastery, (ushort)first.Index);
        Assert.False(server.HasMastery(1, first.Index), "Without a mastery level there is no point to spend.");

        server.GiveMasteryXp(1, Mastery.XpToNext(0) + Mastery.XpToNext(1));
        Run(server, client, network, 12, Command.TakeMastery, (ushort)second.Index);
        Assert.False(server.HasMastery(1, second.Index), "A branch is taken in order.");
        Run(server, client, network, 12, Command.TakeMastery, (ushort)first.Index);
        Run(server, client, network, 12, Command.TakeMastery, (ushort)second.Index);
        Assert.True(server.HasMastery(1, first.Index) && server.HasMastery(1, second.Index));
        Assert.Equal(1ul << first.Index | 1ul << second.Index, client.Latest!.MasteryTaken);
    }

    [Fact]
    public void UnbuiltMoves_CannotBeTakenYet()
    {
        var unlock = Mastery.Nodes.First(n => n.Unlock is not null && !MasteryNode.BuiltUnlocks.Contains(n.Unlock));
        Assert.False(Mastery.CanTake(unlock, ulong.MaxValue & ~(1ul << unlock.Index), 99));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 3)]
    public void TripleShot_LoosesThreeArrows(bool taken, int arrows)
    {
        var (server, client, network) = Start();
        var bow = server.GiveGear(1, new GearPiece(ItemDef.Index("hunters_longbow"), Rarity.Common, 1, Array.Empty<AffixRoll>()));
        Run(server, client, network, 12, GearCommands.Toggle(bow));
        if (taken)
        {
            server.GrantMastery(1, "triple_shot");
        }

        for (var tick = 0; tick < Tuning.FullDraw + 12; tick++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, tick < Tuning.FullDraw ? Buttons.Light : Buttons.None);
        }

        Assert.Equal(arrows, server.ProjectileCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Riposte_AnswersAParryWithABlow(bool taken)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, -30), 0.5f, 1, Types: new[] { "goblin_skirmisher" });
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 2 });
        var client = new ClientPrediction(network.Connect(1), world);
        Run(server, client, network, 20);
        server.EnemiesFrozen = true;
        if (taken)
        {
            server.GrantMastery(1, "riposte");
        }

        var foe = server.Enemies[0];
        for (var i = 0; i < 30 && server.GetPlayer(1)!.Value.BlockTicks == 0; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.Block);
        }

        var player = server.GetPlayer(1)!.Value.Position;
        foe.Position = player + new Vector3(0, 0, -2f);
        foe.Facing = MathF.PI;
        var health = foe.Health;
        ((IEnemyEvents)server).Fire(foe, foe.Def.Abilities!.First(a => a.Effect == "melee"), new Target(1, player, Vector3.Zero));
        Assert.Equal(1, server.Stats.Parries);
        Assert.Equal(taken ? 1 : 0, server.Stats.Ripostes);
        Assert.Equal(taken, foe.Health < health);
    }

    [Fact]
    public void TakenNodes_AddTheirStatsToTheirWeaponOnly()
    {
        var sword = new GearStats { Weapon = "sword" };
        var bow = new GearStats { Weapon = "bow" };
        var taken = 1ul << Node("sword", 0, 0).Index;
        Mastery.AddTo(sword, taken);
        Mastery.AddTo(bow, taken);
        Assert.Equal(4, sword.DamagePct);
        Assert.Equal(0, bow.DamagePct);
    }
}
