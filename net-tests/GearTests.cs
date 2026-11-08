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

    /// <summary>
    /// Draws for some ticks, lets go, and waits for the arrow. Returns the damage it did. The shot is
    /// aimed at <paramref name="at"/>, lifted for the drop as an archer would, or straight ahead.
    /// </summary>
    private static int Shoot(GameServer server, ClientPrediction client, SimNetwork network, int draw, Vector3? at = null)
    {
        var before = server.Enemies[0].Health;
        var (yaw, pitch) = at is { } target ? AimAt(client.State.Position, target, draw) : ((ushort)0, (short)0);
        for (var tick = 0; tick < draw + 150; tick++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, tick < draw ? Buttons.Light : Buttons.None, (byte)(yaw >> 8), Command.None, yaw, pitch);
        }

        return before - server.Enemies[0].Health;
    }

    /// <summary>The fine aim from a player's bow at a point, lifted by the drop the arrow will suffer.</summary>
    private static (ushort Yaw, short Pitch) AimAt(Vector3 feet, Vector3 point, int draw)
    {
        var flat = Vector3.Normalize((point - feet) with { Y = 0 });
        var origin = GameServer.ArrowOrigin(feet, flat);
        var to = point - origin;
        var distance = to.Length();
        var drawn = Tuning.Drawn((byte)Math.Max(draw, Tuning.MinDraw));
        var time = distance / Tuning.ArrowSpeed(drawn);
        to.Y += 0.5f * Tuning.ArrowGravity * time * time;
        var yaw = MathF.Atan2(-to.X, -to.Z);
        var pitch = MathF.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
        return ((ushort)MathF.Round((yaw / MathF.Tau + 1f) % 1f * 65536f), (short)MathF.Round(pitch / MathF.Tau * 65536f));
    }

    private static Vector3 Chest(Vector3 feet) => feet + new Vector3(0, 1.3f * 0.6f, 0);

    [Fact]
    public void Arrows_HurtMoreAtFullDraw()
    {
        var target = new Vector3(0, 0, -14);
        var (server, client, network) = StartArcher(target);
        var quick = Shoot(server, client, network, 4, Chest(target));
        var (fullServer, fullClient, fullNetwork) = StartArcher(target);
        var full = Shoot(fullServer, fullClient, fullNetwork, Tuning.FullDraw + 10, Chest(target));
        Assert.True(quick > 0, "a quick shot still hits");
        Assert.True(full > quick * 1.6f, $"a full draw ({full}) hits much harder than a quick shot ({quick})");
    }

    [Fact]
    public void Arrows_FlyWhereAimedWithNoHelp()
    {
        // Aimed straight at it, up a slope: the arrow hits.
        var high = new Vector3(3f, 4f, -18);
        var (server, client, network) = StartArcher(high);
        Assert.True(Shoot(server, client, network, Tuning.FullDraw, Chest(high)) > 0);

        // A little to the side of the aim: nothing bends the arrow onto it.
        var (wide, wideClient, wideNetwork) = StartArcher(new Vector3(2.5f, 0, -16));
        Assert.Equal(0, Shoot(wide, wideClient, wideNetwork, Tuning.FullDraw, Chest(new Vector3(0, 0, -16))));
    }

    [Fact]
    public void Arrows_AreSpentAtTheirRange()
    {
        // Even a full draw, aimed with the drop allowed for, does not reach this far.
        var far = new Vector3(0, 0, -(Tuning.ArrowRange(1f) + 8f));
        var (server, client, network) = StartArcher(far);
        Assert.Equal(0, Shoot(server, client, network, Tuning.FullDraw, Chest(far)));

        var near = new Vector3(0, 0, -(Tuning.ArrowRange(1f) - 6f));
        var (nearServer, nearClient, nearNetwork) = StartArcher(near);
        Assert.True(Shoot(nearServer, nearClient, nearNetwork, Tuning.FullDraw, Chest(near)) > 0);
        _output.WriteLine($"range: quick {Tuning.ArrowRange(Tuning.Drawn(Tuning.MinDraw)):0} m, full {Tuning.ArrowRange(1f):0} m");
    }

    [Fact]
    public void Bow_CannotBeSpammed()
    {
        // Clicking as fast as the input allows: every other tick down, for ten seconds.
        var world = new World(Array.Empty<Aabb>());
        var state = PlayerState.Spawn(Vector3.Zero);
        state.Weapon = Weapons.Bow;
        var shots = 0;
        for (var tick = 0; tick < Tuning.TickRate * 10; tick++)
        {
            state.Stamina = Tuning.MaxStamina;
            var input = new InputCmd { Tick = (uint)tick, Buttons = tick % 2 == 0 ? Buttons.Light : Buttons.None };
            if (Movement.Step(ref state, input, world) == AttackKind.BowShot)
            {
                shots++;
                Assert.True(state.ShotDraw >= Tuning.MinDraw, "no arrow leaves before the minimum draw");
            }
        }

        _output.WriteLine($"{shots / 10f:0.0} shots a second when clicking as fast as possible");
        Assert.InRange(shots, 10, 15);
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
    [Fact]
    public void TheWornWeapon_SurvivesDeath()
    {
        var network = new SimNetwork(80, 8, 2, 7);
        var world = new World(Array.Empty<Aabb>());
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 3, EnemyCount = 0 });
        var client = new ClientPrediction(network.Connect(1), world);
        var place = -1;
        for (var tick = 0; tick < Tuning.TickRate * 4; tick++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            if (tick == 30)
            {
                place = server.GiveGear(1, GearPiece.Roll(Item("goblin_shortbow"), Rarity.Common, 1, new Random(1)));
            }

            if (tick == 120)
            {
                server.HurtForTest(1, 10000);
            }

            client.Step(0, 0, Buttons.None, (byte)(tick / 30), tick == 40 ? GearCommands.Toggle(place) : Command.None);
        }

        Assert.Equal(Weapons.Bow, server.GetPlayer(1)!.Value.Weapon);
        Assert.Equal(Weapons.Bow, client.State.Weapon);
    }
    /// <summary>A player holding the item, with frozen enemies standing at the given spots relative to the player.</summary>
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) StartWith(string item, params Vector3[] enemies)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camps = enemies.Select(e => new EnemySpawn(e, 0.1f, 1, Types: new[] { "goblin_brute" })).ToArray();
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, camps);
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 3 });
        var client = new ClientPrediction(network.Connect(1), world);
        Run(server, client, network, 10);
        server.EnemiesFrozen = true;
        var feet = server.GetPlayer(1)!.Value.Position;
        for (var i = 0; i < enemies.Length; i++)
        {
            server.Enemies[i].Position = feet + enemies[i];
        }

        var place = server.GiveGear(1, new GearPiece(Item(item), Rarity.Common, 1, Array.Empty<AffixRoll>()));
        Run(server, client, network, 12, GearCommands.Toggle(place));
        return (server, client, network);
    }

    /// <summary>Presses a button once, aimed at a point (or straight ahead), and waits; returns each enemy's loss of health.</summary>
    private static int[] Attack(GameServer server, ClientPrediction client, SimNetwork network, Buttons button, Vector3? at = null)
    {
        var before = server.Enemies.Select(e => (int)e.Health).ToArray();
        var (yaw, pitch) = at is { } target ? AimAt(client.State.Position, client.State.Position + target, Tuning.FullDraw) : ((ushort)0, (short)0);
        for (var tick = 0; tick < 150; tick++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, tick == 0 ? button : Buttons.None, (byte)(yaw >> 8), Command.None, yaw, pitch);
        }

        return server.Enemies.Select((e, i) => before[i] - e.Health).ToArray();
    }

    [Fact]
    public void Club_SwingsInFrontAndSpinsAllAround()
    {
        var front = new Vector3(0, 0, -2f);
        var behind = new Vector3(0, 0, 2.2f);
        var (server, client, network) = StartWith("spiked_club", front, behind);
        Assert.Equal(Weapons.Club, client.State.Weapon);
        var swing = Attack(server, client, network, Buttons.Light);
        Assert.True(swing[0] > 0 && swing[1] == 0, $"the swing hits in front only ({swing[0]}, {swing[1]})");

        var (spinServer, spinClient, spinNetwork) = StartWith("spiked_club", front, behind);
        var spin = Attack(spinServer, spinClient, spinNetwork, Buttons.Heavy);
        Assert.True(spin[0] > 0 && spin[1] > 0, $"the spin hits all around ({spin[0]}, {spin[1]})");
    }

    [Fact]
    public void StaffFireball_FliesWhereAimedAndBursts()
    {
        var target = new Vector3(2f, 0, -16f);
        var (server, client, network) = StartWith("bone_staff", target);
        Assert.Equal(Weapons.Staff, client.State.Weapon);
        Assert.True(Attack(server, client, network, Buttons.Light, Chest(target))[0] > 0, "the fireball hits where it was aimed");

        var (wide, wideClient, wideNetwork) = StartWith("bone_staff", new Vector3(6f, 0, -16f));
        Assert.Equal(0, Attack(wide, wideClient, wideNetwork, Buttons.Light, Chest(new Vector3(0, 0, -16f)))[0]);

        var (far, farClient, farNetwork) = StartWith("bone_staff", new Vector3(0, 0, -(Tuning.StaffBoltRange + 6f)));
        Assert.Equal(0, Attack(far, farClient, farNetwork, Buttons.Light, Chest(new Vector3(0, 0, -(Tuning.StaffBoltRange + 6f))))[0]);
    }

    [Fact]
    public void StaffWall_BurnsAStraightStripAheadOnce()
    {
        var inLine = new Vector3(0.3f, 0, -6f);
        var beside = new Vector3(3.2f, 0, -6f);
        var beyond = new Vector3(0, 0, -(Tuning.StaffWallLength + 3f));
        var (server, client, network) = StartWith("bone_staff", inLine, beside, beyond);
        var burnt = Attack(server, client, network, Buttons.Heavy);
        Assert.True(burnt[0] > 0, "the enemy in the line burns");
        Assert.Equal(0, burnt[1]);
        Assert.Equal(0, burnt[2]);
        Assert.Equal(burnt[0], Attack(server, client, network, Buttons.None)[0] + burnt[0]);
    }

    [Theory]
    [InlineData("spiked_club")]
    [InlineData("bone_staff")]
    public void ClubAndStaff_ArePredictedWithoutCorrections(string item)
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

        var place = server.GiveGear(1, GearPiece.Roll(Item(item), Rarity.Common, 1, new Random(1)));
        for (var tick = 0; tick < Tuning.TickRate * 8; tick++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            var buttons = tick > 60 && tick % 80 == 0 ? Buttons.Heavy : tick % 37 == 0 ? Buttons.Light : Buttons.None;
            client.Step(40, 90, buttons, (byte)(tick / 40), tick == 0 ? GearCommands.Toggle(place) : Command.None);
        }

        Assert.True(client.Stats.Corrections <= 2, $"{client.Stats.Corrections} corrections with the {item}");
    }
}
