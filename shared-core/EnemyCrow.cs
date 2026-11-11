using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// Carrion crows: a flock that circles over the player out of reach, then dives one or two at a time
/// (the close-blow turns) and pecks on the ground for a moment after each dive, which is when a blade or an
/// arrow can find it. A crow that is hit scatters the whole flock for a moment before it comes back.
/// The server keeps crows on the ground; the client draws them high while they are aloft.
/// </summary>
public sealed partial class Enemy
{
    /// <summary>A crow in the air, out of reach of blows: everything but the dive, the peck after it and a stagger.</summary>
    public bool Aloft => Def.Kind == "crow" && Alive && Action is not (EnemyAction.Leap or EnemyAction.Strike or EnemyAction.Stagger);

    /// <summary>Beyond harm for the moment: sunk in shadow, dashing aside, or a crow aloft. Blows pass through.</summary>
    public bool Untouchable => InShadow || Dodging || Aloft || Action is EnemyAction.Shell or EnemyAction.ShellSpin;

    /// <summary>Ticks left of a crow's flight away from the player after its flock was struck.</summary>
    public int ScatterTicks;

    public const int ScatterDuration = Tuning.TickRate * 2;
    private const float ScatterSpeed = 1.7f;

    /// <summary>How far from the player a circling flock flies, in metres.</summary>
    public const float FlockRing = 5.5f;

    /// <summary>How far ahead round the ring a circling crow aims, in radians.</summary>
    private const float FlockLead = 0.6f;

    /// <summary>How long a dive takes from the air to the ground.</summary>
    public const int DiveTicks = 22;

    /// <summary>Every crow of this flock scatters from the player, the one struck as well, except one already diving.</summary>
    private void ScatterFlock()
    {
        foreach (var other in Crowd)
        {
            if (other.Alive && other.Def.Kind == "crow" && other.Home == Home && other.Action != EnemyAction.Leap)
            {
                other.ScatterTicks = ScatterDuration;
            }
        }

        if (Crowd.Count == 0 || !Crowd.Contains(this))
        {
            ScatterTicks = ScatterDuration;
        }
    }

    /// <summary>A scattered crow flies off away from the player until its scare is spent. Returns true while that lasts.</summary>
    private bool StepScatter(World world, IReadOnlyList<Target> targets, IEnemyEvents events)
    {
        if (ScatterTicks <= 0 || Action == EnemyAction.Leap)
        {
            return false;
        }

        if (ScatterTicks == ScatterDuration)
        {
            EndTurn(events);
        }

        ScatterTicks--;
        var away = FindTarget(targets) is { } target ? Flat(Position - target.Position) : Flat(Position - Home.Center);
        if (away == Vector3.Zero)
        {
            away = new Vector3(-MathF.Sin(Facing), 0, -MathF.Cos(Facing));
        }

        Walk(world, away, ScatterSpeed);
        return true;
    }

    /// <summary>A circling crow flies round the player on a ring, a little ahead of where it is.</summary>
    private void Orbit(World world, Target target)
    {
        var from = Flat(Position - target.Position);
        if (from == Vector3.Zero)
        {
            from = Vector3.UnitX;
        }

        var angle = MathF.Atan2(from.X, from.Z) + FlockLead * _orbitSide;
        var spot = target.Position + new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * FlockRing;
        Walk(world, spot - Position, 1f);
    }

    /// <summary>Starts a dive at once from the air, along a line towards where the player stands.</summary>
    private void Dive()
    {
        var flat = Flat(LeapTarget - Position);
        Face(LeapTarget);
        Velocity.X = flat.X / (DiveTicks * Tuning.TickSeconds);
        Velocity.Z = flat.Z / (DiveTicks * Tuning.TickSeconds);
        Action = EnemyAction.Leap;
        ActionTicks = DiveTicks;
    }

    /// <summary>
    /// Makes a summoned crow wind up an ability at once against a player, after a delay on top of the wind-up,
    /// so a called flock comes in one after another.
    /// </summary>
    public void CastAt(byte index, Target target, int delay)
    {
        if (Def.Abilities is not { } abilities || index >= abilities.Length)
        {
            return;
        }

        AbilityIndex = index;
        Action = EnemyAction.Cast;
        ActionTicks = abilities[index].WindupTicks + delay;
        _cooldowns[index] = abilities[index].CooldownTicks + delay;
        TargetId = target.Id;
        LeapTarget = target.Position;
    }

    /// <summary>Whether a light or creature this enemy called still lives: it calls again only when they are gone.</summary>
    private bool HasSummons()
    {
        foreach (var other in Crowd)
        {
            if (other.SummonedBy == Id && other.Alive)
            {
                return true;
            }
        }

        return false;
    }
}
