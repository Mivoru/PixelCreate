using System.Diagnostics;
using System.Numerics;
using PixelCreate.Core;
using PixelCreate.Transport;
using Xunit;
using Xunit.Abstractions;

namespace PixelCreate.NetTests;

/// <summary>Plays like a person who cannot sit still: walks, jumps, dashes and attacks at random.</summary>
public sealed class Bot
{
    private readonly Random _random;
    private sbyte _moveX;
    private sbyte _moveZ;
    private int _ticksLeft;

    public byte Aim { get; private set; }

    public Bot(int seed) => _random = new Random(seed);

    public (sbyte X, sbyte Z, Buttons Buttons) Next()
    {
        if (--_ticksLeft <= 0)
        {
            _ticksLeft = _random.Next(20, 90);
            _moveX = (sbyte)_random.Next(-127, 128);
            _moveZ = (sbyte)_random.Next(-127, 128);
            Aim = (byte)_random.Next(256);
        }

        var buttons = Buttons.None;
        if (_random.Next(40) == 0) buttons |= Buttons.Jump;
        if (_random.Next(70) == 0) buttons |= Buttons.Dash;
        if (_random.Next(30) == 0) buttons |= Buttons.Light;
        if (_random.Next(90) == 0) buttons |= Buttons.Heavy;
        return (_moveX, _moveZ, buttons);
    }
}

public sealed record SimResult(ClientStats[] Clients, ServerStats Server, float[] FinalGaps, long Sent, long Lost, int[] Buffered);

public sealed class NetSimTests
{
    private const int Seconds = 120;

    /// <summary>A dash moves 0.33 m per tick; this allows a fading correction on top of it.</summary>
    private const float MaxShownStep = 0.9f;
    private readonly ITestOutputHelper _output;

    public NetSimTests(ITestOutputHelper output) => _output = output;

    private SimResult Run(double rtt, double jitter, double loss, int enemies, int seed = 7, float drift = 1f, bool clockSync = false, World? map = null)
    {
        var world = map ?? World.Arena();
        var network = new SimNetwork(seed, rtt, jitter, loss);
        var server = new GameServer(network.Connect(0), world, new ServerOptions { EnemyCount = enemies, Seed = seed });
        var clients = new[] { new ClientPrediction(network.Connect(1), world), new ClientPrediction(network.Connect(2), world) };
        var bots = new[] { new Bot(seed + 1), new Bot(seed + 2) };
        var clocks = new float[clients.Length];

        for (var tick = 0; tick < Seconds * Tuning.TickRate; tick++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            for (var i = 0; i < clients.Length; i++)
            {
                // A client clock that runs at `drift` times the speed of the server clock.
                clocks[i] += drift;
                var length = clockSync ? clients[i].TickScale : 1f;
                while (clocks[i] >= length)
                {
                    clocks[i] -= length;
                    var (x, z, buttons) = bots[i].Next();
                    clients[i].Step(x, z, buttons, bots[i].Aim);
                }
            }
        }

        var buffered = clients.Select(c => (int)c.Latest!.BufferedInputs).ToArray();

        // Stand still until everything in flight has landed, then the two sides must agree.
        server.EnemiesFrozen = true;
        for (var tick = 0; tick < Tuning.TickRate * 2; tick++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            foreach (var client in clients)
            {
                client.Step(0, 0, Buttons.None);
            }
        }

        var gaps = clients.Select((c, i) => Vector3.Distance(c.State.Position, server.GetPlayer(i + 1)!.Value.Position)).ToArray();
        var result = new SimResult(clients.Select(c => c.Stats).ToArray(), server.Stats, gaps, network.Sent, network.Lost, buffered);
        foreach (var (stats, i) in result.Clients.Select((s, i) => (s, i)))
        {
            _output.WriteLine($"client {i}: corrections {stats.Corrections}, resimulated {stats.ResimulatedTicks}, " +
                              $"max error {stats.MaxError:F3} m, max snap {stats.MaxVisibleSnap:F3} m, max shown step {stats.MaxShownStep:F3} m, gap {gaps[i]:F4} m");
        }

        _output.WriteLine($"server: missed inputs {server.Stats.MissedInputs}, kills {server.Stats.EnemiesKilled}, " +
                          $"hits {server.Stats.PlayerHits}; packets {network.Sent}, lost {network.Lost}");
        return result;
    }

    [Fact]
    public void PerfectNetwork_NeverCorrects()
    {
        var result = Run(rtt: 80, jitter: 0, loss: 0, enemies: 0);
        Assert.All(result.Clients, c => Assert.Equal(0, c.Corrections));
        Assert.Equal(0, result.Server.MissedInputs);
        Assert.All(result.FinalGaps, gap => Assert.Equal(0f, gap));
    }

    [Fact]
    public void Target_80msRtt_2PercentLoss_NoEnemies_StaysInSync()
    {
        var result = Run(rtt: 80, jitter: 8, loss: 2, enemies: 0);
        Assert.InRange(result.Lost, 1, result.Sent / 20);
        Assert.All(result.Clients, c => Assert.Equal(0, c.Corrections));
        Assert.All(result.FinalGaps, gap => Assert.Equal(0f, gap));
    }

    [Fact]
    public void Target_80msRtt_2PercentLoss_WithEnemies_CorrectsAndConverges()
    {
        var result = Run(rtt: 80, jitter: 8, loss: 2, enemies: 4);
        Assert.True(result.Server.PlayerHits > 0, "Enemies must reach players, or nothing is tested.");
        Assert.True(result.Server.EnemyHits > 0, "Players must hit enemies, or nothing is tested.");
        Assert.All(result.Clients, c => Assert.True(c.Corrections > 0));
        Assert.All(result.Clients, c => Assert.InRange(c.MaxShownStep, 0f, MaxShownStep));
        Assert.All(result.FinalGaps, gap => Assert.InRange(gap, 0f, 0.001f));
    }

    [Fact]
    public void HeavyLoss_RecoversAfterMissedInputs()
    {
        var result = Run(rtt: 200, jitter: 30, loss: 20, enemies: 4);
        Assert.True(result.Server.MissedInputs > 0, "This network must be bad enough to lose inputs.");
        Assert.All(result.FinalGaps, gap => Assert.InRange(gap, 0f, 0.001f));
    }

    [Theory]
    [InlineData(1.005f)]
    [InlineData(0.995f)]
    public void ClockDrift_IsAbsorbedByClockSync(float drift)
    {
        var result = Run(rtt: 80, jitter: 8, loss: 2, enemies: 0, drift: drift, clockSync: true);
        _output.WriteLine($"buffered inputs at the end: {string.Join(", ", result.Buffered)}");
        Assert.All(result.Buffered, count => Assert.InRange(count, 1, 6));
        Assert.InRange(result.Server.MissedInputs, 0, 10);
    }

    [Fact]
    public void ClockDrift_WithoutClockSync_DelayGrows()
    {
        var result = Run(rtt: 80, jitter: 8, loss: 2, enemies: 0, drift: 1.005f);
        _output.WriteLine($"buffered inputs at the end: {string.Join(", ", result.Buffered)}");
        Assert.All(result.Buffered, count => Assert.True(count > 20));
    }

    [Fact]
    public void Zone_80msRtt_2PercentLoss_CorrectsAndConverges()
    {
        var result = Run(rtt: 80, jitter: 8, loss: 2, enemies: 4, map: Zone.Load("meadow-valley").World);
        Assert.True(result.Server.PlayerHits > 0, "Enemies must reach players, or nothing is tested.");
        Assert.All(result.Clients, c => Assert.InRange(c.MaxShownStep, 0f, MaxShownStep));
        Assert.All(result.FinalGaps, gap => Assert.InRange(gap, 0f, 0.001f));
    }

    [Fact]
    public void SameSeed_GivesSameRun()
    {
        var first = Run(rtt: 80, jitter: 8, loss: 2, enemies: 4);
        var second = Run(rtt: 80, jitter: 8, loss: 2, enemies: 4);
        Assert.Equal(first.Clients[0].Corrections, second.Clients[0].Corrections);
        Assert.Equal(first.Clients[1].MaxError, second.Clients[1].MaxError);
        Assert.Equal(first.Server.PlayerHits, second.Server.PlayerHits);
    }

    [Fact]
    public void RealUdp_TwoBotsStayInSync()
    {
        const int port = 27777;
        var world = World.Arena();
        using var host = UdpTransport.Listen(port);
        using var firstLink = UdpTransport.Connect("127.0.0.1", port);
        using var secondLink = UdpTransport.Connect("127.0.0.1", port);
        var server = new GameServer(host, world, new ServerOptions { EnemyCount = 0 });
        var clients = new[] { new ClientPrediction(firstLink, world), new ClientPrediction(secondLink, world) };
        var bots = new[] { new Bot(1), new Bot(2) };

        var connecting = Stopwatch.StartNew();
        while (!(firstLink.IsConnected && secondLink.IsConnected) && connecting.Elapsed.TotalSeconds < 5)
        {
            server.Step();
            firstLink.TryReceive(out _, out _);
            secondLink.TryReceive(out _, out _);
            Thread.Sleep(5);
        }

        Assert.True(firstLink.IsConnected && secondLink.IsConnected, "Clients did not connect.");
        for (var tick = 0; tick < Tuning.TickRate * 3; tick++)
        {
            server.Step();
            for (var i = 0; i < clients.Length; i++)
            {
                var (x, z, buttons) = bots[i].Next();
                clients[i].Step(x, z, buttons);
            }

            Thread.Sleep(16);
        }

        foreach (var client in clients)
        {
            _output.WriteLine($"udp client: snapshots {client.Stats.Snapshots}, corrections {client.Stats.Corrections}");
            Assert.True(client.Stats.Snapshots > 30, "Snapshots must arrive over real UDP.");
            Assert.NotNull(client.Latest);
        }
    }
}

public sealed class EnemyTests
{
    private static GameServer Server(SimNetwork network, Vector3 spawn) =>
        new(network.Connect(0), new World(Array.Empty<Aabb>(), null, spawn), new ServerOptions { EnemyCount = 12, Seed = 2 });

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start()
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var server = Server(network, Vector3.Zero);
        var client = new ClientPrediction(network.Connect(1), new World(Array.Empty<Aabb>()));
        return (server, client, network);
    }

    private static void Play(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Buttons buttons = Buttons.None)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, buttons);
        }
    }

    [Fact]
    public void Definitions_LoadBothSlimes()
    {
        Assert.Contains(EnemyDef.All, d => d.Id == "slime");
        var rare = EnemyDef.All.Single(d => d.Id == "rare_slime");
        var slime = EnemyDef.All.Single(d => d.Id == "slime");
        Assert.True(rare.Size < slime.Size);
        Assert.True(rare.Speed > slime.Speed);
        Assert.True(rare.SpitRange > 0 && slime.SpitRange == 0);
    }

    [Fact]
    public void Slimes_LeapSplashAndSlow()
    {
        var (server, client, network) = Start();
        var slowed = false;
        for (var i = 0; i < 60 * 60 && !slowed; i++)
        {
            Play(server, client, network, 1);
            slowed = server.GetPlayer(1)?.SlowTicks > 0;
        }

        Assert.True(server.Stats.Splashes > 0, "Slimes must leap and splash.");
        Assert.True(slowed, "A splash must slow the player.");
        Assert.True(server.GetPlayer(1)!.Value.Health < Tuning.PlayerHealth);
    }

    [Fact]
    public void RareSlimes_Spit()
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var server = new GameServer(network.Connect(0), new World(Array.Empty<Aabb>()), new ServerOptions { EnemyCount = 40, Seed = 3 });
        var client = new ClientPrediction(network.Connect(1), new World(Array.Empty<Aabb>()));
        Assert.Contains(server.Enemies, e => e.Def.Id == "rare_slime");
        Play(server, client, network, 60 * 30);
        Assert.True(server.Stats.Spits > 0, "Rare slimes must spit.");
    }

    [Fact]
    public void PlayerAttack_HitsEnemiesInFrontOnly()
    {
        var (server, client, network) = Start();
        Play(server, client, network, 60);
        var enemy = server.Enemies[0];
        var player = server.GetPlayer(1)!.Value;
        server.EnemiesFrozen = true;

        enemy.Position = player.Position + new Vector3(0, 0, 2);
        var health = enemy.Health;
        Play(server, client, network, 30, Buttons.Light);
        Assert.Equal(health, enemy.Health);

        enemy.Position = player.Position + new Vector3(0, 0, -2);
        Play(server, client, network, 30, Buttons.Light);
        Assert.True(enemy.Health < health);
    }

    [Fact]
    public void Enemies_DieAndComeBack()
    {
        var (server, client, network) = Start();
        Play(server, client, network, 5);
        var enemy = server.Enemies[0];
        enemy.TakeHit(short.MaxValue, Vector3.Zero);
        Assert.False(enemy.Alive);
        Play(server, client, network, 300);
        Assert.True(enemy.Alive);
        Assert.Equal(enemy.MaxHealth, enemy.Health);
    }

    [Theory]
    [InlineData(1, 2, Threat.Weak)]
    [InlineData(2, 2, Threat.Even)]
    [InlineData(3, 2, Threat.Strong)]
    public void Threat_FollowsLevels(int enemy, int player, Threat expected)
    {
        Assert.Equal(expected, ThreatRules.Compare(enemy, player));
    }
}

public sealed class ZoneTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    private static PlayerState Walk(World world, Vector3 start, sbyte x, sbyte z, int ticks)
    {
        var state = PlayerState.Spawn(start with { Y = world.GroundHeight(start.X, start.Z) });
        var input = new InputCmd { MoveX = x, MoveZ = z };
        for (var i = 0; i < ticks; i++)
        {
            Movement.Step(ref state, input, world);
        }

        return state;
    }

    [Fact]
    public void Loading_IsDeterministic()
    {
        var other = Zone.Load("meadow-valley");
        Assert.Equal(Valley.Props, other.Props);
        Assert.Equal(Valley.World.GroundHeight(13.3f, -41.2f), other.World.GroundHeight(13.3f, -41.2f));
        Assert.True(Valley.Props.Count > 50, "The zone must have generated detail.");
    }

    [Fact]
    public void Walking_FollowsTheTerrain()
    {
        var hills = new HeightField(40f, 40, (x, z) => x * 0.5f);
        var world = new World(Array.Empty<Aabb>(), hills);
        var state = Walk(world, new Vector3(-10, 0, 0), 127, 0, 60);
        Assert.True(state.Position.X > -5f, "The player must climb a gentle slope.");
        Assert.Equal(world.GroundHeight(state.Position.X, state.Position.Z), state.Position.Y, 2);
        Assert.True(state.Grounded);

        var back = Walk(world, new Vector3(10, 0, 0), -127, 0, 60);
        Assert.Equal(world.GroundHeight(back.Position.X, back.Position.Z), back.Position.Y, 1);
    }

    [Fact]
    public void Cliff_BlocksWalking()
    {
        var cliff = new HeightField(40f, 80, (x, z) => x > 5f ? (x - 5f) * 4f : 0f);
        var world = new World(Array.Empty<Aabb>(), cliff);
        var state = Walk(world, Vector3.Zero, 127, 0, 180);
        Assert.InRange(state.Position.X, 3f, 6.5f);
    }

    [Fact]
    public void Props_BlockWalking()
    {
        var cottage = Valley.Props.First(p => p.Model == "cottage");
        var state = Walk(Valley.World, cottage.Position + new Vector3(0, 0, 12), 0, -127, 240);
        Assert.True(state.Position.Z > cottage.Position.Z + 2f, "The player must stop at the wall.");
    }

    [Fact]
    public void Edge_KeepsThePlayerInTheZone()
    {
        var state = Walk(Valley.World, Valley.World.Spawn, 127, 0, 60 * 30);
        Assert.True(state.Position.X < Valley.Size / 2);
    }

    [Fact]
    public void Bots_NeverTeleport()
    {
        var world = Valley.World;
        for (var seed = 0; seed < 8; seed++)
        {
            var bot = new Bot(seed);
            var state = PlayerState.Spawn(world.Spawn);
            for (var tick = 0; tick < 60 * 300; tick++)
            {
                var before = state;
                var (x, z, buttons) = bot.Next();
                var input = new InputCmd { MoveX = x, MoveZ = z, Buttons = buttons };
                Movement.Step(ref state, input, world);
                var step = Vector3.Distance(before.Position, state.Position);
                Assert.True(step < 0.6f, $"seed {seed} tick {tick}: moved {step:F2} m from {before.Position} to {state.Position}");
            }
        }
    }

    [Fact]
    public void StartingInsideABox_DoesNotThrowThePlayerOut()
    {
        var world = new World(new[] { new Aabb(new Vector3(-1, 0, -1), new Vector3(1, 2, 1)) });
        var state = PlayerState.Spawn(new Vector3(0, 0.5f, 0));
        Movement.Step(ref state, new InputCmd { MoveX = 127 }, world);
        Assert.InRange(state.Position.X, 0f, 0.2f);
    }

    [Fact]
    public void Spawn_IsFree()
    {
        var spawn = Valley.World.Spawn;
        Assert.False(Valley.World.IsBlocked(spawn + new Vector3(0, 1, 0)));
    }
}

public sealed class MovementTests
{
    private static readonly World Arena = World.Arena();

    private static PlayerState Run(PlayerState state, InputCmd input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            Movement.Step(ref state, input, Arena);
        }

        return state;
    }

    [Fact]
    public void Walking_ReachesMoveSpeed()
    {
        var state = Run(PlayerState.Spawn(Vector3.Zero), new InputCmd { MoveX = 127 }, 30);
        Assert.Equal(Tuning.MoveSpeed, state.Velocity.X, 2);
    }

    [Fact]
    public void Stamina_RegeneratesAfterADelay()
    {
        var state = PlayerState.Spawn(Vector3.Zero);
        state.Stamina = 10f;
        state.StaminaDelay = 5;
        state = Run(state, new InputCmd(), 3);
        Assert.Equal(10f, state.Stamina);
        state = Run(state, new InputCmd(), 60);
        Assert.True(state.Stamina > 20f);
    }

    [Fact]
    public void Dash_NeedsStamina()
    {
        var state = PlayerState.Spawn(Vector3.Zero);
        state.Stamina = Tuning.DashStamina - 1f;
        state = Run(state, new InputCmd { MoveX = 127, Buttons = Buttons.Dash }, 1);
        Assert.Equal(0, state.DashTicks);
    }

    [Theory]
    [InlineData(Buttons.Light, AttackKind.Light)]
    [InlineData(Buttons.Heavy, AttackKind.Heavy)]
    public void Attack_LandsOnItsHitTick(Buttons button, AttackKind kind)
    {
        var state = PlayerState.Spawn(Vector3.Zero);
        var landedAt = -1;
        for (var tick = 0; tick < 60; tick++)
        {
            var input = new InputCmd { Buttons = tick == 0 ? button : Buttons.None };
            if (Movement.Step(ref state, input, Arena) == kind)
            {
                landedAt = tick;
            }
        }

        Assert.Equal(Tuning.Attack(kind).HitAt, landedAt);
        Assert.Equal(AttackKind.None, state.Attack);
    }

    [Fact]
    public void LightAttack_ChainsIntoHeavyDuringRecovery()
    {
        var state = PlayerState.Spawn(Vector3.Zero);
        state = Run(state, new InputCmd { Buttons = Buttons.Light }, 1);
        state = Run(state, new InputCmd(), Tuning.Attack(AttackKind.Light).HitAt + 6);
        state = Run(state, new InputCmd { Buttons = Buttons.Heavy }, 1);
        Assert.Equal(AttackKind.Heavy, state.Attack);
    }

    [Fact]
    public void Attack_CannotInterruptItsOwnWindup()
    {
        var state = PlayerState.Spawn(Vector3.Zero);
        state = Run(state, new InputCmd { Buttons = Buttons.Heavy }, 1);
        state = Run(state, new InputCmd { Buttons = Buttons.Light }, 3);
        Assert.Equal(AttackKind.Heavy, state.Attack);
    }

    [Fact]
    public void Slow_HalvesSpeed()
    {
        var state = PlayerState.Spawn(Vector3.Zero);
        state.SlowTicks = 200;
        state = Run(state, new InputCmd { MoveX = 127 }, 60);
        Assert.Equal(Tuning.MoveSpeed * Tuning.SlowFactor, state.Velocity.X, 2);
    }

    [Fact]
    public void Walls_StopThePlayer()
    {
        var state = Run(PlayerState.Spawn(Vector3.Zero), new InputCmd { MoveX = 127 }, 60 * 12);
        Assert.Equal(20f - Tuning.PlayerHalfSize.X, state.Position.X, 3);
    }

    [Fact]
    public void Pillar_BlocksADash()
    {
        var state = PlayerState.Spawn(new Vector3(0, 0, -8));
        state = Run(state, new InputCmd { MoveX = 127, Buttons = Buttons.Dash }, 30);
        Assert.True(state.Position.X <= 6.5f - Tuning.PlayerHalfSize.X + 0.001f);
    }

    [Fact]
    public void Jump_LeavesAndReturnsToTheGround()
    {
        var state = PlayerState.Spawn(Vector3.Zero);
        state = Run(state, new InputCmd { Buttons = Buttons.Jump }, 1);
        Assert.True(state.Position.Y > 0);
        state = Run(state, new InputCmd(), 60);
        Assert.Equal(0f, state.Position.Y);
        Assert.True(state.Grounded);
    }

    [Fact]
    public void Dash_IsFasterThanWalkingAndHasACooldown()
    {
        var state = PlayerState.Spawn(Vector3.Zero);
        state = Run(state, new InputCmd { MoveX = 127, Buttons = Buttons.Dash }, 2);
        Assert.Equal(Tuning.DashSpeed, state.Velocity.X, 2);
        Assert.True(state.DashCooldown > 0);
    }

    [Fact]
    public void Snapshot_SurvivesEncoding()
    {
        var snapshot = new Snapshot
        {
            ServerTick = 9,
            YourId = 2,
            AckTick = 5,
            Self = PlayerState.Spawn(new Vector3(1, 2, 3)),
            Others = new[] { new RemotePlayer(1, Vector3.One, Vector3.UnitX, 9, AttackKind.Heavy, 3) },
            Enemies = new[] { new EnemyView(4, 1, 3, Vector3.UnitZ, 1.5f, 30, 40, EnemyAction.Leap, 2) },
            Spits = new[] { new SpitView(7, Vector3.UnitY) },
        };
        Assert.True(Protocol.TryDecodeSnapshot(Protocol.EncodeSnapshot(snapshot), out var decoded));
        Assert.Equal(snapshot.Self, decoded.Self);
        Assert.Equal(snapshot.Others, decoded.Others);
        Assert.Equal(snapshot.Enemies, decoded.Enemies);
        Assert.Equal(snapshot.Spits, decoded.Spits);
    }
}
