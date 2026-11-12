using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// Mirefins: predatory fish in Mirror Lake. Under the water they are only a fin, beyond reach. They follow a
/// player along the shore and, once the player stands at the water's edge, surface (the wind-up, when they can
/// be struck) and hurl themselves out at them. After the leap the fish lies stranded on the bank, flopping, which
/// is the moment to finish it, then wriggles back into the lake. A player who keeps away from the water is safe.
/// The rare Old Gulper also spits a jet of water that throws the player back from the shore. The camp's middle
/// and radius are the lake.
/// </summary>
public sealed partial class Enemy
{
    /// <summary>How close to the lake's edge a player must stand before a fish will leap at them, in metres.</summary>
    public const float ShoreReach = 4.5f;

    /// <summary>How long a fish lies stranded on the bank after its leap.</summary>
    public const int StrandedTicks = Tuning.TickRate * 5 / 2;

    /// <summary>How far inside the lake's edge a swimming fish keeps.</summary>
    private const float SwimMargin = 1.5f;

    public bool IsFish => Def.Kind == "fish";

    /// <summary>A fish under the water: swimming or waiting inside its lake, where no blow reaches it.</summary>
    public bool Submerged => IsFish && Alive && Action is EnemyAction.Idle or EnemyAction.Walk
        && Flat(Home.Center - Position).Length() < Home.Radius - 0.5f;

    /// <summary>Whether a player stands near enough to the lake's edge for a fish to leap at them.</summary>
    private bool AtTheShore(Target target) => Flat(target.Position - Home.Center).Length() < Home.Radius + ShoreReach;

    /// <summary>
    /// A swimming fish shadows its player along the inside of the shore, or wanders the lake without one.
    /// </summary>
    private void Lurk(World world, Random random, Target? target)
    {
        var fromMiddle = target is { } prey ? Flat(prey.Position - Home.Center) : Flat(Position - Home.Center);
        Vector3 spot;
        if (target is not null && fromMiddle != Vector3.Zero)
        {
            spot = Home.Center + Vector3.Normalize(fromMiddle) * (Home.Radius - SwimMargin);
        }
        else
        {
            var angle = (float)(random.NextDouble() * Math.Tau);
            spot = Home.Center + new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)) * (float)random.NextDouble() * (Home.Radius - SwimMargin);
        }

        var path = Flat(spot - Position);
        if (path.Length() < 0.8f)
        {
            Action = EnemyAction.Idle;
            Velocity.X = 0;
            Velocity.Z = 0;
            ActionTicks = random.Next(10, 30);
            return;
        }

        Walk(world, path, target is null ? 0.5f : 1f);
        ActionTicks = target is null ? random.Next(40, 90) : 10;
    }

    /// <summary>A stranded fish flops on the bank; when its time is up it wriggles back to the water.</summary>
    private bool StepStranded(World world)
    {
        if (!IsFish || Action != EnemyAction.Stagger)
        {
            return false;
        }

        Velocity.X = 0;
        Velocity.Z = 0;
        if (ActionTicks <= 0)
        {
            Returning = true;
            Action = EnemyAction.Idle;
            ActionTicks = 2;
        }

        return true;
    }
}
