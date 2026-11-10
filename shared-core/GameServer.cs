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

    /// <summary>The Guild examiner starts with one hit point, for the bot that plays the Guild story.</summary>
    public bool FrailExaminer { get; init; }

    /// <summary>A saved game to continue: the world at once, the player when they join.</summary>
    public SaveData? Save { get; init; }

    /// <summary>A new game begins with the prologue: the fall, the cage, the escape. Off for test runs and for saves.</summary>
    public bool Prologue { get; init; }

    /// <summary>New players start with the default look instead of an unchosen one, so no creator screen opens (tests and tools).</summary>
    public bool ChosenLook { get; init; } = true;
}

public sealed class ServerStats
{
    public long MissedInputs;
    public long DroppedInputs;
    public long EnemiesKilled;

    /// <summary>The largest snapshot the server encoded before trimming it to fit one packet, in bytes.</summary>
    public int LargestSnapshot;
    public long PlayerHits;
    public long EnemyHits;
    public long Splashes;
    public long Spits;
    public long Dodges;
    public long PotionsDrunk;
    public long SkillsLearned;
    public long SkillsCast;
    public long BooksDropped;
    public long Blocks;
    public long Parries;
    public long Ripostes;
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

    /// <summary>An orc longsword's heavy blow: a standing crescent of air that flies a few metres.</summary>
    AirSlash = 6,

    /// <summary>An orc spearwoman's thrown spear. It flies flat and fast; she always has it back in hand.</summary>
    ThrownSpear = 7,

    /// <summary>The swordmaster's flat crescent: as wide as a man is tall, lying level as it flies.</summary>
    AirSlashWide = 8,

    /// <summary>A spear the spearcaller conjures over her head; it hangs there, then shoots at the player.</summary>
    SummonedSpear = 9,

    /// <summary>One of Redclaw's three slanted red crescents, torn from the air by a swipe of the paw.</summary>
    FangSlash = 10,

    /// <summary>Staff mastery Chain Lightning: a bolt that leaps from one enemy to the next.</summary>
    ChainBolt = 11,

    /// <summary>Dagger mastery Fan of Knives: one of the knives thrown in a ring.</summary>
    Knife = 12,

    /// <summary>Staff mastery Meteor: a burning rock falling on the end of the line of fire.</summary>
    Meteor = 13,
}

/// <summary>What a ground hazard is: the shaman's burning V or a player's strip of fire, or an orc's crack in the earth.</summary>
public enum HazardKind : byte
{
    Fire = 0,
    EarthCrack = 1,
}

/// <summary>An orc battle axe's heavy blow splits the ground in a small V ahead of it. It hurts only while the crack runs.</summary>
public static class EarthCrack
{
    public const float Length = 6.5f;
    public const float HalfAngle = 0.33f;
    public const int GrowTicks = 14;
    public const int LingerTicks = 54;
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
    public HazardKind Kind;

    /// <summary>For a player's skill: the skill whose damage it deals, or -1.</summary>
    public int Skill = -1;

    /// <summary>Half the opening of the V, in radians.</summary>
    public float Angle = HalfAngle;
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
    private const float AirSlashSpeed = 15f;
    private const float AirSlashReach = 8f;
    private const float AirSlashRadius = 1.4f;
    private const float WideSlashRadius = 2.1f;
    private const float ConjuredSpearSpeed = 26f;
    private const float FangSlashSpeed = 16f;
    private const float FangSlashReach = 9f;
    private const float FangSlashSpacing = 0.75f;

    /// <summary>When each conjured spear shoots, in ticks after they appear.</summary>
    private static readonly int[] SpearDelays = { 30, 46, 62 };
    private const float SpearSpeed = 24f;
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
        public readonly Queue<(Command Command, ushort Arg)> Commands = new();

        /// <summary>The fine aim of the last skill cast: through the crosshair.</summary>
        public ushort SkillYaw;
        public short SkillPitch;

        /// <summary>The Guild chest: stacks of items, and gear pieces by place, sent in slices like the bag.</summary>
        public readonly Dictionary<ushort, int> Storage = new();
        public readonly GearPiece?[] StoredGear = new GearPiece?[GearSlots];
        public readonly HashSet<ushort> StorageChanged = new();
        public readonly HashSet<int> StoredGearChanged = new();
        public int StorageCursor;
        public int StoredGearCursor;
        public uint LastCommandTick;
        public uint DoorReadyTick;
        public int CatchUpTicks;
        public byte Level = 1;

        /// <summary>Skills learned, by place in the skill list, and the tick from which each can be cast again.</summary>
        public readonly HashSet<int> Skills = new();
        public readonly uint[] SkillReady = new uint[SkillDef.All.Count];

        /// <summary>The skill in each skill slot, or <see cref="Skills.NoSkill"/>. Only slotted skills can be cast.</summary>
        public readonly byte[] SkillSlots = Enumerable.Repeat(Core.Skills.NoSkill, Core.Skills.Slots).ToArray();

        /// <summary>Weapon mastery by place in <see cref="Mastery.Weapons"/>: level and experience toward the next; and the nodes taken.</summary>
        public readonly byte[] MasteryLevels = new byte[Mastery.Weapons.Length];
        public readonly int[] MasteryXp = new int[Mastery.Weapons.Length];
        public ulong MasteryTaken;

        /// <summary>Points spent in each attribute.</summary>
        public readonly byte[] Attributes = new byte[Core.Attributes.Count];

        public int UnspentPoints => Core.Attributes.Points(Level) - Attributes.Sum(a => a);

        public int Attribute(PlayerAttribute which) => Attributes[(int)which];
        public int Xp;
        public uint LastHurtTick;

        /// <summary>Dagger mastery Shadow Step: steps in the current run, and the tick of the last.</summary>
        public int ShadowSteps;
        public uint LastShadowTick;

        /// <summary>The tick from which the next potion may be drunk.</summary>
        public uint PotionReady;
        public byte Dodges;
        public byte Blocks;
        public byte Parries;
        public byte Ripostes;

        /// <summary>Health the Arcane Barrier soaks up, and the part of a point it has refilled.</summary>
        public int Barrier;
        public float BarrierCarry;
        public GuildRank Rank;
        public int Gold;

        /// <summary>Story entries changed since this player last heard them, and the place of the rotating slice.</summary>
        public readonly HashSet<(StoryKind Kind, string Key)> StoryChanged = new();
        public int StoryCursor;

        /// <summary>Set while the player reads a dialogue or the menu.</summary>
        public bool Reading;

        /// <summary>The block, parry and dodge counts when the current round of the prologue trial began.</summary>
        public (byte Blocks, byte Parries, byte Dodges) TrialMark;

        /// <summary>How the player looks, one choice per <see cref="AppearanceField"/>.</summary>
        public byte[] Appearance = Core.Appearance.NotChosen;

        /// <summary>Every death after registering: where, when and with what.</summary>
        public readonly List<DeathRecord> Deaths = new();

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

        /// <summary>For a hunter's shot: the creature it is meant for, which it can hit as well as players.</summary>
        public Enemy? Prey;

        /// <summary>For a player's skill shot: the skill it carries, or -1, and the enemies it has cut already.</summary>
        public int Skill = -1;
        public readonly HashSet<int> Struck = new();

        /// <summary>For a player's conjured spear: the aim to shoot along when no enemy is near.</summary>
        public Vector3 Aim;

        /// <summary>A heavy shot partly breaks through a guard; arrows and spit do not.</summary>
        public bool Heavy;
        public int HunterId = -1;
        public required ProjectileKind Kind;
        public required short Damage;
        public byte SlowTicks;
        public float Gravity;
        public float SplashRadius;
        public bool Pierces;

        /// <summary>Bow mastery Piercing Arrow: the arrow flies on through every enemy it hits.</summary>
        public bool PiercesEnemies;

        /// <summary>For a chain bolt: leaps still to make after this one.</summary>
        public int Jumps;



        /// <summary>For a player's arrow: who loosed it and how far the bow was drawn, as a share of a full draw.</summary>
        public int Shooter = -1;

        /// <summary>For a player's shot, the attack whose damage it carries.</summary>
        public AttackKind Blow = AttackKind.BowShot;
        public float Draw = 1f;
        public Vector3 Position;
        public Vector3 Velocity;
        public int Life = ProjectileLifeTicks;

        /// <summary>How close to a player's chest the shot must pass to hit.</summary>
        public float Radius = HitRadius;

        /// <summary>For a conjured spear: ticks it still hangs in the air, and the player it then shoots at.</summary>
        public int Delay;
        public int TargetId = -1;
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

    /// <summary>Groups that walk a route instead of staying in one camp.</summary>
    private readonly List<Patrol> _patrols = new();

    /// <summary>Camps and patrols that go hunting now and then.</summary>
    private readonly List<Hunt> _hunts = new();

    public IReadOnlyList<Hunt> Hunts => _hunts;

    /// <summary>The fighters holding a turn to come in close at each player.</summary>
    private readonly Dictionary<int, HashSet<Enemy>> _turns = new();

    /// <summary>Until which tick a pack leader's call lets more of the pack strike a player at once.</summary>
    private readonly Dictionary<int, uint> _rushUntil = new();

    /// <summary>How many fighters may come in close at one player, and how many more in a pack's rush.</summary>
    public const int MaxCloseAttackers = 2;
    private const int RushExtra = 2;
    private const int RushTicks = 60 * 5;

    /// <summary>How long after one potion the next can be drunk: three seconds.</summary>
    public const int PotionCooldownTicks = 60 * 3;

    /// <summary>Clones called up during this tick's enemy steps; they join the enemies after the loop.</summary>
    private readonly List<Enemy> _summons = new();

    /// <summary>How long a shadow clone lasts, and how far to each side of its wolf it appears.</summary>
    private const int CloneLifeTicks = 60 * 14;
    private const float CloneSpread = 2.4f;
    private int _nextEnemyId;
    private int _nextId;

    public uint Tick { get; private set; }
    public ServerStats Stats { get; } = new();

    /// <summary>Stops enemies, so a test can check that both sides settle on the same state.</summary>
    public bool EnemiesFrozen { get; set; }

    public IReadOnlyList<Enemy> Enemies => _enemies;

    private readonly WorldState _story = new();
    private readonly Calendar _calendar = new();

    // The scene playing, played for the first player (the game is for one; co-op later).
    private SceneDef? _scene;
    private int _sceneStep;
    private uint _sceneStepTick;
    private int _sceneSlide;
    private int _sceneRead = -1;
    private bool _sceneTalked;
    private bool _sceneLocked;
    private bool _sceneEntered;
    private bool _sceneApart;

    // Where a player not yet bound to the Anchor Stone wakes after dying, set by a scene's checkpoint step.
    private Vector3? _checkpoint;
    private readonly Dictionary<int, string> _enemyTags = new();

    /// <summary>The scene playing and its step, for tests.</summary>
    public (string? Scene, int Step) SceneState => (_scene?.Id, _sceneStep);

    /// <summary>Starts a scene at once. For tests and tools; scenes also start from triggers and effects.</summary>
    public void StartScene(string id)
    {
        if (Story.Scene(id) is { } scene)
        {
            StartScene(scene);
        }
    }

    /// <summary>Starts a scene that need not be in the data, for tests.</summary>
    public void StartScene(SceneDef scene)
    {
        {
            _scene = scene;
            _sceneStep = 0;
            _sceneStepTick = Tick;
            _sceneSlide = 0;
            _sceneTalked = false;
            _sceneEntered = false;
        }
    }

    private readonly List<Actor> _actors;
    private readonly Dictionary<string, Actor> _actorById;

    /// <summary>Everyone who can move: the zone's NPCs and the scene actors. For scenes and tests.</summary>
    public IReadOnlyList<Actor> Actors => _actors;

    public Actor Actor(string id) => _actorById[id];

    public GameServer(ITransport transport, World world, ServerOptions options)
    {
        _transport = transport;
        _world = world;
        _options = options;
        _random = new Random(options.Seed);
        _actors = Core.Actors.For(world);
        _actorById = _actors.ToDictionary(a => a.Id);
        if (options.Save is { } save)
        {
            save.World.Into(_story);
            _calendar.Now = _story.Time;
        }

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

            if (spawn.Route is { Length: > 0 })
            {
                _patrols.Add(new Patrol(spawn, herd));
            }

            if (spawn.Hunts)
            {
                _hunts.Add(new Hunt(herd, spawn, _random));
            }
        }
    }

    public PlayerState? GetPlayer(int id) => _players.TryGetValue(id, out var player) ? player.State : null;

    public int GetLevel(int id) => _players[id].Level;

    public int GetXp(int id) => _players[id].Xp;

    public int GetCrystals(int id, CrystalRarity rarity) => _players[id].Crystals[(int)rarity];

    public GuildRank GetRank(int id) => _players[id].Rank;

    public int GetGold(int id) => _players[id].Gold;

    /// <summary>How many of an item a player holds. For tests and tools.</summary>
    public int GetBagCount(int id, ushort item) => _players[id].Bag.GetValueOrDefault(item);

    /// <summary>Puts items in a player's bag. For tests and tools.</summary>
    public void GiveItem(int id, ushort item, int count) => AddItem(_players[id], item, count);

    /// <summary>Hands a player gold. For tests and tools.</summary>
    public void GiveGold(int id, int gold) => _players[id].Gold += gold;

    /// <summary>Sets a player's health. For tests and tools.</summary>
    public void SetHealth(int id, short health) => _players[id].State.Health = health;

    /// <summary>The world the story remembers: flags, quests, fates, the calendar. One for the whole server.</summary>
    public WorldState StoryState => _story;

    /// <summary>The story calendar, which the server moves on with its ticks.</summary>
    public Calendar Calendar => _calendar;

    /// <summary>Called with a save each morning and when the player quits. The server process writes it to disk.</summary>
    public Action<SaveData>? Autosave { get; set; }

    /// <summary>The story context of one player, for tests that check conditions as the server sees them.</summary>
    public IStoryActions StoryFor(int id) => new StoryActions(this, _players[id]);

    /// <summary>For tests: gives a player crystals as if looted.</summary>
    public void GiveCrystals(int id, CrystalRarity rarity, int count) => _players[id].Crystals[(int)rarity] += count;

    public int ProjectileCount => _projectiles.Count;

    public int FireWallCount => _walls.Count;

    public int CrystalsOnGround => _loot.Count(l => ItemDef.All[l.Item].IsCrystal);

    public int LootOnGround => _loot.Count;

    /// <summary>For tests: wounds a player without an enemy.</summary>
    public void HurtForTest(int id, short damage) => _players[id].State.Health -= damage;

    /// <summary>Sets a player's standing with the Guild. For tests and tools.</summary>
    public void SetRank(int id, GuildRank rank) => _players[id].Rank = rank;

    /// <summary>The deaths written down for a player. For tests and tools.</summary>
    public IReadOnlyList<DeathRecord> GetDeaths(int id) => _players[id].Deaths;

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
        StepCalendar();
        StepActors();
        StepScene();
        foreach (var finished in _finishedTrials)
        {
            _enemies.Remove(finished);
        }

        _finishedTrials.Clear();
        if (!EnemiesFrozen)
        {
            // A player a scene holds still (in the cage, in a talk) is out of the fight: nothing notices them.
            var held = _sceneLocked && !_sceneApart;
            var targets = _players.Values.Where(p => !held).OrderBy(p => p.Id)
                .Select(p => new Target(p.Id, p.State.Position, p.State.Velocity, p.State.Attack != AttackKind.None)).Concat(CreatureTargets()).ToArray();
            // The first night is the story's: from the cage to the morning in Hearthmere only what a scene
            // sends comes for the player. The wild leaves them be unless they strike it first.
            var firstNight = _story.Has("prologue.started") && !_story.Has("prologue.slept");
            var unseen = firstNight ? CreatureTargets().ToArray() : targets;
            foreach (var enemy in _enemies)
            {
                enemy.Crowd = _enemies;
                var wild = firstNight && !_enemyTags.ContainsKey(enemy.Id) && enemy.Health >= enemy.MaxHealth;
                enemy.Step(_world, _random, wild ? unseen : targets, this);
            }

            _enemies.AddRange(_summons);
            _summons.Clear();

            // A clone is gone once it falls or fades, and with the wolf that called it.
            var living = _enemies.Where(e => e.Alive && e.SummonedBy < 0).Select(e => e.Id).ToHashSet();
            _enemies.RemoveAll(e => e.SummonedBy >= 0 && (!e.Alive || !living.Contains(e.SummonedBy)));
        }

        foreach (var hunt in _hunts)
        {
            hunt.Step(_enemies, _random);
        }

        foreach (var patrol in _patrols)
        {
            patrol.Step(_random);
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

            if (_options.Save?.Player is { } saved && _players.Count == 1)
            {
                Restore(player, saved);
            }
            else if (_options.Prologue && _players.Count == 1)
            {
                _story.SetFlag("prologue.new");
            }
            else
            {
                // Everyone starts with the wooden sword in hand: a weapon like any other.
                player.Gear[0] = new GearPiece(ItemDef.Index("wooden_sword"), Rarity.Common, 1, Array.Empty<AffixRoll>());
                player.GearChanged.Add(0);
                ToggleWorn(player, 0, player.Gear[0]!);
            }

            if (_options.TestMode || _options.Save?.Player is null && _options.ChosenLook)
            {
                // Test runs and tools start with the hero as drawn, so no creator screen stops a bot.
                player.Appearance = Core.Appearance.Default;
            }

            if (_options.TestMode)
            {
                // Test runs know every skill and carry one book, so skills can be tried at once.
                for (var skill = 0; skill < SkillDef.All.Count; skill++)
                {
                    player.Skills.Add(skill);
                    FillSkillSlot(player, skill);
                }

                AddItem(player, SkillDef.All[0].Book, 1);
                // Every built mastery move is taken, with three points left to spend per weapon.
                foreach (var end in Mastery.Nodes.Where(n => n.Unlock is not null && n.Ready))
                {
                    foreach (var node in Mastery.Nodes.Where(n => n.Weapon == end.Weapon && n.Branch == end.Branch))
                    {
                        player.MasteryTaken |= 1ul << node.Index;
                    }
                }

                foreach (var weapon in Mastery.Weapons)
                {
                    player.MasteryLevels[Mastery.WeaponIndex(weapon)] = (byte)(Mastery.Spent(weapon, player.MasteryTaken) + 3);
                }

                RecomputeStats(player);

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
                player.Commands.Enqueue((command.Command, command.Arg));
                if (command.Command == Command.CastSkill)
                {
                    // A skill flies where the crosshair pointed when it was cast.
                    player.SkillYaw = command.AimYaw;
                    player.SkillPitch = command.AimPitch;
                }
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
        player.Reading = command.Buttons.HasFlag(Buttons.Reading);
        if (_sceneLocked && !_sceneApart)
        {
            // A scene holds the player still; the client holds its prediction still the same way.
            command.MoveX = 0;
            command.MoveZ = 0;
            command.Buttons &= Buttons.Reading;
        }

        var drawing = player.State.DrawTicks > 0;
        var landed = Movement.Step(ref player.State, command, _world);
        if ((drawing && player.State.DrawTicks == 0) ||
            (player.State.Attack is AttackKind.StaffBolt or AttackKind.StaffWall && player.State.AttackTick == 0))
        {
            // A spell is aimed where the player pointed when it began.
            player.ShotYaw = command.AimYaw;
            player.ShotPitch = command.AimPitch;
        }

        if (player.State.Attack == AttackKind.ShadowStep && player.State.AttackTick == 1)
        {
            ShadowStep(player);
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
            Obey(player, request.Command, request.Arg);
        }

        var maxHealth = MaxHealth(player);
        StepBarrier(player, maxHealth);
        StepTrialRounds(player);
        if (player.State.Health <= 0)
        {
            if (player.Rank == GuildRank.InTrial)
            {
                EndTrial(player);
                player.Rank = GuildRank.Newcomer;
            }
            else if (player.Rank == GuildRank.Adventurer)
            {
                // The Anchor Stone rebuilds the body from what the player carries: a tenth of the
                // crystals not yet sold. The death is written down for the echoes of act III.
                for (var i = 0; i < player.Crystals.Length; i++)
                {
                    player.Crystals[i] -= player.Crystals[i] / 10;
                }

                var at = player.State.Position;
                player.Deaths.Add(new DeathRecord(_calendar.Now.Day, new[] { at.X, at.Y, at.Z }, player.Level,
                    player.Stats.Weapon is { Length: > 0 } weapon ? weapon : "none"));
            }

            // A player bound to the Anchor Stone wakes beside it; before that, where the zone starts everyone.
            // During the prologue a scene's checkpoint stands in, so a lost fight starts again where it was.
            var wake = _story.Has("anchor.bound") && _actorById.TryGetValue("anchor", out var anchor) ? anchor.Position + new Vector3(0, 0, 1.6f)
                : _checkpoint is { } checkpoint ? checkpoint with { Y = _world.GroundHeight(checkpoint.X, checkpoint.Z) } : _world.Spawn;
            player.State = PlayerState.Spawn(wake, maxHealth);
            // Worn gear survives death, so the weapon it picks does too; so do the attributes.
            player.State.Weapon = Weapons.Of(player.Stats.Weapon);
            player.State.Endurance = player.Attributes[(int)PlayerAttribute.Endurance];
            player.State.Moves = (byte)Mastery.MovesFor(player.Stats.Weapon, player.MasteryTaken);
            player.State.Stamina = Core.Attributes.MaxStamina(player.State);
        }
        else if (Tick - player.LastHurtTick > RegenDelayTicks && player.State.Health < maxHealth)
        {
            // One point every third of a second, plus what regen gear gives each second; below a
            // third of the health, second wind makes it all heal faster.
            var perSecond = (Tuning.TickRate / (float)RegenIntervalTicks + player.Stats.Regen)
                * (1f + player.Attribute(PlayerAttribute.Vitality) * Core.Attributes.RegenPctPerVitality / 100f);
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
    private static short MaxHealth(Player player) => (short)(Progression.MaxHealth(player.Level) + player.Stats.MaxHealth
        + player.Attribute(PlayerAttribute.Vitality) * Core.Attributes.HealthPerVitality);

    /// <summary>Eats, drinks or reads an item from the bag, the way its kind is used.</summary>
    private void UseItem(Player player, ushort item)
    {
        var def = ItemDef.All[item];
        if (player.Bag.GetValueOrDefault(item) <= 0)
        {
            return;
        }

        if (def.IsFood || def.IsPotion)
        {
            Obey(player, ItemCommands.Eat(item));
            return;
        }

        if (def.IsSkillBook && SkillDef.Index(def.Skill) is var skill and >= 0 && player.Skills.Add(skill))
        {
            AddItem(player, item, -1);
            Stats.SkillsLearned++;
            FillSkillSlot(player, skill);
        }
    }

    /// <summary>A newly learned skill takes the first empty skill slot, if there is one.</summary>
    private static void FillSkillSlot(Player player, int skill)
    {
        var free = Array.IndexOf(player.SkillSlots, Skills.NoSkill);
        if (free >= 0)
        {
            player.SkillSlots[free] = (byte)skill;
        }
    }

    /// <summary>
    /// Puts a learned skill in a slot (moving it if it sat in another), or empties the slot. A skill
    /// put in starts on its cooldown, so swapping cannot stand in for more slots.
    /// </summary>
    private void SlotSkill(Player player, ushort arg)
    {
        var skill = (byte)(arg & 0xFF);
        var slot = arg >> 8;
        if (slot >= Skills.Slots || (skill != Skills.NoSkill && (skill >= SkillDef.All.Count || !player.Skills.Contains(skill))))
        {
            return;
        }

        if (skill != Skills.NoSkill)
        {
            if (player.SkillSlots[slot] == skill)
            {
                return;
            }

            var before = Array.IndexOf(player.SkillSlots, skill);
            if (before >= 0)
            {
                // Moving a skill between slots keeps its cooldown as it is.
                player.SkillSlots[before] = player.SkillSlots[slot];
                player.SkillSlots[slot] = skill;
                return;
            }

            var cooldown = Tick + (uint)Skills.Cooldown(SkillDef.All[skill], player.Attribute(PlayerAttribute.Intelligence));
            player.SkillReady[skill] = Math.Max(player.SkillReady[skill], cooldown);
        }

        player.SkillSlots[slot] = skill;
    }

    /// <summary>
    /// Casts a learned skill along the player's aim, if it is off cooldown and the player has the
    /// stamina. Stamina is spent here on the server, so the client learns of it in the next snapshot.
    /// </summary>
    private void CastSkill(Player player, ushort index)
    {
        var skill = SkillDef.All[index];
        if (!player.Skills.Contains(index) || Array.IndexOf(player.SkillSlots, (byte)index) < 0 || Tick < player.SkillReady[index] || player.State.Stamina < skill.Stamina || player.State.Health <= 0)
        {
            return;
        }

        player.State.Stamina -= skill.Stamina;
        player.State.StaminaDelay = Tuning.StaminaRegenDelayTicks;
        player.SkillReady[index] = Tick + (uint)Skills.Cooldown(skill, player.Attribute(PlayerAttribute.Intelligence));
        Stats.SkillsCast++;
        var (aim, flat) = Direction(player.SkillYaw, player.SkillPitch);
        var origin = ArrowOrigin(player.State.Position, flat) + flat * 0.4f;
        switch (skill.Effect)
        {
            case "fireball":
                _projectiles.Add(new Projectile
                {
                    Id = _nextId++, Kind = ProjectileKind.PlayerFireball, Damage = 0, Position = origin, Velocity = aim * Tuning.StaffBoltSpeed,
                    Shooter = player.Id, SplashRadius = skill.Radius, Skill = index,
                    Life = (int)MathF.Ceiling(Tuning.StaffBoltRange / Tuning.StaffBoltSpeed * Tuning.TickRate),
                });
                break;
            case "airSlash":
                PlayerSlash(player, ProjectileKind.AirSlash, origin, flat, index, skill.Radius);
                break;
            case "fangSlash":
                var across = new Vector3(-flat.Z, 0, flat.X);
                for (var claw = -1; claw <= 1; claw++)
                {
                    PlayerSlash(player, ProjectileKind.FangSlash, origin + across * (claw * FangSlashSpacing), flat, index, skill.Radius);
                }

                break;
            case "summonSpears":
                var side = new Vector3(-flat.Z, 0, flat.X);
                for (var i = 0; i < SpearDelays.Length; i++)
                {
                    _projectiles.Add(new Projectile
                    {
                        Id = _nextId++, Kind = ProjectileKind.SummonedSpear, Damage = 0, Shooter = player.Id, Skill = index,
                        Position = player.State.Position + Vector3.UnitY * 2.4f + side * ((i - 1) * 0.9f),
                        Velocity = Vector3.Zero, Delay = SpearDelays[i], Pierces = true, Life = SpearDelays[i] + ProjectileLifeTicks,
                        Aim = aim,
                    });
                }

                break;
            case "earthCrack":
                _walls.Add(new FireWallHazard
                {
                    Id = _nextId++, Origin = player.State.Position + flat * 0.8f, Direction = flat, Damage = skill.Damage,
                    Kind = HazardKind.EarthCrack, Angle = EarthCrack.HalfAngle, Span = EarthCrack.Length,
                    Grow = EarthCrack.GrowTicks, Burn = EarthCrack.LingerTicks, Owner = player.Id, Skill = index,
                });
                break;
        }
    }

    /// <summary>A flying crescent from a player: it cuts every enemy it passes once, up to its reach.</summary>
    private void PlayerSlash(Player player, ProjectileKind kind, Vector3 from, Vector3 flat, int skill, float radius)
    {
        var speed = kind == ProjectileKind.FangSlash ? FangSlashSpeed : AirSlashSpeed;
        var reach = kind == ProjectileKind.FangSlash ? FangSlashReach : AirSlashReach + 1f;
        _projectiles.Add(new Projectile
        {
            Id = _nextId++, Kind = kind, Damage = 0, Position = from, Velocity = flat * speed, Shooter = player.Id, Skill = skill,
            Radius = radius, Life = (int)(reach / speed * Tuning.TickRate),
        });
    }

    /// <summary>One blow of a skill on an enemy: its own damage, raised by Intelligence, with all that follows from a blow.</summary>
    private void SkillBlow(Player player, Enemy enemy, int skill, float share, Vector3 from)
    {
        var damage = (short)Math.Max(1, MathF.Round(BlowDamage(player, AttackKind.Light, enemy, SkillDef.All[skill].Damage, PlayerAttribute.Intelligence) * share));
        var killed = enemy.TakeHit(damage, from, player.Id);
        AfterBlow(player, enemy, damage);
        Stats.EnemyHits++;
        CallForHelp(enemy, player.Id);
        if (killed)
        {
            OnKilled(enemy, player);
        }
    }

    /// <summary>A player's skill shot in flight: slashes cut every enemy they pass once; a spear stops at the first.</summary>
    private bool SkillShotHits(Projectile shot, Player shooter)
    {
        foreach (var enemy in _enemies.ToList())
        {
            var chest = enemy.Position + new Vector3(0, enemy.Def.Size * 0.6f, 0);
            if (!enemy.Alive || shot.Struck.Contains(enemy.Id) || Vector3.Distance(chest, shot.Position) >= shot.Radius + enemy.Def.Size * 0.35f)
            {
                continue;
            }

            shot.Struck.Add(enemy.Id);
            SkillBlow(shooter, enemy, shot.Skill, 1f, shot.Position - shot.Velocity * Tuning.TickSeconds);
            if (shot.Kind == ProjectileKind.SummonedSpear)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Puts what the attributes change about movement into the player state, which both sides simulate.</summary>
    private static void ApplyAttributes(Player player)
    {
        player.State.Endurance = player.Attributes[(int)PlayerAttribute.Endurance];
        player.State.Stamina = MathF.Min(player.State.Stamina, Core.Attributes.MaxStamina(player.State));
        player.State.Health = Math.Min(player.State.Health, MaxHealth(player));
    }

    /// <summary>Points spent in one attribute. For tests and tools.</summary>
    public int GetAttribute(int id, PlayerAttribute which) => _players[id].Attribute(which);

    /// <summary>Gives a player levels. For tests and tools.</summary>
    public void SetLevel(int id, byte level) => _players[id].Level = level;

    /// <summary>Carries out what a player asked for, when the player is allowed to and close enough.</summary>
    private void Obey(Player player, Command command, ushort arg = 0)
    {
        if (command is Command.StoreItem or Command.TakeItem or Command.StoreGear or Command.TakeGear)
        {
            UseChest(player, command, arg);
            return;
        }

        if (command == Command.UseItem && arg < ItemDef.All.Count)
        {
            UseItem(player, arg);
            return;
        }

        if (command == Command.TakeMastery && arg < Mastery.Nodes.Count)
        {
            var node = Mastery.Nodes[arg];
            var weapon = Mastery.WeaponIndex(node.Weapon);
            var free = player.MasteryLevels[weapon] - Mastery.Spent(node.Weapon, player.MasteryTaken);
            if (Mastery.CanTake(node, player.MasteryTaken, free))
            {
                player.MasteryTaken |= 1ul << node.Index;
                RecomputeStats(player);
            }

            return;
        }

        if (command == Command.CastSkill && arg < SkillDef.All.Count)
        {
            CastSkill(player, arg);
            return;
        }

        if (command == Command.SlotSkill)
        {
            SlotSkill(player, arg);
            return;
        }

        if (command == Command.RaiseAttribute && arg < Core.Attributes.Count && player.UnspentPoints > 0)
        {
            player.Attributes[arg]++;
            ApplyAttributes(player);
            return;
        }

        if (command == Command.ResetAttributes && Near(player, "receptionist") && player.Gold >= Core.Attributes.ResetGoldPerLevel * player.Level)
        {
            player.Gold -= Core.Attributes.ResetGoldPerLevel * player.Level;
            Array.Clear(player.Attributes);
            ApplyAttributes(player);
            return;
        }

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
            case Command.BeginTrial:
                RunAction(player, "beginTrial");
                break;
            case Command.SellCrystals:
                RunAction(player, "sellCrystals");
                break;
            case Command.SellLoot:
                RunAction(player, "sellLoot");
                break;
            case Command.Sleep:
                RunAction(player, "sleep");
                break;
            case Command.Choose:
                Choose(player, arg);
                break;
            case Command.HearRumor:
                HearRumor(player, arg);
                break;
            case Command.SceneNext when _scene is not null && arg == _sceneStep:
                if (_scene.Steps[_sceneStep].Kind == "slides")
                {
                    _sceneSlide++;
                }
                else
                {
                    _sceneRead = _sceneStep;
                }

                break;
            case Command.Quit:
                Autosave?.Invoke(Export());
                break;
            case Command.SetAppearance:
                var field = (AppearanceField)(arg >> 8);
                var confirmed = arg == Core.Appearance.Confirm;
                if (confirmed || ((int)field < player.Appearance.Length && Core.Appearance.Valid(field, arg & 0xff)))
                {
                    if (!Core.Appearance.IsChosen(player.Appearance))
                    {
                        player.Appearance = Core.Appearance.Default;
                    }

                    if (confirmed)
                    {
                        // Only "This is me" ends the choosing: the prologue waits for it, not for the first change.
                        _story.SetFlag("look.chosen");
                    }
                    else
                    {
                        player.Appearance[(int)field] = (byte)(arg & 0xff);
                    }
                }

                break;
            case var eat when ItemCommands.TryEat(eat, out var food) && ItemDef.All[food].IsFood &&
                              player.Bag.GetValueOrDefault(food) > 0 && player.State.Health > 0:
                AddItem(player, food, -1);
                player.State.Health = (short)Math.Min(player.State.Health + ItemDef.All[food].Heal, MaxHealth(player));
                Stats.MealsEaten++;
                break;
            case var drink when ItemCommands.TryEat(drink, out var potion) && ItemDef.All[potion].IsPotion &&
                                player.Bag.GetValueOrDefault(potion) > 0 && player.State.Health > 0 && Tick >= player.PotionReady:
                AddItem(player, potion, -1);
                player.State.Health = (short)Math.Min(player.State.Health + ItemDef.All[potion].Heal, MaxHealth(player));
                player.PotionReady = Tick + PotionCooldownTicks;
                Stats.PotionsDrunk++;
                break;
            case var buy when ShopCommands.TryBuy(buy, out var ware) && Near(player, ItemDef.All[ware].Shop) && player.Gold >= ItemDef.All[ware].Price:
                player.Gold -= ItemDef.All[ware].Price;
                AddItem(player, ware, 1);
                break;
            case var wear when GearCommands.TryToggle(wear, out var place) && player.Gear[place] is { } piece:
                ToggleWorn(player, place, piece);
                break;
            case var discard when GearCommands.TryDiscard(discard, out var spot) && player.Gear[spot] is not null && !player.Worn.Contains(spot):
                player.Gear[spot] = null;
                player.GearChanged.Add(spot);
                break;
        }
    }

    /// <summary>The game as it stands: the world and the first player. The game is for one player; co-op saves come later.</summary>
    public SaveData Export()
    {
        _story.Time = _calendar.Now;
        var data = new SaveData { World = SavedWorld.From(_story) };
        if (_players.Values.OrderBy(p => p.Id).FirstOrDefault() is not { } player)
        {
            return data;
        }

        static SavedGear? Gear(GearPiece? piece) => piece is null ? null
            : new SavedGear(ItemDef.All[piece.Item].Id, (byte)piece.Rarity, piece.Level, piece.Affixes.SelectMany(a => new[] { (short)a.Affix, a.Value }).ToArray());
        var at = player.State.Position;
        data.Player = new SavedPlayer
        {
            Appearance = player.Appearance.ToArray(),
            Level = player.Level,
            Xp = player.Xp,
            Gold = player.Gold,
            Rank = (byte)(player.Rank == GuildRank.InTrial ? GuildRank.Newcomer : player.Rank),
            Crystals = player.Crystals.ToArray(),
            Attributes = player.Attributes.ToArray(),
            Skills = player.Skills.Order().ToArray(),
            SkillSlots = player.SkillSlots.ToArray(),
            MasteryLevels = player.MasteryLevels.ToArray(),
            MasteryXp = player.MasteryXp.ToArray(),
            MasteryTaken = player.MasteryTaken,
            Bag = player.Bag.Where(p => p.Value > 0).ToDictionary(p => ItemDef.All[p.Key].Id, p => p.Value),
            Gear = player.Gear.Select(Gear).ToArray(),
            Worn = player.Worn.Order().ToArray(),
            Storage = player.Storage.Where(p => p.Value > 0).ToDictionary(p => ItemDef.All[p.Key].Id, p => p.Value),
            StoredGear = player.StoredGear.Select(Gear).ToArray(),
            Position = player.Rank == GuildRank.InTrial ? Array.Empty<float>() : new[] { at.X, at.Y, at.Z },
            Deaths = player.Deaths.ToList(),
        };
        return data;
    }

    /// <summary>Puts a saved player back: purse, bag, gear, growth and place.</summary>
    private void Restore(Player player, SavedPlayer saved)
    {
        static GearPiece? Gear(SavedGear? piece)
        {
            if (piece is null || !ItemDef.TryIndex(piece.Item, out var item))
            {
                return null;
            }

            var affixes = new List<AffixRoll>();
            for (var i = 0; i + 1 < piece.Affixes.Length; i += 2)
            {
                affixes.Add(new AffixRoll((byte)piece.Affixes[i], piece.Affixes[i + 1]));
            }

            return new GearPiece(item, (Rarity)piece.Rarity, piece.Level, affixes.ToArray());
        }

        if (saved.Appearance.Length == Core.Appearance.FieldCount)
        {
            player.Appearance = saved.Appearance.ToArray();
        }

        player.Level = (byte)Math.Clamp(saved.Level, 1, 255);
        player.Xp = saved.Xp;
        player.Gold = saved.Gold;
        player.Rank = (GuildRank)saved.Rank;
        saved.Crystals.Take(player.Crystals.Length).ToArray().CopyTo(player.Crystals, 0);
        saved.Attributes.Take(player.Attributes.Length).ToArray().CopyTo(player.Attributes, 0);
        foreach (var skill in saved.Skills.Where(s => s >= 0 && s < SkillDef.All.Count))
        {
            player.Skills.Add(skill);
        }

        saved.SkillSlots.Take(player.SkillSlots.Length).ToArray().CopyTo(player.SkillSlots, 0);
        saved.MasteryLevels.Take(player.MasteryLevels.Length).ToArray().CopyTo(player.MasteryLevels, 0);
        saved.MasteryXp.Take(player.MasteryXp.Length).ToArray().CopyTo(player.MasteryXp, 0);
        player.MasteryTaken = saved.MasteryTaken;
        foreach (var (id, count) in saved.Bag)
        {
            if (ItemDef.TryIndex(id, out var item))
            {
                AddItem(player, item, count);
            }
        }

        foreach (var (id, count) in saved.Storage)
        {
            if (ItemDef.TryIndex(id, out var item))
            {
                player.Storage[item] = count;
                player.StorageChanged.Add(item);
            }
        }

        for (var i = 0; i < player.Gear.Length && i < saved.Gear.Length; i++)
        {
            player.Gear[i] = Gear(saved.Gear[i]);
            player.GearChanged.Add(i);
        }

        for (var i = 0; i < player.StoredGear.Length && i < saved.StoredGear.Length; i++)
        {
            player.StoredGear[i] = Gear(saved.StoredGear[i]);
            player.StoredGearChanged.Add(i);
        }

        foreach (var place in saved.Worn.Where(p => p >= 0 && p < player.Gear.Length && player.Gear[p] is not null))
        {
            ToggleWorn(player, place, player.Gear[place]!);
        }

        player.Deaths.AddRange(saved.Deaths);
        if (saved.Position.Length == 3)
        {
            Move(player, new Vector3(saved.Position[0], saved.Position[1], saved.Position[2]));
        }

        ApplyAttributes(player);
        RecomputeStats(player);
        player.State.Health = MaxHealth(player);
    }

    /// <summary>
    /// A choice in a dialogue: checked again with the server's own state, and only by the NPC who
    /// says it, then its effects run.
    /// </summary>
    private void Choose(Player player, ushort index)
    {
        if (index >= Story.Options.Count)
        {
            return;
        }

        var option = Story.Options[index];
        var context = new StoryActions(this, player);
        var talk = _scene is not null && _scene.Steps[_sceneStep] is { Kind: "talk" } step && step.Actor == option.Npc ? _scene.Steps[_sceneStep] : null;
        if (!NearNpc(player, option.Npc) && talk is null)
        {
            return;
        }

        if (!option.When.Holds(context) || !Story.Dialogues.TryGetValue(option.Npc, out var dialogue) ||
            !(dialogue.Reachable(context, option.Node) || (talk is not null && dialogue.Reachable(context, option.Node, talk.Node))))
        {
            return;
        }

        Effect.ApplyAll(option.Effects, context);
        // How the answer was said matters to whoever heard it, each in their own way.
        People.HearTone(_story, option);
        if (talk is not null && option.Next is null)
        {
            _sceneTalked = true;
        }
    }

    /// <summary>The player has heard a rumour, if someone near them is telling it.</summary>
    private void HearRumor(Player player, ushort index)
    {
        if (index >= Story.Rumors.Count)
        {
            return;
        }

        var rumor = Story.Rumors[index];
        var context = new StoryActions(this, player);
        if (_actors.Any(a => NearNpc(player, a.Id) && rumor.ToldBy(a.Id, context)))
        {
            _story.SetFlag(rumor.HeardFlag);
        }
    }

    /// <summary>The server actions that story data can name. Each checks what it needs by itself.</summary>
    private void RunAction(Player player, string action)
    {
        switch (action)
        {
            case "beginTrial" when player.Rank == GuildRank.Newcomer && Near(player, "receptionist"):
                BeginTrial(player);
                break;
            case "sellCrystals" when player.Rank == GuildRank.Adventurer && Near(player, "receptionist"):
                for (var i = 0; i < player.Crystals.Length; i++)
                {
                    player.Gold += player.Crystals[i] * Guild.CrystalPrices[i];
                    player.Crystals[i] = 0;
                }

                break;
            case "sellLoot" when player.Rank == GuildRank.Adventurer && Near(player, "appraiser"):
                foreach (var (item, count) in player.Bag.Where(p => p.Value > 0 && ItemDef.All[p.Key].Sellable).ToList())
                {
                    player.Gold += count * ItemDef.All[item].Price;
                    Stats.ItemsSold += count;
                    AddItem(player, item, -count);
                }

                break;
            case "resetAttributes":
                Obey(player, Command.ResetAttributes);
                break;
            case var start when start.StartsWith("scene:", StringComparison.Ordinal) && _scene is null:
                StartScene(start[6..]);
                break;
            case "sleep" when _calendar.Now.CanSleep:
                _calendar.Now = _calendar.Now.AfterSleep();
                _story.Time = _calendar.Now;
                RunAgendas();
                Autosave?.Invoke(Export());
                break;
        }
    }

    /// <summary>What the appraiser pays for the materials and food in a player's bag.</summary>
    private static int SaleValue(Player player) =>
        player.Bag.Where(p => p.Value > 0 && ItemDef.All[p.Key].Sellable).Sum(p => p.Value * ItemDef.All[p.Key].Price);

    private bool NearNpc(Player player, string id) => _actorById.TryGetValue(id, out var actor) && Present(actor) &&
        Vector3.Distance(actor.Position with { Y = 0 }, player.State.Position with { Y = 0 }) < Guild.TalkRange + 1f;

    /// <summary>Whether an actor is in the world to be spoken to: brought in, and neither dead nor gone.</summary>
    private bool Present(Actor actor) => actor.Active && _story.Fate(actor.Id) is NpcFate.Alive or NpcFate.Moved;

    /// <summary>
    /// Runs the scene playing: steps that happen at once run in a row, until one that waits. With no
    /// scene playing, looks for one whose trigger holds, now and then.
    /// </summary>
    private void StepScene()
    {
        if (_players.Values.OrderBy(p => p.Id).FirstOrDefault() is not { } player)
        {
            return;
        }

        var context = new StoryActions(this, player);
        _sceneApart = false;
        if (_scene is null)
        {
            if (Tick % 15 != 0)
            {
                return;
            }

            foreach (var candidate in Story.Scenes)
            {
                if (candidate.Trigger is { } trigger && !_story.Has(candidate.DoneFlag) && trigger.When.Holds(context) &&
                    (trigger.Near is not { } near || Vector3.Distance(near with { Y = 0 }, player.State.Position with { Y = 0 }) < trigger.Radius))
                {
                    StartScene(candidate.Id);
                    break;
                }
            }

            if (_scene is null)
            {
                return;
            }
        }

        for (var guard = 0; guard < 64 && _scene is not null; guard++)
        {
            if (_sceneStep >= _scene.Steps.Length || _scene.Until?.Holds(context) == true)
            {
                _story.SetFlag(_scene.DoneFlag);
                _scene = null;
                _sceneLocked = false;
                return;
            }

            var step = _scene.Steps[_sceneStep];
            if (SceneData.Holds(step) && step.Kind == "move" && !_sceneEntered)
            {
                // A walk that holds the scene starts when the scene reaches it.
                _sceneEntered = true;
                RunStep(step, player, context);
            }

            if (_sceneRead != _sceneStep && !_sceneTalked && step.Actor.Length > 0 && _actorById.TryGetValue(step.Actor, out var speaker) &&
                speaker.Active && SceneData.Apart(step, speaker.Position, player.State.Position))
            {
                // Words wait until the two stand together: the player is free to walk up, a speaker
                // who approaches comes over, and the time to read starts when they meet.
                _sceneApart = true;
                _sceneStepTick = Tick;
                if (step.Approach && speaker.Follows < 0 && Tick % 20 == 0)
                {
                    var gap = player.State.Position - speaker.Position;
                    gap.Y = 0;
                    var stop = player.State.Position - Vector3.Normalize(gap) * 2.2f;
                    speaker.Path.Clear();
                    speaker.Speed = Math.Max(speaker.Speed, 4.5f);
                    speaker.Path.Enqueue(stop with { Y = _world.GroundHeight(stop.X, stop.Z) });
                }

                return;
            }

            if (SceneData.Holds(step) ? !StepDone(step, context) : !RunStep(step, player, context))
            {
                return;
            }

            _sceneStep++;
            _sceneStepTick = Tick;
            _sceneSlide = 0;
            _sceneTalked = false;
            _sceneEntered = false;
        }
    }

    private bool StepDone(SceneStep step, IStoryActions context)
    {
        var elapsed = (Tick - _sceneStepTick) * Tuning.TickSeconds;
        return step.Kind switch
        {
            "fade" or "wait" => elapsed >= step.Seconds,
            "slides" => _sceneSlide >= step.Slides.Length || elapsed >= step.Seconds * step.Slides.Length,
            "say" => step.Bark ? elapsed >= step.ReadSeconds : _sceneRead == _sceneStep || elapsed >= step.ReadSeconds * 1.6f,
            "talk" => _sceneTalked,
            "waitFor" => step.When.Holds(context),
            "waitForKills" => !_enemies.Any(e => e.Alive && _enemyTags.TryGetValue(e.Id, out var tag) && tag == step.Tag),
            "move" => !_actorById.TryGetValue(step.Actor, out var mover) || mover.Path.Count == 0,
            _ => true,
        };
    }

    /// <summary>Runs a step that happens at once. Returns false when the scene changed under it.</summary>
    private bool RunStep(SceneStep step, Player player, IStoryActions context)
    {
        var actor = step.Actor.Length > 0 && _actorById.TryGetValue(step.Actor, out var found) ? found : null;
        Vector3 Ground(Vector3 point) => point with { Y = _world.GroundHeight(point.X, point.Z) };
        switch (step.Kind)
        {
            case "lockInput":
                _sceneLocked = true;
                break;
            case "unlockInput":
                _sceneLocked = false;
                break;
            case "spawn" or "place" when actor is not null:
                actor.Take();
                actor.Path.Clear();
                actor.Position = Ground(step.Points[0]);
                actor.Yaw = step.Yaw;
                actor.Active = true;
                break;
            case "despawn" when actor is not null:
                actor.Rest();
                break;
            case "move" when actor is not null:
                actor.Take();
                actor.Speed = step.Speed;
                foreach (var point in step.Points)
                {
                    actor.Path.Enqueue(Ground(point));
                }

                break;
            case "follow" when actor is not null:
                actor.Take();
                actor.Follows = player.Id;
                break;
            case "unfollow" when actor is not null:
                actor.Follows = -1;
                break;
            case "pose" when actor is not null:
                actor.Take();
                actor.Pose = step.Pose;
                break;
            case "teleport":
                Move(player, step.Points[0]);
                break;
            case "time":
                _calendar.Now = new GameTime(step.Day, step.Hour * 60);
                _story.Time = _calendar.Now;
                if (_calendar.Now.Minute == GameTime.DayStartMinute)
                {
                    // A scene that brings the morning saves, like waking does.
                    Autosave?.Invoke(Export());
                }

                break;
            case "effects":
                var before = _scene;
                Effect.ApplyAll(step.Effects, context);
                return _scene == before;
            case "spawnEnemies":
                SpawnTagged(step, player);
                break;
            case "drop":
                DropAt(player, step.Item, step.Points[0], step.Count);
                break;
            case "checkpoint":
                _checkpoint = step.Points[0];
                break;
            case "scene":
                _story.SetFlag(_scene!.DoneFlag);
                StartScene(step.Scene);
                return false;
        }

        return true;
    }

    private void SpawnTagged(SceneStep step, Player player)
    {
        var type = (byte)EnemyDef.All.ToList().FindIndex(d => d.Id == step.Item);
        for (var i = 0; i < step.Count; i++)
        {
            var angle = i * MathF.Tau / Math.Max(1, step.Count);
            var at = step.Points[0] + new Vector3(MathF.Cos(angle) * 1.5f, 0, MathF.Sin(angle) * 1.5f);
            at.Y = _world.GroundHeight(at.X, at.Z);
            var enemy = new Enemy
            {
                Id = _nextEnemyId++,
                Type = type,
                Home = new EnemySpawn(at, 3f, 1, step.Level, step.Level, int.MaxValue, false, new[] { step.Item }),
            };
            enemy.Reset(_random, _world);
            enemy.Position = at;
            enemy.Health = (short)Math.Max(1, (int)(enemy.Health * step.Health));
            enemy.TargetId = player.Id;
            _enemyTags[enemy.Id] = step.Tag;
            _enemies.Add(enemy);
        }
    }

    private void DropAt(Player player, string id, Vector3 at, int count)
    {
        if (!ItemDef.TryIndex(id, out var item))
        {
            return;
        }

        for (var i = 0; i < count; i++)
        {
            var spot = at + new Vector3(i * 0.5f, 0, 0);
            spot.Y = _world.GroundHeight(spot.X, spot.Z);
            var loot = new Loot { Id = _nextId++, Item = item, Owner = player.Id, Position = spot, Life = int.MaxValue / 2 };
            if (ItemDef.All[item].IsEquipment)
            {
                loot.Gear = new GearPiece(item, Rarity.Common, 1, Array.Empty<AffixRoll>());
            }

            _loot.Add(loot);
        }
    }

    private void StepActors()
    {
        var hour = _calendar.Now.Day * 24 + _calendar.Now.Hour;
        if (hour != _scheduleHour)
        {
            // People move on with their day each hour, on foot; a jump in time (sleep, a scene, a loaded game) puts them there.
            ApplySchedules(hour != _scheduleHour + 1);
            _scheduleHour = hour;
        }

        foreach (var actor in _actors.Where(a => a.Active))
        {
            Vector3? leader = actor.Follows >= 0 && _players.TryGetValue(actor.Follows, out var followed) ? followed.State.Position : null;
            actor.Step(_world, leader);
        }
    }

    private const float StrollSpeed = 2.3f;
    private int _scheduleHour = int.MinValue;
    private RoadMap? _roads;

    private void ApplySchedules(bool atOnce)
    {
        _roads ??= new RoadMap(_world.Roads);
        var context = new WorldOnly(_story);
        foreach (var actor in _actors)
        {
            var spot = Schedules.RestOf(actor, context, _calendar.Now.Hour, _world);
            var block = Schedules.Now(actor.Id, context, _calendar.Now.Hour);
            var by = block?.By ?? Array.Empty<Vector3>();
            if (actor.Scripted || actor.Follows >= 0)
            {
                // A scene has them: they go back to their day when it lets them go.
                actor.Spot = spot;
                actor.Way = by;
                continue;
            }

            if (spot == actor.Spot)
            {
                continue;
            }

            if (!atOnce && !actor.Active && block?.Via is { } door)
            {
                // Indoors somewhere else (the Guild hall is far off the map): they come out by this door.
                actor.Position = door with { Y = _world.GroundHeight(door.X, door.Z) };
            }

            // Someone indoors may turn up anywhere; anyone else walks, unless time jumped or the way is absurd.
            if (atOnce || Vector3.Distance(actor.Position, spot.At) > 150f)
            {
                actor.Spot = spot;
                actor.Rest();
            }
            else
            {
                // Someone at their place leaves it the way they came in; someone caught on the road just turns.
                var leaving = actor.Active && actor.Path.Count == 0 ? actor.Way : Array.Empty<Vector3>();
                actor.WalkTo(spot, Schedules.Way(_roads, _world, actor.Position, leaving, by, spot.At), StrollSpeed);
            }

            actor.Way = by;
        }
    }

    private ActorView[] ActorSlice(Vector3 origin) => _actors
        .Where(a => a.Away && Vector3.Distance((a.Active ? a.Position : a.Home) with { Y = 0 }, origin with { Y = 0 }) < InterestRange)
        .OrderBy(a => Vector3.DistanceSquared(a.Position, origin)).Take(10)
        .Select(a => new ActorView(a.Index, a.Position, a.Yaw, a.Pose, a.Active)).ToArray();

    /// <summary>Moves the calendar on, and runs the faction plans once an hour.</summary>
    private void StepCalendar()
    {
        _calendar.Paused = _players.Count > 0 && _players.Values.All(p => p.Reading);
        if (_calendar.Step() == 0)
        {
            return;
        }

        _story.Time = _calendar.Now;
        if (_calendar.Now.Minute % 60 == 0)
        {
            RunAgendas();
        }

        if (_calendar.Now.Minute == GameTime.DayStartMinute)
        {
            Autosave?.Invoke(Export());
        }
    }

    private void RunAgendas()
    {
        var player = _players.Values.OrderBy(p => p.Id).FirstOrDefault();
        Story.RunAgendas(player is null ? new WorldOnly(_story) : new StoryActions(this, player));
    }

    /// <summary>The story as seen by one player on the server: their purse, bag and place, and the shared world.</summary>
    private sealed class StoryActions(GameServer server, Player player) : IStoryActions
    {
        public WorldState World => server._story;

        public int Rank => player.Rank == GuildRank.Adventurer ? 1 : 0;

        public int Gold => player.Gold;

        public int Level => player.Level;

        public int SaleValue => GameServer.SaleValue(player);

        public int ItemCount(string item) => ItemDef.TryIndex(item, out var index) ? player.Bag.GetValueOrDefault(index) : 0;

        public int Attribute(string name) => Enum.TryParse<PlayerAttribute>(name, true, out var which) ? player.Attribute(which) : 0;

        public bool Near(string role) => server.Near(player, role);

        public Vector3 Position => player.State.Position;

        public void AddGold(int amount) => player.Gold = Math.Max(0, player.Gold + amount);

        public void GiveItem(string item, int count)
        {
            if (ItemDef.TryIndex(item, out var index))
            {
                GameServer.AddItem(player, index, count);
            }
        }

        public void TakeItem(string item, int count)
        {
            if (ItemDef.TryIndex(item, out var index))
            {
                GameServer.AddItem(player, index, -Math.Min(count, player.Bag.GetValueOrDefault(index)));
            }
        }

        public void AddXp(int amount) => server.AwardXp(player.Id, amount);

        public void RunAction(string action) => server.RunAction(player, action);
    }

    /// <summary>The story with no player in it: faction plans still run while nobody is connected.</summary>
    private sealed class WorldOnly(WorldState world) : IStoryActions
    {
        public WorldState World => world;

        public int Rank => 0;

        public int Gold => 0;

        public int Level => 1;

        public int SaleValue => 0;

        public int ItemCount(string item) => 0;

        public int Attribute(string name) => 0;

        public bool Near(string role) => false;

        public Vector3 Position => new(float.MaxValue, 0, float.MaxValue);

        public void AddGold(int amount)
        {
        }

        public void GiveItem(string item, int count)
        {
        }

        public void TakeItem(string item, int count)
        {
        }

        public void AddXp(int amount)
        {
        }

        public void RunAction(string action)
        {
        }
    }

    private bool Near(Player player, string role) => _actors.Any(a => a.Role == role && Present(a) &&
        Vector3.Distance(a.Position with { Y = 0 }, player.State.Position with { Y = 0 }) < Guild.TalkRange + 1f);

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
        if (_story.Has("prologue.started") && !_story.Has("prologue.done"))
        {
            // The prologue's trial teaches first: guard, then dash, then a real duel. A retry after a
            // lost duel starts at the duel.
            examiner.TrialRound = _story.Has("trial.round3") || _options.FrailExaminer ? 3 : 1;
            player.TrialMark = (player.Blocks, player.Parries, player.Dodges);
        }

        if (_options.FrailExaminer)
        {
            examiner.Health = 1;
            player.Crystals[0] += 3;
        }

        _enemies.Add(examiner);
    }

    private void EndTrial(Player player) => _enemies.RemoveAll(e => e.TrialOf == player.Id);

    /// <summary>Moves the prologue trial on when the player has shown the lesson of the round.</summary>
    private void StepTrialRounds(Player player)
    {
        if (player.Rank != GuildRank.InTrial || _enemies.FirstOrDefault(e => e.TrialOf == player.Id && e.TrialRound is 1 or 2) is not { } examiner)
        {
            return;
        }

        var blocks = (byte)(player.Blocks - player.TrialMark.Blocks);
        var parries = (byte)(player.Parries - player.TrialMark.Parries);
        var dodges = (byte)(player.Dodges - player.TrialMark.Dodges);
        if (examiner.TrialRound == 1 && blocks >= 3 && parries >= 1)
        {
            examiner.TrialRound = 2;
            player.TrialMark = (player.Blocks, player.Parries, player.Dodges);
            _story.SetFlag("trial.round2");
        }
        else if (examiner.TrialRound == 2 && dodges >= 3)
        {
            examiner.TrialRound = 3;
            _story.SetFlag("trial.round3");
        }
    }

    /// <summary>
    /// Hurts a player, unless a dash avoids it or the guard takes it. Returns true when the attack
    /// connects. From is where the blow comes from, for the guard; heavy blows break through a guard
    /// in part, and a blow caught just as the guard rises is parried.
    /// </summary>
    private bool Hurt(Player player, short damage, byte slowTicks, Enemy? attacker = null, bool heavy = false, Vector3? from = null)
    {
        if (player.State.DashTicks > 0)
        {
            player.Dodges++;
            Stats.Dodges++;
            return false;
        }

        if (Guard(player, ref damage, attacker, heavy, from ?? attacker?.Position))
        {
            return false;
        }

        // Defense takes a share off every blow; at least one point always gets through.
        damage = (short)Math.Max(1, MathF.Round(damage * GearStats.DamageTaken(player.Stats.Defense)));
        if (player.Barrier > 0)
        {
            // Staff mastery Arcane Barrier: the barrier soaks up what it can first.
            var soaked = Math.Min(player.Barrier, (int)damage);
            player.Barrier -= soaked;
            damage -= (short)soaked;
            player.LastHurtTick = Tick;
            if (damage <= 0)
            {
                Stats.PlayerHits++;
                return true;
            }
        }

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

    /// <summary>
    /// The player's guard against one blow. Returns true when nothing gets through; otherwise the
    /// damage may be cut. A parry staggers an attacker no more than a couple of levels stronger.
    /// </summary>
    private bool Guard(Player player, ref short damage, Enemy? attacker, bool heavy, Vector3? from)
    {
        ref var state = ref player.State;
        if (state.BlockTicks == 0 || from is not { } source)
        {
            return false;
        }

        var toward = (source - state.Position) with { Y = 0 };
        var facing = toward.LengthSquared() < 0.01f || Vector3.Dot(Vector3.Normalize(toward), Movement.AimDirection(state.BlockAim)) >= Tuning.BlockMinDot;
        if (!facing)
        {
            return false;
        }

        if (state.BlockTicks <= Tuning.ParryTicks)
        {
            player.Parries++;
            Stats.Parries++;
            if (attacker is { Alive: true } && Vector3.Distance(attacker.Position, state.Position) < 6f)
            {
                var gap = attacker.Level - player.Level;
                attacker.Stagger(gap <= 0 ? Tuning.ParryStaggerTicks : gap <= Tuning.ParryLevelReach ? Tuning.ParryStaggerTicksStronger : 0);
                Riposte(player, attacker);
            }

            return true;
        }

        var cost = heavy ? Tuning.BlockHeavyStamina : Tuning.BlockLightStamina;
        if (state.Stamina < cost)
        {
            // Out of stamina the guard breaks: the blow lands in full and the guard drops.
            state.Stamina = 0f;
            state.BlockTicks = 0;
            return false;
        }

        state.Stamina -= cost;
        state.StaminaDelay = Tuning.StaminaRegenDelayTicks;
        player.Blocks++;
        Stats.Blocks++;
        if (!heavy)
        {
            return true;
        }

        damage = (short)Math.Max(1, MathF.Round(damage * Tuning.BlockHeavyShare));
        return false;
    }

    /// <summary>
    /// Sword mastery Riposte: a parried foe within reach takes a heavy blow at once, and while it
    /// reels that blow lands harder still.
    /// </summary>
    private void Riposte(Player player, Enemy attacker)
    {
        var reach = Tuning.RiposteReach + attacker.Def.Size * 0.5f;
        if (!Mastery.Has(player.Stats.Weapon, player.MasteryTaken, "riposte") || Vector3.Distance(attacker.Position, player.State.Position) > reach)
        {
            return;
        }

        var damage = BlowDamage(player, AttackKind.Heavy, attacker);
        var killed = attacker.TakeHit(damage, player.State.Position, player.Id);
        AfterBlow(player, attacker, damage);
        player.Ripostes++;
        Stats.Ripostes++;
        Stats.EnemyHits++;
        if (killed)
        {
            OnKilled(attacker, player);
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

            var damage = BlowDamage(player, kind, enemy);
            var killed = enemy.TakeHit(damage, player.State.Position, player.Id);
            AfterBlow(player, enemy, damage);
            Stats.EnemyHits++;
            CallForHelp(enemy, player.Id);
            if (killed)
            {
                OnKilled(enemy, player);
            }
            else if (kind == AttackKind.ClubSpin && Mastery.Has(player.Stats.Weapon, player.MasteryTaken, "stunning_bash"))
            {
                // Club mastery Stunning Bash: the spin leaves everyone it hit reeling.
                enemy.Stagger(Tuning.StunTicks);
            }
            else if (kind == AttackKind.RisingSlash)
            {
                enemy.Launch(Tuning.RisingSlashLaunch, Tuning.RisingSlashAirTicks);
            }
        }

        if (kind == AttackKind.DaggerLeap && Mastery.Has(player.Stats.Weapon, player.MasteryTaken, "fan_of_knives"))
        {
            FanOfKnives(player);
        }

        if (kind == AttackKind.ClubSpin && Mastery.Has(player.Stats.Weapon, player.MasteryTaken, "shockwave"))
        {
            Shockwave(player, attack.Range);
        }
    }

    /// <summary>Dagger mastery Fan of Knives: the landing leap throws knives out in a ring.</summary>
    private void FanOfKnives(Player player)
    {
        for (var i = 0; i < Tuning.FanKnives; i++)
        {
            var angle = i * MathF.Tau / Tuning.FanKnives;
            var direction = new Vector3(-MathF.Sin(angle), 0, -MathF.Cos(angle));
            _projectiles.Add(new Projectile
            {
                Id = _nextId++,
                Kind = ProjectileKind.Knife,
                Damage = 0,
                Position = player.State.Position + Vector3.UnitY * 0.9f + direction * 0.5f,
                Velocity = direction * Tuning.KnifeSpeed,
                Shooter = player.Id,
                Blow = AttackKind.DaggerLeap,
                Draw = Tuning.KnifeShare,
                Life = (int)MathF.Ceiling(Tuning.KnifeRange / Tuning.KnifeSpeed * Tuning.TickRate),
            });
        }
    }

    /// <summary>
    /// Dagger mastery Shadow Step: the nearest enemy ahead, in reach and in plain sight, finds the
    /// player suddenly at its back, facing it. With nobody there, or after
    /// <see cref="Tuning.ShadowStepChain"/> steps in a row, the stab simply falls where the player is.
    /// </summary>
    private void ShadowStep(Player player)
    {
        if (Tick - player.LastShadowTick > Tuning.ShadowStepChainTicks)
        {
            player.ShadowSteps = 0;
        }

        if (player.ShadowSteps >= Tuning.ShadowStepChain)
        {
            return;
        }

        var feet = player.State.Position;
        var forward = Movement.AimDirection(player.State.AttackAim);
        var marks = _enemies.Where(e => e.Alive && !e.InShadow)
            .Select(e => (Enemy: e, Offset: (e.Position - feet) with { Y = 0 }))
            .Where(m => m.Offset.Length() is > 0.1f and < Tuning.ShadowStepRange && Vector3.Dot(Vector3.Normalize(m.Offset), forward) > 0.3f)
            .OrderBy(m => m.Offset.Length()).Select(m => m.Enemy);
        foreach (var mark in marks)
        {
            var through = Vector3.Normalize((mark.Position - feet) with { Y = 0 });
            var spot = mark.Position + through * (mark.Def.Size * 0.5f + 1.1f);
            spot.Y = _world.GroundHeight(spot.X, spot.Z);
            // No stepping through walls, rocks or hills: the way to the enemy and on round it must be open.
            if (!Open(feet, mark.Position) || !Open(mark.Position, spot))
            {
                continue;
            }

            Move(player, spot);
            player.ShadowSteps++;
            player.LastShadowTick = Tick;
            var back = -through;
            player.State.AttackAim = (byte)((int)MathF.Round(MathF.Atan2(-back.X, -back.Z) / MathF.Tau * 256f) & 0xFF);
            return;
        }
    }

    /// <summary>Whether a body could pass in a straight line between two spots on the ground: nothing solid and no ground rising across the way at waist height.</summary>
    private bool Open(Vector3 start, Vector3 end)
    {
        var low = start with { Y = start.Y + 1f };
        var high = end with { Y = end.Y + 1f };
        var length = Vector3.Distance(low, high);
        for (var along = 0f; along <= length; along += 0.25f)
        {
            var point = Vector3.Lerp(low, high, length < 0.01f ? 1f : along / length);
            if (_world.IsBlocked(point) || _world.GroundHeight(point.X, point.Z) > point.Y - 0.3f)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Staff mastery Arcane Barrier: while a staff is in hand, the barrier refills once the player is left alone a while.</summary>
    private void StepBarrier(Player player, short maxHealth)
    {
        var most = Mastery.Has(player.Stats.Weapon, player.MasteryTaken, "arcane_barrier") ? (int)(maxHealth * Tuning.BarrierShare) : 0;
        if (player.Barrier >= most)
        {
            player.Barrier = most;
            return;
        }

        if (Tick - player.LastHurtTick > Tuning.BarrierDelayTicks)
        {
            player.BarrierCarry += most / (Tuning.BarrierFillSeconds * Tuning.TickRate);
            var gain = (int)player.BarrierCarry;
            player.BarrierCarry -= gain;
            player.Barrier = Math.Min(most, player.Barrier + gain);
        }
    }

    /// <summary>Where the aim from the bow first meets the ground or something solid, within the rain's range; else the ground below its end.</summary>
    private Vector3 RainSpot(Vector3 origin, Vector3 aim)
    {
        for (var along = 0.5f; along <= Tuning.RainRange; along += 0.5f)
        {
            var point = origin + aim * along;
            if (point.Y <= _world.GroundHeight(point.X, point.Z) || _world.IsBlocked(point))
            {
                return point;
            }
        }

        return origin + aim * Tuning.RainRange;
    }

    /// <summary>Bow mastery Rain of Arrows: arrows fall from high above round the spot.</summary>
    private void RainAt(Player player, Vector3 where)
    {
        var ground = _world.GroundHeight(where.X, where.Z);
        for (var i = 0; i < Tuning.RainArrows; i++)
        {
            var angle = (float)(_random.NextDouble() * MathF.Tau);
            var reach = MathF.Sqrt((float)_random.NextDouble()) * Tuning.RainRadius;
            var height = 12f + i * 1.6f;
            var spot = new Vector3(where.X - MathF.Sin(angle) * reach, ground, where.Z - MathF.Cos(angle) * reach);
            const float speed = 28f;
            _projectiles.Add(new Projectile
            {
                Id = _nextId++,
                Kind = ProjectileKind.PlayerArrow,
                Damage = 0,
                Position = spot + Vector3.UnitY * height,
                Velocity = new Vector3(0, -speed, 0),
                Shooter = player.Id,
                Draw = Tuning.RainShare,
                Pierces = true,
                Life = (int)MathF.Ceiling(height / speed * Tuning.TickRate) + 2,
            });
        }
    }

    /// <summary>Club mastery Shockwave: the spin sends a ring of force out past its own reach.</summary>
    private void Shockwave(Player player, float spinReach)
    {
        foreach (var enemy in _enemies.ToList())
        {
            var offset = enemy.Position - player.State.Position;
            var height = MathF.Abs(offset.Y);
            var distance = (offset with { Y = 0 }).Length() - enemy.Def.Size * 0.5f;
            if (enemy.Alive && distance > spinReach && distance <= Tuning.ShockwaveRadius && height < 2.5f)
            {
                Smite(player, enemy, AttackKind.ClubSpin, Tuning.ShockwaveShare, player.State.Position);
            }
        }
    }

    /// <summary>Staff mastery Chain Lightning: a bolt leaves an enemy for the nearest one not yet struck.</summary>
    private void Chain(Player caster, Vector3 from, HashSet<int> struck, int jumps)
    {
        var next = _enemies.Where(e => e.Alive && !struck.Contains(e.Id) && Vector3.Distance(e.Position, from) < Tuning.ChainReach)
            .OrderBy(e => Vector3.Distance(e.Position, from)).FirstOrDefault();
        if (jumps <= 0 || next is null)
        {
            return;
        }

        var bolt = new Projectile
        {
            Id = _nextId++,
            Kind = ProjectileKind.ChainBolt,
            Damage = 0,
            Position = from,
            Shooter = caster.Id,
            Blow = AttackKind.StaffBolt,
            TargetId = next.Id,
            Jumps = jumps - 1,
            Pierces = true,
            Life = Tuning.TickRate,
        };
        bolt.Struck.UnionWith(struck);
        _projectiles.Add(bolt);
    }

    /// <summary>A chain bolt flies at its enemy; on reaching it, it strikes and leaps on.</summary>
    private void StepChainBolt(Projectile bolt)
    {
        var target = _enemies.FirstOrDefault(e => e.Id == bolt.TargetId && e.Alive);
        if (target is null || !_players.TryGetValue(bolt.Shooter, out var caster))
        {
            bolt.Life = 0;
            return;
        }

        var chest = target.Position + new Vector3(0, target.Def.Size * 0.6f, 0);
        var line = chest - bolt.Position;
        var step = Tuning.ChainSpeed * Tuning.TickSeconds;
        bolt.Life--;
        if (line.Length() > step)
        {
            bolt.Velocity = Vector3.Normalize(line) * Tuning.ChainSpeed;
            bolt.Position += bolt.Velocity * Tuning.TickSeconds;
            return;
        }

        bolt.Position = chest;
        bolt.Life = 0;
        bolt.Struck.Add(target.Id);
        Smite(caster, target, AttackKind.StaffBolt, Tuning.ChainShare, chest);
        Chain(caster, chest, bolt.Struck, bolt.Jumps);
    }

    /// <summary>
    /// The damage of one blow: the attack, scaled by level and the weapon's power, plus flat
    /// damage; then every percentage bonus that applies to this target and this blow; a critical
    /// hit; and last the target's defense, less the armour the weapon pierces.
    /// </summary>
    private short BlowDamage(Player player, AttackKind kind, Enemy enemy, short? skillDamage = null, PlayerAttribute? attribute = null)
    {
        var gear = player.Stats;
        // A skill hits with its own damage, not the weapon's.
        var damage = skillDamage is { } own
            ? own * Progression.DamageScale(player.Level) + gear.Damage
            : Tuning.Attack(kind).Damage * Progression.DamageScale(player.Level) * gear.Power + gear.Damage;
        var bonus = gear.DamagePct;
        if (enemy.Def.Kind == "goblin")
        {
            bonus += gear.GoblinBane;
        }
        else if (enemy.Def.Kind is "animal" or "slime" or "wolf")
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

        // The attribute that goes with the weapon adds its share.
        bonus += player.Attribute(attribute ?? Core.Attributes.ForWeapon(gear.Weapon)) * Core.Attributes.DamagePctPerPoint;
        damage *= 1f + bonus / 100f;
        var crit = gear.CritChance + player.Attribute(PlayerAttribute.Dexterity) * Core.Attributes.CritChancePerDexterity;
        if (_random.NextDouble() * 100 < crit)
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
        RecomputeStats(player);
    }

    /// <summary>Worn gear, then the mastery of the weapon in hand, make the stats every blow uses.</summary>
    private static void RecomputeStats(Player player)
    {
        player.Stats = GearStats.Of(player.Worn.Select(w => player.Gear[w]!));
        Mastery.AddTo(player.Stats, player.MasteryTaken);
        player.State.Moves = (byte)Mastery.MovesFor(player.Stats.Weapon, player.MasteryTaken);
        player.State.Health = Math.Min(player.State.Health, MaxHealth(player));

        // The weapon decides the attacks; the client learns it through the next snapshot.
        player.State.Weapon = Weapons.Of(player.Stats.Weapon);
    }

    /// <summary>A kill earns the weapon in hand mastery experience, the same as the kill's experience.</summary>
    private static void AwardMastery(Player player, int xp)
    {
        var weapon = Mastery.WeaponIndex(player.Stats.Weapon);
        if (weapon < 0 || player.MasteryLevels[weapon] >= Mastery.MaxLevel)
        {
            return;
        }

        player.MasteryXp[weapon] += xp;
        while (player.MasteryLevels[weapon] < Mastery.MaxLevel && player.MasteryXp[weapon] >= Mastery.XpToNext(player.MasteryLevels[weapon]))
        {
            player.MasteryXp[weapon] -= Mastery.XpToNext(player.MasteryLevels[weapon]);
            player.MasteryLevels[weapon]++;
        }
    }

    /// <summary>A player's mastery level with a weapon kind. For tests and tools.</summary>
    public int GetMasteryLevel(int id, string weapon) => _players[id].MasteryLevels[Mastery.WeaponIndex(weapon)];

    public bool HasMastery(int id, int node) => (_players[id].MasteryTaken & (1ul << node)) != 0;

    /// <summary>Gives a player mastery experience with the weapon in hand. For tests and tools.</summary>
    public void GiveMasteryXp(int id, int xp) => AwardMastery(_players[id], xp);

    /// <summary>Takes the whole branch that ends in a move, without points. For tests and tools.</summary>
    public void GrantMastery(int id, string unlock)
    {
        var player = _players[id];
        var end = Mastery.Nodes.Single(n => n.Unlock == unlock);
        foreach (var node in Mastery.Nodes.Where(n => n.Weapon == end.Weapon && n.Branch == end.Branch))
        {
            player.MasteryTaken |= 1ul << node.Index;
        }

        RecomputeStats(player);
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

        // A clone is only a shadow: it gives nothing.
        if (enemy.SummonedBy >= 0)
        {
            return;
        }

        if (enemy.TrialOf >= 0)
        {
            killer.Rank = GuildRank.Adventurer;
            _story.SetFlag("guild.registered");
            killer.Gold += 25;
            AwardXp(killer.Id, 60);
            _finishedTrials.Add(enemy);
            return;
        }

        Story.OnKill(_story, enemy.Def.Id);

        // The first monster the hero kills wakes the Mark: levels, experience and skills appear.
        if (_story.Has("prologue.started") && !_story.Has("hud.mark") && enemy.Def.Kind != "animal")
        {
            _story.SetFlag("hud.mark");
        }

        var winner = _players.TryGetValue(enemy.TaggedBy, out var tagged) ? tagged : killer;
        AwardXp(winner.Id, Progression.KillXp(enemy.Level, winner.Level, enemy.Def.Rare));
        AwardMastery(killer, Progression.KillXp(enemy.Level, killer.Level, enemy.Def.Rare));
        foreach (var drop in enemy.Def.Drops ?? Array.Empty<DropEntry>())
        {
            if (_random.NextDouble() < drop.Chance)
            {
                Drop(enemy, winner, ItemDef.CrystalIndex(drop.Rarity));
                Stats.CrystalsDropped++;
            }
        }

        DropGear(enemy, winner);

        // An enemy that uses a skill can drop the book that teaches it.
        foreach (var skill in SkillDef.All)
        {
            if (skill.ChanceFrom(enemy.Def) is var chance and > 0 && _random.NextDouble() < chance)
            {
                Drop(enemy, winner, skill.Book);
                Stats.BooksDropped++;
            }
        }
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
                if (piece.Def.Slot == "weapon" && !owner.Worn.Any(w => owner.Gear[w]?.Def.Slot == "weapon"))
                {
                    // A weapon picked up with empty hands goes straight into the hand.
                    ToggleWorn(owner, free, piece);
                }
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

    /// <summary>
    /// The Guild chest, used while standing by it. A stack moves whole, either way; a gear piece that
    /// is not worn goes to the first free place in the chest, and comes back to the first free place
    /// in the gear list.
    /// </summary>
    private void UseChest(Player player, Command command, ushort arg)
    {
        if (!Near(player, "storage"))
        {
            return;
        }

        switch (command)
        {
            case Command.StoreItem when arg < ItemDef.All.Count && player.Bag.GetValueOrDefault(arg) is var held and > 0 && !ItemDef.All[arg].IsCrystal:
                AddItem(player, arg, -held);
                player.Storage[arg] = player.Storage.GetValueOrDefault(arg) + held;
                player.StorageChanged.Add(arg);
                break;
            case Command.TakeItem when player.Storage.GetValueOrDefault(arg) is var kept and > 0:
                player.Storage[arg] = 0;
                player.StorageChanged.Add(arg);
                AddItem(player, arg, kept);
                break;
            case Command.StoreGear when arg < GearSlots && player.Gear[arg] is { } piece && !player.Worn.Contains(arg)
                                        && Array.IndexOf(player.StoredGear, null) is var room and >= 0:
                player.StoredGear[room] = piece;
                player.StoredGearChanged.Add(room);
                player.Gear[arg] = null;
                player.GearChanged.Add(arg);
                break;
            case Command.TakeGear when arg < GearSlots && player.StoredGear[arg] is { } stored
                                       && Array.IndexOf(player.Gear, null) is var free and >= 0:
                player.Gear[free] = stored;
                player.GearChanged.Add(free);
                player.StoredGear[arg] = null;
                player.StoredGearChanged.Add(arg);
                break;
        }
    }

    /// <summary>The chest's stacks for a snapshot: changed ones first, then a slice of the rest, only by the chest.</summary>
    private BagEntry[] StorageSlice(Player player)
    {
        if (!Near(player, "storage"))
        {
            return Array.Empty<BagEntry>();
        }

        var entries = new List<BagEntry>(BagEntriesPerSnapshot);
        foreach (var item in player.StorageChanged.Take(BagEntriesPerSnapshot).ToList())
        {
            entries.Add(new BagEntry(item, (ushort)player.Storage[item]));
            player.StorageChanged.Remove(item);
        }

        var keys = player.Storage.Keys.ToList();
        for (var i = 0; i < keys.Count && entries.Count < BagEntriesPerSnapshot; i++)
        {
            var item = keys[player.StorageCursor++ % keys.Count];
            if (entries.All(e => e.Item != item))
            {
                entries.Add(new BagEntry(item, (ushort)player.Storage[item]));
            }
        }

        return entries.ToArray();
    }

    /// <summary>The chest's gear for a snapshot, the same way as the gear list, only by the chest.</summary>
    private GearEntry[] StoredGearSlice(Player player)
    {
        if (!Near(player, "storage"))
        {
            return Array.Empty<GearEntry>();
        }

        var entries = new List<GearEntry>(GearEntriesPerSnapshot);
        foreach (var place in player.StoredGearChanged.Take(GearEntriesPerSnapshot).ToList())
        {
            entries.Add(new GearEntry((byte)place, player.StoredGear[place], false));
            player.StoredGearChanged.Remove(place);
        }

        if (entries.Count < GearEntriesPerSnapshot)
        {
            var place = player.StoredGearCursor++ % GearSlots;
            if (entries.All(e => e.Index != place))
            {
                entries.Add(new GearEntry((byte)place, player.StoredGear[place], false));
            }
        }

        return entries.ToArray();
    }

    /// <summary>What a player keeps in the Guild chest. For tests and tools.</summary>
    public int GetStoredCount(int id, ushort item) => _players[id].Storage.GetValueOrDefault(item);

    public GearPiece? GetStoredGear(int id, int place) => _players[id].StoredGear[place];

    public GearPiece? GetGear(int id, int place) => _players[id].Gear[place];

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
        // A pack leader rallies every wolf of its kind nearby, and its pack rushes the player for a while.
        var leader = enemy.Def.Leader;
        if (leader)
        {
            _rushUntil[targetId] = Tick + RushTicks;
        }

        foreach (var other in _enemies)
        {
            var kin = other.Home == enemy.Home || (leader && other.Def.Kind == enemy.Def.Kind);
            if (other.Alive && other.TargetId < 0 && !other.Returning && kin &&
                Vector3.Distance(other.Position, enemy.Position) < (leader ? HelpRadius * 2.5f : HelpRadius))
            {
                other.TargetId = targetId;
            }
        }
    }

    bool IEnemyEvents.TryTakeTurn(Enemy enemy, int targetId)
    {
        if (!_turns.TryGetValue(targetId, out var holders))
        {
            holders = new HashSet<Enemy>();
            _turns[targetId] = holders;
        }

        holders.RemoveWhere(e => !e.Alive || !e.HasTurn || e.TargetId != targetId || e.Returning || !_enemies.Contains(e));
        var rush = _rushUntil.TryGetValue(targetId, out var until) && Tick < until && enemy.Role == "pack";
        if (holders.Count >= MaxCloseAttackers + (rush ? RushExtra : 0))
        {
            return false;
        }

        holders.Add(enemy);
        return true;
    }

    void IEnemyEvents.EndTurn(Enemy enemy)
    {
        foreach (var holders in _turns.Values)
        {
            holders.Remove(enemy);
        }
    }

    /// <summary>The fighters that hold a turn at a player right now.</summary>
    public int CloseAttackers(int playerId) =>
        _turns.TryGetValue(playerId, out var holders) ? holders.Count(e => e.Alive && e.HasTurn && e.TargetId == playerId) : 0;

    void IEnemyEvents.Splash(Enemy enemy)
    {
        Stats.Splashes++;
        foreach (var player in _players.Values)
        {
            var away = player.State.Position - enemy.Position;
            if (away.Length() > enemy.Def.SplashRadius || !Hurt(player, enemy.Def.SplashDamage, enemy.Def.SlowTicks, enemy, heavy: true))
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
        var shotsBefore = _projectiles.Count;
        Fire(enemy, ability, target);
        if (Creature(enemy.TargetId) is { } prey)
        {
            for (var i = shotsBefore; i < _projectiles.Count; i++)
            {
                _projectiles[i].Prey = prey;
                _projectiles[i].HunterId = enemy.Id;
            }
        }

        for (var i = shotsBefore; i < _projectiles.Count; i++)
        {
            _projectiles[i].Heavy = ability.Effect is not ("arrow" or "volley");
        }
    }

    private void Fire(Enemy enemy, Ability ability, Target? target)
    {
        var chest = enemy.Position + Vector3.UnitY * (enemy.Def.Size * 1.1f);
        // The attack goes where the warning showed, even if the player moved after it locked.
        var aimPoint = enemy.LeapTarget + Vector3.UnitY;
        var direction = aimPoint - chest;
        direction = direction == Vector3.Zero ? Vector3.UnitZ : Vector3.Normalize(direction);

        // A plain blow is light: a guard takes all of it. Everything else is heavy and partly breaks through.
        _heavy = ability.Effect is not ("melee" or "combo");
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
            case "pounce":
            case "shadowRun":
            case "superJump":
                Area(enemy.Position + Facing(enemy) * 0.5f, ability.Radius, ability.Damage, enemy);
                break;
            case "fangSlash":
                // Three crescents side by side, like the marks of three claws, all flying the same way.
                var across = new Vector3(-direction.Z, 0, direction.X);
                across = across == Vector3.Zero ? Vector3.UnitX : Vector3.Normalize(across);
                for (var claw = -1; claw <= 1; claw++)
                {
                    var fang = Shoot(ProjectileKind.FangSlash, chest + Facing(enemy) * 0.8f + across * (claw * FangSlashSpacing),
                        direction * FangSlashSpeed, ability.Damage);
                    fang.Life = (int)(FangSlashReach / FangSlashSpeed * Tuning.TickRate);
                }

                break;
            case "shadowClone":
                Summon(enemy, target);
                break;
            case "airSlash":
                // The crescent leaves the blade at chest height and dies after a few metres.
            case "airSlashWide":
                var wide = ability.Effect == "airSlashWide";
                var slash = Shoot(wide ? ProjectileKind.AirSlashWide : ProjectileKind.AirSlash, chest + Facing(enemy) * 0.8f,
                    direction * AirSlashSpeed, ability.Damage);
                slash.Life = (int)(AirSlashReach / AirSlashSpeed * Tuning.TickRate);
                slash.Radius = wide ? WideSlashRadius : AirSlashRadius;
                break;
            case "combo":
                Area(enemy.Position + Facing(enemy) * 1.1f, ability.Radius, ability.Damage, enemy);
                break;
            case "tornado":
                Area(enemy.Position, ability.Radius, ability.Damage, enemy);
                break;
            case "summonSpears":
                var side = new Vector3(MathF.Cos(enemy.Facing), 0, -MathF.Sin(enemy.Facing));
                for (var i = 0; i < SpearDelays.Length; i++)
                {
                    var hover = enemy.Position + Vector3.UnitY * (enemy.Def.Size * 2.3f) + side * ((i - 1) * 0.9f);
                    var conjured = Shoot(ProjectileKind.SummonedSpear, hover, Vector3.Zero, ability.Damage);
                    conjured.Delay = SpearDelays[i];
                    conjured.TargetId = target?.Id ?? enemy.TargetId;
                    conjured.Pierces = true;
                    conjured.Life = SpearDelays[i] + ProjectileLifeTicks;
                }

                break;
            case "spearThrow":
                var spear = Shoot(ProjectileKind.ThrownSpear, chest + Vector3.UnitY * 0.3f, direction * SpearSpeed, ability.Damage);
                spear.Gravity = Tuning.Gravity * 0.2f;
                spear.Velocity.Y += spear.Gravity * 0.5f * (Vector3.Distance(aimPoint, chest) / SpearSpeed);
                break;
            case "earthCrack":
                var ahead = direction with { Y = 0 };
                ahead = ahead == Vector3.Zero ? Facing(enemy) : Vector3.Normalize(ahead);
                _walls.Add(new FireWallHazard
                {
                    Id = _nextId++, Origin = enemy.Position + ahead * 0.8f, Direction = ahead, Damage = ability.Damage,
                    Kind = HazardKind.EarthCrack, Angle = EarthCrack.HalfAngle, Span = EarthCrack.Length,
                    Grow = EarthCrack.GrowTicks, Burn = EarthCrack.LingerTicks,
                });
                break;
        }
    }

    /// <summary>Two shadow copies of the wolf rise at its sides and join the hunt.</summary>
    private void Summon(Enemy wolf, Target? target)
    {
        var type = (byte)EnemyDef.All.ToList().FindIndex(d => d.Id == "shadow_clone");
        var side = new Vector3(MathF.Cos(wolf.Facing), 0, -MathF.Sin(wolf.Facing));
        foreach (var sign in new[] { -1f, 1f })
        {
            var clone = new Enemy
            {
                Id = _nextEnemyId++,
                Type = type,
                Home = wolf.Home with { Types = new[] { "shadow_clone" }, RespawnTicks = int.MaxValue },
                SummonedBy = wolf.Id,
                LifeTicks = CloneLifeTicks,
            };
            clone.Reset(_random, _world);
            var spot = wolf.Position + side * sign * CloneSpread;
            if (_world.IsBlocked(spot with { Y = _world.GroundHeight(spot.X, spot.Z) + 0.6f }))
            {
                spot = wolf.Position;
            }

            clone.Position = spot with { Y = _world.GroundHeight(spot.X, spot.Z) };
            clone.Level = wolf.Level;
            clone.Health = (short)clone.MaxHealth;
            clone.Facing = wolf.Facing;
            clone.TargetId = target?.Id ?? wolf.TargetId;
            clone.ActionTicks = 12;
            _summons.Add(clone);
        }
    }

    /// <summary>Whether the ability going off in <see cref="Fire(Enemy, Ability, Target?)"/> is a heavy blow.</summary>
    private bool _heavy;

    private static Vector3 Facing(Enemy enemy) => new(-MathF.Sin(enemy.Facing), 0, -MathF.Cos(enemy.Facing));

    /// <summary>
    /// A blow that lands on every player in a circle; the attacker feels any thorns. A hunter's blow
    /// also lands on its prey, and a boar's on the hunter it turned on.
    /// </summary>
    private void Area(Vector3 center, float radius, short damage, Enemy attacker)
    {
        var heavy = _heavy;
        foreach (var player in _players.Values.ToList())
        {
            if (Vector3.Distance(player.State.Position, center) <= radius + 0.4f && Hurt(player, damage, 0, attacker, heavy))
            {
                attacker.Landed = true;
            }
        }

        if (Creature(attacker.TargetId) is { } victim && Vector3.Distance(victim.Position, center) <= radius + victim.Def.Size * 0.4f + 0.4f)
        {
            victim.TakeHit(damage, attacker.Position, Enemy.CreatureBase + attacker.Id);
            attacker.Landed = true;
        }
    }

    /// <summary>The enemy or animal a creature target id names, if it is alive.</summary>
    private Enemy? Creature(int targetId) =>
        targetId >= Enemy.CreatureBase ? _enemies.FirstOrDefault(e => e.Id == targetId - Enemy.CreatureBase && e.Alive) : null;

    /// <summary>
    /// Creatures given to the enemies as targets alongside the players: every hunter, so animals run
    /// from it, and every creature that something is after, so the chaser can find it.
    /// </summary>
    private IEnumerable<Target> CreatureTargets()
    {
        var wanted = new HashSet<int>();
        foreach (var enemy in _enemies)
        {
            if (!enemy.Alive)
            {
                continue;
            }

            if (enemy.Hunting)
            {
                wanted.Add(enemy.Id);
            }

            if (enemy.TargetId >= Enemy.CreatureBase)
            {
                wanted.Add(enemy.TargetId - Enemy.CreatureBase);
            }
        }

        if (wanted.Count == 0)
        {
            yield break;
        }

        foreach (var enemy in _enemies)
        {
            if (enemy.Alive && wanted.Contains(enemy.Id))
            {
                yield return new Target(Enemy.CreatureBase + enemy.Id, enemy.Position, enemy.Velocity);
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

            // A crack hurts only while it runs; the split ground left behind is harmless.
            if (wall.Kind == HazardKind.EarthCrack && wall.Age > wall.Grow + 2)
            {
                continue;
            }

            foreach (var player in _players.Values)
            {
                if (!wall.Struck.Contains(player.Id) && InWedge(wall, player.State.Position, length))
                {
                    wall.Struck.Add(player.Id);
                    Hurt(player, wall.Damage, 0, heavy: true, from: wall.Origin);
                }
            }
        }

        _walls.RemoveAll(w => w.Age > w.Grow + w.Burn || (w.Owner >= 0 && !_players.ContainsKey(w.Owner)));
    }

    /// <summary>A player's line of fire: every enemy inside the strip takes the staff's blow, once.</summary>
    private void BurnEnemies(FireWallHazard wall, float length)
    {
        var caster = _players[wall.Owner];
        if (wall.Kind == HazardKind.EarthCrack)
        {
            // A player's crack: everything inside the V while it runs takes the skill's blow, once.
            if (wall.Age > wall.Grow + 2)
            {
                return;
            }

            foreach (var enemy in _enemies.ToList())
            {
                if (enemy.Alive && !wall.Struck.Contains(enemy.Id) && InWedge(wall, enemy.Position, length + enemy.Def.Size * 0.4f))
                {
                    wall.Struck.Add(enemy.Id);
                    SkillBlow(caster, enemy, wall.Skill, 1f, wall.Origin);
                }
            }

            return;
        }

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

        return distance < FireWallHazard.Reach || Vector3.Dot(offset / distance, wall.Direction) >= MathF.Cos(wall.Angle);
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
        var origin = ArrowOrigin(player.State.Position, flat);
        if (player.State.ShotDraw >= Tuning.FullDraw + Tuning.RainHoldTicks && Mastery.Has(player.Stats.Weapon, player.MasteryTaken, "rain_of_arrows"))
        {
            // Bow mastery Rain of Arrows: the held draw calls arrows down where the crosshair points.
            RainAt(player, RainSpot(origin, aim));
            return;
        }

        var piercing = Mastery.Has(player.Stats.Weapon, player.MasteryTaken, "piercing_arrow");
        var share = Tuning.DrawShare(player.State.ShotDraw);
        var life = (int)MathF.Ceiling(Tuning.ArrowRange(drawn) / speed * Tuning.TickRate);
        AddArrow(aim, share);

        // Bow mastery Triple Shot: two more arrows fan out either side of the aim, weaker.
        if (Mastery.Has(player.Stats.Weapon, player.MasteryTaken, "triple_shot"))
        {
            AddArrow(Vector3.Transform(aim, Matrix4x4.CreateRotationY(Tuning.TripleShotSpread)), share * Tuning.TripleShotShare);
            AddArrow(Vector3.Transform(aim, Matrix4x4.CreateRotationY(-Tuning.TripleShotSpread)), share * Tuning.TripleShotShare);
        }

        void AddArrow(Vector3 direction, float draw) => _projectiles.Add(new Projectile
        {
            Id = _nextId++,
            Kind = ProjectileKind.PlayerArrow,
            Damage = 0,
            Position = origin,
            Velocity = direction * speed,
            Gravity = Tuning.ArrowGravity,
            Shooter = player.Id,
            Draw = draw,
            Life = life,
            PiercesEnemies = piercing,
        });
    }

    /// <summary>The aim a player last fixed for a shot or a spell, as a direction, and flat.</summary>
    private static (Vector3 Aim, Vector3 Flat) ShotDirection(Player player) => Direction(player.ShotYaw, player.ShotPitch);

    /// <summary>A fine aim (yaw and pitch in 65536ths of a turn) as a direction, and flat.</summary>
    private static (Vector3 Aim, Vector3 Flat) Direction(ushort fineYaw, short finePitch)
    {
        var yaw = fineYaw / 65536f * MathF.Tau;
        var pitch = Math.Clamp(finePitch / 65536f * MathF.Tau, -Tuning.MaxShotPitch, Tuning.MaxShotPitch);
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

        if (Mastery.Has(player.Stats.Weapon, player.MasteryTaken, "meteor"))
        {
            // Staff mastery Meteor: a burning rock comes down on the far end of the line.
            var end = player.State.Position + flat * (0.6f + Tuning.StaffWallLength);
            end.Y = _world.GroundHeight(end.X, end.Z);
            var start = end + new Vector3(0, 16f, 0) - flat * 3f;
            var ticks = (int)MathF.Ceiling(Tuning.MeteorFlightSeconds * Tuning.TickRate);
            _projectiles.Add(new Projectile
            {
                Id = _nextId++,
                Kind = ProjectileKind.Meteor,
                Damage = 0,
                Position = start,
                Velocity = (end - start) / (ticks * Tuning.TickSeconds),
                Shooter = player.Id,
                Blow = AttackKind.StaffBolt,
                Draw = Tuning.MeteorShare,
                SplashRadius = Tuning.MeteorSplash,
                Pierces = true,
                Life = ticks,
            });
        }
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
            if (!enemy.Alive || shot.Struck.Contains(enemy.Id) || Vector3.Distance(chest, shot.Position) >= HitRadius + enemy.Def.Size * 0.35f)
            {
                continue;
            }

            var damage = (short)Math.Max(1, MathF.Round(BlowDamage(shooter, shot.Blow, enemy) * shot.Draw));
            var killed = enemy.TakeHit(damage, shot.Position - shot.Velocity, shooter.Id);
            AfterBlow(shooter, enemy, damage);
            Stats.EnemyHits++;
            CallForHelp(enemy, shooter.Id);
            if (killed)
            {
                OnKilled(enemy, shooter);
            }

            if (!shot.PiercesEnemies)
            {
                return true;
            }

            shot.Struck.Add(enemy.Id);
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

            if (shot.Delay > 0 && shot.Shooter >= 0)
            {
                // A player's conjured spear: when its moment comes it shoots at the nearest enemy,
                // or along the aim kept in it when nobody is near.
                shot.Life--;
                if (--shot.Delay == 0)
                {
                    var mark = _enemies.Where(e => e.Alive && Vector3.Distance(e.Position, shot.Position) < 22f)
                        .OrderBy(e => Vector3.Distance(e.Position, shot.Position)).FirstOrDefault();
                    var aim = mark is null
                        ? shot.Aim
                        : mark.Position + Vector3.UnitY * (mark.Def.Size * 0.6f) - shot.Position;
                    shot.Velocity = aim == Vector3.Zero ? Vector3.UnitZ : Vector3.Normalize(aim) * ConjuredSpearSpeed;
                }

                continue;
            }

            if (shot.Delay > 0)
            {
                // A conjured spear hangs still, then shoots straight at its player's chest.
                shot.Life--;
                if (--shot.Delay == 0)
                {
                    var aim = _players.TryGetValue(shot.TargetId, out var mark) ? mark.State.Position + Vector3.UnitY : shot.Position + Vector3.UnitZ;
                    var line = aim - shot.Position;
                    shot.Velocity = line == Vector3.Zero ? Vector3.UnitZ : Vector3.Normalize(line) * ConjuredSpearSpeed;
                    shot.Pierces = false;
                }

                continue;
            }

            if (shot.Kind == ProjectileKind.ChainBolt)
            {
                StepChainBolt(shot);
                continue;
            }

            shot.Velocity.Y -= shot.Gravity * Tuning.TickSeconds;
            shot.Position += shot.Velocity * Tuning.TickSeconds;
            shot.Life--;
            var struck = !shot.Pierces && _world.IsBlocked(shot.Position);
            if (shot.Shooter >= 0 && shot.Skill >= 0 && shot.Kind != ProjectileKind.PlayerFireball)
            {
                if (struck || !_players.TryGetValue(shot.Shooter, out var caster) || SkillShotHits(shot, caster))
                {
                    shot.Life = 0;
                }

                continue;
            }

            if (shot.Shooter >= 0)
            {
                if (shot.Kind is ProjectileKind.PlayerFireball or ProjectileKind.Meteor)
                {
                    if (struck || shot.Life <= 0 || (shot.Kind == ProjectileKind.PlayerFireball && TouchesEnemy(shot)))
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

            if (shot.Prey is { Alive: true } prey &&
                Vector3.Distance(prey.Position + Vector3.UnitY * (prey.Def.Size * 0.6f), shot.Position) < shot.Radius + prey.Def.Size * 0.35f)
            {
                prey.TakeHit(shot.Damage, shot.Position - shot.Velocity * Tuning.TickSeconds, Enemy.CreatureBase + shot.HunterId);
                struck = true;
            }

            foreach (var player in _players.Values)
            {
                var chest = player.State.Position + Vector3.UnitY;
                if (Vector3.Distance(chest, shot.Position) >= shot.Radius)
                {
                    continue;
                }

                if (shot.SplashRadius > 0f)
                {
                    struck = true;
                }
                else
                {
                    Hurt(player, shot.Damage, shot.SlowTicks, heavy: shot.Heavy, from: shot.Position - shot.Velocity * Tuning.TickSeconds * 4f);
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

        // Staff mastery Chain Lightning: the plain fireball's nearest victim sends a bolt leaping on.
        var chain = shot.Skill < 0 && shot.Kind == ProjectileKind.PlayerFireball && Mastery.Has(caster.Stats.Weapon, caster.MasteryTaken, "chain_lightning");
        var struck = new HashSet<int>();
        Enemy? first = null;
        var firstDistance = float.MaxValue;
        foreach (var enemy in _enemies.ToList())
        {
            var chest = enemy.Position + new Vector3(0, enemy.Def.Size * 0.6f, 0);
            var distance = Vector3.Distance(chest, shot.Position) - enemy.Def.Size * 0.35f;
            if (enemy.Alive && distance < shot.SplashRadius)
            {
                struck.Add(enemy.Id);
                if (distance < firstDistance)
                {
                    (first, firstDistance) = (enemy, distance);
                }

                if (shot.Skill >= 0)
                {
                    SkillBlow(caster, enemy, shot.Skill, distance < HitRadius ? 1f : 0.6f, shot.Position - shot.Velocity);
                }
                else
                {
                    Smite(caster, enemy, shot.Blow, (distance < HitRadius ? 1f : 0.6f) * shot.Draw, shot.Position - shot.Velocity);
                }
            }
        }

        if (chain && first is not null)
        {
            Chain(caster, first.Position + new Vector3(0, first.Def.Size * 0.6f, 0), struck, Tuning.ChainJumps);
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
                Hurt(player, shot.Damage, shot.SlowTicks, heavy: true, from: shot.Position - shot.Velocity * Tuning.TickSeconds * 4f);
            }
        }
    }

    private void SendSnapshots()
    {
        foreach (var player in _players.Values)
        {
            player.StoryChanged.UnionWith(_story.Changed);
        }

        _story.Changed.Clear();
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
                Attributes = (byte[])player.Attributes.Clone(),
                KnownSkills = player.Skills.Aggregate(0u, (bits, skill) => bits | (1u << skill)),
                MasteryLevels = (byte[])player.MasteryLevels.Clone(),
                MasteryXp = (int[])player.MasteryXp.Clone(),
                MasteryTaken = player.MasteryTaken,
                SkillSlots = (byte[])player.SkillSlots.Clone(),
                SkillCooldowns = player.SkillReady.Select(ready => (byte)Math.Clamp((ready > Tick ? ready - Tick : 0) / 6, 0, 255)).ToArray(),
                UnspentPoints = (byte)Math.Clamp(player.UnspentPoints, 0, 255),
                Xp = player.Xp,
                XpToNext = Progression.XpToNext(player.Level),
                MaxHealth = MaxHealth(player),
                Dodges = player.Dodges,
                Blocks = player.Blocks,
                Parries = player.Parries,
                Ripostes = player.Ripostes,
                Barrier = (short)player.Barrier,
                Rank = player.Rank,
                Gold = player.Gold,
                Appearance = player.Appearance,
                Actors = ActorSlice(origin),
                Scene = _scene is null ? Snapshot.NoScene : (ushort)_scene.Index,
                SceneStep = (ushort)_sceneStep,
                SceneStepTick = _sceneStepTick,
                SceneSlide = (byte)Math.Min(_sceneSlide, 255),
                Day = (ushort)_calendar.Now.Day,
                Minute = (ushort)_calendar.Now.Minute,
                Story = StoryMirror.Slice(_story, player.StoryChanged, ref player.StoryCursor),
                Crystals = player.Crystals.ToArray(),
                Others = _players.Values.Where(p => p.Id != player.Id).Take(6)
                    .Select(p => new RemotePlayer(p.Id, p.State.Position, p.State.Velocity, p.State.AttackAim, p.State.Attack, p.State.AttackTick, p.State.Weapon)).ToArray(),
                Enemies = enemies,
                Projectiles = shots,
                Drops = drops,
                Bag = BagSlice(player),
                Gear = GearSlice(player),
                Storage = StorageSlice(player),
                StoredGear = StoredGearSlice(player),
                FireWalls = _walls.Where(w => Vector3.Distance(w.Origin, origin) < InterestRange).Take(2)
                    .Select(w => new FireWallView(w.Id, w.Origin, MathF.Atan2(-w.Direction.X, -w.Direction.Z), w.Length, w.HalfWidth, w.Kind)).ToArray(),
            };
            _transport.Send(player.Id, Fit(snapshot));
        }
    }

    /// <summary>Real UDP refuses larger unreliable packets (LiteNetLib stops at 1023 bytes, with room kept for its header).</summary>
    public const int MaxSnapshotBytes = 1000;

    /// <summary>
    /// Encodes a snapshot, dropping the farthest shots, then drops, then enemies until it fits one
    /// packet. A crowded fight (a rain of arrows over a camp) once made one too big, and the
    /// refused send brought the whole server down.
    /// </summary>
    private byte[] Fit(Snapshot snapshot)
    {
        var bytes = Protocol.EncodeSnapshot(snapshot);
        Stats.LargestSnapshot = Math.Max(Stats.LargestSnapshot, bytes.Length);
        while (bytes.Length > MaxSnapshotBytes)
        {
            if (snapshot.Projectiles.Length > 0)
            {
                snapshot.Projectiles = snapshot.Projectiles[..(snapshot.Projectiles.Length * 2 / 3)];
            }
            else if (snapshot.Drops.Length > 0)
            {
                snapshot.Drops = snapshot.Drops[..(snapshot.Drops.Length / 2)];
            }
            else if (snapshot.Actors.Length > 4)
            {
                snapshot.Actors = snapshot.Actors[..4];
            }
            else if (snapshot.Enemies.Length > 0)
            {
                snapshot.Enemies = snapshot.Enemies[..(snapshot.Enemies.Length * 3 / 4)];
            }
            else
            {
                break;
            }

            bytes = Protocol.EncodeSnapshot(snapshot);
        }

        return bytes;
    }
}
