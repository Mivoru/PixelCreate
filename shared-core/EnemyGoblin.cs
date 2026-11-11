using System.Numerics;

namespace PixelCreate.Core;

/// <summary>One thing an enemy can do. Effects: melee, dashStrike, slam, spin, arrow, volley, powershot, fireball, firewall,
/// airSlash (a flying crescent), spearThrow (a thrown spear), earthCrack (a small V of split ground),
/// pounce (a leap onto the player), shadowRun (a zigzag under the ground), shadowClone (two short-lived copies),
/// combo (three blows in a row), airSlashWide (a flat crescent), summonSpears (three spears loosed one by one
/// from above the head), tornado (a long spin that drifts after the player), fangSlash (three slanted red
/// crescents side by side), superJump (a charged leap from far off that rolls on a miss) and runeWave (three
/// straight lanes of rune light along the ground, with gaps to stand in) and warDance (a shaman's dance that
/// hastens the goblins of its camp nearby) and axeThrow (a hand axe thrown end over end), wispBolt (a slow light
/// that turns after the player), wispSplit (a lantern becomes three, two of them false), dive (a crow's swoop from
/// the air onto the player) and rookCall (five crows called to dive one after another).</summary>
public sealed record Ability(
    string Effect, float Range, int WindupTicks, int RecoverTicks, int CooldownTicks, short Damage,
    float Radius = 0f, float MinRange = 0f, string Telegraph = "none");

/// <summary>Goblins walk and fight with abilities, unlike slimes, which hop and leap.</summary>
public sealed partial class Enemy
{
    private const int LungeTicks = 12;
    private const float LungeSpeed = 15f;
    private readonly int[] _cooldowns = new int[4];

    /// <summary>The ability being used, while the action is Cast, Leap or Strike.</summary>
    public byte AbilityIndex;

    public Ability? CurrentAbility => Def.Abilities is { } list && AbilityIndex < list.Length ? list[AbilityIndex] : null;

    private void StepGoblin(World world, Random random, IReadOnlyList<Target> targets, IEnemyEvents events)
    {
        // A hastened goblin's abilities come round twice as fast.
        for (var i = 0; i < _cooldowns.Length; i++)
        {
            _cooldowns[i] = Math.Max(0, _cooldowns[i] - (HasteTicks > 0 ? 2 : 1));
        }

        Rammed = false;
        if (RoutTicks > 0) RoutTicks--;
        if (HasteTicks > 0) HasteTicks--;
        if (!Enraged && Def.EnrageAt > 0f && Alive && Health <= MaxHealth * Def.EnrageAt)
        {
            Enraged = true;
        }

        StepTurn(events, Action == EnemyAction.Strike && _lastGoblinAction != EnemyAction.Strike);
        _lastGoblinAction = Action;
        if (StepWard(world, targets) || StepScatter(world, targets, events) || StepShell(targets, world, random))
        {
            Fall(world, random, events);
            return;
        }

        if (Action != EnemyAction.Stagger && DodgeIfAttacked(random, targets))
        {
            Fall(world, random, events);
            return;
        }

        switch (Action)
        {
            case EnemyAction.Idle:
            case EnemyAction.Walk:
                if (ActionTicks <= 0)
                {
                    DecideGoblin(world, random, targets, events);
                }

                break;
            case EnemyAction.Cast:
                Velocity.X = 0;
                Velocity.Z = 0;
                TrackTarget(world, targets, CurrentAbility!.Effect is not ("spin" or "tornado"));
                if (ActionTicks <= 0)
                {
                    Release(events, targets);
                }

                break;
            case EnemyAction.ShellSpin:
                StepSpin(targets, events);
                break;
            case EnemyAction.Leap when CurrentAbility?.Effect == "charge":
                StepCharge(targets, events);
                break;
            case EnemyAction.Leap:
                if (ActionTicks <= 0)
                {
                    Landed = false;
                    events.Fire(this, CurrentAbility!, null);
                    Action = EnemyAction.Strike;
                    ActionTicks = CurrentAbility!.RecoverTicks;
                    Velocity.X = 0;
                    Velocity.Z = 0;

                    // A great leap that finds nobody ends in a forward roll instead of a strike.
                    if (CurrentAbility.Effect == "superJump" && !Landed)
                    {
                        Action = EnemyAction.Roll;
                        ActionTicks = RollTicks;
                    }
                }

                break;
            case EnemyAction.Roll:
                StepRoll();
                break;
            case EnemyAction.Stagger:
                Velocity.X = 0;
                Velocity.Z = 0;
                if (ActionTicks <= 0)
                {
                    Action = EnemyAction.Idle;
                    ActionTicks = 6;
                }

                break;
            case EnemyAction.Strike:
                Velocity.X = 0;
                Velocity.Z = 0;
                if (ActionTicks <= 0 && !NextComboBlow() && !(FindTarget(targets) is { } foe && SpringBack(random, foe)))
                {
                    Action = EnemyAction.Idle;
                    ActionTicks = random.Next(4, 14);
                }

                break;
            case EnemyAction.Shadow:
                StepShadow(targets);
                break;
            case EnemyAction.Surface:
                StepSurface(targets);
                break;
            case EnemyAction.Tornado:
                StepTornado(targets, events);
                break;
            case EnemyAction.Dodge:
                StepDodge();
                break;
            case EnemyAction.Drum:
                Velocity.X = 0;
                Velocity.Z = 0;
                if (ActionTicks <= 0)
                {
                    events.SoundAlarm(this, TargetId);
                    Action = EnemyAction.Idle;
                    ActionTicks = 6;
                }

                break;
        }

        Fall(world, random, events);
    }

    private EnemyAction _lastGoblinAction;

    /// <summary>Ticks left of a goblin's flight after it lost heart; it fights again, cornered, when they run out.</summary>
    public int RoutTicks;

    /// <summary>Set once a goblin has fled: it runs only once in its life.</summary>
    public bool Routed;

    /// <summary>Whether this wolf ran with a living leader of its camp, which has since fallen.</summary>
    private bool LeaderFell()
    {
        var led = false;
        foreach (var other in Crowd)
        {
            if (other.Alive && other.Def.Leader && other.Home == Home)
            {
                led = true;
                break;
            }
        }

        if (led)
        {
            PackLed = true;
            return false;
        }

        return PackLed;
    }

    /// <summary>Set once a pack wolf has run with a living leader; cleared when it respawns.</summary>
    public bool PackLed;

    /// <summary>A goblin below this share of its health breaks off and runs.</summary>
    public const float RoutHealth = 0.25f;

    public const int RoutDuration = Tuning.TickRate * 4;
    private const float RoutSpeed = 1.45f;

    /// <summary>
    /// A goblin near death loses heart once and runs for its camp, giving up its turn at the player.
    /// Brutes stand their ground, and so do goblins a scene brings or a trial sets.
    /// </summary>
    private bool LosesHeart(Target target, IEnemyEvents events)
    {
        if (Routed || Staged || TrialOf >= 0 || TargetId >= CreatureBase)
        {
            return false;
        }

        var goblinBreaks = Def.Kind == "goblin" && Def.Behavior != "brute" && Health <= MaxHealth * RoutHealth;
        var packBreaks = Def.Kind == "wolf" && !Def.Leader && SummonedBy < 0 && LeaderFell() && Health <= MaxHealth / 2;
        if (!goblinBreaks && !packBreaks)
        {
            return false;
        }

        Routed = true;
        RoutTicks = RoutDuration;
        EndTurn(events);
        return true;
    }

    private void DecideGoblin(World world, Random random, IReadOnlyList<Target> targets, IEnemyEvents events)
    {
        var homeDistance = Flat(Home.Center - Position).Length();
        if (Returning)
        {
            EndTurn(events);
            if (homeDistance < Home.Radius * 0.6f)
            {
                Returning = false;
                Action = EnemyAction.Idle;
                ActionTicks = 30;
                return;
            }

            Walk(world, Home.Center - Position, 1.6f);
            return;
        }

        if (TargetId >= 0 && (FindTarget(targets) is null || homeDistance > Home.Radius + ChaseLeash))
        {
            EndTurn(events);
            TargetId = -1;
            Returning = homeDistance > Home.Radius;
            Action = EnemyAction.Idle;
            ActionTicks = 10;
            return;
        }

        // A hunter still looks out for players: one who comes close matters more than the prey.
        if (TargetId < 0 || TargetId >= CreatureBase)
        {
            Notice(targets, events);
        }

        if (FindTarget(targets) is not { } target)
        {
            Patrol(world, random, homeDistance);
            return;
        }

        if (RunsToDrum(world, events))
        {
            return;
        }

        if (LosesHeart(target, events) || RoutTicks > 0)
        {
            // Away from the foe and back towards the camp, at a run.
            var away = Flat(Position - target.Position);
            var home = Flat(Home.Center - Position);
            var way = (away == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(away)) + (home == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(home) * 0.6f);
            Walk(world, way == Vector3.Zero ? away : way, RoutSpeed);
            return;
        }

        var distance = Flat(target.Position - Position).Length();
        Face(target.Position);
        for (var i = (Def.Abilities?.Length ?? 0) - 1; i >= 0; i--)
        {
            var ability = Def.Abilities![i];
            if (_cooldowns[i] != 0 || distance > ability.Range || distance < ability.MinRange)
            {
                continue;
            }

            // A shaman dances only with goblins of its camp near enough to be stirred by it.
            if (ability.Effect == "warDance" && !AlliesNear(DanceRadius))
            {
                continue;
            }

            // A lantern splits only when no false light of its own still burns.
            if (ability.Effect is "wispSplit" or "rookCall" && HasSummons())
            {
                continue;
            }

            // A close blow needs a turn; while others have them, this fighter waits its chance.
            if (IsClose(ability) && Def.Kind != "human" && !TakeTurn(events, target))
            {
                continue;
            }

            Cast((byte)i, ability, target, world, random);
            return;
        }

        Manoeuvre(world, random, target, distance);
    }

    private void Patrol(World world, Random random, float homeDistance)
    {
        // A guardian stands on its plinth like the statue it looks like until someone comes close.
        if (Def.Statue)
        {
            if (Flat(Post - Position).Length() > StatueSlack)
            {
                Walk(world, Post - Position, 0.7f);
                return;
            }

            Action = EnemyAction.Idle;
            Velocity.X = 0;
            Velocity.Z = 0;
            Facing = StatueFacing;
            ActionTicks = 30;
            return;
        }

        if (homeDistance > Home.Radius)
        {
            Walk(world, Home.Center - Position, 0.7f);
        }
        else if (random.Next(3) == 0)
        {
            var angle = (float)(random.NextDouble() * Math.Tau);
            Walk(world, new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)), 0.5f);
            ActionTicks = random.Next(40, 90);
        }
        else
        {
            Action = EnemyAction.Idle;
            Velocity.X = 0;
            Velocity.Z = 0;
            ActionTicks = random.Next(50, 160);
        }
    }

    /// <summary>How far a statue may stand from its spot before it walks back onto it, in metres.</summary>
    private const float StatueSlack = 0.8f;

    /// <summary>Which way a statue faces while it stands guard: the way it faced when it rose.</summary>
    public float StatueFacing;

    /// <summary>Where a statue stands guard: the spot it rose on.</summary>
    public Vector3 Post;

    private void Walk(World world, Vector3 direction, float speedFactor)
    {
        var flat = Flat(direction);
        if (flat == Vector3.Zero)
        {
            return;
        }

        // A fighter keeps clear of its allies as it goes, so a group spreads round the player.
        if (TargetId >= 0 && !IsAnimal)
        {
            var spread = Vector3.Normalize(flat) + AllySpacing();
            flat = spread.LengthSquared() > 0.01f ? spread : flat;
        }

        var heading = Steer(world, Vector3.Normalize(flat));
        if (IsAnimal)
        {
            // An animal turns its body; it cannot snap round. Steering that flickers between two
            // ways round an obstacle from tick to tick then averages into one smooth curve.
            heading = TurnToward(heading, AnimalTurnRate * Tuning.TickSeconds);
        }

        Facing = MathF.Atan2(-heading.X, -heading.Z);
        Action = EnemyAction.Walk;
        ActionTicks = 14;
        var speed = Def.Speed * speedFactor * (HasteTicks > 0 ? HasteSpeed : 1f);
        Velocity.X = heading.X * speed;
        Velocity.Z = heading.Z * speed;
    }

    /// <summary>How fast an animal can swing round while it walks or runs, in radians a second.</summary>
    private const float AnimalTurnRate = 4f;

    /// <summary>The heading after turning from the current facing towards a wanted one by at most a step.</summary>
    private Vector3 TurnToward(Vector3 wanted, float step)
    {
        var current = MathF.Atan2(-wanted.X, -wanted.Z);
        var difference = MathF.IEEERemainder(current - Facing, MathF.Tau);
        var angle = Facing + Math.Clamp(difference, -step, step);
        return new Vector3(-MathF.Sin(angle), 0, -MathF.Cos(angle));
    }

    private void Cast(byte index, Ability ability, Target target, World world, Random random)
    {
        AbilityIndex = index;
        Action = EnemyAction.Cast;
        ActionTicks = Quicker(ability.WindupTicks, (Enraged ? Def.EnrageWindup : 1f) * (HasteTicks > 0 ? HasteWindup : 1f));
        _cooldowns[index] = ability.CooldownTicks + random.Next(30);
        LeapTarget = ability.Effect is "spin" or "shadowClone" or "tornado" ? Position : target.Position;
        ComboLeft = 0;
        LeapTarget.Y = world.GroundHeight(LeapTarget.X, LeapTarget.Z);
    }

    private void Release(IEnemyEvents events, IReadOnlyList<Target> targets)
    {
        var ability = CurrentAbility!;
        if (ability.Effect == "pounce")
        {
            PounceAt(LeapTarget);
            return;
        }

        if (ability.Effect == "superJump")
        {
            PounceAt(LeapTarget, SuperJumpAirTicks);
            return;
        }

        if (ability.Effect == "combo")
        {
            ComboBlow(events, targets, ability);
            return;
        }

        if (ability.Effect == "tornado")
        {
            Action = EnemyAction.Tornado;
            ActionTicks = TornadoTicks;
            return;
        }

        if (ability.Effect == "shadowRun")
        {
            ShadowSlides = 0;
            Submerge(targets);
            return;
        }

        if (ability.Effect == "shellSpin")
        {
            StartSpin(targets);
            return;
        }

        if (ability.Effect == "charge")
        {
            StartCharge();
            return;
        }

        if (ability.Effect == "dive")
        {
            Dive();
            return;
        }

        if (ability.Effect == "dashStrike")
        {
            var flat = Flat(LeapTarget - Position);
            if (flat != Vector3.Zero)
            {
                var direction = Vector3.Normalize(flat);
                Velocity.X = direction.X * LungeSpeed;
                Velocity.Z = direction.Z * LungeSpeed;
            }

            Action = EnemyAction.Leap;
            ActionTicks = LungeTicks;
            return;
        }

        events.Fire(this, ability, FindTarget(targets));
        Action = EnemyAction.Strike;
        ActionTicks = Quicker(ability.RecoverTicks, Enraged ? Def.EnrageRecover : 1f);
    }

    /// <summary>
    /// A fighter of a camp with a war drum that has just noticed a player runs to the drum instead of at the
    /// player, if nobody else is already on the way, and beats it once there. Returns true while it is busy.
    /// </summary>
    private bool RunsToDrum(World world, IEnemyEvents events)
    {
        if (Home.Drum is not { } drum)
        {
            return false;
        }

        if (!GoingToDrum)
        {
            if (DrumTried || Flat(drum - Position).Length() > DrumReach || !events.ClaimDrum(this))
            {
                return false;
            }

            DrumTried = true;
            GoingToDrum = true;
            _drumRunTicks = 0;
            EndTurn(events);
        }

        // A drummer that cannot get there in time gives up and fights; someone else may try.
        var way = Flat(drum - Position);
        if (way.Length() > DrumStand)
        {
            if (++_drumRunTicks > DrumRunLimit)
            {
                GoingToDrum = false;
                return false;
            }

            Walk(world, way, DrumRunSpeed);
            return true;
        }

        GoingToDrum = false;
        Facing = MathF.Atan2(-way.X, -way.Z);
        Action = EnemyAction.Drum;
        ActionTicks = DrumTicks;
        return true;
    }

    /// <summary>On its way to the war drum, or not.</summary>
    public bool GoingToDrum;

    /// <summary>Set once this fighter has gone for the drum: if it is stopped, someone else must try.</summary>
    public bool DrumTried;

    /// <summary>How long the drum must be beaten before the camp hears it.</summary>
    public const int DrumTicks = Tuning.TickRate * 3;

    /// <summary>Only a fighter this close to the drum goes for it, in metres.</summary>
    private const float DrumReach = 45f;

    /// <summary>How close a drummer stands to its drum.</summary>
    private const float DrumStand = 2.4f;

    /// <summary>Decisions spent running to the drum; each walk step lasts several ticks.</summary>
    private int _drumRunTicks;

    /// <summary>How many walk decisions a drummer gets to reach the drum before it gives up.</summary>
    private const int DrumRunLimit = 40;
    private const float DrumRunSpeed = 1.4f;

    /// <summary>A number of ticks scaled down, but never under the floor every blow keeps for a fair warning.</summary>
    private static int Quicker(int ticks, float scale) =>
        scale >= 1f ? ticks : Math.Max(Math.Min(ticks, MinWindupTicks), (int)MathF.Round(ticks * scale));

    /// <summary>No blow winds up quicker than this, enraged or hastened.</summary>
    public const int MinWindupTicks = 16;

    /// <summary>Whether a living fighter of this camp and kind other than this one stands within reach.</summary>
    private bool AlliesNear(float reach)
    {
        foreach (var other in Crowd)
        {
            if (other != this && other.Alive && other.Home == Home && other.Def.Kind == Def.Kind && Vector3.Distance(other.Position, Position) < reach)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Ticks left of the haste a shaman's dance gave: faster on its feet, quicker to wind up, abilities back sooner.</summary>
    public int HasteTicks;

    /// <summary>Set once, when the enemy falls under its enrage share of health; cleared when it respawns.</summary>
    public bool Enraged;

    public const float DanceRadius = 9f;
    public const int HasteDuration = Tuning.TickRate * 5;
    private const float HasteSpeed = 1.35f;
    private const float HasteWindup = 0.8f;
}
