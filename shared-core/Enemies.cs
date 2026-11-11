using System.Numerics;
using System.Reflection;
using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>An enemy type, loaded from world/enemies.json.</summary>
/// <summary>
/// The gear an enemy can drop: the chance that it drops a piece at all, the weights of each rarity
/// (common, rare, epic, legendary) and the base items to choose from.
/// </summary>
public sealed record GearTable(double Chance, int[] Rarity, string[] Items);

public sealed record EnemyDef(
    string Id, string Name, int MinLevel, int MaxLevel, int Health, int HealthPerLevel, float Speed, float Size,
    float AggroRange, int HopTicks = 40, float LeapRange = 0f, int LeapCooldownTicks = 0, float SplashRadius = 0f,
    short SplashDamage = 0, byte SlowTicks = 0, float SpitRange = 0f, short SpitDamage = 0, int SpitCooldownTicks = 0,
    double RareChance = 0, string Kind = "slime", bool Rare = false, string Weapon = "", float PreferredRange = 2f,
    Ability[]? Abilities = null, DropEntry[]? Drops = null, string Temper = "hostile", LootEntry[]? Loot = null,
    float AlertRange = 0f, float TagHeight = 0f, GearTable? Gear = null, int Defense = 0, string? Behavior = null,
    bool Leader = false, bool Statue = false, float FrontGuard = 1f, float BackBonus = 1f,
    float EnrageAt = 0f, float EnrageWindup = 1f, float EnrageRecover = 1f, float EnrageTaken = 1f)
{
    private static readonly Lazy<IReadOnlyList<EnemyDef>> Loaded = new(Load);

    public static IReadOnlyList<EnemyDef> All => Loaded.Value;

    private static IReadOnlyList<EnemyDef> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("enemies")!;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<EnemyDef[]>(stream, options)!;
    }
}

/// <summary>
/// A camp: a place enemies live, return to and respawn at. A rare camp holds only rare enemies.
/// With a route it is a patrol: the group walks from point to point and back, resting at each.
/// A camp that hunts goes after deer and boar now and then. Facing, when set, is the way its statues stand guard.
/// </summary>
public readonly record struct EnemySpawn(
    Vector3 Center, float Radius, int Count, int MinLevel = 1, int MaxLevel = 3, int RespawnTicks = 240, bool Rare = false,
    string[]? Types = null, Vector3[]? Route = null, bool Hunts = false, float? Facing = null);

public enum EnemyAction : byte
{
    Idle = 0,
    Hop = 1,
    Windup = 2,
    Leap = 3,
    Splat = 4,
    Spit = 5,
    Dead = 6,
    Walk = 7,
    Cast = 8,
    Strike = 9,

    /// <summary>An animal with its head down, eating.</summary>
    Graze = 10,

    /// <summary>An animal standing tall and watching something.</summary>
    Alert = 11,

    /// <summary>A wolf sunk into the ground as a shadow: hidden, sliding, and beyond harm.</summary>
    Shadow = 12,

    /// <summary>A shadow wolf risen between two slides, where it can be hit.</summary>
    Surface = 13,

    /// <summary>An orc spinning round and round with its axe, drifting towards the player.</summary>
    Tornado = 14,

    /// <summary>A duelist dashing aside, beyond harm for the moment.</summary>
    Dodge = 15,

    /// <summary>A werewolf that missed its great leap, rolling forward out of the landing.</summary>
    Roll = 16,

    /// <summary>Reeling from a parried blow: it cannot act, and takes more from every hit.</summary>
    Stagger = 17,
}

/// <summary>The nametag colour rule: how an enemy compares with the player.</summary>
public enum Threat
{
    Weak,
    Even,
    Strong,
}

public static class ThreatRules
{
    public static Threat Compare(int enemyLevel, int playerLevel) =>
        enemyLevel > playerLevel ? Threat.Strong : enemyLevel < playerLevel ? Threat.Weak : Threat.Even;
}

/// <summary>A player as an enemy sees it.</summary>
/// <param name="Attacking">The player is in the middle of an attack, which a duelist may dodge.</param>
public readonly record struct Target(int Id, Vector3 Position, Vector3 Velocity, bool Attacking = false);

/// <summary>What an enemy asks the game to do.</summary>
public interface IEnemyEvents
{
    void Splash(Enemy enemy);
    void Spit(Enemy enemy, Target target);

    /// <summary>An ability goes off. The target can be missing, as at the end of a lunge.</summary>
    void Fire(Enemy enemy, Ability ability, Target? target);

    /// <summary>The enemy has noticed a player; its neighbours join in.</summary>
    void CallForHelp(Enemy enemy, int targetId);

    /// <summary>Asks for a turn to come in close at a player. Only a few fighters get one at a time.</summary>
    bool TryTakeTurn(Enemy enemy, int targetId) => true;

    /// <summary>The fighter has finished its turn and makes way.</summary>
    void EndTurn(Enemy enemy) { }
}

/// <summary>
/// One enemy on the server. It wanders its camp, chases what it notices, leaps with a warning circle
/// and splashes on landing. Pulled too far from home it gives up, returns and heals.
/// </summary>
public sealed partial class Enemy
{
    private const int WindupTicks = 34;
    private const int SpitWindupTicks = 26;
    private const int SplatTicks = 26;
    private const int LeapAirTicks = 34;
    private const float HopSpeed = 5.5f;
    private const float LeashExtra = 16f;

    /// <summary>
    /// How far past its camp a fighter chases a player before it gives up and goes home. Long enough
    /// that a player can pull one or two away from a group and fight them alone.
    /// </summary>
    public const float ChaseLeash = 45f;

    /// <summary>How long a hit walker or fighter reels before it acts again.</summary>
    private const int StaggerTicks = 6;
    private const float LeapLead = 0.3f;

    /// <summary>Ticks before an attack lands in which its aim no longer moves.</summary>
    public const int AimLockTicks = 14;
    private const int StuckCheckTicks = 40;
    private const float StuckDistance = 0.5f;
    private const int DetourTicks = 80;
    private const int GiveUpChecks = 9;
    private const float GiveUpDistance = 7f;
    private const float ReturnSpeedFactor = 1.8f;
    private const int RegenIntervalTicks = 60;

    /// <summary>Seconds a full heal takes, as one step of health comes each second.</summary>
    private const int RegenSteps = 45;
    private int _regenTicks;

    public required int Id;
    public required byte Type;
    public required EnemySpawn Home;
    public byte Level;
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 LeapTarget;
    public short Health;
    public EnemyAction Action;
    public int ActionTicks;
    public int LeapCooldown;
    public int SpitCooldown;
    public byte HitFlash;
    public float Facing;

    /// <summary>
    /// The player this enemy is after, or -1. From <see cref="CreatureBase"/> up it is another creature:
    /// prey for a hunter, or the hunter a boar turns on.
    /// </summary>
    public int TargetId = -1;

    /// <summary>Target ids from here up name an enemy or animal: the id minus this.</summary>
    public const int CreatureBase = 1_000_000;

    /// <summary>Out hunting with its camp; animals run from it while this lasts.</summary>
    public bool Hunting;

    /// <summary>The first player to hit this enemy gets the experience for it.</summary>
    public int TaggedBy = -1;

    /// <summary>The player taking the guild trial against this enemy, or -1.</summary>
    public int TrialOf = -1;

    /// <summary>
    /// The round of the prologue trial: 1 only light blows (to block and parry), 2 only the heavy
    /// slam (to dash through), 3 a real duel; 0 for an ordinary trial or any other enemy.
    /// </summary>
    public int TrialRound;

    /// <summary>Rhea's training lesson (<see cref="Tutorial.Lessons"/>, from 1), or 0. In a striking or sliding lesson she stands still; she cannot be hurt.</summary>
    public int TutorLesson;

    /// <summary>In a lesson round, holds back every ability but the one the round teaches, and keeps that one coming.</summary>
    private void HoldTrialAbilities()
    {
        if ((TrialRound is not (1 or 2) && TutorLesson is not (3 or 4)) || Def.Abilities is not { } abilities)
        {
            return;
        }

        var taught = TrialRound == 1 || TutorLesson == 3 ? "melee" : "slam";
        for (var i = 0; i < abilities.Length; i++)
        {
            _cooldowns[i] = abilities[i].Effect == taught ? Math.Min(_cooldowns[i], TrialRound == 1 || TutorLesson == 3 ? 40 : 100) : Math.Max(_cooldowns[i], 2);
        }
    }

    public bool Returning;

    /// <summary>Brought by a scene: it plays its part to the end and never loses heart.</summary>
    public bool Staged;

    /// <summary>Bleeding: ticks left, damage dealt each second, and the player who caused it.</summary>
    public int BleedTicks;
    public short BleedDamage;
    public int BleedOwner = -1;
    private int _detour;
    private float _detourSide = 1f;
    private int _stuckClock;
    private int _stalls;
    private Vector3 _lastCheck;
    private Vector3 _anchor;

    public EnemyDef Def => EnemyDef.All[Type];
    public int MaxHealth => Def.Health + Def.HealthPerLevel * (Level - 1);
    public bool Alive => Action != EnemyAction.Dead;

    public void Reset(Random random, World world)
    {
        var chosen = Choose(random);
        Type = (byte)EnemyDef.All.ToList().IndexOf(chosen);
        Level = (byte)(Home.Types is not null ? random.Next(chosen.MinLevel, chosen.MaxLevel + 1)
            : chosen.Rare ? Home.MaxLevel + 1 : random.Next(Home.MinLevel, Home.MaxLevel + 1));
        var angle = (float)(random.NextDouble() * Math.Tau);
        var reach = (float)random.NextDouble() * Home.Radius;
        var spot = Home.Center + new Vector3(MathF.Cos(angle) * reach, 0, MathF.Sin(angle) * reach);
        spot.Y = world.GroundHeight(spot.X, spot.Z);
        for (var tries = 0; tries < 12 && world.IsBlocked(spot + Vector3.UnitY * 0.6f); tries++)
        {
            angle += 1.1f;
            spot = Home.Center + new Vector3(MathF.Cos(angle) * reach, 0, MathF.Sin(angle) * reach);
            spot.Y = world.GroundHeight(spot.X, spot.Z);
        }

        Position = spot;
        // A statue keeps the spot and the facing it rose with, and goes back to them after a fight.
        Post = spot;
        StatueFacing = Facing = Home.Facing ?? angle;
        Velocity = Vector3.Zero;
        Health = (short)MaxHealth;
        Action = EnemyAction.Idle;
        ActionTicks = random.Next(10, 60);
        LeapCooldown = 60;
        SpitCooldown = 60;
        TargetId = -1;
        TaggedBy = -1;
        Returning = false;
        Routed = false;
        RoutTicks = 0;
        PackLed = false;
        Enraged = false;
        HasteTicks = 0;
    }

    private EnemyDef Choose(Random random)
    {
        if (Home.Types is { Length: > 0 } types)
        {
            var id = types[random.Next(types.Length)];
            return EnemyDef.All.First(d => d.Id == id);
        }

        var rare = EnemyDef.All.First(d => d.RareChance > 0);
        return Home.Rare || random.NextDouble() < rare.RareChance ? rare : EnemyDef.All[0];
    }

    public void Step(World world, Random random, IReadOnlyList<Target> targets, IEnemyEvents events)
    {
        if (HitFlash > 0) HitFlash--;
        HoldTrialAbilities();
        Recover();
        WatchForStuck(world, random, targets);
        if (LeapCooldown > 0) LeapCooldown--;
        if (SpitCooldown > 0) SpitCooldown--;
        ActionTicks--;

        // A summoned clone fades away when its time runs out; the server then removes it.
        if (SummonedBy >= 0 && Alive && --LifeTicks <= 0)
        {
            Action = EnemyAction.Dead;
            Velocity = Vector3.Zero;
            return;
        }

        if (TutorLesson > 0 && TutorLesson is not (3 or 4))
        {
            // In a lesson where the player strikes or slides, Rhea stands and watches them.
            Velocity = Vector3.Zero;
            Action = EnemyAction.Idle;
            ActionTicks = 10;
            if (FindTarget(targets) is { } watched)
            {
                var look = watched.Position - Position;
                Facing = MathF.Atan2(-look.X, -look.Z);
            }

            return;
        }

        if (IsAnimal && Action != EnemyAction.Dead)
        {
            StepAnimal(world, random, targets, events);
            return;
        }

        if (Def.Kind != "slime" && Action != EnemyAction.Dead)
        {
            StepGoblin(world, random, targets, events);
            return;
        }

        switch (Action)
        {
            case EnemyAction.Dead:
                if (ActionTicks <= 0)
                {
                    Reset(random, world);
                }

                return;
            case EnemyAction.Idle:
                Velocity.X *= 0.8f;
                Velocity.Z *= 0.8f;
                if (ActionTicks <= 0)
                {
                    Decide(world, random, targets, events);
                }

                break;
            case EnemyAction.Windup:
                Velocity = Vector3.Zero;
                TrackTarget(world, targets, true);
                if (ActionTicks <= 0)
                {
                    Launch();
                }

                break;
            case EnemyAction.Spit:
                Velocity = Vector3.Zero;
                if (ActionTicks <= 0)
                {
                    if (FindTarget(targets) is { } aim)
                    {
                        events.Spit(this, aim);
                    }

                    ToIdle(random);
                }

                break;
            case EnemyAction.Splat:
                Velocity = Vector3.Zero;
                if (ActionTicks <= 0)
                {
                    ToIdle(random);
                }

                break;
        }

        Fall(world, random, events);
    }

    /// <summary>
    /// Notices an enemy that wants to move but does not get anywhere. It first walks around the
    /// obstacle, and if that keeps failing it gives up and goes home, as MMO enemies do.
    /// </summary>
    private void WatchForStuck(World world, Random random, IReadOnlyList<Target> targets)
    {
        if (_detour > 0) _detour--;
        if (++_stuckClock < StuckCheckTicks)
        {
            return;
        }

        _stuckClock = 0;
        var moving = Action is EnemyAction.Walk or EnemyAction.Hop;
        if (moving && Flat(Position - _lastCheck).Length() < StuckDistance)
        {
            _detour = DetourTicks;
            _detourSide = random.Next(2) == 0 ? -1f : 1f;
        }

        _lastCheck = Position;

        // Going nowhere for a long time, while the goal is still far away, means it cannot be reached.
        var goal = FindTarget(targets) is { } target ? target.Position : Home.Center;
        var far = Flat(goal - Position).Length() > Def.PreferredRange + 4f;
        if (Flat(Position - _anchor).Length() > GiveUpDistance || !moving || !far)
        {
            _anchor = Position;
            _stalls = 0;
            return;
        }

        if (++_stalls >= GiveUpChecks)
        {
            _stalls = 0;
            TargetId = -1;
            Returning = false;
            var spot = Home.Center;
            spot.Y = world.GroundHeight(spot.X, spot.Z);
            Position = spot;
            Velocity = Vector3.Zero;
            ToIdle(random);
        }
    }

    /// <summary>Out of a fight an enemy heals slowly, so a retreat does not undo the damage at once.</summary>
    private void Recover()
    {
        if (!Alive || TargetId >= 0 || Health >= MaxHealth)
        {
            return;
        }

        if (++_regenTicks >= RegenIntervalTicks)
        {
            _regenTicks = 0;
            Health = (short)Math.Min(MaxHealth, Health + Math.Max(1, MaxHealth / RegenSteps));
            if (Health >= MaxHealth)
            {
                TaggedBy = -1;
            }
        }
    }

    /// <summary>Takes damage from a player. Returns true when this hit kills the enemy.</summary>
    public bool TakeHit(short damage, Vector3 from, int attackerId = -1)
    {
        // The first two rounds of the prologue trial are lessons: the examiner cannot be hurt.
        if (InShadow || Dodging || TrialRound is 1 or 2 || TutorLesson > 0)
        {
            return false;
        }

        // A reeling enemy has its guard wide open.
        if (Action == EnemyAction.Stagger)
        {
            damage = (short)MathF.Round(damage * Tuning.StaggerDamageScale);
        }

        // A berserker in its frenzy has given up guarding itself.
        if (Enraged)
        {
            damage = (short)MathF.Round(damage * Def.EnrageTaken);
        }

        // A guardian's front is armoured and its back is not: blows are worth going round for.
        var side = GuardSide(from);
        if (side > 0 && Def.FrontGuard < 1f && Action != EnemyAction.Stagger)
        {
            damage = (short)Math.Max(1, MathF.Round(damage * Def.FrontGuard));
            Guarded = 8;
        }
        else if (side < 0 && Def.BackBonus > 1f)
        {
            damage = (short)MathF.Round(damage * Def.BackBonus);
        }

        if (TaggedBy < 0)
        {
            TaggedBy = attackerId;
        }

        if (attackerId >= 0 && !Returning)
        {
            TargetId = attackerId;
        }

        Health -= damage;
        HitFlash = 10;
        var away = Flat(Position - from);
        if (away != Vector3.Zero && !Def.Statue)
        {
            Velocity += Vector3.Normalize(away) * 5f + Vector3.UnitY * 3f;
        }

        if (Action == EnemyAction.Windup || Action == EnemyAction.Spit)
        {
            Action = EnemyAction.Hop;
        }
        else if (Def.Kind != "slime" && !Def.Statue && Action is not (EnemyAction.Cast or EnemyAction.Leap or EnemyAction.Strike or EnemyAction.Stagger))
        {
            // A short stagger, then it turns on the attacker. Without this a grazing boar kept an
            // action the fighting code does not know and slid away on the knockback for good, still
            // facing where it had looked.
            Action = EnemyAction.Idle;
            ActionTicks = StaggerTicks;
        }
        else if (Action == EnemyAction.Cast && MaxHealth - Health > 0 && Def.Weapon == "dagger")
        {
            Action = EnemyAction.Idle;
            ActionTicks = 8;
        }

        if (Health > 0)
        {
            return false;
        }

        Action = EnemyAction.Dead;
        ActionTicks = Home.RespawnTicks;
        Velocity = Vector3.Zero;
        return true;
    }

    /// <summary>Ticks left of the spark a guarded blow throws off, for the client.</summary>
    public byte Guarded;

    /// <summary>Whether a blow from a point lands on the front (1), the back (-1) or a flank (0).</summary>
    public int GuardSide(Vector3 from)
    {
        var toward = Flat(from - Position);
        if (toward == Vector3.Zero)
        {
            return 0;
        }

        var facing = new Vector3(-MathF.Sin(Facing), 0, -MathF.Cos(Facing));
        var dot = Vector3.Dot(Vector3.Normalize(toward), facing);
        return dot > GuardCone ? 1 : dot < -GuardCone ? -1 : 0;
    }

    /// <summary>How square in front or behind a blow must come to count as front or back (the cosine of the angle).</summary>
    public const float GuardCone = 0.4f;

    /// <summary>
    /// Keeps the warning on the player while an attack winds up. It locks a moment before the attack
    /// lands, so what the warning shows is what happens, and the player can still step out.
    /// </summary>
    private void TrackTarget(World world, IReadOnlyList<Target> targets, bool follow)
    {
        if (ActionTicks <= AimLockTicks || FindTarget(targets) is not { } target)
        {
            return;
        }

        Face(target.Position);
        if (follow)
        {
            LeapTarget = target.Position;
            LeapTarget.Y = world.GroundHeight(LeapTarget.X, LeapTarget.Z);
        }
    }

    private Target? FindTarget(IReadOnlyList<Target> targets)
    {
        foreach (var target in targets)
        {
            if (target.Id == TargetId)
            {
                return target;
            }
        }

        return null;
    }

    private void ToIdle(Random random)
    {
        Action = EnemyAction.Idle;
        ActionTicks = TargetId >= 0 || Returning ? random.Next(6, 18) : random.Next(Def.HopTicks, Def.HopTicks * 4);
    }

    private void Decide(World world, Random random, IReadOnlyList<Target> targets, IEnemyEvents events)
    {
        var homeDistance = Flat(Home.Center - Position).Length();
        if (Returning)
        {
            if (homeDistance < Home.Radius * 0.6f)
            {
                Returning = false;
                ToIdle(random);
                return;
            }

            Hop(world, random, Home.Center - Position, ReturnSpeedFactor);
            return;
        }

        if (TargetId >= 0 && (FindTarget(targets) is null || homeDistance > Home.Radius + LeashExtra))
        {
            TargetId = -1;
            Returning = homeDistance > Home.Radius;
            ToIdle(random);
            return;
        }

        if (TargetId < 0)
        {
            Notice(targets, events);
        }

        if (FindTarget(targets) is { } target)
        {
            Fight(world, random, target, events);
            return;
        }

        // Wander around the camp, and drift back towards the middle near its edge.
        if (homeDistance > Home.Radius)
        {
            Hop(world, random, Home.Center - Position, 1f);
        }
        else if (random.Next(3) == 0)
        {
            ToIdle(random);
        }
        else
        {
            var angle = (float)(random.NextDouble() * Math.Tau);
            Hop(world, random, new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)), 0.6f);
        }
    }

    private void Notice(IReadOnlyList<Target> targets, IEnemyEvents events)
    {
        var nearest = Def.AggroRange;
        foreach (var target in targets)
        {
            // Only players are noticed; prey and hunters are given as targets on purpose.
            if (target.Id >= CreatureBase)
            {
                continue;
            }

            var distance = Vector3.Distance(target.Position, Position);
            if (distance < nearest)
            {
                nearest = distance;
                TargetId = target.Id;
            }
        }

        if (TargetId >= 0)
        {
            events.CallForHelp(this, TargetId);
        }
    }

    private void Fight(World world, Random random, Target target, IEnemyEvents events)
    {
        var distance = Vector3.Distance(target.Position, Position);
        Face(target.Position);
        if (distance < Def.LeapRange && LeapCooldown == 0)
        {
            Action = EnemyAction.Windup;
            ActionTicks = WindupTicks;
            LeapCooldown = Def.LeapCooldownTicks + random.Next(60);
            LeapTarget = target.Position + Flat(target.Velocity) * LeapLead;
            LeapTarget.Y = world.GroundHeight(LeapTarget.X, LeapTarget.Z);
            return;
        }

        if (Def.SpitRange > 0 && distance < Def.SpitRange && SpitCooldown == 0)
        {
            Action = EnemyAction.Spit;
            ActionTicks = SpitWindupTicks;
            SpitCooldown = Def.SpitCooldownTicks;
            return;
        }

        // A spitter keeps its distance; a plain slime closes in.
        var toward = target.Position - Position;
        Hop(world, random, Def.SpitRange > 0 && distance < Def.LeapRange * 0.6f ? -toward : toward, 1f);
    }

    /// <summary>Hops in a direction, turning aside when something stands in the way.</summary>
    private void Hop(World world, Random random, Vector3 direction, float speedFactor)
    {
        var flat = Flat(direction);
        if (flat == Vector3.Zero)
        {
            ToIdle(random);
            return;
        }

        var heading = Steer(world, Vector3.Normalize(flat));
        Facing = MathF.Atan2(-heading.X, -heading.Z);
        Action = EnemyAction.Hop;
        ActionTicks = Def.HopTicks;
        Velocity = heading * (Def.Speed * speedFactor) + Vector3.UnitY * HopSpeed;
    }

    private Vector3 Steer(World world, Vector3 heading)
    {
        if (_detour > 0)
        {
            heading = Vector3.Transform(heading, Matrix4x4.CreateRotationY(_detourSide * 1.25f));
        }

        foreach (var turn in new[] { 0f, 0.45f, -0.45f, 0.9f, -0.9f, 1.4f, -1.4f, 2f, -2f, 2.7f, -2.7f })
        {
            var rotated = Vector3.Transform(heading, Matrix4x4.CreateRotationY(_detour > 0 ? turn * _detourSide : turn));
            if (PathIsClear(world, rotated))
            {
                return rotated;
            }
        }

        return heading;
    }

    /// <summary>Checks a body-wide strip ahead, so an enemy does not clip a corner and stop.</summary>
    private bool PathIsClear(World world, Vector3 heading)
    {
        var side = new Vector3(-heading.Z, 0, heading.X) * (0.55f * Def.Size);
        foreach (var distance in new[] { 0.9f, 1.8f, 2.8f })
        {
            foreach (var offset in new[] { Vector3.Zero, side, -side })
            {
                var point = Position + heading * distance + offset;
                point.Y = world.GroundHeight(point.X, point.Z) + 0.6f;
                if (world.IsBlocked(point))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void Launch()
    {
        Face(LeapTarget);
        var air = LeapAirTicks * Tuning.TickSeconds;
        Velocity = Flat(LeapTarget - Position) / air;
        Velocity.Y = Tuning.Gravity * air / 2f;
        Action = EnemyAction.Leap;
        ActionTicks = LeapAirTicks * 2;
    }

    private void Face(Vector3 aim)
    {
        var flat = Flat(aim - Position);
        if (flat != Vector3.Zero)
        {
            Facing = MathF.Atan2(-flat.X, -flat.Z);
        }
    }

    private static Vector3 Flat(Vector3 value) => value with { Y = 0 };

    /// <summary>Moves along each axis alone, so a blocked enemy slides along a wall instead of stopping.</summary>
    private Vector3 Slide(World world)
    {
        var next = Position;
        var step = Flat(Velocity) * Tuning.TickSeconds;
        var alongX = next + new Vector3(step.X, 0, 0);
        if (Free(world, alongX))
        {
            next = alongX;
        }
        else
        {
            Velocity.X = 0;
        }

        var alongZ = next + new Vector3(0, 0, step.Z);
        if (Free(world, alongZ))
        {
            next = alongZ;
        }
        else
        {
            Velocity.Z = 0;
        }

        return next;
    }

    private bool Free(World world, Vector3 spot)
    {
        var reach = 0.45f * Def.Size;
        var height = MathF.Max(Position.Y, world.GroundHeight(spot.X, spot.Z)) + 0.6f;
        var direction = Flat(spot - Position);
        var ahead = direction == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(direction) * reach;
        return !world.IsBlocked((spot + ahead) with { Y = height });
    }

    private void Fall(World world, Random random, IEnemyEvents events)
    {
        var next = Slide(world);
        Velocity.Y -= Tuning.Gravity * Tuning.TickSeconds;
        next.Y = Position.Y + Velocity.Y * Tuning.TickSeconds;
        var ground = world.GroundHeight(next.X, next.Z);
        var landed = next.Y <= ground;
        if (landed)
        {
            next.Y = ground;
            Velocity.Y = 0;
        }

        Position = next;
        if (!landed)
        {
            return;
        }

        if (Def.Kind != "slime")
        {
            return;
        }

        if (Action == EnemyAction.Leap)
        {
            Action = EnemyAction.Splat;
            ActionTicks = SplatTicks;
            events.Splash(this);
        }
        else if (Action == EnemyAction.Hop)
        {
            ToIdle(random);
        }
    }
}
