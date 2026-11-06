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
    public long Dodges;
    public long LevelUps;
}

/// <summary>The authoritative game. It owns every player and enemy, and knows nothing about rendering.</summary>
public sealed class GameServer : IEnemyEvents
{
    private const float SpitSpeed = 13f;
    private const float SpitHitRadius = 0.9f;
    private const int SpitLifeTicks = 150;
    private const float SplashPush = 4f;

    /// <summary>A player hears only about nearby enemies, which keeps a snapshot inside one UDP packet.</summary>
    private const float InterestRange = 70f;
    private const int MaxEnemiesPerSnapshot = 14;
    private const int MaxSpitsPerSnapshot = 8;
    private const float SpitSplashRadius = 1.8f;
    private const float SpitLead = 0.6f;
    private const float HelpRadius = 9f;
    private const uint RegenDelayTicks = 300;
    private const uint RegenIntervalTicks = 20;

    private sealed class Player
    {
        public required int Id;
        public PlayerState State;
        public readonly SortedDictionary<uint, InputCmd> Inputs = new();
        public uint NextTick;
        public int WaitTicks;
        public InputCmd LastInput;
        public byte Level = 1;
        public int Xp;
        public uint LastHurtTick;
        public byte Dodges;
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

    public int GetLevel(int id) => _players[id].Level;

    public int GetXp(int id) => _players[id].Xp;

    /// <summary>Gives a player experience and raises the level as often as it allows.</summary>
    public void AwardXp(int id, int amount)
    {
        var player = _players[id];
        player.Xp += amount;
        while (player.Level < Progression.MaxLevel && player.Xp >= Progression.XpToNext(player.Level))
        {
            player.Xp -= Progression.XpToNext(player.Level);
            player.Level++;
            player.State.Health = Progression.MaxHealth(player.Level);
            Stats.LevelUps++;
        }
    }

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
            var targets = _players.Values.OrderBy(p => p.Id)
                .Select(p => new Target(p.Id, p.State.Position, p.State.Velocity)).ToArray();
            foreach (var enemy in _enemies)
            {
                enemy.Step(_world, _random, targets, this);
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

        var maxHealth = Progression.MaxHealth(player.Level);
        if (player.State.Health <= 0)
        {
            player.State = PlayerState.Spawn(_world.Spawn, maxHealth);
        }
        else if (Tick - player.LastHurtTick > RegenDelayTicks && Tick % RegenIntervalTicks == 0 && player.State.Health < maxHealth)
        {
            player.State.Health++;
        }
    }

    /// <summary>Hurts a player, unless a dash avoids it. Returns true when the attack connects.</summary>
    private bool Hurt(Player player, short damage, byte slowTicks)
    {
        if (player.State.DashTicks > 0)
        {
            player.Dodges++;
            Stats.Dodges++;
            return false;
        }

        player.State.Health -= damage;
        player.State.SlowTicks = slowTicks;
        player.LastHurtTick = Tick;
        Stats.PlayerHits++;
        return true;
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

            var damage = (short)MathF.Round(attack.Damage * Progression.DamageScale(player.Level));
            var killed = enemy.TakeHit(damage, player.State.Position, player.Id);
            Stats.EnemyHits++;
            CallForHelp(enemy, player.Id);
            if (killed)
            {
                Stats.EnemiesKilled++;
                var winner = _players.ContainsKey(enemy.TaggedBy) ? enemy.TaggedBy : player.Id;
                AwardXp(winner, Progression.KillXp(enemy.Level, _players[winner].Level, enemy.Def.RareChance > 0));
            }
        }
    }

    void IEnemyEvents.CallForHelp(Enemy enemy, int targetId) => CallForHelp(enemy, targetId);

    private void CallForHelp(Enemy enemy, int targetId)
    {
        foreach (var other in _enemies)
        {
            if (other.Alive && other.TargetId < 0 && !other.Returning && other.Home == enemy.Home &&
                Vector3.Distance(other.Position, enemy.Position) < HelpRadius)
            {
                other.TargetId = targetId;
            }
        }
    }

    void IEnemyEvents.Splash(Enemy enemy)
    {
        Stats.Splashes++;
        foreach (var player in _players.Values)
        {
            var away = player.State.Position - enemy.Position;
            if (away.Length() > enemy.Def.SplashRadius)
            {
                continue;
            }

            if (!Hurt(player, enemy.Def.SplashDamage, enemy.Def.SlowTicks))
            {
                continue;
            }

            away.Y = 0;
            if (away != Vector3.Zero)
            {
                player.State.Velocity += Vector3.Normalize(away) * SplashPush;
            }
        }
    }

    void IEnemyEvents.Spit(Enemy enemy, Target target)
    {
        Stats.Spits++;
        var start = enemy.Position + Vector3.UnitY * 0.7f;
        var flight = Vector3.Distance(target.Position, start) / SpitSpeed;

        // Aim where the player is heading, but not all the way, so moving still helps.
        var lead = target.Velocity with { Y = 0 } * flight * SpitLead;
        var aim = target.Position + lead + Vector3.UnitY * 0.9f - start;
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
            var struck = _world.IsBlocked(spit.Position);
            foreach (var player in _players.Values)
            {
                var chest = player.State.Position + Vector3.UnitY;
                struck |= Vector3.Distance(chest, spit.Position) < SpitHitRadius;
            }

            if (struck)
            {
                SpitSplash(spit);
                spit.Life = 0;
            }
        }

        _spits.RemoveAll(s => s.Life <= 0);
    }

    /// <summary>A spit bursts on impact, so a near miss on the ground still catches the player.</summary>
    private void SpitSplash(Spit spit)
    {
        foreach (var player in _players.Values)
        {
            var chest = player.State.Position + Vector3.UnitY;
            if (Vector3.Distance(chest, spit.Position) < SpitSplashRadius)
            {
                Hurt(player, spit.Damage, spit.SlowTicks);
            }
        }
    }

    private void SendSnapshots()
    {
        foreach (var player in _players.Values)
        {
            var origin = player.State.Position;
            var enemies = _enemies
                .Where(e => Vector3.Distance(e.Position, origin) < InterestRange)
                .OrderBy(e => Vector3.Distance(e.Position, origin))
                .Take(MaxEnemiesPerSnapshot)
                .Select(e => new EnemyView(e.Id, e.Type, e.Level, e.Position, e.Facing, e.Health, (short)e.MaxHealth,
                    e.Action, e.HitFlash, e.LeapTarget, e.TargetId >= 0)).ToArray();
            var spits = _spits.OrderBy(s => Vector3.Distance(s.Position, origin)).Take(MaxSpitsPerSnapshot)
                .Select(s => new SpitView(s.Id, s.Position)).ToArray();
            var snapshot = new Snapshot
            {
                ServerTick = Tick,
                YourId = player.Id,
                AckTick = player.NextTick - 1,
                BufferedInputs = (byte)Math.Min(player.Inputs.Count, byte.MaxValue),
                Self = player.State,
                Level = player.Level,
                Xp = player.Xp,
                XpToNext = Progression.XpToNext(player.Level),
                MaxHealth = Progression.MaxHealth(player.Level),
                Dodges = player.Dodges,
                Others = _players.Values.Where(p => p.Id != player.Id)
                    .Select(p => new RemotePlayer(p.Id, p.State.Position, p.State.Velocity, p.State.AttackAim, p.State.Attack, p.State.AttackTick)).ToArray(),
                Enemies = enemies,
                Spits = spits,
            };
            _transport.Send(player.Id, Protocol.EncodeSnapshot(snapshot));
        }
    }
}
