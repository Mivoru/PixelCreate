using System.Numerics;

namespace PixelCreate.Core;

public sealed class ServerOptions
{
    public int EnemyCount { get; init; } = 4;
    public int Seed { get; init; } = 1;

    /// <summary>Server ticks between snapshots to each client.</summary>
    public int SnapshotInterval { get; init; } = 2;

    /// <summary>Ticks of input held back per player, to absorb jitter and short loss.</summary>
    public int InputBufferTicks { get; init; } = 3;
}

public sealed class ServerStats
{
    public long MissedInputs;
    public long EnemiesKilled;
    public long PlayerHits;
}

/// <summary>The authoritative game. It owns every player and enemy, and knows nothing about rendering.</summary>
public sealed class GameServer
{
    private sealed class Player
    {
        public required int Id;
        public PlayerState State;
        public readonly SortedDictionary<uint, InputCmd> Inputs = new();
        public uint NextTick;
        public int WaitTicks;
        public InputCmd LastInput;
    }

    private sealed class Enemy
    {
        public required int Id;
        public Vector3 Position;
        public short Health;
        public byte Cooldown;
    }

    private readonly ITransport _transport;
    private readonly World _world;
    private readonly ServerOptions _options;
    private readonly Random _random;
    private readonly Dictionary<int, Player> _players = new();
    private readonly List<Enemy> _enemies = new();

    public uint Tick { get; private set; }
    public ServerStats Stats { get; } = new();

    /// <summary>Stops enemies, so a test can check that both sides settle on the same state.</summary>
    public bool EnemiesFrozen { get; set; }

    public GameServer(ITransport transport, World world, ServerOptions options)
    {
        _transport = transport;
        _world = world;
        _options = options;
        _random = new Random(options.Seed);
        for (var i = 0; i < options.EnemyCount; i++)
        {
            var enemy = new Enemy { Id = i };
            Respawn(enemy);
            _enemies.Add(enemy);
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

        foreach (var enemy in EnemiesFrozen ? Enumerable.Empty<Enemy>() : _enemies)
        {
            StepEnemy(enemy);
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
            var spawn = new Vector3(MathF.Cos(angle) * 4f, 0, MathF.Sin(angle) * 4f);
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
        if (Movement.Step(ref player.State, command, _world))
        {
            Attack(player);
        }

        if (player.State.Health <= 0)
        {
            player.State.Health = Tuning.PlayerHealth;
        }
    }

    private void Attack(Player player)
    {
        foreach (var enemy in _enemies)
        {
            if (Vector3.Distance(enemy.Position, player.State.Position) > Tuning.AttackRange)
            {
                continue;
            }

            enemy.Health -= Tuning.AttackDamage;
            if (enemy.Health <= 0)
            {
                Stats.EnemiesKilled++;
                Respawn(enemy);
            }
        }
    }

    private void StepEnemy(Enemy enemy)
    {
        if (enemy.Cooldown > 0)
        {
            enemy.Cooldown--;
        }

        Player? target = null;
        var nearest = float.MaxValue;
        foreach (var player in _players.Values.OrderBy(p => p.Id))
        {
            var distance = Vector3.Distance(player.State.Position, enemy.Position);
            if (distance < nearest)
            {
                nearest = distance;
                target = player;
            }
        }

        if (target is null)
        {
            return;
        }

        var toTarget = target.State.Position - enemy.Position;
        toTarget.Y = 0;
        if (nearest > Tuning.EnemyReach)
        {
            enemy.Position += Vector3.Normalize(toTarget) * Tuning.EnemySpeed * Tuning.TickSeconds;
        }
        else if (enemy.Cooldown == 0)
        {
            enemy.Cooldown = Tuning.EnemyCooldownTicks;
            var push = toTarget == Vector3.Zero ? Vector3.UnitX : Vector3.Normalize(toTarget);
            target.State.StunTicks = Tuning.StunTicks;
            target.State.Velocity += push * Tuning.Knockback;
            target.State.Health -= Tuning.EnemyDamage;
            Stats.PlayerHits++;
        }
    }

    private void Respawn(Enemy enemy)
    {
        var angle = (float)(_random.NextDouble() * Math.Tau);
        enemy.Position = new Vector3(MathF.Cos(angle) * 15f, 0, MathF.Sin(angle) * 15f);
        enemy.Health = Tuning.EnemyHealth;
        enemy.Cooldown = Tuning.EnemyCooldownTicks;
    }

    private void SendSnapshots()
    {
        var enemies = _enemies.Select(e => new EnemyView(e.Id, e.Position, e.Health)).ToArray();
        foreach (var player in _players.Values)
        {
            var snapshot = new Snapshot
            {
                ServerTick = Tick,
                YourId = player.Id,
                AckTick = player.NextTick - 1,
                Self = player.State,
                Others = _players.Values.Where(p => p.Id != player.Id)
                    .Select(p => new RemotePlayer(p.Id, p.State.Position)).ToArray(),
                Enemies = enemies,
            };
            _transport.Send(player.Id, Protocol.EncodeSnapshot(snapshot));
        }
    }
}
