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
    public void ClockDrift_WithoutClockSync_DelayStaysBounded()
    {
        var result = Run(rtt: 80, jitter: 8, loss: 2, enemies: 0, drift: 1.005f);
        _output.WriteLine($"buffered inputs at the end: {string.Join(", ", result.Buffered)}");
        Assert.All(result.Buffered, count => Assert.InRange(count, 0, 26));
    }

    [Fact]
    public void Zone_80msRtt_2PercentLoss_CorrectsAndConverges()
    {
        var result = Run(rtt: 80, jitter: 8, loss: 2, enemies: 4, map: Zone.Load("meadow-valley").World);
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
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { new EnemySpawn(new Vector3(0, 0, 9), 2f, 3, 1, 1, 900, true) });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 3 });
        var client = new ClientPrediction(network.Connect(1), world);
        Assert.All(server.Enemies, e => Assert.Equal("rare_slime", e.Def.Id));
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

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) StartCamp(EnemySpawn camp)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 5 });
        var client = new ClientPrediction(network.Connect(1), world);
        return (server, client, network);
    }

    [Fact]
    public void Kills_GiveXpAndLevelsRaiseHealthAndDamage()
    {
        var (server, client, network) = Start();
        Play(server, client, network, 30);
        Assert.Equal(1, server.GetLevel(1));

        server.AwardXp(1, Progression.XpToNext(1) + 5);
        Assert.Equal(2, server.GetLevel(1));
        Assert.Equal(5, server.GetXp(1));
        Assert.Equal(Progression.MaxHealth(2), server.GetPlayer(1)!.Value.Health);
        Assert.True(Progression.MaxHealth(2) > Progression.MaxHealth(1));
        Assert.True(Progression.DamageScale(2) > Progression.DamageScale(1));

        Play(server, client, network, 10);
        Assert.Equal(2, client.Latest!.Level);
    }

    [Fact]
    public void KillXp_FallsForEnemiesFarBelowThePlayer()
    {
        Assert.True(Progression.KillXp(5, 5, false) > Progression.KillXp(1, 5, false));
        Assert.True(Progression.KillXp(5, 5, true) > Progression.KillXp(5, 5, false));
        Assert.True(Progression.KillXp(1, 60, false) > 0);
    }

    [Fact]
    public void KillingBlow_GivesXpToThePlayer()
    {
        var (server, client, network) = Start();
        Play(server, client, network, 60);
        server.EnemiesFrozen = true;
        var enemy = server.Enemies[0];
        enemy.Health = 1;
        enemy.Position = server.GetPlayer(1)!.Value.Position + new Vector3(0, 0, -2);
        Play(server, client, network, 30, Buttons.Light);
        Assert.False(enemy.Alive);
        Assert.True(server.GetXp(1) > 0 || server.GetLevel(1) > 1);
    }

    [Fact]
    public void PulledEnemy_LeashesHomeAndHeals()
    {
        var camp = new EnemySpawn(new Vector3(100, 0, 0), 5f, 1, 1, 1);
        var (server, client, network) = StartCamp(camp);
        Play(server, client, network, 30);
        var enemy = server.Enemies[0];
        enemy.TakeHit(5, Vector3.Zero, 1);
        enemy.Position = new Vector3(40, 0, 0);
        Play(server, client, network, 60 * 60);
        Assert.False(enemy.Returning);
        Assert.Equal(-1, enemy.TargetId);
        Assert.Equal(enemy.MaxHealth, enemy.Health);
        Assert.True(Vector3.Distance(enemy.Position, camp.Center) < camp.Radius + 3f);
    }

    [Fact]
    public void EnemyAtHome_HealsSlowly()
    {
        var camp = new EnemySpawn(new Vector3(100, 0, 0), 5f, 1, 1, 1);
        var (server, client, network) = StartCamp(camp);
        Play(server, client, network, 5);
        var enemy = server.Enemies[0];
        enemy.Health = 1;
        Play(server, client, network, 60 * 5);
        Assert.InRange(enemy.Health, 2, enemy.MaxHealth / 2);
    }

    [Fact]
    public void AggroRange_DoesNotReachAVillage()
    {
        var zone = Zone.Load("meadow-valley");
        foreach (var camp in zone.World.EnemySpawns)
        {
            var edge = Vector3.Distance(camp.Center with { Y = 0 }, new Vector3(2, 0, 4)) - camp.Radius;
            Assert.True(edge > 22f, $"The camp at {camp.Center} is too close to the village.");
        }
    }

    [Fact]
    public void HittingOneSlime_PullsItsNeighbours()
    {
        var camp = new EnemySpawn(new Vector3(0, 0, 60), 3f, 4, 1, 1);
        var (server, client, network) = StartCamp(camp);
        Play(server, client, network, 30);
        var player = server.GetPlayer(1)!.Value;
        server.EnemiesFrozen = true;
        server.Enemies[0].Position = player.Position + new Vector3(0, 0, -2);
        foreach (var other in server.Enemies.Skip(1))
        {
            other.Position = player.Position + new Vector3(3, 0, -3);
        }

        Play(server, client, network, 30, Buttons.Light);
        Assert.All(server.Enemies, e => Assert.Equal(1, e.TargetId));
    }

    [Fact]
    public void RareCamp_HoldsARareSlime()
    {
        var (server, _, _) = StartCamp(new EnemySpawn(new Vector3(50, 0, 0), 4f, 1, 4, 4, 900, true));
        Assert.Equal("rare_slime", server.Enemies[0].Def.Id);
        Assert.Equal(5, server.Enemies[0].Level);
    }

    [Fact]
    public void Dash_AvoidsASplash()
    {
        var (server, client, network) = Start();
        Play(server, client, network, 30);
        var enemy = server.Enemies[0];
        enemy.Position = server.GetPlayer(1)!.Value.Position;
        var health = server.GetPlayer(1)!.Value.Health;
        server.EnemiesFrozen = true;

        for (var i = 0; i < 6; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(127, 0, Buttons.Dash);
        }

        Assert.True(server.GetPlayer(1)!.Value.DashTicks > 0);
        ((IEnemyEvents)server).Splash(enemy);
        Assert.Equal(health, server.GetPlayer(1)!.Value.Health);
        Assert.Equal(1, server.Stats.Dodges);
    }

    [Fact]
    public void ManyEnemies_FitInOnePacket()
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { new EnemySpawn(Vector3.Zero, 20f, 60) });
        var server = new GameServer(network.Connect(0), world, new ServerOptions());
        var clients = new[] { new ClientPrediction(network.Connect(1), world), new ClientPrediction(network.Connect(2), world) };
        for (var i = 0; i < 300; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            foreach (var client in clients)
            {
                client.Step(0, 0, Buttons.None);
            }
        }

        Assert.All(clients, c => Assert.InRange(c.Latest!.Enemies.Length, 1, 16));
    }

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) StartGoblin(string id, float distance)
    {
        var camp = new EnemySpawn(new Vector3(0, 0, distance), 1f, 1, Types: new[] { id });
        return StartCamp(camp);
    }

    [Theory]
    [InlineData("goblin_skirmisher", 6f)]
    [InlineData("goblin_brute", 6f)]
    [InlineData("goblin_rapid_archer", 12f)]
    [InlineData("goblin_sharpshooter", 15f)]
    [InlineData("goblin_shaman", 13f)]
    public void Goblins_HurtTheirTarget(string id, float distance)
    {
        var (server, client, network) = StartGoblin(id, distance);
        Assert.Equal(id, server.Enemies[0].Def.Id);
        Play(server, client, network, 60 * 30);
        Assert.True(server.Stats.PlayerHits > 0, $"{id} must hit a player who stands still.");
    }

    [Fact]
    public void Volley_FiresThreeArrowsThatSpreadApart()
    {
        var (server, client, network) = StartGoblin("goblin_rapid_archer", 10f);
        var seen = 0;
        for (var i = 0; i < 60 * 30 && seen < 3; i++)
        {
            Play(server, client, network, 1);
            seen = Math.Max(seen, server.ProjectileCount);
        }

        Assert.True(seen >= 3, "The archer must loose its three arrow volley.");
    }

    [Fact]
    public void Powershot_PassesThroughWalls()
    {
        var wall = new Aabb(new Vector3(-6, 0, 8), new Vector3(6, 6, 9));
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, 16), 1f, 1, Types: new[] { "goblin_sharpshooter" });
        var world = new World(new[] { wall }, null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 9 });
        var client = new ClientPrediction(network.Connect(1), world);
        Play(server, client, network, 60 * 40);
        Assert.True(server.Stats.PlayerHits > 0, "A powershot must reach a player behind a wall.");
    }

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network, Enemy Shaman) CastWall()
    {
        var (server, client, network) = StartGoblin("goblin_shaman", 12f);
        Play(server, client, network, 30);
        var shaman = server.Enemies[0];
        shaman.Position = server.GetPlayer(1)!.Value.Position + new Vector3(0, 0, 8);
        return (server, client, network, shaman);
    }

    private static void Fire(GameServer server, Enemy shaman, Vector3 at)
    {
        var ability = shaman.Def.Abilities!.Single(a => a.Effect == "firewall");
        shaman.LeapTarget = at;
        ((IEnemyEvents)server).Fire(shaman, ability, new Target(1, at, Vector3.Zero));
        shaman.TakeHit(short.MaxValue, Vector3.Zero);
    }

    [Fact]
    public void Warning_FollowsThePlayerThenLocks()
    {
        var (server, client, network) = StartGoblin("goblin_shaman", 12f);
        var enemy = server.Enemies[0];
        for (var i = 0; i < 60 * 20 && enemy.Action != EnemyAction.Cast; i++)
        {
            Play(server, client, network, 1);
        }

        Assert.Equal(EnemyAction.Cast, enemy.Action);
        for (var i = 0; i < 400 && enemy.ActionTicks > Enemy.AimLockTicks + 1; i++)
        {
            MoveAway(server, client, network);
        }

        var player = server.GetPlayer(1)!.Value.Position;
        Assert.True(Vector3.Distance(enemy.LeapTarget with { Y = 0 }, player with { Y = 0 }) < 1.5f, "The warning must follow the player.");
        var locked = enemy.LeapTarget;
        for (var i = 0; i < 8; i++)
        {
            MoveAway(server, client, network);
        }

        Assert.Equal(locked, enemy.LeapTarget);
    }

    private static void MoveAway(GameServer server, ClientPrediction client, SimNetwork network)
    {
        network.Advance(1000.0 / Tuning.TickRate);
        server.Step();
        client.Step(127, 0, Buttons.None);
    }

    [Fact]
    public void Goblin_WalksAroundALongWall()
    {
        var wall = new Aabb(new Vector3(-7, 0, 4), new Vector3(7, 3, 5));
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, 9), 1f, 1, Types: new[] { "goblin_skirmisher" });
        var world = new World(new[] { wall }, null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 4 });
        var client = new ClientPrediction(network.Connect(1), world);
        Play(server, client, network, 60 * 30);
        Assert.True(server.Stats.PlayerHits > 0, "The goblin must find its way around the wall.");
    }

    [Fact]
    public void TrappedEnemy_GivesUpAndGoesHome()
    {
        var boxes = new[]
        {
            new Aabb(new Vector3(-3, 0, 2), new Vector3(3, 3, 3)),
            new Aabb(new Vector3(-3, 0, 7), new Vector3(3, 3, 8)),
            new Aabb(new Vector3(-3, 0, 2), new Vector3(-2, 3, 8)),
            new Aabb(new Vector3(2, 0, 2), new Vector3(3, 3, 8)),
        };
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(30, 0, 30), 1f, 1, Types: new[] { "goblin_skirmisher" });
        var world = new World(boxes, null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 4 });
        var client = new ClientPrediction(network.Connect(1), world);
        Play(server, client, network, 30);
        var enemy = server.Enemies[0];
        enemy.Position = new Vector3(0, 0, 5);
        enemy.TargetId = 1;
        Play(server, client, network, 60 * 30);
        Assert.True(Vector3.Distance(enemy.Position, camp.Center) < 8f, "A trapped enemy must return home.");
    }

    [Fact]
    public void FireWall_BurnsTheWholeVOnce()
    {
        var (server, client, network, shaman) = CastWall();
        Fire(server, shaman, server.GetPlayer(1)!.Value.Position);
        Assert.Equal(1, server.FireWallCount);
        var hits = server.Stats.PlayerHits;
        Play(server, client, network, 200);
        Assert.Equal(hits + 1, server.Stats.PlayerHits);
        Assert.Equal(0, server.FireWallCount);
    }

    [Fact]
    public void FireWall_MissesAPlayerOutsideTheV()
    {
        var (server, client, network, shaman) = CastWall();
        var player = server.GetPlayer(1)!.Value.Position;
        var away = Vector3.Normalize(player - shaman.Position);
        Fire(server, shaman, shaman.Position + Vector3.Transform(away, Matrix4x4.CreateRotationY(1.4f)) * 8f);
        var hits = server.Stats.PlayerHits;
        Play(server, client, network, 200);
        Assert.Equal(hits, server.Stats.PlayerHits);
    }

    /// <summary>An orc of the given type stands this far in front of the player and uses one ability at once, then falls.</summary>
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network, long Hits) OrcFires(string id, string effect, float distance, float turn = 0f)
    {
        var (server, client, network) = StartGoblin(id, 12f);
        Play(server, client, network, 30);
        var orc = server.Enemies[0];
        var player = server.GetPlayer(1)!.Value.Position;
        orc.Position = player + new Vector3(0, 0, distance);
        var at = orc.Position + Vector3.Transform(player - orc.Position, Matrix4x4.CreateRotationY(turn));
        orc.LeapTarget = at;
        orc.Facing = MathF.Atan2(-(at - orc.Position).X, -(at - orc.Position).Z);
        var hits = server.Stats.PlayerHits;
        ((IEnemyEvents)server).Fire(orc, orc.Def.Abilities!.Single(a => a.Effect == effect), new Target(1, at, Vector3.Zero));
        orc.TakeHit(short.MaxValue, Vector3.Zero);
        return (server, client, network, hits);
    }

    [Theory]
    [InlineData("orc_warrior", 4f, 1)]
    [InlineData("orc_huntress", 5.5f, 1)]
    [InlineData("orc_warrior", 9f, 0)]
    public void AirSlash_FliesOnlyAFewMetres(string id, float distance, int expected)
    {
        var (server, client, network, hits) = OrcFires(id, "airSlash", distance);
        Play(server, client, network, 120);
        Assert.Equal(hits + expected, server.Stats.PlayerHits);
        Assert.Equal(0, server.ProjectileCount);
    }

    [Fact]
    public void SpearThrow_HitsAPlayerFarAway()
    {
        var (server, client, network, hits) = OrcFires("orc_spearwoman", "spearThrow", 11f);
        Play(server, client, network, 120);
        Assert.Equal(hits + 1, server.Stats.PlayerHits);
    }

    [Fact]
    public void EarthCrack_HitsInsideTheSmallVOnce()
    {
        var (server, client, network, hits) = OrcFires("orc_berserker", "earthCrack", 4f);
        Assert.Equal(1, server.FireWallCount);
        Play(server, client, network, 120);
        Assert.Equal(hits + 1, server.Stats.PlayerHits);
        Assert.Equal(0, server.FireWallCount);
    }

    [Theory]
    [InlineData(4f, 0.9f)]
    [InlineData(9f, 0f)]
    public void EarthCrack_MissesOutsideTheV(float distance, float turn)
    {
        var (server, client, network, hits) = OrcFires("orc_berserker", "earthCrack", distance, turn);
        Play(server, client, network, 120);
        Assert.Equal(hits, server.Stats.PlayerHits);
    }

    [Fact]
    public void Kills_DropCrystalsThatThePlayerLoots()
    {
        var (server, client, network) = Start();
        Play(server, client, network, 60);
        server.EnemiesFrozen = true;
        var enemy = server.Enemies[0];
        enemy.Health = 1;
        // Close enough that whatever it drops lands within reach of the player.
        enemy.Position = server.GetPlayer(1)!.Value.Position + new Vector3(0, 0, -1.2f);
        Play(server, client, network, 40, Buttons.Light);
        Assert.True(server.Stats.CrystalsDropped > 0, "Every slime drops a crystal.");
        Assert.Equal(server.Stats.CrystalsDropped, server.Stats.CrystalsLooted);
        Assert.True(server.GetCrystals(1, CrystalRarity.Common) > 0);
        Assert.True(client.Latest!.Crystals[0] > 0);
    }

    [Fact]
    public void Kills_DropLootThatReachesTheClientBag()
    {
        var (server, client, network) = Start();
        Play(server, client, network, 60);
        server.EnemiesFrozen = true;
        for (var round = 0; round < 6; round++)
        {
            var enemy = server.Enemies.First(e => e.Alive);
            enemy.Health = 1;
            enemy.Position = server.GetPlayer(1)!.Value.Position + new Vector3(0, 0, -2);
            Play(server, client, network, 40, Buttons.Light);
        }

        Play(server, client, network, 30);
        Assert.True(server.Stats.ItemsDropped > 0, "Slimes drop gel most of the time.");
        Assert.True(server.Stats.ItemsLooted > 0, "Loot near the player is picked up by walking over it.");
        Assert.Equal(server.Stats.ItemsDropped, server.Stats.ItemsLooted + server.LootOnGround - server.CrystalsOnGround);
        var gel = ItemDef.Index("slime_gel");
        Assert.Contains(client.Latest!.Bag, e => e.Item == gel && e.Count == server.GetItem(1, "slime_gel"));
    }

    [Fact]
    public void LootTables_NameRealItemsWithIcons()
    {
        var icons = Path.Combine(FindRoot(), "client", "assets", "ui", "icons");
        foreach (var def in EnemyDef.All)
        {
            foreach (var entry in def.Loot ?? Array.Empty<LootEntry>())
            {
                var item = ItemDef.All[ItemDef.Index(entry.Item)];
                Assert.InRange(entry.Chance, 0.0001, 1);
                Assert.InRange(entry.Min, 1, entry.Max);
                Assert.False(item.IsCrystal, $"{def.Id}: crystals drop through 'drops', not 'loot'.");
            }
        }

        Assert.All(ItemDef.All, item => Assert.True(File.Exists(Path.Combine(icons, item.Icon + ".png")), $"No icon for {item.Id}"));
        Assert.All(ItemDef.All.Where(i => i.IsFood), food => Assert.True(food.Heal > 0));
        Assert.All(ItemDef.All.Where(i => i.IsEquipment), gear => Assert.NotEmpty(gear.Slot));
        Assert.True(ItemCommands.EatBase + ItemDef.All.Count <= byte.MaxValue, "Eat commands must fit one byte.");
    }

    private static string FindRoot()
    {
        var folder = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(folder, "world", "items.json")))
        {
            folder = Path.GetDirectoryName(folder)!;
        }

        return folder;
    }

    [Fact]
    public void Crystals_HaveFourTiersAndGetHeavier()
    {
        Assert.Equal(new[] { "Common", "Rare", "Epic", "Legendary" }, Crystals.All.Select(c => c.Rarity.ToString()));
        Assert.True(Crystals.All.Zip(Crystals.All.Skip(1)).All(p => p.First.Weight < p.Second.Weight));
        Assert.All(EnemyDef.All.Where(d => d.Id != Guild.ExaminerId && d.Id != "shadow_clone" && d.Temper != "passive"), def => Assert.NotEmpty(def.Drops!));
    }

    [Fact]
    public void Slimes_SteerAroundObstacles()
    {
        var wall = new Aabb(new Vector3(-2, 0, 3), new Vector3(2, 3, 4));
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, 7.5f), 1f, 1, 1, 1);
        var world = new World(new[] { wall }, null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 8 });
        var client = new ClientPrediction(network.Connect(1), world);
        Play(server, client, network, 60 * 20);
        Assert.True(server.Stats.PlayerHits > 0 || server.Enemies[0].Position.Z < 4f, "The slime must get around the wall.");
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

public sealed class GuildTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network, World World) Start()
    {
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { new EnemySpawn(new Vector3(200, 0, 0), 1f, 1) })
        {
            Npcs = new[]
            {
                new Npc("mira", "Mira", "receptionist", new Vector3(0, 0, 1), 0),
                new Npc("board", "Quest Board", "quest_board", new Vector3(1, 0, 0), 0),
                new Npc("hollis", "Hollis", "appraiser", new Vector3(-1, 0, 0), 0),
            },
            Portals = new[] { new Portal(new Vector3(-1.5f, 0, 2.6f), new Vector3(50, 0, 50)) },
            TrialGround = new Vector3(0, 0, 40),
        };
        var network = new SimNetwork(1, 0, 0, 0);
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 2, SpawnOverride = Vector3.Zero });
        var client = new ClientPrediction(network.Connect(1), world);
        Run(server, client, network, 20);
        return (server, client, network, world);
    }

    private static void Run(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Command command = Command.None, Buttons buttons = Buttons.None)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, buttons, 0, i == 0 ? command : Command.None);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void FastClient_GainsNoDistance(int stepsEvery2Ticks)
    {
        var (server, client, network, _) = Start();
        var start = server.GetPlayer(1)!.Value.Position;
        const int ticks = 600;
        for (var tick = 0; tick < ticks; tick++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            var steps = tick % 2 == 0 ? stepsEvery2Ticks - stepsEvery2Ticks / 2 : stepsEvery2Ticks / 2;
            for (var i = 0; i < steps; i++)
            {
                client.Step(0, 127, Buttons.None);
            }
        }

        var walked = Vector3.Distance(server.GetPlayer(1)!.Value.Position, start);
        Assert.InRange(walked, 0f, Tuning.MoveSpeed * ticks / Tuning.TickRate * 1.04f);
    }

    [Fact]
    public void ButtonsPressed_AfterAClientStall_StillCount()
    {
        var (server, client, network, _) = Start();
        for (var i = 0; i < 200; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
        }

        Run(server, client, network, 60, Command.None, Buttons.Light);
        Assert.Equal(AttackKind.Light, server.GetPlayer(1)!.Value.Attack);
    }

    [Fact]
    public void LateCommand_StillHappens()
    {
        var (server, client, network, _) = Start();
        for (var i = 0; i < 20; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
        }

        Run(server, client, network, 4, Command.UseDoor);
        Assert.Equal(50f, server.GetPlayer(1)!.Value.Position.X);
    }

    [Fact]
    public void Door_TakesThePlayerThrough()
    {
        var (server, client, network, _) = Start();
        Run(server, client, network, 12, Command.UseDoor);
        Assert.Equal(50f, server.GetPlayer(1)!.Value.Position.X);
    }

    [Fact]
    public void Newcomer_CannotSellOrTakeQuests()
    {
        var (server, client, network, _) = Start();
        server.GiveCrystals(1, CrystalRarity.Common, 3);
        Run(server, client, network, 12, Command.SellCrystals);
        Run(server, client, network, 12, Command.AcceptSlimeQuest);
        Assert.Equal(0, server.GetGold(1));
        Assert.Equal(0, server.GetQuest(1));
    }

    [Fact]
    public void BeatingTheExaminer_MakesAnAdventurer()
    {
        var (server, client, network, _) = Start();
        Run(server, client, network, 12, Command.BeginTrial);
        Assert.Equal(GuildRank.InTrial, server.GetRank(1));
        var examiner = server.Enemies.Single(e => e.TrialOf == 1);
        Assert.Equal(Guild.ExaminerId, examiner.Def.Id);
        server.EnemiesFrozen = true;
        examiner.Health = 1;
        examiner.Position = server.GetPlayer(1)!.Value.Position + new Vector3(0, 0, -2);
        Run(server, client, network, 30, Command.None, Buttons.Light);
        Assert.Equal(GuildRank.Adventurer, server.GetRank(1));
        Assert.DoesNotContain(server.Enemies, e => e.TrialOf == 1);
        Assert.True(server.GetGold(1) > 0);
    }

    [Fact]
    public void LosingTheTrial_ResetsTheRank()
    {
        var (server, client, network, _) = Start();
        Run(server, client, network, 12, Command.BeginTrial);
        var examiner = server.Enemies.Single(e => e.TrialOf == 1);
        Run(server, client, network, 60 * 90);
        Assert.Equal(GuildRank.Newcomer, server.GetRank(1));
        Assert.DoesNotContain(server.Enemies, e => e.TrialOf == 1);
    }

    private static void Promote(GameServer server, ClientPrediction client, SimNetwork network)
    {
        Run(server, client, network, 12, Command.BeginTrial);
        server.EnemiesFrozen = true;
        var examiner = server.Enemies.Single(e => e.TrialOf == 1);
        examiner.Health = 1;
        examiner.Position = server.GetPlayer(1)!.Value.Position + new Vector3(0, 0, -2);
        Run(server, client, network, 30, Command.None, Buttons.Light);
    }

    [Fact]
    public void Crystals_SellForGold()
    {
        var (server, client, network, _) = Start();
        Promote(server, client, network);
        var start = server.GetGold(1);
        server.GiveCrystals(1, CrystalRarity.Common, 2);
        server.GiveCrystals(1, CrystalRarity.Rare, 1);

        // Far from the receptionist nothing happens; after the walk back the sale goes through.
        Run(server, client, network, 12, Command.SellCrystals);
        Assert.Equal(start, server.GetGold(1));
        WalkTo(server, client, network, Vector3.Zero);
        Run(server, client, network, 12, Command.SellCrystals);
        Assert.Equal(start + 2 * Guild.CrystalPrices[0] + Guild.CrystalPrices[1], server.GetGold(1));
        Assert.Equal(0, server.GetCrystals(1, CrystalRarity.Common));
    }

    [Fact]
    public void Materials_SellToTheAppraiser_ButEquipmentStays()
    {
        var (server, client, network, _) = Start();
        Promote(server, client, network);
        WalkTo(server, client, network, Vector3.Zero);
        var start = server.GetGold(1);
        server.GiveItem(1, "slime_gel", 4);
        server.GiveItem(1, "raw_beef", 1);
        server.GiveItem(1, "rusty_dagger", 1);
        Run(server, client, network, 12, Command.SellLoot);
        var price = 4 * ItemDef.All[ItemDef.Index("slime_gel")].Price + ItemDef.All[ItemDef.Index("raw_beef")].Price;
        Assert.Equal(start + price, server.GetGold(1));
        Assert.Equal(0, server.GetItem(1, "slime_gel"));
        Assert.Equal(1, server.GetItem(1, "rusty_dagger"));

        // The client hears that the gel is gone, not only what is left.
        Assert.Contains(client.Latest!.Bag, e => e.Item == ItemDef.Index("slime_gel") && e.Count == 0);
    }

    [Fact]
    public void DeliveryJob_TakesTheItemsAndPays()
    {
        var (server, client, network, _) = Start();
        Promote(server, client, network);
        WalkTo(server, client, network, Vector3.Zero);
        Run(server, client, network, 12, QuestCommands.Accept(3));
        Assert.Equal(3, server.GetQuest(1));
        server.GiveItem(1, "slime_gel", 4);
        Run(server, client, network, 12, Command.TurnInQuest);
        Assert.Equal(3, server.GetQuest(1));
        Assert.Equal(4, server.GetQuestProgress(1));

        var start = server.GetGold(1);
        server.GiveItem(1, "slime_gel", 3);
        Run(server, client, network, 12, Command.TurnInQuest);
        Assert.Equal(0, server.GetQuest(1));
        Assert.Equal(start + Guild.Quest(3)!.Gold, server.GetGold(1));
        Assert.Equal(1, server.GetItem(1, "slime_gel"));
    }

    [Fact]
    public void Eating_HealsAndUsesUpTheFood()
    {
        var (server, client, network, _) = Start();
        server.GiveItem(1, "raw_beef", 2);
        server.GiveItem(1, "rusty_dagger", 1);
        var beef = ItemDef.Index("raw_beef");
        server.HurtForTest(1, 50);
        var hurt = server.GetPlayer(1)!.Value.Health;
        Run(server, client, network, 12, ItemCommands.Eat(beef));
        Assert.Equal(hurt + ItemDef.All[beef].Heal, server.GetPlayer(1)!.Value.Health);
        Assert.Equal(1, server.GetItem(1, "raw_beef"));

        // Equipment is not food.
        Run(server, client, network, 12, ItemCommands.Eat(ItemDef.Index("rusty_dagger")));
        Assert.Equal(1, server.GetItem(1, "rusty_dagger"));
    }

    private static void WalkTo(GameServer server, ClientPrediction client, SimNetwork network, Vector3 goal)
    {
        for (var i = 0; i < 60 * 12; i++)
        {
            var offset = goal - server.GetPlayer(1)!.Value.Position;
            offset.Y = 0;
            if (offset.Length() < 1f)
            {
                break;
            }

            var heading = Vector3.Normalize(offset) * 127f;
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step((sbyte)heading.X, (sbyte)heading.Z, Buttons.None);
        }
    }

    [Fact]
    public void Quest_CountsKillsAndPaysOut()
    {
        var (server, client, network, _) = Start();
        Promote(server, client, network);
        WalkTo(server, client, network, Vector3.Zero);
        Run(server, client, network, 12, Command.AcceptSlimeQuest);
        Assert.Equal(1, server.GetQuest(1));

        var slime = server.Enemies[0];
        var position = server.GetPlayer(1)!.Value.Position;
        for (var kill = 0; kill < 5; kill++)
        {
            slime.Reset(new Random(kill), new World(Array.Empty<Aabb>()));
            slime.Health = 1;
            slime.Position = position + new Vector3(0, 0, -2);
            Run(server, client, network, 30, Command.None, Buttons.Light);
        }

        Assert.Equal(5, server.GetQuestProgress(1));
        var gold = server.GetGold(1);
        Run(server, client, network, 12, Command.TurnInQuest);
        Assert.True(server.GetGold(1) > gold);
        Assert.Equal(0, server.GetQuest(1));
    }
}

public sealed class ZoneTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    [Fact]
    public void Rivers_HaveWaterAlongTheWholeValley()
    {
        foreach (var (points, _) in Valley.RiverCourses)
        {
            var level = float.MaxValue;
            for (var i = 0; i + 1 < points.Length; i++)
            {
                for (var t = 0f; t < 1f; t += 0.05f)
                {
                    var point = Vector2.Lerp(points[i], points[i + 1], t);
                    var bed = Valley.World.Terrain!.At(point.X, point.Y);
                    level = MathF.Min(level, bed + 1.1f);
                    if (MathF.Max(MathF.Abs(point.X), MathF.Abs(point.Y)) < 150f)
                    {
                        Assert.True(level > bed + 0.3f, $"dry river at {point}: bed {bed}, water {level}");
                    }
                }
            }
        }
    }

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
        Assert.Equal("Goblin Hollow", Valley.RegionAt(84, -92));
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
            Enemies = new[] { new EnemyView(4, 1, 3, Vector3.UnitZ, 1.5f, 30, 40, EnemyAction.Leap, 2, Vector3.UnitX, true) },
            Projectiles = new[] { new ProjectileView(7, ProjectileKind.Arrow, Vector3.UnitY) },
            Drops = new[] { new LootView(8, 2, Vector3.UnitX) },
            Bag = new[] { new BagEntry(5, 3), new BagEntry(9, 0) },
        };
        Assert.True(Protocol.TryDecodeSnapshot(Protocol.EncodeSnapshot(snapshot), out var decoded));
        Assert.Equal(snapshot.Self, decoded.Self);
        Assert.Equal(snapshot.Others, decoded.Others);
        Assert.Equal(snapshot.Enemies, decoded.Enemies);
        Assert.Equal(snapshot.Projectiles, decoded.Projectiles);
        Assert.Equal(snapshot.Drops, decoded.Drops);
        Assert.Equal(snapshot.Bag, decoded.Bag);
    }
}
