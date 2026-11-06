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

    /// <summary>Starts new players somewhere else, to test a far part of the zone.</summary>
    public Vector3? SpawnOverride { get; init; }
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
    public long CrystalsDropped;
    public long CrystalsLooted;
}

public enum ProjectileKind : byte
{
    Spit = 0,
    Arrow = 1,
    PowerArrow = 2,
    Fireball = 3,
}

/// <summary>A burning V on the ground. See <see cref="GameServer"/>.</summary>
public sealed class FireWallHazard
{
    public const float HalfAngle = 0.5236f;
    public const float MaxLength = 15f;
    public const int GrowTicks = 48;
    public const int BurnTicks = 80;

    /// <summary>How far past the edge of the flames a player still gets burnt, in metres.</summary>
    public const float Reach = 0.5f;

    public required int Id;
    public required Vector3 Origin;
    public required Vector3 Direction;
    public required short Damage;
    public int Age;
    public readonly HashSet<int> Struck = new();

    public float Length => MaxLength * MathF.Min(1f, Age / (float)GrowTicks);
}

/// <summary>The authoritative game. It owns every player and enemy, and knows nothing about rendering.</summary>
public sealed class GameServer : IEnemyEvents
{
    private const float SpitSpeed = 13f;
    private const float ArrowSpeed = 22f;
    private const float PowerArrowSpeed = 48f;
    private const float FireballSpeed = 13f;
    private const float HitRadius = 0.9f;
    private const float SpitSplashRadius = 1.8f;
    private const int ProjectileLifeTicks = 170;
    private const float SpitLead = 0.6f;
    private const float VolleySpread = 0.2f;
    private const float SplashPush = 4f;
    private const float HelpRadius = 6f;
    private const uint RegenDelayTicks = 300;
    private const uint RegenIntervalTicks = 20;
    private const float LootRadius = 2.4f;
    private const int CrystalLifeTicks = 60 * 90;

    /// <summary>A player hears only about nearby things, which keeps a snapshot inside one UDP packet.</summary>
    private const float InterestRange = 70f;
    private const int MaxEnemiesPerSnapshot = 12;
    private const int MaxProjectilesPerSnapshot = 16;
    private const int MaxDropsPerSnapshot = 6;

    private sealed class Player
    {
        public required int Id;
        public PlayerState State;
        public readonly SortedDictionary<uint, InputCmd> Inputs = new();
        public readonly int[] Crystals = new int[Core.Crystals.Tiers];
        public uint NextTick;
        public int WaitTicks;
        public InputCmd LastInput;
        public byte Level = 1;
        public int Xp;
        public uint LastHurtTick;
        public byte Dodges;
    }

    private sealed class Projectile
    {
        public required int Id;
        public required ProjectileKind Kind;
        public required short Damage;
        public byte SlowTicks;
        public float Gravity;
        public float SplashRadius;
        public bool Pierces;
        public Vector3 Position;
        public Vector3 Velocity;
        public int Life = ProjectileLifeTicks;
    }

    private sealed class Crystal
    {
        public required int Id;
        public required CrystalRarity Rarity;
        public required int Owner;
        public Vector3 Position;
        public int Life = CrystalLifeTicks;
    }

    private readonly ITransport _transport;
    private readonly World _world;
    private readonly ServerOptions _options;
    private readonly Random _random;
    private readonly Dictionary<int, Player> _players = new();
    private readonly List<Enemy> _enemies = new();
    private readonly List<Projectile> _projectiles = new();
    private readonly List<Crystal> _crystals = new();
    private readonly List<FireWallHazard> _walls = new();
    private int _nextId;

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

    public int GetCrystals(int id, CrystalRarity rarity) => _players[id].Crystals[(int)rarity];

    public int ProjectileCount => _projectiles.Count;

    public int FireWallCount => _walls.Count;

    public int CrystalsOnGround => _crystals.Count;

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

        StepCrystals();
        if (!EnemiesFrozen)
        {
            var targets = _players.Values.OrderBy(p => p.Id)
                .Select(p => new Target(p.Id, p.State.Position, p.State.Velocity)).ToArray();
            foreach (var enemy in _enemies)
            {
                enemy.Step(_world, _random, targets, this);
            }

            StepProjectiles();
            StepWalls();
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
            var spawn = (_options.SpawnOverride ?? _world.Spawn) + new Vector3(MathF.Cos(angle) * 3f, 0, MathF.Sin(angle) * 3f);
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
        if (slowTicks > 0)
        {
            player.State.SlowTicks = slowTicks;
        }

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
                OnKilled(enemy, player);
            }
        }
    }

    private void OnKilled(Enemy enemy, Player killer)
    {
        Stats.EnemiesKilled++;
        var winner = _players.TryGetValue(enemy.TaggedBy, out var tagged) ? tagged : killer;
        AwardXp(winner.Id, Progression.KillXp(enemy.Level, winner.Level, enemy.Def.Rare));
        foreach (var drop in enemy.Def.Drops ?? Array.Empty<DropEntry>())
        {
            if (_random.NextDouble() >= drop.Chance)
            {
                continue;
            }

            var angle = (float)(_random.NextDouble() * Math.Tau);
            var spot = enemy.Position + new Vector3(MathF.Cos(angle) * 0.8f, 0, MathF.Sin(angle) * 0.8f);
            spot.Y = _world.GroundHeight(spot.X, spot.Z);
            _crystals.Add(new Crystal { Id = _nextId++, Rarity = drop.Rarity, Owner = winner.Id, Position = spot });
            Stats.CrystalsDropped++;
        }
    }

    private void StepCrystals()
    {
        foreach (var crystal in _crystals)
        {
            crystal.Life--;
            if (_players.TryGetValue(crystal.Owner, out var owner) &&
                Vector3.Distance(owner.State.Position, crystal.Position) < LootRadius)
            {
                owner.Crystals[(int)crystal.Rarity]++;
                crystal.Life = 0;
                Stats.CrystalsLooted++;
            }
        }

        _crystals.RemoveAll(c => c.Life <= 0);
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
            if (away.Length() > enemy.Def.SplashRadius || !Hurt(player, enemy.Def.SplashDamage, enemy.Def.SlowTicks))
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
        const float gravity = Tuning.Gravity * 0.5f;
        velocity.Y += gravity * 0.5f * flight;
        _projectiles.Add(new Projectile
        {
            Id = _nextId++,
            Kind = ProjectileKind.Spit,
            Position = start,
            Velocity = velocity,
            Gravity = gravity,
            Damage = enemy.Def.SpitDamage,
            SlowTicks = enemy.Def.SlowTicks,
            SplashRadius = SpitSplashRadius,
        });
    }

    void IEnemyEvents.Fire(Enemy enemy, Ability ability, Target? target)
    {
        var chest = enemy.Position + Vector3.UnitY * (enemy.Def.Size * 1.1f);
        var aimPoint = (target?.Position ?? enemy.LeapTarget) + Vector3.UnitY;
        var direction = aimPoint - chest;
        direction = direction == Vector3.Zero ? Vector3.UnitZ : Vector3.Normalize(direction);
        switch (ability.Effect)
        {
            case "melee":
            case "dashStrike":
                Area(enemy.Position + Facing(enemy) * 1.1f, ability.Radius, ability.Damage);
                break;
            case "slam":
                Area(enemy.LeapTarget, ability.Radius, ability.Damage);
                break;
            case "spin":
                Area(enemy.Position, ability.Radius, ability.Damage);
                break;
            case "arrow":
                Shoot(ProjectileKind.Arrow, chest, direction * ArrowSpeed, ability.Damage);
                break;
            case "volley":
                foreach (var angle in new[] { -VolleySpread, 0f, VolleySpread })
                {
                    Shoot(ProjectileKind.Arrow, chest, Vector3.Transform(direction, Matrix4x4.CreateRotationY(angle)) * ArrowSpeed * 0.65f, ability.Damage);
                }

                break;
            case "powershot":
                var arrow = Shoot(ProjectileKind.PowerArrow, chest, direction * PowerArrowSpeed, ability.Damage);
                arrow.Pierces = true;
                break;
            case "fireball":
                var ball = Shoot(ProjectileKind.Fireball, chest, direction * FireballSpeed, ability.Damage);
                ball.SplashRadius = ability.Radius;
                break;
            case "firewall":
                FireWall(enemy, direction, ability.Damage);
                break;
        }
    }

    private static Vector3 Facing(Enemy enemy) => new(-MathF.Sin(enemy.Facing), 0, -MathF.Cos(enemy.Facing));

    private void Area(Vector3 center, float radius, short damage)
    {
        foreach (var player in _players.Values)
        {
            if (Vector3.Distance(player.State.Position, center) <= radius + 0.4f)
            {
                Hurt(player, damage, 0);
            }
        }
    }

    private Projectile Shoot(ProjectileKind kind, Vector3 from, Vector3 velocity, short damage)
    {
        var projectile = new Projectile { Id = _nextId++, Kind = kind, Position = from, Velocity = velocity, Damage = damage };
        _projectiles.Add(projectile);
        return projectile;
    }

    /// <summary>
    /// A filled V of fire on the ground. It starts at the caster and spreads at a fixed angle
    /// towards the player, up to full length, where it stops and burns for a moment.
    /// </summary>
    private void FireWall(Enemy enemy, Vector3 aim, short damage)
    {
        var flat = aim with { Y = 0 };
        flat = flat == Vector3.Zero ? Vector3.UnitZ : Vector3.Normalize(flat);
        _walls.Add(new FireWallHazard { Id = _nextId++, Origin = enemy.Position, Direction = flat, Damage = damage });
    }

    private void StepWalls()
    {
        foreach (var wall in _walls)
        {
            wall.Age++;
            var length = wall.Length;
            foreach (var player in _players.Values)
            {
                if (!wall.Struck.Contains(player.Id) && InWedge(wall, player.State.Position, length))
                {
                    wall.Struck.Add(player.Id);
                    Hurt(player, wall.Damage, 0);
                }
            }
        }

        _walls.RemoveAll(w => w.Age > FireWallHazard.GrowTicks + FireWallHazard.BurnTicks);
    }

    private static bool InWedge(FireWallHazard wall, Vector3 position, float length)
    {
        var offset = (position - wall.Origin) with { Y = 0 };
        var distance = offset.Length();
        if (distance > length + FireWallHazard.Reach)
        {
            return false;
        }

        return distance < FireWallHazard.Reach || Vector3.Dot(offset / distance, wall.Direction) >= MathF.Cos(FireWallHazard.HalfAngle);
    }

    private void StepProjectiles()
    {
        foreach (var shot in _projectiles)
        {
            shot.Velocity.Y -= shot.Gravity * Tuning.TickSeconds;
            shot.Position += shot.Velocity * Tuning.TickSeconds;
            shot.Life--;
            var struck = !shot.Pierces && _world.IsBlocked(shot.Position);
            foreach (var player in _players.Values)
            {
                var chest = player.State.Position + Vector3.UnitY;
                if (Vector3.Distance(chest, shot.Position) >= HitRadius)
                {
                    continue;
                }

                if (shot.SplashRadius > 0f)
                {
                    struck = true;
                }
                else
                {
                    Hurt(player, shot.Damage, shot.SlowTicks);
                    struck = true;
                }
            }

            if (struck)
            {
                if (shot.SplashRadius > 0f)
                {
                    Burst(shot);
                }

                shot.Life = 0;
            }
        }

        _projectiles.RemoveAll(s => s.Life <= 0);
    }

    /// <summary>A bursting shot, so a near miss on the ground still catches the player.</summary>
    private void Burst(Projectile shot)
    {
        foreach (var player in _players.Values)
        {
            var chest = player.State.Position + Vector3.UnitY;
            if (Vector3.Distance(chest, shot.Position) < shot.SplashRadius)
            {
                Hurt(player, shot.Damage, shot.SlowTicks);
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
                    e.Action, e.HitFlash, e.LeapTarget, e.TargetId >= 0, e.AbilityIndex)).ToArray();
            var shots = _projectiles
                .OrderBy(s => Vector3.Distance(s.Position, origin)).Take(MaxProjectilesPerSnapshot)
                .Select(s => new ProjectileView(s.Id, s.Kind, s.Position)).ToArray();
            var crystals = _crystals.Where(c => c.Owner == player.Id)
                .OrderBy(c => Vector3.Distance(c.Position, origin)).Take(MaxDropsPerSnapshot)
                .Select(c => new CrystalView(c.Id, c.Rarity, c.Position)).ToArray();
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
                Crystals = player.Crystals.ToArray(),
                Others = _players.Values.Where(p => p.Id != player.Id).Take(6)
                    .Select(p => new RemotePlayer(p.Id, p.State.Position, p.State.Velocity, p.State.AttackAim, p.State.Attack, p.State.AttackTick)).ToArray(),
                Enemies = enemies,
                Projectiles = shots,
                Drops = crystals,
                FireWalls = _walls.Where(w => Vector3.Distance(w.Origin, origin) < InterestRange).Take(2)
                    .Select(w => new FireWallView(w.Id, w.Origin, MathF.Atan2(-w.Direction.X, -w.Direction.Z), w.Length)).ToArray(),
            };
            _transport.Send(player.Id, Protocol.EncodeSnapshot(snapshot));
        }
    }
}
