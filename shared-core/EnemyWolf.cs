using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// The wolves' own moves. A pounce is a real leap through the air onto the player. A shadow run
/// sinks the wolf into the ground as a shadow that cannot be hurt; it slides out to one side, rises,
/// sinks again and slides out to the other side, so it closes in on a zigzag and never in a straight
/// line. Close enough while still a shadow, it bursts out onto the player. Summoned shadow clones
/// live a short while and vanish with the wolf that called them.
/// </summary>
public sealed partial class Enemy
{
    /// <summary>How long a pounce or the leap out of the shadow is in the air.</summary>
    public const int PounceAirTicks = 30;

    /// <summary>The longest one slide under the ground lasts, and how long the wolf stands up between slides.</summary>
    public const int ShadowSlideTicks = 40;
    public const int SurfaceTicks = 24;

    /// <summary>How far one slide goes, and how far it swings off the straight line to the player, in radians.</summary>
    public const float ShadowSlideLength = 4.5f;
    public const float ShadowZigAngle = 0.75f;

    /// <summary>From this close a shadow bursts out onto the player.</summary>
    public const float ShadowStrikeRange = 3.2f;
    public const int MaxShadowSlides = 6;
    private const float ShadowSpeedFactor = 1.6f;

    /// <summary>For a shadow clone: the enemy that summoned it, or -1; and the ticks it has left.</summary>
    public int SummonedBy = -1;
    public int LifeTicks;

    /// <summary>Slides made in this shadow run, and which side the next one swings to.</summary>
    public int ShadowSlides;
    private float _zig = 1f;

    /// <summary>A shadow is under the ground and cannot be hit.</summary>
    public bool InShadow => Action == EnemyAction.Shadow;

    /// <summary>Leaps from where the enemy stands to the spot, landing after the air time.</summary>
    private void PounceAt(Vector3 spot)
    {
        Face(spot);
        var air = PounceAirTicks * Tuning.TickSeconds;
        var flat = Flat(spot - Position);
        // It lands just short of the spot, on the player rather than through them.
        if (flat.Length() > 0.8f)
        {
            flat -= Vector3.Normalize(flat) * 0.6f;
        }

        Velocity = flat / air;
        Velocity.Y = Tuning.Gravity * air / 2f;
        Action = EnemyAction.Leap;
        ActionTicks = PounceAirTicks;
    }

    /// <summary>Sinks into the ground and picks the end of the next slide, off to one side of the player.</summary>
    private void Submerge(IReadOnlyList<Target> targets)
    {
        Action = EnemyAction.Shadow;
        ActionTicks = ShadowSlideTicks;
        if (FindTarget(targets) is not { } target)
        {
            LeapTarget = Position;
            return;
        }

        var toward = Flat(target.Position - Position);
        var distance = toward.Length();
        var straight = distance > 0.01f ? toward / distance : new Vector3(-MathF.Sin(Facing), 0, -MathF.Cos(Facing));
        var turned = Vector3.Transform(straight, Matrix4x4.CreateRotationY(_zig * ShadowZigAngle));
        _zig = -_zig;
        LeapTarget = Position + turned * MathF.Min(ShadowSlideLength, MathF.Max(distance, 1.5f));
        Face(LeapTarget);
    }

    /// <summary>Under the ground: slide on, burst out at a player in reach, or rise at the end of the slide.</summary>
    private void StepShadow(IReadOnlyList<Target> targets)
    {
        var target = FindTarget(targets);
        if (target is { } prey && Flat(prey.Position - Position).Length() <= ShadowStrikeRange)
        {
            LeapTarget = prey.Position;
            PounceAt(prey.Position);
            return;
        }

        var left = Flat(LeapTarget - Position);
        if (left.Length() < 0.4f || ActionTicks <= 0)
        {
            Action = EnemyAction.Surface;
            ActionTicks = SurfaceTicks;
            Velocity.X = 0;
            Velocity.Z = 0;
            if (target is not null)
            {
                Face(target.Value.Position);
            }

            return;
        }

        var heading = Vector3.Normalize(left);
        Velocity.X = heading.X * Def.Speed * ShadowSpeedFactor;
        Velocity.Z = heading.Z * Def.Speed * ShadowSpeedFactor;
    }

    /// <summary>Risen between two slides: a moment where it can be hit, then down again, or the run ends.</summary>
    private void StepSurface(IReadOnlyList<Target> targets)
    {
        Velocity.X = 0;
        Velocity.Z = 0;
        if (ActionTicks > 0)
        {
            return;
        }

        if (ShadowSlides < MaxShadowSlides && FindTarget(targets) is not null)
        {
            ShadowSlides++;
            Submerge(targets);
            return;
        }

        Action = EnemyAction.Idle;
        ActionTicks = 10;
    }
}
