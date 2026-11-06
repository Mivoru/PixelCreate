using System.Numerics;

namespace PixelCreate.Core;

public sealed class ServerOptions
{
    /// <summary>Enemies around the spawn when the world defines no enemy spawns.</summary>
    public int EnemyCount { get; init; } = 4;
    public int Seed { get; init; } = 1;

    /// <summary>Server ticks between snapshots to each client.</summary>
    public int SnapshotInterval { get; init; } = 2;

    public int InputBufferTicks { get; init; } = Tuning.InputBufferTicks;
}

public sealed class ServerStats
{
    public long MissedInputs;
    public long EnemiesKilled;
    public long PlayerHits;
    public long EnemyHits;
    public long Splashes;
    public long Spits;
}

/// <summary>The authoritative game. It owns every player and enemy, and knows nothing about rendering.</summary>
public sealed class GameServer
{
    private const float SpitSpeed = 13f;
    private const float SpitHitRadius = 0.9f;
    private const int SpitLifeTicks = 150;
    private const float SplashPush = 4f;

    private sealed class Player
    {
        public required int Id;
        public PlayerState State;
        public readonly SortedDictionary<uint, InputCmd> Inputs = new();
        public uint NextTick;
        public int WaitTicks;
        public InputCmd LastInput;
    }

    private sealed class Spit
    {
        public required int Id;
        public required short Damage;
        public required byte SlowTicks;
        public Vector3 Position;
        public Vector3 Velocity;
        public int Life;
    }

    private readonly ITransport _transport;
    private readonly World _world;
    private readonly ServerOptions _options;
    private readonly Random _random;
    private readonly Dictionary<int, Player> _players = new();
    private readonly List<Enemy> _enemies = new();
    private readonly List<Spit> _spits = new();
    private int _nextSpitId;

    public uint Tick { get; private set; }
    public ServerStats Stats { get; } = new();

    /// <summary>Stops enemies, so a test can check that both sides settle on the same state.</summary>
    public bool EnemiesFrozen { get; set; }

    public IReadOnlyList<Enemy> Enemies => _enemies;

    public GameServer(ITransport transport, World world, ServerOptions options)
    {
        _transport = transport;
        _world = world;
        _options = options;
        _random = new Random(options.Seed);
        var spawns = world.EnemySpawns.Count > 0 ? world.EnemySpawns
            : new[] { new EnemySpawn(world.Spawn, 15f, options.EnemyCount) };
        foreach (var spawn in spawns)
        {
            for (var i = 0; i < spawn.Count; i++)
            {
                var enemy = new Enemy { Id = _enemies.Count, Type = 0, Home = spawn };
                enemy.Reset(_random, world);
                _enemies.Add(enemy);
            }
        }
    }

    public PlayerState? GetPlayer(int id) => _players.TryGetValue(id, out var player) ? player.State : null;

    public void Step()
    {
        Tick++;
        while (_transport.TryReceive(out var peer, out var data))
        {
            if (Protocol.TryDecodeInputs(data, out var commands))
            {
                AddInputs(peer, commands);
            }
        }

        foreach (var player in _players.Values.OrderBy(p => p.Id))
        {
            StepPlayer(player);
        }

        if (!EnemiesFrozen)
        {
            foreach (var enemy in _enemies)
            {
                enemy.Step(_world, _random, NearestPlayer(enemy.Position), Splash, ShootSpit);
            }

            StepSpits();
        }

        if (Tick % (uint)_options.SnapshotInterval == 0)
        {
            SendSnapshots();
        }
    }

    private void AddInputs(int peer, InputCmd[] commands)
    {
        if (commands.Length == 0)
        {
            return;
        }

        if (!_players.TryGetValue(peer, out var player))
        {
            var angle = peer * 2.1f;
            var spawn = _world.Spawn + new Vector3(MathF.Cos(angle) * 3f, 0, MathF.Sin(angle) * 3f);
            spawn.Y = _world.GroundHeight(spawn.X, spawn.Z);
            player = new Player
            {
                Id = peer,
                State = PlayerState.Spawn(spawn),
                NextTick = commands.Min(c => c.Tick),
                WaitTicks = _options.InputBufferTicks,
            };
            _players.Add(peer, player);
        }

        foreach (var command in commands)
        {
            if (command.Tick >= player.NextTick)
            {
                player.Inputs[command.Tick] = command;
            }
        }
    }

    private void StepPlayer(Player player)
    {
        if (player.WaitTicks > 0)
        {
            player.WaitTicks--;
            return;
        }

        if (!player.Inputs.Remove(player.NextTick, out var command))
        {
            // Keep walking the same way, but never repeat a button press.
            command = player.LastInput;
            command.Tick = player.NextTick;
            command.Buttons = Buttons.None;
            Stats.MissedInputs++;
        }

        player.LastInput = command;
        player.NextTick++;
        var landed = Movement.Step(ref player.State, command, _world);
        if (landed != AttackKind.None)
        {
            Strike(player, landed);
        }

        if (player.State.Health <= 0)
        {
            player.State = PlayerState.Spawn(_world.Spawn);
        }
    }

    private void Strike(Player player, AttackKind kind)
    {
        var attack = Tuning.Attack(kind);
        var forward = Movement.AimDirection(player.State.AttackAim);
        foreach (var enemy in _enemies)
        {
            if (!enemy.Alive)
            {
                continue;
            }

            var toEnemy = enemy.Position - player.State.Position;
            var height = MathF.Abs(toEnemy.Y);
            toEnemy.Y = 0;
            var distance = toEnemy.Length();
            var inFront = distance < 0.6f || Vector3.Dot(toEnemy / distance, forward) >= attack.MinDot;
            if (distance > attack.Range + enemy.Def.Size * 0.5f || height > 2.2f || !inFront)
            {
                continue;
            }

            enemy.TakeHit(attack.Damage, player.State.Position);
            Stats.EnemyHits++;
            if (!enemy.Alive)
            {
                Stats.EnemiesKilled++;
            }
        }
    }

    private Vector3? NearestPlayer(Vector3 from)
    {
        Vector3? nearest = null;
        var best = float.MaxValue;
        foreach (var player in _players.Values.OrderBy(p => p.Id))
        {
            var distance = Vector3.Distance(player.State.Position, from);
            if (distance < best)
            {
                best = distance;
                nearest = player.State.Position;
            }
        }

        return nearest;
    }

    private void Splash(Enemy enemy)
    {
        Stats.Splashes++;
        foreach (var player in _players.Values)
        {
            var away = player.State.Position - enemy.Position;
            if (away.Length() > enemy.Def.SplashRadius)
            {
                continue;
            }

            player.State.Health -= enemy.Def.SplashDamage;
            player.State.SlowTicks = enemy.Def.SlowTicks;
            away.Y = 0;
            if (away != Vector3.Zero)
            {
                player.State.Velocity += Vector3.Normalize(away) * SplashPush;
            }

            Stats.PlayerHits++;
        }
    }

    private void ShootSpit(Enemy enemy, Vector3 target)
    {
        Stats.Spits++;
        var start = enemy.Position + Vector3.UnitY * 0.5f;
        var aim = target + Vector3.UnitY * 1f - start;
        var flight = aim.Length() / SpitSpeed;
        var velocity = aim / flight;
        velocity.Y += Tuning.Gravity * 0.25f * flight;
        _spits.Add(new Spit
        {
            Id = _nextSpitId++,
            Position = start,
            Velocity = velocity,
            Life = SpitLifeTicks,
            Damage = enemy.Def.SpitDamage,
            SlowTicks = enemy.Def.SlowTicks,
        });
    }

    private void StepSpits()
    {
        foreach (var spit in _spits)
        {
            spit.Velocity.Y -= Tuning.Gravity * 0.5f * Tuning.TickSeconds;
            spit.Position += spit.Velocity * Tuning.TickSeconds;
            spit.Life--;
            foreach (var player in _players.Values)
            {
                var chest = player.State.Position + Vector3.UnitY;
                if (Vector3.Distance(chest, spit.Position) < SpitHitRadius)
                {
                    player.State.Health -= spit.Damage;
                    player.State.SlowTicks = spit.SlowTicks;
                    spit.Life = 0;
                    Stats.PlayerHits++;
                }
            }

            if (_world.IsBlocked(spit.Position))
            {
                spit.Life = 0;
            }
        }

        _spits.RemoveAll(s => s.Life <= 0);
    }

    private void SendSnapshots()
    {
        var enemies = _enemies.Select(e => new EnemyView(
            e.Id, e.Type, e.Level, e.Position, e.Facing, e.Health, (short)e.MaxHealth, e.Action, e.HitFlash)).ToArray();
        var spits = _spits.Select(s => new SpitView(s.Id, s.Position)).ToArray();
        foreach (var player in _players.Values)
        {
            var snapshot = new Snapshot
            {
                ServerTick = Tick,
                YourId = player.Id,
                AckTick = player.NextTick - 1,
                BufferedInputs = (byte)Math.Min(player.Inputs.Count, byte.MaxValue),
                Self = player.State,
                Others = _players.Values.Where(p => p.Id != player.Id)
                    .Select(p => new RemotePlayer(p.Id, p.State.Position, p.State.Velocity, p.State.AttackAim, p.State.Attack, p.State.AttackTick)).ToArray(),
                Enemies = enemies,
                Spits = spits,
            };
            _transport.Send(player.Id, Protocol.EncodeSnapshot(snapshot));
        }
    }
}
