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

    public Bot(int seed) => _random = new Random(seed);

    public (sbyte X, sbyte Z, Buttons Buttons) Next()
    {
        if (--_ticksLeft <= 0)
        {
            _ticksLeft = _random.Next(20, 90);
            _moveX = (sbyte)_random.Next(-127, 128);
            _moveZ = (sbyte)_random.Next(-127, 128);
        }

        var buttons = Buttons.None;
        if (_random.Next(40) == 0) buttons |= Buttons.Jump;
        if (_random.Next(70) == 0) buttons |= Buttons.Dash;
        if (_random.Next(30) == 0) buttons |= Buttons.Attack;
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

    private SimResult Run(double rtt, double jitter, double loss, int enemies, int seed = 7, float drift = 1f, bool clockSync = false)
    {
        var world = World.Arena();
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
                    clients[i].Step(x, z, buttons);
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
        Assert.True(result.Server.EnemiesKilled > 0, "Players must kill enemies, or nothing is tested.");
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
    public void Walls_StopThePlayer()
    {
        var state = Run(PlayerState.Spawn(Vector3.Zero), new InputCmd { MoveX = 127 }, 60 * 10);
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
            Others = new[] { new RemotePlayer(1, Vector3.One) },
            Enemies = new[] { new EnemyView(4, Vector3.UnitZ, 30) },
        };
        Assert.True(Protocol.TryDecodeSnapshot(Protocol.EncodeSnapshot(snapshot), out var decoded));
        Assert.Equal(snapshot.Self, decoded.Self);
        Assert.Equal(snapshot.Others, decoded.Others);
        Assert.Equal(snapshot.Enemies, decoded.Enemies);
    }
}
