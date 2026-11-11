using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// Thornbacks charge: a boar the size of a cart lowers its head, paws the ground and runs the whole way at the
/// player, goring whoever it meets on the way and running on past. At the end it stands winded, which is the
/// moment to hit it. A charge that runs into a tree or a rock leaves it stunned for longer, so a player who
/// steps aside in front of a trunk is rewarded. Between charges it backs off to get a run-up (the "charger" role).
/// </summary>
public sealed partial class Enemy
{
    /// <summary>How fast a charge runs, in metres a second.</summary>
    public const float ChargeSpeed = 15f;

    /// <summary>How far past the spot it aimed at a charge carries on.</summary>
    private const float ChargeOvershoot = 2.5f;

    /// <summary>How long a thornback that rammed something solid stands stunned.</summary>
    public const int RamStunTicks = Tuning.TickRate * 2;

    /// <summary>How far off a charger waits for its run-up while it has no turn to come in close.</summary>
    public const float ChargeRing = 7.5f;

    private bool _chargeHit;
    private int _chargeStall;

    /// <summary>Set for one tick when a charge ends against something solid; the client shows the crash.</summary>
    public bool Rammed;

    private void StartCharge()
    {
        var flat = Flat(LeapTarget - Position);
        var direction = flat == Vector3.Zero ? new Vector3(-MathF.Sin(Facing), 0, -MathF.Cos(Facing)) : Vector3.Normalize(flat);
        Face(Position + direction);
        var ticks = (int)((flat.Length() + ChargeOvershoot) / ChargeSpeed * Tuning.TickRate);
        Velocity.X = direction.X * ChargeSpeed;
        Velocity.Z = direction.Z * ChargeSpeed;
        Action = EnemyAction.Leap;
        ActionTicks = Math.Clamp(ticks, 8, Tuning.TickRate);
        _chargeHit = false;
        _chargeStall = 0;
    }

    /// <summary>
    /// One tick of a charge: it gores the player once if it runs into them, keeps its speed, and stops stunned
    /// if something solid stops it. Returns true while the charge goes on.
    /// </summary>
    private bool StepCharge(IReadOnlyList<Target> targets, IEnemyEvents events)
    {
        var ability = CurrentAbility!;
        if (!_chargeHit && FindTarget(targets) is { } target && Flat(target.Position - Position).Length() < ability.Radius + Def.Size * 0.4f)
        {
            _chargeHit = true;
            events.Fire(this, ability, target);
        }

        // Slide stops the velocity along an axis that runs into something solid.
        var speed = Flat(Velocity).Length();
        _chargeStall = speed < ChargeSpeed * 0.4f ? _chargeStall + 1 : 0;
        if (_chargeStall >= 2)
        {
            Velocity.X = 0;
            Velocity.Z = 0;
            Rammed = true;
            Action = EnemyAction.Stagger;
            ActionTicks = RamStunTicks;
            return false;
        }

        if (ActionTicks > 0)
        {
            var heading = new Vector3(-MathF.Sin(Facing), 0, -MathF.Cos(Facing));
            Velocity.X = heading.X * ChargeSpeed;
            Velocity.Z = heading.Z * ChargeSpeed;
            return true;
        }

        Velocity.X = 0;
        Velocity.Z = 0;
        Action = EnemyAction.Strike;
        ActionTicks = Quicker(ability.RecoverTicks, Enraged ? Def.EnrageRecover : 1f);
        return false;
    }

    /// <summary>
    /// A charger with no turn, or whose gore is not ready, backs off to its run-up ring and paces there, so its
    /// charge (which needs no turn) has room. With a turn and the gore ready it closes in.
    /// </summary>
    private void Pace(World world, Random random, Target target, float distance)
    {
        var toward = Flat(target.Position - Position);
        var goreReady = Def.Abilities is { Length: > 0 } && _cooldowns[0] == 0;
        if (HasTurn && goreReady)
        {
            Close(world, toward, distance);
            return;
        }

        var gap = distance - ChargeRing;
        if (MathF.Abs(gap) < 1.2f)
        {
            if (random.Next(40) == 0)
            {
                _orbitSide = -_orbitSide;
            }

            Walk(world, Vector3.Cross(toward, Vector3.UnitY) * _orbitSide, 0.5f);
            return;
        }

        Walk(world, gap > 0 ? toward : -toward, gap < 0 ? 0.9f : 0.8f);
    }
}
