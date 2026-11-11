using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// Stonebacks: turtles that carry a dome of rock. Struck, one pulls into its shell and cannot be hurt; it
/// comes out when the player has backed off and lunges to bite, which is the opening to hit it. The rare
/// Mossback Elder also spins in its shell and slides at the player, glancing off rocks and trees, and stands
/// dizzy afterwards.
/// </summary>
public sealed partial class Enemy
{
    /// <summary>The longest a stoneback hides in its shell, however close the player stays.</summary>
    public const int ShellMaxTicks = Tuning.TickRate * 6;

    /// <summary>How far the player must back off, and for how long, before a stoneback comes out.</summary>
    public const float ShellCalmDistance = 3.5f;
    public const int ShellCalmTicks = Tuning.TickRate * 3 / 4;

    public const float SpinSpeed = 9f;
    public const int SpinTicks = Tuning.TickRate * 3;
    public const int SpinDizzyTicks = Tuning.TickRate * 3 / 2;
    private const int SpinHitEvery = 30;

    private int _shellCalm;
    private Vector3 _spinVelocity;
    private int _spinHitClock;

    /// <summary>Set for one tick when a spinning shell glances off something solid; the client throws sparks.</summary>
    public bool Glanced;

    /// <summary>A stoneback struck out of its shell pulls into it.</summary>
    private void Withdraw()
    {
        if (!Def.Shells || Action is EnemyAction.Shell or EnemyAction.ShellSpin || Health <= 0)
        {
            return;
        }

        Action = EnemyAction.Shell;
        ActionTicks = ShellMaxTicks;
        _shellCalm = 0;
        Velocity.X = 0;
        Velocity.Z = 0;
    }

    /// <summary>
    /// A stoneback in its shell waits until the player has stood back for a moment (or it has waited long
    /// enough), then comes out with a lunge at them. Returns true while it hides.
    /// </summary>
    private bool StepShell(IReadOnlyList<Target> targets, World world, Random random)
    {
        if (Action != EnemyAction.Shell)
        {
            return false;
        }

        Velocity.X = 0;
        Velocity.Z = 0;
        var target = FindTarget(targets);
        var distance = target is { } t ? Flat(t.Position - Position).Length() : float.MaxValue;
        _shellCalm = distance > ShellCalmDistance ? _shellCalm + 1 : 0;
        if (_shellCalm < ShellCalmTicks && ActionTicks > 0)
        {
            return true;
        }

        // Out it comes, straight into a lunge if the player is in reach of one.
        var lunge = Def.Abilities is { } list ? Array.FindIndex(list, a => a.Effect == "dashStrike") : -1;
        if (target is { } foe && lunge >= 0 && distance < Def.Abilities![lunge].Range)
        {
            Cast((byte)lunge, Def.Abilities[lunge], foe, world, random);
            return true;
        }

        Action = EnemyAction.Idle;
        ActionTicks = 10;
        return true;
    }

    private void StartSpin(IReadOnlyList<Target> targets)
    {
        var toward = FindTarget(targets) is { } t ? Flat(t.Position - Position) : Vector3.Zero;
        var way = toward == Vector3.Zero ? new Vector3(-MathF.Sin(Facing), 0, -MathF.Cos(Facing)) : Vector3.Normalize(toward);
        _spinVelocity = way * SpinSpeed;
        Velocity.X = _spinVelocity.X;
        Velocity.Z = _spinVelocity.Z;
        Action = EnemyAction.ShellSpin;
        ActionTicks = SpinTicks;
        _spinHitClock = 0;
    }

    /// <summary>
    /// One tick of the Elder's spin: it slides on, bending a little towards the player, bounces off whatever
    /// stopped it last tick and strikes the player when it runs into them. At the end it stands dizzy.
    /// </summary>
    private void StepSpin(IReadOnlyList<Target> targets, IEnemyEvents events)
    {
        Glanced = false;

        // Slide zeroes the speed along an axis that ran into something: bounce that way back.
        if (MathF.Abs(Velocity.X) < 0.01f && MathF.Abs(_spinVelocity.X) > 0.01f)
        {
            _spinVelocity.X = -_spinVelocity.X;
            Glanced = true;
        }

        if (MathF.Abs(Velocity.Z) < 0.01f && MathF.Abs(_spinVelocity.Z) > 0.01f)
        {
            _spinVelocity.Z = -_spinVelocity.Z;
            Glanced = true;
        }

        if (FindTarget(targets) is { } foe)
        {
            var toward = Flat(foe.Position - Position);
            if (toward != Vector3.Zero)
            {
                _spinVelocity = Vector3.Normalize(_spinVelocity + Vector3.Normalize(toward) * 0.12f) * SpinSpeed;
            }

            if (++_spinHitClock >= SpinHitEvery && toward.Length() < CurrentAbility!.Radius + 0.5f)
            {
                _spinHitClock = 0;
                events.Fire(this, CurrentAbility!, foe);
            }
        }

        Velocity.X = _spinVelocity.X;
        Velocity.Z = _spinVelocity.Z;
        Facing += 0.5f;
        if (ActionTicks <= 0)
        {
            Velocity.X = 0;
            Velocity.Z = 0;
            Action = EnemyAction.Stagger;
            ActionTicks = SpinDizzyTicks;
        }
    }
}
