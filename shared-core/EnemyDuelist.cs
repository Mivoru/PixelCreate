using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// A duelist fights like a trained swordsman. It circles just outside reach, steps in to strike,
/// and when the player swings at it from close by it often dashes aside, beyond harm for the
/// moment of the dash, exactly as a player's own dash works. After its own blow it sometimes
/// springs back out of reach rather than trading hits.
/// </summary>
public sealed partial class Enemy
{
    /// <summary>How long a dash lasts, how fast it goes, and the wait before the next.</summary>
    public const int DodgeTicks = 10;
    public const float DodgeSpeed = 13f;
    public const int DodgeCooldownTicks = 120;

    /// <summary>How close a swinging player must be for a dodge, and how often the duelist sees it coming.</summary>
    private const float DodgeReach = 4f;
    private const int DodgeChancePercent = 70;

    /// <summary>The ring a duelist circles on while it waits for an opening.</summary>
    private const float DuelRing = 3.2f;

    private int _dodgeCooldown;

    /// <summary>In the middle of a dash: nothing can hurt it.</summary>
    public bool Dodging => Action == EnemyAction.Dodge;

    /// <summary>Counts down the dash cooldown, and dashes aside from a player who swings at it.</summary>
    private bool DodgeIfAttacked(Random random, IReadOnlyList<Target> targets)
    {
        if (_dodgeCooldown > 0)
        {
            _dodgeCooldown--;
        }

        if (Role != "duelist" || _dodgeCooldown > 0 || Action is not (EnemyAction.Idle or EnemyAction.Walk or EnemyAction.Strike)
            || FindTarget(targets) is not { Attacking: true } attacker)
        {
            return false;
        }

        var toward = Flat(attacker.Position - Position);
        if (toward.Length() > DodgeReach)
        {
            return false;
        }

        // Seen or not, it cannot try again at once: a player who keeps swinging gets through.
        _dodgeCooldown = DodgeCooldownTicks / 3;
        if (random.Next(100) >= DodgeChancePercent)
        {
            return false;
        }

        var away = toward == Vector3.Zero ? new Vector3(1, 0, 0) : -Vector3.Normalize(toward);
        var side = Vector3.Cross(away, Vector3.UnitY) * (random.Next(2) == 0 ? 1f : -1f);
        Dash(away * 0.45f + side);
        return true;
    }

    /// <summary>Dashes along a direction at speed for a moment.</summary>
    private void Dash(Vector3 direction)
    {
        var flat = Flat(direction);
        flat = flat == Vector3.Zero ? new Vector3(1, 0, 0) : Vector3.Normalize(flat);
        Velocity.X = flat.X * DodgeSpeed;
        Velocity.Z = flat.Z * DodgeSpeed;
        Action = EnemyAction.Dodge;
        ActionTicks = DodgeTicks;
        _dodgeCooldown = DodgeCooldownTicks;
    }

    private void StepDodge()
    {
        if (ActionTicks > 0)
        {
            return;
        }

        Velocity.X = 0;
        Velocity.Z = 0;
        Action = EnemyAction.Idle;
        ActionTicks = 4;
    }

    /// <summary>After its own blow, a duelist sometimes springs back out of reach.</summary>
    private bool SpringBack(Random random, Target target)
    {
        if (Role != "duelist" || _dodgeCooldown > 0 || random.Next(100) >= 45)
        {
            return false;
        }

        Dash(Position - target.Position);
        return true;
    }

    /// <summary>Whether a short-reach blow is off cooldown.</summary>
    private bool ReadyToStrike()
    {
        for (var i = 0; i < (Def.Abilities?.Length ?? 0); i++)
        {
            if (_cooldowns[i] == 0 && Def.Abilities![i].Range < 3f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Waiting for an opening: circle on a ring just outside reach, swapping direction now and then.</summary>
    private void Circle(World world, Random random, Target target, float distance)
    {
        var toward = Flat(target.Position - Position);
        if (random.Next(50) == 0)
        {
            _orbitSide = -_orbitSide;
        }

        var tangent = Vector3.Cross(toward, Vector3.UnitY) * _orbitSide;
        var correction = (distance - DuelRing) * Vector3.Normalize(toward == Vector3.Zero ? Vector3.UnitZ : toward);
        Walk(world, Vector3.Normalize(tangent == Vector3.Zero ? Vector3.UnitX : tangent) + correction * 0.6f, 0.75f);
    }
}
