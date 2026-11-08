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

    /// <summary>For scripted live checks: a frail examiner, and crystals in the pocket.</summary>
    public bool TestMode { get; init; }
}

public sealed class ServerStats
{
    public long MissedInputs;
    public long DroppedInputs;
    public long EnemiesKilled;
    public long PlayerHits;
    public long EnemyHits;
    public long Splashes;
    public long Spits;
    public long Dodges;
    public long LevelUps;
    public long CrystalsDropped;
    public long CrystalsLooted;
    public long ItemsDropped;
    public long ItemsLooted;
    public long ItemsSold;
    public long MealsEaten;
    public long CriticalHits;

    /// <summary>Pieces of gear dropped, by rarity.</summary>
    public readonly long[] GearDropped = new long[4];
}

public enum ProjectileKind : byte
{
    Spit = 0,
    Arrow = 1,
    PowerArrow = 2,
    Fireball = 3,
    PlayerArrow = 4,
    PlayerFireball = 5,
}

/// <summary>A burning V on the ground. See <see cref="GameServer"/>.</summary>
public sealed class FireWallHazard
{
    public const float HalfAngle = 0.5236f;
    public const float MaxLength = 15f;
    public const int GrowTicks = 48;
    public const int BurnTicks = 80;

    /// <summary>
    /// A player's wall is a straight strip of this half width instead of a V, burns enemies instead
    /// of players, and is shorter and quicker; 0 means the shaman's V.
    /// </summary>
    public float HalfWidth;
    public int Owner = -1;
    public float Span = MaxLength;
    public int Grow = GrowTicks;
    public int Burn = BurnTicks;

    /// <summary>How far past the edge of the flames a player still gets burnt, in metres.</summary>
    public const float Reach = 0.5f;

    public required int Id;
    public required Vector3 Origin;
    public required Vector3 Direction;
    public required short Damage;
    public int Age;
    public readonly HashSet<int> Struck = new();

    public float Length => Span * MathF.Min(1f, Age / (float)Grow);
}

/// <summary>The authoritative game. It owns every player and enemy, and knows nothing about rendering.</summary>
public sealed class GameServer : IEnemyEvents
{
    /// <summary>Inputs above the normal buffer before the server starts to catch up.</summary>
    private const int CatchUpSurplus = 4;

    /// <summary>The most missed time a player can save up, which covers a stall of three seconds.</summary>
    private const int MaxCatchUpTicks = 180;

    /// <summary>The most inputs the server holds for a player.</summary>
    private const int MaxInputBacklog = 24;

    /// <summary>A second press of the key right after a door must not lead straight back out.</summary>
    private const int DoorCooldownTicks = 45;

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
    private const int MaxEnemiesPerSnapshot = 16;

    /// <summary>Animals are less urgent than enemies, so they count as this much further away.</summary>
    private const float AnimalInterestPenalty = 20f;
    private const int MaxProjectilesPerSnapshot = 16;
    private const int MaxDropsPerSnapshot = 8;
    private const int BagEntriesPerSnapshot = 6;
    private const int GearEntriesPerSnapshot = 2;

    /// <summary>How many pieces of gear a player can carry, worn ones included.</summary>
    public const int GearSlots = 24;

    /// <summary>Bleeding lasts this long and deals this share of the blow that caused it, spread over its length.</summary>
    private const int BleedTicks = 180;
    private const float BleedShare = 0.6f;
    private const int MaxStack = 999;

    private sealed class Player
    {
        public required int Id;
        public PlayerState State;
        public readonly SortedDictionary<uint, InputCmd> Inputs = new();
        public readonly int[] Crystals = new int[Core.Crystals.Tiers];
        public uint NextTick;
        public int WaitTicks;
        public readonly Queue<Command> Commands = new();
        public uint LastCommandTick;
        public uint DoorReadyTick;
        public int CatchUpTicks;
        public byte Level = 1;
        public int Xp;
        public uint LastHurtTick;
        public byte Dodges;
        public GuildRank Rank;
        public int Gold;
        public byte QuestId;
        public byte QuestProgress;

        /// <summary>Items held, by index. An item stays listed at zero so the client hears that it is gone.</summary>
        public readonly SortedDictionary<ushort, int> Bag = new();
        public readonly HashSet<ushort> BagChanged = new();
        public int BagCursor;

        /// <summary>Gear carried, by place; a place is empty when null. Worn pieces stay in the list.</summary>
        public readonly GearPiece?[] Gear = new GearPiece?[GearSlots];
        public readonly HashSet<int> Worn = new();
        public readonly HashSet<int> GearChanged = new();
        public int GearCursor;

        /// <summary>The fine aim at the moment the bow was let go, for the arrow that leaves next tick.</summary>
        public ushort ShotYaw;
        public short ShotPitch;

        /// <summary>What the worn gear adds up to; worked out again whenever it changes.</summary>
        public GearStats Stats = new();
        public float RegenCarry;
        public float LifestealCarry;
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

        /// <summary>For a player's arrow: who loosed it and how far the bow was drawn, as a share of a full draw.</summary>
        public int Shooter = -1;

        /// <summary>For a player's shot, the attack whose damage it carries.</summary>
        public AttackKind Blow = AttackKind.BowShot;
        public float Draw = 1f;
        public Vector3 Position;
        public Vector3 Velocity;
        public int Life = ProjectileLifeTicks;
    }

    /// <summary>A crystal or item on the ground. Only the player who earned it sees it and can pick it up.</summary>
    private sealed class Loot
    {
        public required int Id;
        public required ushort Item;

        /// <summary>The rolled piece, when the loot is gear.</summary>
        public GearPiece? Gear;
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
    private readonly List<Loot> _loot = new();
    private readonly List<FireWallHazard> _walls = new();
    private readonly List<Enemy> _finishedTrials = new();
    private int _nextEnemyId;
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
            var herd = new List<Enemy>();
            for (var i = 0; i < spawn.Count; i++)
            {
                var enemy = new Enemy { Id = _nextEnemyId++, Type = 0, Home = spawn, Herd = herd };
                enemy.Reset(_random, world);
                _enemies.Add(enemy);
                herd.Add(enemy);
            }
        }
    }

    public PlayerState? GetPlayer(int id) => _players.TryGetValue(id, out var player) ? player.State : null;

    public int GetLevel(int id) => _players[id].Level;

    public int GetXp(int id) => _players[id].Xp;

    public int GetCrystals(int id, CrystalRarity rarity) => _players[id].Crystals[(int)rarity];

    public GuildRank GetRank(int id) => _players[id].Rank;

    public int GetGold(int id) => _players[id].Gold;

    public byte GetQuest(int id) => _players[id].QuestId;

    public int GetQuestProgress(int id) => QuestProgress(_players[id]);

    /// <summary>For tests: gives a player crystals as if looted.</summary>
    public void GiveCrystals(int id, CrystalRarity rarity, int count) => _players[id].Crystals[(int)rarity] += count;

    public int ProjectileCount => _projectiles.Count;

    public int FireWallCount => _walls.Count;

    public int CrystalsOnGround => _loot.Count(l => ItemDef.All[l.Item].IsCrystal);

    public int LootOnGround => _loot.Count;

    /// <summary>For tests: wounds a player without an enemy.</summary>
    public void HurtForTest(int id, short damage) => _players[id].State.Health -= damage;

    /// <summary>How many of an item a player holds.</summary>
    public int GetItem(int id, string item) => _players[id].Bag.GetValueOrDefault(ItemDef.Index(item));

    /// <summary>For tests: puts a piece of gear in a player's gear list and returns its place.</summary>
    public int GiveGear(int id, GearPiece piece)
    {
        var player = _players[id];
        var free = Array.IndexOf(player.Gear, null);
        player.Gear[free] = piece;
        player.GearChanged.Add(free);
        return free;
    }

    /// <summary>For tests: what a player's worn gear adds up to, and the player's maximum health.</summary>
    public GearStats GearOf(int id) => _players[id].Stats;

    public short MaxHealthOf(int id) => MaxHealth(_players[id]);

    /// <summary>For tests: the gear a player carries, by place.</summary>
    public IReadOnlyList<GearPiece?> GearList(int id) => _players[id].Gear;

    /// <summary>For tests: gives a player items as if looted.</summary>
    public void GiveItem(int id, string item, int count) => AddItem(_players[id], ItemDef.Index(item), count);

    private static void AddItem(Player player, ushort item, int count)
    {
        player.Bag[item] = Math.Clamp(player.Bag.GetValueOrDefault(item) + count, 0, MaxStack);
        player.BagChanged.Add(item);
    }

    /// <summary>Gives a player experience and raises the level as often as it allows.</summary>
    public void AwardXp(int id, int amount)
    {
        var player = _players[id];
        player.Xp += amount;
        while (player.Level < Progression.MaxLevel && player.Xp >= Progression.XpToNext(player.Level))
        {
            player.Xp -= Progression.XpToNext(player.Level);
            player.Level++;
            player.State.Health = MaxHealth(player);
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

        StepLoot();
        StepBleeding();
        foreach (var finished in _finishedTrials)
        {
            _enemies.Remove(finished);
        }

        _finishedTrials.Clear();
        if (!EnemiesFrozen)
        {
            var targets = _players.Values.OrderBy(p => p.Id)
                .Select(p => new Target(p.Id, p.State.Position, p.State.Velocity)).ToArray();
            foreach (var enemy in _enemies)
            {
                enemy.Step(_world, _random, targets, this);
            }
        }

        StepWalls();

        StepProjectiles();

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
            // Players spread round the spawn point, each at the first free spot from their own angle,
            // so nobody starts inside a wall or a doorway.
            var centre = _options.SpawnOverride ?? _world.Spawn;
            var spawn = centre with { Y = _world.GroundHeight(centre.X, centre.Z) };
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var angle = peer * 2.1f + attempt * 0.4f;
                var reach = 3f - attempt * 0.15f;
                var spot = centre + new Vector3(MathF.Cos(angle) * reach, 0, MathF.Sin(angle) * reach);
                spot.Y = _world.GroundHeight(spot.X, spot.Z);
                if (!_world.OverlapsPlayer(spot))
                {
                    spawn = spot;
                    break;
                }
            }

            player = new Player
            {
                Id = peer,
                State = PlayerState.Spawn(spawn),
                NextTick = commands.Min(c => c.Tick),
                WaitTicks = _options.InputBufferTicks,
            };
            _players.Add(peer, player);

            // Everyone starts with the wooden sword in hand: a weapon like any other.
            player.Gear[0] = new GearPiece(ItemDef.Index("wooden_sword"), Rarity.Common, 1, Array.Empty<AffixRoll>());
            player.GearChanged.Add(0);
            ToggleWorn(player, 0, player.Gear[0]!);
            if (_options.TestMode)
            {
                // Test runs start with one piece of each rarity, so gear can be checked at once.
                var kit = new[] { "twin_daggers", "iron_helm", "leather_jerkin", "chain_leggings", "hunters_longbow", "spiked_club", "bone_staff" };
                for (var i = 0; i < kit.Length; i++)
                {
                    player.Gear[i + 1] = GearPiece.Roll(ItemDef.Index(kit[i]), (Rarity)(i % 4), 5, _random);
                    player.GearChanged.Add(i + 1);
                }
            }
        }

        foreach (var command in commands.OrderBy(c => c.Tick))
        {
            if (command.Tick >= player.NextTick)
            {
                player.Inputs[command.Tick] = command;
            }

            // Movement that arrives late is dropped, but a request must still happen, exactly once.
            if (command.Command != Command.None && command.Tick > player.LastCommandTick)
            {
                player.LastCommandTick = command.Tick;
                player.Commands.Enqueue(command.Command);
            }
        }
    }

    /// <summary>
    /// Simulates the next input of a player. When it has not arrived the player simply waits: if
    /// the server ran on without it, a late input could never be used and its button press would
    /// be lost. Only an input that is truly gone, with later ones piling up, is skipped.
    /// </summary>
    private void RunInput(Player player)
    {
        if (!player.Inputs.Remove(player.NextTick, out var command))
        {
            Stats.MissedInputs++;
            player.CatchUpTicks = Math.Min(player.CatchUpTicks + 1, MaxCatchUpTicks);
            if (player.Inputs.Count >= Protocol.InputRedundancy)
            {
                player.NextTick = player.Inputs.First().Key;
            }

            return;
        }

        player.NextTick++;
        var drawing = player.State.DrawTicks > 0;
        var landed = Movement.Step(ref player.State, command, _world);
        if ((drawing && player.State.DrawTicks == 0) ||
            (player.State.Attack is AttackKind.StaffBolt or AttackKind.StaffWall && player.State.AttackTick == 0))
        {
            // A spell is aimed where the player pointed when it began.
            player.ShotYaw = command.AimYaw;
            player.ShotPitch = command.AimPitch;
        }

        if (landed == AttackKind.BowShot)
        {
            Loose(player);
        }
        else if (landed == AttackKind.StaffBolt)
        {
            CastFireball(player);
        }
        else if (landed == AttackKind.StaffWall)
        {
            CastWall(player);
        }
        else if (landed != AttackKind.None)
        {
            Strike(player, landed);
        }
    }

    private void StepPlayer(Player player)
    {
        if (player.WaitTicks > 0)
        {
            player.WaitTicks--;
            return;
        }

        // A client that stalled sends a burst when it recovers. It may run extra inputs only with
        // ticks it missed while stalled, so over time nobody simulates faster than real time.
        RunInput(player);
        if (player.Inputs.Count > _options.InputBufferTicks + CatchUpSurplus && player.CatchUpTicks > 0 && Tick % 2 == 0)
        {
            player.CatchUpTicks--;
            RunInput(player);
        }

        // Inputs beyond that come from a client that runs too fast. They are thrown away, and the
        // client is pulled back by the next snapshot.
        while (player.Inputs.Count > MaxInputBacklog)
        {
            player.Inputs.Remove(player.Inputs.First().Key);
            player.NextTick = player.Inputs.First().Key;
            Stats.DroppedInputs++;
        }

        while (player.Commands.TryDequeue(out var request))
        {
            Obey(player, request);
        }

        var maxHealth = MaxHealth(player);
        if (player.State.Health <= 0)
        {
            if (player.Rank == GuildRank.InTrial)
            {
                EndTrial(player);
                player.Rank = GuildRank.Newcomer;
            }

            player.State = PlayerState.Spawn(_world.Spawn, maxHealth);
            // Worn gear survives death, so the weapon it picks does too.
            player.State.Weapon = Weapons.Of(player.Stats.Weapon);
        }
        else if (Tick - player.LastHurtTick > RegenDelayTicks && player.State.Health < maxHealth)
        {
            // One point every third of a second, plus what regen gear gives each second; below a
            // third of the health, second wind makes it all heal faster.
            var perSecond = Tuning.TickRate / (float)RegenIntervalTicks + player.Stats.Regen;
            if (player.State.Health < maxHealth / 3)
            {
                perSecond *= 1f + player.Stats.SecondWind / 100f;
            }

            player.RegenCarry += perSecond / Tuning.TickRate;
            var heal = (int)player.RegenCarry;
            player.RegenCarry -= heal;
            player.State.Health = (short)Math.Min(maxHealth, player.State.Health + heal);
        }
    }

    /// <summary>A player's maximum health: from the level, plus what the worn gear adds.</summary>
    private static short MaxHealth(Player player) => (short)(Progression.MaxHealth(player.Level) + player.Stats.MaxHealth);

    /// <summary>Carries out what a player asked for, when the player is allowed to and close enough.</summary>
    private void Obey(Player player, Command command)
    {
        switch (command)
        {
            case Command.UseDoor when Tick >= player.DoorReadyTick:
                foreach (var portal in _world.Portals)
                {
                    if (Vector3.Distance(portal.From with { Y = 0 }, player.State.Position with { Y = 0 }) < Guild.DoorRange)
                    {
                        player.DoorReadyTick = Tick + DoorCooldownTicks;
                        Move(player, portal.To);
                        break;
                    }
                }

                break;
            case Command.BeginTrial when player.Rank == GuildRank.Newcomer && Near(player, "receptionist"):
                BeginTrial(player);
                break;
            case Command.SellCrystals when player.Rank == GuildRank.Adventurer && Near(player, "receptionist"):
                for (var i = 0; i < player.Crystals.Length; i++)
                {
                    player.Gold += player.Crystals[i] * Guild.CrystalPrices[i];
                    player.Crystals[i] = 0;
                }

                break;
            case Command.AcceptSlimeQuest when player.Rank == GuildRank.Adventurer && player.QuestId == 0 && Near(player, "quest_board"):
                player.QuestId = 1;
                player.QuestProgress = 0;
                break;
            case Command.AcceptGoblinQuest when player.Rank == GuildRank.Adventurer && player.QuestId == 0 && Near(player, "quest_board"):
                player.QuestId = 2;
                player.QuestProgress = 0;
                break;
            case var take when QuestCommands.TryAccept(take, out var job) && player.Rank == GuildRank.Adventurer &&
                               player.QuestId == 0 && Near(player, "quest_board"):
                player.QuestId = job;
                player.QuestProgress = 0;
                break;
            case Command.SellLoot when player.Rank == GuildRank.Adventurer && Near(player, "appraiser"):
                foreach (var (item, count) in player.Bag.Where(p => p.Value > 0 && ItemDef.All[p.Key].Sellable).ToList())
                {
                    player.Gold += count * ItemDef.All[item].Price;
                    Stats.ItemsSold += count;
                    AddItem(player, item, -count);
                }

                break;
            case var eat when ItemCommands.TryEat(eat, out var food) && ItemDef.All[food].IsFood &&
                              player.Bag.GetValueOrDefault(food) > 0 && player.State.Health > 0:
                AddItem(player, food, -1);
                player.State.Health = (short)Math.Min(player.State.Health + ItemDef.All[food].Heal, MaxHealth(player));
                Stats.MealsEaten++;
                break;
            case var wear when GearCommands.TryToggle(wear, out var place) && player.Gear[place] is { } piece:
                ToggleWorn(player, place, piece);
                break;
            case var discard when GearCommands.TryDiscard(discard, out var spot) && player.Gear[spot] is not null && !player.Worn.Contains(spot):
                player.Gear[spot] = null;
                player.GearChanged.Add(spot);
                break;
            case Command.TurnInQuest when Guild.Quest(player.QuestId) is { } quest && QuestProgress(player) >= quest.Count && Near(player, "receptionist"):
                if (quest.IsDelivery)
                {
                    AddItem(player, ItemDef.Index(quest.Item), -quest.Count);
                }

                player.Gold += quest.Gold;
                player.QuestId = 0;
                player.QuestProgress = 0;
                AwardXp(player.Id, quest.Xp);
                break;
        }
    }

    /// <summary>Kills counted so far, or for a delivery the items in the bag, up to what the job asks.</summary>
    private static int QuestProgress(Player player) => Guild.Quest(player.QuestId) is { IsDelivery: true } quest
        ? Math.Min(player.Bag.GetValueOrDefault(ItemDef.Index(quest.Item)), quest.Count)
        : player.QuestProgress;

    private bool Near(Player player, string role) => _world.Npcs.Any(n => n.Role == role &&
        Vector3.Distance(n.Position with { Y = 0 }, player.State.Position with { Y = 0 }) < Guild.TalkRange + 1f);

    private void Move(Player player, Vector3 to)
    {
        to.Y = _world.GroundHeight(to.X, to.Z);
        player.State.Position = to;
        player.State.Velocity = Vector3.Zero;
    }

    private void BeginTrial(Player player)
    {
        player.Rank = GuildRank.InTrial;
        player.State.Health = MaxHealth(player);
        var ground = _world.TrialGround;
        Move(player, ground + new Vector3(0, 0, 7));
        var type = (byte)EnemyDef.All.ToList().FindIndex(d => d.Id == Guild.ExaminerId);
        var examiner = new Enemy
        {
            Id = _nextEnemyId++,
            Type = type,
            Home = new EnemySpawn(ground, 2f, 1, 2, 2, int.MaxValue, false, new[] { Guild.ExaminerId }),
            TrialOf = player.Id,
        };
        examiner.Reset(_random, _world);
        examiner.TargetId = player.Id;
        if (_options.TestMode)
        {
            examiner.Health = 1;
            player.Crystals[0] += 3;
        }

        _enemies.Add(examiner);
    }

    private void EndTrial(Player player) => _enemies.RemoveAll(e => e.TrialOf == player.Id);

    /// <summary>Hurts a player, unless a dash avoids it. Returns true when the attack connects.</summary>
    private bool Hurt(Player player, short damage, byte slowTicks, Enemy? attacker = null)
    {
        if (player.State.DashTicks > 0)
        {
            player.Dodges++;
            Stats.Dodges++;
            return false;
        }

        // Defense takes a share off every blow; at least one point always gets through.
        damage = (short)Math.Max(1, MathF.Round(damage * GearStats.DamageTaken(player.Stats.Defense)));
        player.State.Health -= damage;
        if (attacker is { Alive: true } && player.Stats.Thorns > 0 && attacker.TakeHit((short)player.Stats.Thorns, player.State.Position, player.Id))
        {
            OnKilled(attacker, player);
        }

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

            var damage = BlowDamage(player, kind, enemy);
            var killed = enemy.TakeHit(damage, player.State.Position, player.Id);
            AfterBlow(player, enemy, damage);
            Stats.EnemyHits++;
            CallForHelp(enemy, player.Id);
            if (killed)
            {
                OnKilled(enemy, player);
            }
        }
    }

    /// <summary>
    /// The damage of one blow: the attack, scaled by level and the weapon's power, plus flat
    /// damage; then every percentage bonus that applies to this target and this blow; a critical
    /// hit; and last the target's defense, less the armour the weapon pierces.
    /// </summary>
    private short BlowDamage(Player player, AttackKind kind, Enemy enemy)
    {
        var gear = player.Stats;
        var damage = Tuning.Attack(kind).Damage * Progression.DamageScale(player.Level) * gear.Power + gear.Damage;
        var bonus = gear.DamagePct;
        if (enemy.Def.Kind == "goblin")
        {
            bonus += gear.GoblinBane;
        }
        else if (enemy.Def.Kind is "animal" or "slime")
        {
            bonus += gear.BeastBane;
        }

        if (kind == AttackKind.Heavy)
        {
            bonus += gear.Crushing;
        }

        // From behind: the enemy faces away from the player.
        var facing = new Vector3(-MathF.Sin(enemy.Facing), 0, -MathF.Cos(enemy.Facing));
        var fromEnemy = player.State.Position - enemy.Position;
        fromEnemy.Y = 0;
        if (gear.Backstab > 0 && fromEnemy != Vector3.Zero && Vector3.Dot(Vector3.Normalize(fromEnemy), facing) < -0.3f)
        {
            bonus += gear.Backstab;
        }

        damage *= 1f + bonus / 100f;
        if (_random.Next(100) < gear.CritChance)
        {
            damage *= 1f + (GearStats.BaseCritDamage + gear.CritDamage) / 100f;
            Stats.CriticalHits++;
        }

        damage *= GearStats.DamageTaken(enemy.Def.Defense - gear.ArmorPen);
        return (short)Math.Max(1, MathF.Round(damage));
    }

    /// <summary>What a landed blow does besides its damage: lifesteal heals, and it may cause bleeding.</summary>
    private void AfterBlow(Player player, Enemy enemy, short damage)
    {
        var gear = player.Stats;
        if (gear.Lifesteal > 0)
        {
            player.LifestealCarry += damage * gear.Lifesteal / 100f;
            var heal = (int)player.LifestealCarry;
            player.LifestealCarry -= heal;
            player.State.Health = (short)Math.Min(MaxHealth(player), player.State.Health + heal);
        }

        if (enemy.Alive && _random.Next(100) < gear.Bleed)
        {
            enemy.BleedTicks = BleedTicks;
            enemy.BleedDamage = (short)Math.Max(1, MathF.Round(damage * BleedShare / (BleedTicks / (float)Tuning.TickRate)));
            enemy.BleedOwner = player.Id;
        }
    }

    /// <summary>Bleeding enemies lose health once a second until it runs out.</summary>
    private void StepBleeding()
    {
        foreach (var enemy in _enemies)
        {
            if (enemy.BleedTicks <= 0 || !enemy.Alive)
            {
                continue;
            }

            enemy.BleedTicks--;
            if (enemy.BleedTicks % Tuning.TickRate != 0)
            {
                continue;
            }

            var owner = _players.GetValueOrDefault(enemy.BleedOwner);
            if (enemy.TakeHit(enemy.BleedDamage, enemy.Position, enemy.BleedOwner) && owner is not null)
            {
                OnKilled(enemy, owner);
            }
        }
    }

    /// <summary>Puts a piece on, taking off whatever was worn in its slot, or takes it off.</summary>
    private static void ToggleWorn(Player player, int place, GearPiece piece)
    {
        if (!player.Worn.Remove(place))
        {
            foreach (var other in player.Worn.Where(o => player.Gear[o]?.Def.Slot == piece.Def.Slot).ToList())
            {
                player.Worn.Remove(other);
                player.GearChanged.Add(other);
            }

            player.Worn.Add(place);
        }

        player.GearChanged.Add(place);
        player.Stats = GearStats.Of(player.Worn.Select(w => player.Gear[w]!));
        player.State.Health = Math.Min(player.State.Health, MaxHealth(player));

        // The weapon decides the attacks; the client learns it through the next snapshot.
        player.State.Weapon = Weapons.Of(player.Stats.Weapon);
    }

    /// <summary>Rolls an enemy's gear table: maybe one piece, of a rarity picked by the table's weights.</summary>
    private void DropGear(Enemy enemy, Player owner)
    {
        if (enemy.Def.Gear is not { } table || _random.NextDouble() >= table.Chance)
        {
            return;
        }

        var item = ItemDef.Index(table.Items[_random.Next(table.Items.Length)]);
        var piece = GearPiece.Roll(item, GearPiece.RollRarity(table.Rarity, _random), (byte)Math.Clamp((int)enemy.Level, 1, 255), _random);
        var loot = Drop(enemy, owner, item);
        loot.Gear = piece;
        Stats.GearDropped[(int)piece.Rarity]++;
    }

    private void OnKilled(Enemy enemy, Player killer)
    {
        Stats.EnemiesKilled++;
        if (enemy.TrialOf >= 0)
        {
            killer.Rank = GuildRank.Adventurer;
            killer.Gold += 25;
            AwardXp(killer.Id, 60);
            _finishedTrials.Add(enemy);
            return;
        }

        if (Guild.Quest(killer.QuestId) is { } quest && quest.Matches(enemy.Def.Id) && killer.QuestProgress < quest.Count)
        {
            killer.QuestProgress++;
        }

        var winner = _players.TryGetValue(enemy.TaggedBy, out var tagged) ? tagged : killer;
        AwardXp(winner.Id, Progression.KillXp(enemy.Level, winner.Level, enemy.Def.Rare));
        foreach (var drop in enemy.Def.Drops ?? Array.Empty<DropEntry>())
        {
            if (_random.NextDouble() < drop.Chance)
            {
                Drop(enemy, winner, ItemDef.CrystalIndex(drop.Rarity));
                Stats.CrystalsDropped++;
            }
        }

        DropGear(enemy, winner);
        foreach (var entry in enemy.Def.Loot ?? Array.Empty<LootEntry>())
        {
            if (_random.NextDouble() >= entry.Chance)
            {
                continue;
            }

            var item = ItemDef.Index(entry.Item);
            var count = _random.Next(entry.Min, entry.Max + 1);
            for (var i = 0; i < count; i++)
            {
                Drop(enemy, winner, item);
                Stats.ItemsDropped++;
            }
        }
    }

    private Loot Drop(Enemy enemy, Player owner, ushort item)
    {
        var angle = (float)(_random.NextDouble() * Math.Tau);
        var reach = 0.5f + (float)_random.NextDouble() * 0.6f;
        var spot = enemy.Position + new Vector3(MathF.Cos(angle) * reach, 0, MathF.Sin(angle) * reach);
        spot.Y = _world.GroundHeight(spot.X, spot.Z);
        var loot = new Loot { Id = _nextId++, Item = item, Owner = owner.Id, Position = spot };
        _loot.Add(loot);
        return loot;
    }

    private void StepLoot()
    {
        foreach (var loot in _loot)
        {
            loot.Life--;
            if (!_players.TryGetValue(loot.Owner, out var owner) ||
                Vector3.Distance(owner.State.Position, loot.Position) >= LootRadius)
            {
                continue;
            }

            var def = ItemDef.All[loot.Item];
            if (loot.Gear is { } piece)
            {
                // Gear waits on the ground while the gear list is full.
                var free = Array.IndexOf(owner.Gear, null);
                if (free < 0)
                {
                    continue;
                }

                owner.Gear[free] = piece;
                owner.GearChanged.Add(free);
                Stats.ItemsLooted++;
            }
            else if (def.IsCrystal)
            {
                owner.Crystals[def.Crystal]++;
                Stats.CrystalsLooted++;
            }
            else
            {
                AddItem(owner, loot.Item, 1);
                Stats.ItemsLooted++;
            }

            loot.Life = 0;
        }

        _loot.RemoveAll(l => l.Life <= 0);
    }

    /// <summary>Changed places of the gear list first, then one more place that moves on each snapshot.</summary>
    private static GearEntry[] GearSlice(Player player)
    {
        var entries = new List<GearEntry>(GearEntriesPerSnapshot);
        foreach (var place in player.GearChanged.Take(GearEntriesPerSnapshot).ToList())
        {
            entries.Add(new GearEntry((byte)place, player.Gear[place], player.Worn.Contains(place)));
            player.GearChanged.Remove(place);
        }

        if (entries.Count < GearEntriesPerSnapshot)
        {
            var place = player.GearCursor++ % GearSlots;
            if (entries.All(e => e.Index != place))
            {
                entries.Add(new GearEntry((byte)place, player.Gear[place], player.Worn.Contains(place)));
            }
        }

        return entries.ToArray();
    }

    /// <summary>Changed items first, then a slice of the rest that moves on with every snapshot.</summary>
    private static BagEntry[] BagSlice(Player player)
    {
        var entries = new List<BagEntry>(BagEntriesPerSnapshot);
        foreach (var item in player.BagChanged.Take(BagEntriesPerSnapshot).ToList())
        {
            entries.Add(new BagEntry(item, (ushort)player.Bag[item]));
            player.BagChanged.Remove(item);
        }

        var keys = player.Bag.Keys.ToList();
        for (var i = 0; i < keys.Count && entries.Count < BagEntriesPerSnapshot; i++)
        {
            var item = keys[player.BagCursor++ % keys.Count];
            if (entries.All(e => e.Item != item))
            {
                entries.Add(new BagEntry(item, (ushort)player.Bag[item]));
            }
        }

        return entries.ToArray();
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
            if (away.Length() > enemy.Def.SplashRadius || !Hurt(player, enemy.Def.SplashDamage, enemy.Def.SlowTicks, enemy))
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
        // The attack goes where the warning showed, even if the player moved after it locked.
        var aimPoint = enemy.LeapTarget + Vector3.UnitY;
        var direction = aimPoint - chest;
        direction = direction == Vector3.Zero ? Vector3.UnitZ : Vector3.Normalize(direction);
        switch (ability.Effect)
        {
            case "melee":
            case "dashStrike":
                Area(enemy.Position + Facing(enemy) * 1.1f, ability.Radius, ability.Damage, enemy);
                break;
            case "slam":
                Area(enemy.LeapTarget, ability.Radius, ability.Damage, enemy);
                break;
            case "spin":
                Area(enemy.Position, ability.Radius, ability.Damage, enemy);
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

    /// <summary>A blow that lands on every player in a circle; the attacker feels any thorns.</summary>
    private void Area(Vector3 center, float radius, short damage, Enemy attacker)
    {
        foreach (var player in _players.Values.ToList())
        {
            if (Vector3.Distance(player.State.Position, center) <= radius + 0.4f)
            {
                Hurt(player, damage, 0, attacker);
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
            // Frozen enemies (in tests) hold their own fire still; a player's still burns.
            if (EnemiesFrozen && wall.Owner < 0)
            {
                continue;
            }

            wall.Age++;
            var length = wall.Length;
            if (wall.Owner >= 0)
            {
                BurnEnemies(wall, length);
                continue;
            }

            foreach (var player in _players.Values)
            {
                if (!wall.Struck.Contains(player.Id) && InWedge(wall, player.State.Position, length))
                {
                    wall.Struck.Add(player.Id);
                    Hurt(player, wall.Damage, 0);
                }
            }
        }

        _walls.RemoveAll(w => w.Age > w.Grow + w.Burn || (w.Owner >= 0 && !_players.ContainsKey(w.Owner)));
    }

    /// <summary>A player's line of fire: every enemy inside the strip takes the staff's blow, once.</summary>
    private void BurnEnemies(FireWallHazard wall, float length)
    {
        var caster = _players[wall.Owner];
        foreach (var enemy in _enemies.ToList())
        {
            if (!enemy.Alive || wall.Struck.Contains(enemy.Id) || !InStrip(wall, enemy.Position, length, enemy.Def.Size * 0.4f))
            {
                continue;
            }

            wall.Struck.Add(enemy.Id);
            Smite(caster, enemy, AttackKind.StaffWall, 1f, wall.Origin);
        }
    }

    private static bool InStrip(FireWallHazard wall, Vector3 position, float length, float margin)
    {
        var offset = (position - wall.Origin) with { Y = 0 };
        var along = Vector3.Dot(offset, wall.Direction);
        var across = MathF.Abs(offset.X * wall.Direction.Z - offset.Z * wall.Direction.X);
        return along >= -margin && along <= length + margin && across <= wall.HalfWidth + margin;
    }

    /// <summary>One blow of a player's attack on an enemy, scaled by share, with all that follows from it.</summary>
    private void Smite(Player player, Enemy enemy, AttackKind kind, float share, Vector3 from)
    {
        var damage = (short)Math.Max(1, MathF.Round(BlowDamage(player, kind, enemy) * share));
        var killed = enemy.TakeHit(damage, from, player.Id);
        AfterBlow(player, enemy, damage);
        Stats.EnemyHits++;
        CallForHelp(enemy, player.Id);
        if (killed)
        {
            OnKilled(enemy, player);
        }
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

    /// <summary>
    /// A player looses an arrow exactly where they aimed, up or down as well: no help finds the
    /// target. A fuller draw flies faster and further; every arrow falls, and is spent at its range.
    /// </summary>
    private void Loose(Player player)
    {
        var yaw = player.ShotYaw / 65536f * MathF.Tau;
        var pitch = Math.Clamp(player.ShotPitch / 65536f * MathF.Tau, -Tuning.MaxShotPitch, Tuning.MaxShotPitch);
        var flat = new Vector3(-MathF.Sin(yaw), 0, -MathF.Cos(yaw));
        var aim = flat * MathF.Cos(pitch) + Vector3.UnitY * MathF.Sin(pitch);
        var drawn = Tuning.Drawn(player.State.ShotDraw);
        var speed = Tuning.ArrowSpeed(drawn);
        _projectiles.Add(new Projectile
        {
            Id = _nextId++,
            Kind = ProjectileKind.PlayerArrow,
            Damage = 0,
            Position = ArrowOrigin(player.State.Position, flat),
            Velocity = aim * speed,
            Gravity = Tuning.ArrowGravity,
            Shooter = player.Id,
            Draw = Tuning.DrawShare(player.State.ShotDraw),
            Life = (int)MathF.Ceiling(Tuning.ArrowRange(drawn) / speed * Tuning.TickRate),
        });
    }

    /// <summary>The aim a player last fixed for a shot or a spell, as a direction, and flat.</summary>
    private static (Vector3 Aim, Vector3 Flat) ShotDirection(Player player)
    {
        var yaw = player.ShotYaw / 65536f * MathF.Tau;
        var pitch = Math.Clamp(player.ShotPitch / 65536f * MathF.Tau, -Tuning.MaxShotPitch, Tuning.MaxShotPitch);
        var flat = new Vector3(-MathF.Sin(yaw), 0, -MathF.Cos(yaw));
        return (flat * MathF.Cos(pitch) + Vector3.UnitY * MathF.Sin(pitch), flat);
    }

    /// <summary>A fireball from the staff, straight along the aim; it bursts on what it meets.</summary>
    private void CastFireball(Player player)
    {
        var (aim, flat) = ShotDirection(player);
        _projectiles.Add(new Projectile
        {
            Id = _nextId++,
            Kind = ProjectileKind.PlayerFireball,
            Damage = 0,
            Position = ArrowOrigin(player.State.Position, flat) + flat * 0.4f,
            Velocity = aim * Tuning.StaffBoltSpeed,
            Shooter = player.Id,
            Blow = AttackKind.StaffBolt,
            SplashRadius = Tuning.StaffBoltSplash,
            Life = (int)MathF.Ceiling(Tuning.StaffBoltRange / Tuning.StaffBoltSpeed * Tuning.TickRate),
        });
    }

    /// <summary>A straight strip of fire from the player's feet along the aim, burning each enemy once.</summary>
    private void CastWall(Player player)
    {
        var (_, flat) = ShotDirection(player);
        _walls.Add(new FireWallHazard
        {
            Id = _nextId++,
            Origin = player.State.Position + flat * 0.6f,
            Direction = flat,
            Damage = 0,
            Owner = player.Id,
            HalfWidth = Tuning.StaffWallHalfWidth,
            Span = Tuning.StaffWallLength,
            Grow = Tuning.StaffWallGrowTicks,
            Burn = Tuning.StaffWallBurnTicks,
        });
    }

    /// <summary>Where a player's arrow starts: at the bow, in front of the chest.</summary>
    public static Vector3 ArrowOrigin(Vector3 feet, Vector3 flatAim) => feet + new Vector3(0, 1.4f, 0) + flatAim * 0.6f;

    /// <summary>A player's arrow strikes the first living enemy it reaches.</summary>
    private bool StrikeWithArrow(Projectile shot)
    {
        if (!_players.TryGetValue(shot.Shooter, out var shooter))
        {
            return true;
        }

        foreach (var enemy in _enemies)
        {
            var chest = enemy.Position + new Vector3(0, enemy.Def.Size * 0.6f, 0);
            if (!enemy.Alive || Vector3.Distance(chest, shot.Position) >= HitRadius + enemy.Def.Size * 0.35f)
            {
                continue;
            }

            var damage = (short)Math.Max(1, MathF.Round(BlowDamage(shooter, AttackKind.BowShot, enemy) * shot.Draw));
            var killed = enemy.TakeHit(damage, shot.Position - shot.Velocity, shooter.Id);
            AfterBlow(shooter, enemy, damage);
            Stats.EnemyHits++;
            CallForHelp(enemy, shooter.Id);
            if (killed)
            {
                OnKilled(enemy, shooter);
            }

            return true;
        }

        return false;
    }

    private void StepProjectiles()
    {
        foreach (var shot in _projectiles.ToList())
        {
            // Frozen enemies (in tests) hold their own shots in the air; players' arrows still fly.
            if (EnemiesFrozen && shot.Shooter < 0)
            {
                continue;
            }

            shot.Velocity.Y -= shot.Gravity * Tuning.TickSeconds;
            shot.Position += shot.Velocity * Tuning.TickSeconds;
            shot.Life--;
            var struck = !shot.Pierces && _world.IsBlocked(shot.Position);
            if (shot.Shooter >= 0)
            {
                if (shot.Kind == ProjectileKind.PlayerFireball)
                {
                    if (struck || shot.Life <= 0 || TouchesEnemy(shot))
                    {
                        BurstOnEnemies(shot);
                        shot.Life = 0;
                    }
                }
                else if (struck || StrikeWithArrow(shot))
                {
                    shot.Life = 0;
                }

                continue;
            }

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

    private bool TouchesEnemy(Projectile shot) => _enemies.Any(e => e.Alive &&
        Vector3.Distance(e.Position + new Vector3(0, e.Def.Size * 0.6f, 0), shot.Position) < HitRadius + e.Def.Size * 0.35f);

    /// <summary>A player's fireball bursts: everything inside the splash takes the blow, more at the centre.</summary>
    private void BurstOnEnemies(Projectile shot)
    {
        if (!_players.TryGetValue(shot.Shooter, out var caster))
        {
            return;
        }

        foreach (var enemy in _enemies.ToList())
        {
            var chest = enemy.Position + new Vector3(0, enemy.Def.Size * 0.6f, 0);
            var distance = Vector3.Distance(chest, shot.Position) - enemy.Def.Size * 0.35f;
            if (enemy.Alive && distance < shot.SplashRadius)
            {
                Smite(caster, enemy, shot.Blow, distance < HitRadius ? 1f : 0.6f, shot.Position - shot.Velocity);
            }
        }
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
                .OrderBy(e => Vector3.Distance(e.Position, origin) + (e.IsAnimal ? AnimalInterestPenalty : 0f))
                .Take(MaxEnemiesPerSnapshot)
                .Select(e => new EnemyView(e.Id, e.Type, e.Level, e.Position, e.Facing, e.Health, (short)e.MaxHealth,
                    e.Action, e.HitFlash, e.LeapTarget, e.TargetId >= 0, e.AbilityIndex)).ToArray();
            var shots = _projectiles
                .OrderBy(s => Vector3.Distance(s.Position, origin)).Take(MaxProjectilesPerSnapshot)
                .Select(s => new ProjectileView(s.Id, s.Kind, s.Position)).ToArray();
            var drops = _loot.Where(l => l.Owner == player.Id)
                .OrderBy(l => Vector3.Distance(l.Position, origin)).Take(MaxDropsPerSnapshot)
                .Select(l => new LootView(l.Id, l.Item, l.Position, l.Gear is { } piece ? (byte)piece.Rarity : LootView.NoRarity)).ToArray();
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
                MaxHealth = MaxHealth(player),
                Dodges = player.Dodges,
                Rank = player.Rank,
                Gold = player.Gold,
                QuestId = player.QuestId,
                QuestProgress = (byte)QuestProgress(player),
                Crystals = player.Crystals.ToArray(),
                Others = _players.Values.Where(p => p.Id != player.Id).Take(6)
                    .Select(p => new RemotePlayer(p.Id, p.State.Position, p.State.Velocity, p.State.AttackAim, p.State.Attack, p.State.AttackTick, p.State.Weapon)).ToArray(),
                Enemies = enemies,
                Projectiles = shots,
                Drops = drops,
                Bag = BagSlice(player),
                Gear = GearSlice(player),
                FireWalls = _walls.Where(w => Vector3.Distance(w.Origin, origin) < InterestRange).Take(2)
                    .Select(w => new FireWallView(w.Id, w.Origin, MathF.Atan2(-w.Direction.X, -w.Direction.Z), w.Length, w.HalfWidth)).ToArray(),
            };
            _transport.Send(player.Id, Protocol.EncodeSnapshot(snapshot));
        }
    }
}
