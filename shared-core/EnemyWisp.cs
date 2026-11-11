using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// Barrow wisps: night lights over the Stone Circle. They are out only in the dark hours, keep their
/// distance and circle (the ranged role), and loose slow bolts that turn after the player. The standing
/// stones are the answer: while the player stands by one, the wisps near it go out (<see cref="EnemyAction.Dim"/>),
/// cannot cast, drift in towards the stone and take more from every blow, and a bolt that reaches a stone fizzles.
/// </summary>
public sealed partial class Enemy
{
    /// <summary>Whether it is dark now. The server sets it on every enemy before each step.</summary>
    public bool Night;

    /// <summary>How close to a standing stone a player must stand for its light to put the wisps out, in metres.</summary>
    public const float WardReach = 1.6f;

    /// <summary>A put-out wisp drifts in this close to the stone, where a blade can reach it.</summary>
    private const float WardDrift = 1.8f;

    /// <summary>How fast a put-out wisp drifts, as a share of its speed.</summary>
    private const float WardDriftSpeed = 0.35f;

    /// <summary>How much more a put-out wisp takes from each blow.</summary>
    public const float DimTaken = 1.5f;

    /// <summary>How long a wisp takes to light again once the player has left the stone.</summary>
    public const int RelightTicks = Tuning.TickRate / 2;

    /// <summary>How far a wisp may be from the stone the player stands by and still be put out by it.</summary>
    private const float WardSway = 16f;

    /// <summary>
    /// A creature of a night camp is out only in the dark: at dawn it fades where it is (no loot, no
    /// experience), and it does not rise again before dusk. Returns true when the step is done here.
    /// </summary>
    private bool KeepsToTheNight(Random random)
    {
        if (!Home.Night || SummonedBy >= 0 || Night)
        {
            return false;
        }

        if (Alive)
        {
            Action = EnemyAction.Dead;
            Velocity = Vector3.Zero;
            TargetId = -1;
            Returning = false;
            // Staggered, so the camp does not rise all at once at dusk.
            ActionTicks = random.Next(30, 240);
            return true;
        }

        ActionTicks = Math.Max(ActionTicks, 1);
        return true;
    }

    /// <summary>The standing stone within reach of a point, if any.</summary>
    public Vector3? WardNear(Vector3 point)
    {
        if (Home.Wards is not { } wards)
        {
            return null;
        }

        foreach (var ward in wards)
        {
            if (Flat(ward - point).Length() < WardReach)
            {
                return ward;
            }
        }

        return null;
    }

    /// <summary>
    /// While the wisp's player stands by a standing stone, the wisp is put out: whatever it was casting
    /// dies away and it drifts in towards the stone. Returns true while that lasts.
    /// </summary>
    private bool StepWard(World world, IReadOnlyList<Target> targets)
    {
        if (Def.Kind != "wisp")
        {
            return false;
        }

        var target = FindTarget(targets);
        var ward = target is { } player ? WardNear(player.Position) : null;
        if (target is not { } foe || ward is not { } stone || Flat(stone - Position).Length() > WardSway)
        {
            if (Action == EnemyAction.Dim)
            {
                Action = EnemyAction.Idle;
                ActionTicks = RelightTicks;
            }

            return false;
        }

        Action = EnemyAction.Dim;
        ActionTicks = 10;
        var toward = Flat(stone - Position);
        if (toward.Length() > WardDrift)
        {
            var heading = Vector3.Normalize(toward);
            Velocity.X = heading.X * Def.Speed * WardDriftSpeed;
            Velocity.Z = heading.Z * Def.Speed * WardDriftSpeed;
        }
        else
        {
            Velocity.X = 0;
            Velocity.Z = 0;
        }

        Face(foe.Position);
        return true;
    }
}
