using System.Numerics;
using PixelCreate.Core;
using Xunit;
using Xunit.Abstractions;

namespace PixelCreate.NetTests;

/// <summary>Gear: rolling rarity and affixes, carrying it over the network, wearing it, and what drops.</summary>
public sealed class GearTests
{
    private readonly ITestOutputHelper _output;

    public GearTests(ITestOutputHelper output) => _output = output;

    private static ushort Item(string id) => ItemDef.Index(id);

    [Theory]
    [InlineData(Rarity.Common, 0, 1, 0)]
    [InlineData(Rarity.Rare, 1, 2, 1)]
    [InlineData(Rarity.Epic, 2, 3, 2)]
    [InlineData(Rarity.Legendary, 3, 3, 3)]
    public void Rolls_FollowTheRarity(Rarity rarity, int fewest, int most, int strongestTier)
    {
        var random = new Random(4);
        foreach (var id in new[] { "rusty_dagger", "spiked_club", "hide_vest", "jelly_ring", "goblin_shortbow" })
        {
            for (var i = 0; i < 200; i++)
            {
                var piece = GearPiece.Roll(Item(id), rarity, 3, random);
                Assert.InRange(piece.Affixes.Length, fewest, most);
                Assert.Equal(piece.Affixes.Length, piece.Affixes.Select(a => a.Affix).Distinct().Count());
                foreach (var affix in piece.Affixes)
                {
                    Assert.True(affix.Def.Fits(piece.Def), $"{affix.Def.Id} rolled on {id}");
                    Assert.InRange(affix.Value, affix.Def.Tiers[0][0], affix.Def.Tiers[strongestTier][1]);
                }
            }
        }
    }

    [Fact]
    public void Affixes_StayOnTheirKindOfGear()
    {
        var dagger = ItemDef.All[Item("rusty_dagger")];
        var club = ItemDef.All[Item("spiked_club")];
        var vest = ItemDef.All[Item("hide_vest")];
        var ring = ItemDef.All[Item("jelly_ring")];
        var backstab = AffixDef.All[AffixDef.Index("backstab")];
        var thorns = AffixDef.All[AffixDef.Index("thorns")];
        var crit = AffixDef.All[AffixDef.Index("crit_chance")];
        Assert.True(backstab.Fits(dagger));
        Assert.False(backstab.Fits(club));
        Assert.False(thorns.Fits(dagger));
        Assert.True(thorns.Fits(vest));
        Assert.False(crit.Fits(vest));
        Assert.True(crit.Fits(ring) && thorns.Fits(ring));
    }

    [Fact]
    public void Gear_TravelsInTheSnapshot()
    {
        var piece = GearPiece.Roll(Item("twin_daggers"), Rarity.Legendary, 9, new Random(1));
        var snapshot = new Snapshot
        {
            Gear = new[] { new GearEntry(3, piece, true), new GearEntry(7, null, false) },
            Drops = new[] { new LootView(5, Item("iron_helm"), Vector3.One, (byte)Rarity.Epic), new LootView(6, 0, Vector3.Zero) },
        };
        Assert.True(Protocol.TryDecodeSnapshot(Protocol.EncodeSnapshot(snapshot), out var back));
        Assert.Equal(3, back.Gear[0].Index);
        Assert.True(back.Gear[0].Worn);
        Assert.Equal(piece.Item, back.Gear[0].Piece!.Item);
        Assert.Equal(piece.Rarity, back.Gear[0].Piece!.Rarity);
        Assert.Equal(piece.Level, back.Gear[0].Piece!.Level);
        Assert.Equal(piece.Affixes, back.Gear[0].Piece!.Affixes);
        Assert.Null(back.Gear[1].Piece);
        Assert.Equal((byte)Rarity.Epic, back.Drops[0].Rarity);
        Assert.Equal(LootView.NoRarity, back.Drops[1].Rarity);
    }

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start()
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var server = new GameServer(network.Connect(0), new World(Array.Empty<Aabb>()), new ServerOptions { Seed = 3 });
        var client = new ClientPrediction(network.Connect(1), new World(Array.Empty<Aabb>()));
        Run(server, client, network, 10);
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
    public void Wearing_ChangesStatsAndOneSlotHoldsOnePiece()
    {
        var (server, client, network) = Start();
        var plain = server.MaxHealthOf(1);
        var helm = new GearPiece(Item("iron_helm"), Rarity.Common, 1, new[] { new AffixRoll(AffixDef.Index("max_health"), 20) });
        var cap = new GearPiece(Item("goblin_cap"), Rarity.Common, 1, Array.Empty<AffixRoll>());
        var helmPlace = server.GiveGear(1, helm);
        var capPlace = server.GiveGear(1, cap);

        Run(server, client, network, 12, GearCommands.Toggle(helmPlace));
        Assert.Equal(plain + 20, server.MaxHealthOf(1));
        Assert.Equal(helm.Defense, server.GearOf(1).Defense);

        // The cap goes on the same head: the helm comes off.
        Run(server, client, network, 12, GearCommands.Toggle(capPlace));
        Assert.Equal(plain, server.MaxHealthOf(1));
        Assert.Equal(cap.Defense, server.GearOf(1).Defense);

        // The client hears which places are worn.
        Run(server, client, network, 60);
        Assert.Contains(client.Gear, e => e.Index == capPlace && e.Worn && e.Piece!.Item == cap.Item);
        Assert.Contains(client.Gear, e => e.Index == helmPlace && !e.Worn);

        // Worn gear cannot be thrown away; the rest can.
        Run(server, client, network, 12, GearCommands.Discard(capPlace));
        Assert.NotNull(server.GearList(1)[capPlace]);
        Run(server, client, network, 12, GearCommands.Discard(helmPlace));
        Assert.Null(server.GearList(1)[helmPlace]);
    }

    [Fact]
    public void Defense_TakesAShareOffEveryBlow()
    {
        Assert.Equal(1f, GearStats.DamageTaken(0));
        Assert.Equal(0.5f, GearStats.DamageTaken(GearStats.DefenseHalving), 3);
        Assert.True(GearStats.DamageTaken(200) > 0.15f, "defense helps less and less and never makes anyone untouchable");
    }

    /// <summary>
    /// Prints what each enemy's gear table gives over an hour of farming it, and checks the shape the
    /// design asks for: slimes almost nothing, goblins more and better, the shaman the most.
    /// The kills an hour are rough guesses of how fast one player clears each camp.
    /// </summary>
    [Fact]
    public void DropTables_GiveWhatTheDesignAsks()
    {
        var killsPerHour = new Dictionary<string, double>
        {
            ["slime"] = 150, ["rare_slime"] = 6, ["goblin_skirmisher"] = 70, ["goblin_rapid_archer"] = 70,
            ["goblin_brute"] = 40, ["goblin_sharpshooter"] = 50, ["goblin_shaman"] = 4, ["pig"] = 60,
        };
        var perHour = new Dictionary<string, double[]>();
        foreach (var def in EnemyDef.All.Where(d => d.Gear is not null))
        {
            var table = def.Gear!;
            Assert.All(table.Items, id => Assert.True(ItemDef.All[Item(id)].IsEquipment, $"{def.Id} drops {id}, which is not gear"));
            var total = table.Rarity.Sum();
            var kills = killsPerHour.GetValueOrDefault(def.Id, 50);
            perHour[def.Id] = table.Rarity.Select(w => kills * table.Chance * w / total).ToArray();
            _output.WriteLine($"{def.Id,-20} {kills,4} kills/h  {kills * table.Chance,5:F2} pieces/h  " +
                              string.Join("  ", perHour[def.Id].Select((n, i) => $"{(Rarity)i} {n:F2}")));
        }

        Assert.True(EnemyDef.All.Single(d => d.Id == "slime").Gear!.Chance < 0.01, "slimes are easy to farm, so they rarely drop gear");
        Assert.Equal(0, perHour["slime"][(int)Rarity.Legendary]);
        Assert.True(perHour["goblin_skirmisher"].Sum() > perHour["slime"].Sum(), "goblins drop more gear than slimes");
        Assert.True(EnemyDef.All.Single(d => d.Id == "goblin_shaman").Gear!.Chance >= 0.2, "the rare shaman is worth hunting");
    }

    [Fact]
    public void DaggerFlurry_StabsTwiceForOnePress()
    {
        var world = new World(Array.Empty<Aabb>());
        var state = PlayerState.Spawn(Vector3.Zero);
        state.Weapon = Weapons.Dagger;
        var blows = new List<int>();
        for (var tick = 0; tick < 40; tick++)
        {
            var input = new InputCmd { Tick = (uint)tick, Buttons = tick == 0 ? Buttons.Light : Buttons.None };
            if (Movement.Step(ref state, input, world) == AttackKind.DaggerFlurry)
            {
                blows.Add(tick);
            }
        }

        Assert.Equal(2, blows.Count);
        Assert.True(blows[1] - blows[0] >= 5, "the second stab follows the first, not with it");
    }

    [Fact]
    public void DaggerLeap_FliesAlongTheAimAndLandsWithTheBlow()
    {
        var world = new World(Array.Empty<Aabb>());
        var state = PlayerState.Spawn(Vector3.Zero);
        state.Weapon = Weapons.Dagger;
        var landedAt = -1;
        var highest = 0f;
        for (var tick = 0; tick < 60; tick++)
        {
            var input = new InputCmd { Tick = (uint)tick, Buttons = tick == 0 ? Buttons.Heavy : Buttons.None, Aim = 0 };
            if (Movement.Step(ref state, input, world) == AttackKind.DaggerLeap)
            {
                landedAt = tick;
                Assert.True(state.Position.Y < 0.5f, $"the daggers strike near the ground, not at {state.Position.Y:F2} m");
            }

            highest = MathF.Max(highest, state.Position.Y);
        }

        Assert.True(landedAt > 0, "the leap's blow lands");
        Assert.InRange(-state.Position.Z, 4f, 6.5f);
        Assert.InRange(highest, 0.5f, 1.5f);
    }

    [Fact]
    public void DaggerLeaps_ArePredictedWithoutCorrections()
    {
        var network = new SimNetwork(80, 8, 2, 7);
        var world = new World(Array.Empty<Aabb>());
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 3, EnemyCount = 0 });
        var client = new ClientPrediction(network.Connect(1), world);
        for (var tick = 0; tick < 30; tick++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None);
        }

        var place = server.GiveGear(1, GearPiece.Roll(Item("twin_daggers"), Rarity.Common, 1, new Random(1)));
        for (var tick = 0; tick < Tuning.TickRate * 8; tick++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            var buttons = tick > 60 && tick % 70 == 0 ? Buttons.Heavy : tick % 23 == 0 ? Buttons.Light : Buttons.None;
            client.Step(0, 90, buttons, (byte)(tick / 40), tick == 0 ? GearCommands.Toggle(place) : Command.None);
        }

        Assert.Equal(Weapons.Dagger, server.GetPlayer(1)!.Value.Weapon);
        Assert.Equal(Weapons.Dagger, client.State.Weapon);
        // One correction when the server hands over the daggers; leaps themselves must agree.
        Assert.True(client.Stats.Corrections <= 2, $"{client.Stats.Corrections} corrections while leaping");
    }

    [Fact]
    public void Bow_DrawsWhileHeldAndLoosesOnRelease()
    {
        var world = new World(Array.Empty<Aabb>());
        var state = PlayerState.Spawn(Vector3.Zero);
        state.Weapon = Weapons.Bow;
        var shotAt = -1;
        var walkedDrawing = 0f;
        for (var tick = 0; tick < 60; tick++)
        {
            var holding = tick < 30;
            var before = state.Position.Z;
            var input = new InputCmd { Tick = (uint)tick, MoveZ = -127, Buttons = holding ? Buttons.Light : Buttons.None };
            if (Movement.Step(ref state, input, world) == AttackKind.BowShot)
            {
                shotAt = tick;
                Assert.Equal(30, state.ShotDraw);
            }

            if (holding && tick > 20)
            {
                walkedDrawing = MathF.Max(walkedDrawing, before - state.Position.Z);
            }
        }

        Assert.True(shotAt >= 30, "the arrow leaves when the button is let go");
        Assert.InRange(walkedDrawing * Tuning.TickRate, 1f, Tuning.MoveSpeed * Tuning.DrawMoveScale + 0.2f);
        Assert.Equal(0, state.DrawTicks);
    }

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) StartArcher(Vector3 target)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(target, 0.1f, 1, Types: new[] { "goblin_brute" });
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 3 });
        var client = new ClientPrediction(network.Connect(1), world);
        Run(server, client, network, 10);
        server.EnemiesFrozen = true;
        server.Enemies[0].Position = target;
        var bow = server.GiveGear(1, new GearPiece(Item("hunters_longbow"), Rarity.Common, 1, Array.Empty<AffixRoll>()));
        Run(server, client, network, 12, GearCommands.Toggle(bow));
        return (server, client, network);
    }

    /// <summary>Draws for some ticks, lets go, and waits for the arrow. Returns the damage it did.</summary>
    private static int Shoot(GameServer server, ClientPrediction client, SimNetwork network, int draw, byte aim = 0)
    {
        var before = server.Enemies[0].Health;
        for (var tick = 0; tick < draw + 90; tick++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, tick < draw ? Buttons.Light : Buttons.None, aim);
        }

        return before - server.Enemies[0].Health;
    }

    [Fact]
    public void Arrows_HurtMoreAtFullDraw()
    {
        var (server, client, network) = StartArcher(new Vector3(0, 0, -14));
        var quick = Shoot(server, client, network, 4);
        var (fullServer, fullClient, fullNetwork) = StartArcher(new Vector3(0, 0, -14));
        var full = Shoot(fullServer, fullClient, fullNetwork, Tuning.FullDraw + 10);
        Assert.True(quick > 0, "a quick shot still hits");
        Assert.True(full > quick * 2, $"a full draw ({full}) hits much harder than a quick shot ({quick})");
    }

    [Fact]
    public void Arrows_BendTowardsAnEnemyNearTheAimOnly()
    {
        // A little to the side of the aim: the arrow bends onto it.
        var (server, client, network) = StartArcher(new Vector3(0.8f, 0, -16));
        Assert.True(Shoot(server, client, network, Tuning.FullDraw) > 0);

        // Far off to the side: the arrow flies on along the aim.
        var (wide, wideClient, wideNetwork) = StartArcher(new Vector3(9f, 0, -16));
        Assert.Equal(0, Shoot(wide, wideClient, wideNetwork, Tuning.FullDraw));
    }

    [Fact]
    public void BowDraws_ArePredictedWithoutCorrections()
    {
        var network = new SimNetwork(80, 8, 2, 7);
        var world = new World(Array.Empty<Aabb>());
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 3, EnemyCount = 0 });
        var client = new ClientPrediction(network.Connect(1), world);
        for (var tick = 0; tick < 30; tick++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None);
        }

        var place = server.GiveGear(1, GearPiece.Roll(Item("goblin_shortbow"), Rarity.Common, 1, new Random(1)));
        for (var tick = 0; tick < Tuning.TickRate * 8; tick++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            var drawing = tick > 60 && tick % 80 < 50;
            client.Step(60, 90, drawing ? Buttons.Light : Buttons.None, (byte)(tick / 30), tick == 0 ? GearCommands.Toggle(place) : Command.None);
        }

        Assert.Equal(Weapons.Bow, client.State.Weapon);
        Assert.True(client.Stats.Corrections <= 2, $"{client.Stats.Corrections} corrections while drawing and loosing");
    }
}
