using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// The rare orcs' own moves. A combo is three blows in a row with a short gap between them. A
/// tornado spins the axe round and round for several turns, hurting everything close at every
/// pulse while it drifts towards the player, and leaves the orc dizzy at the end.
/// </summary>
public sealed partial class Enemy
{
    /// <summary>Blows in a combo, the gap after each but the last, and the short wind-up before the next.</summary>
    public const int ComboBlows = 3;
    public const int ComboGapTicks = 8;
    public const int ComboWindupTicks = 10;

    /// <summary>How long a tornado lasts (five turns of the axe), how often it hurts, and how fast it drifts.</summary>
    public const int TornadoTicks = 160;
    public const int TornadoPulseTicks = 16;
    private const float TornadoSpeedFactor = 0.55f;

    /// <summary>Blows left in the combo under way.</summary>
    public int ComboLeft;

    /// <summary>One blow of a combo, then the gap before the next or the recovery after the last.</summary>
    private void ComboBlow(IEnemyEvents events, IReadOnlyList<Target> targets, Ability ability)
    {
        if (ComboLeft <= 0)
        {
            ComboLeft = ComboBlows;
        }

        events.Fire(this, ability, FindTarget(targets));
        ComboLeft--;
        Action = EnemyAction.Strike;
        ActionTicks = ComboLeft > 0 ? ComboGapTicks : ability.RecoverTicks;
    }

    /// <summary>At the end of a gap in a combo the next blow winds up.</summary>
    private bool NextComboBlow()
    {
        if (ComboLeft <= 0 || CurrentAbility?.Effect != "combo")
        {
            return false;
        }

        Action = EnemyAction.Cast;
        ActionTicks = ComboWindupTicks;
        return true;
    }

    /// <summary>Spinning: drift towards the player and hurt everything close at each pulse.</summary>
    private void StepTornado(IReadOnlyList<Target> targets, IEnemyEvents events)
    {
        var target = FindTarget(targets);
        if (target is { } prey)
        {
            var toward = Flat(prey.Position - Position);
            if (toward.LengthSquared() > 0.25f)
            {
                var heading = Vector3.Normalize(toward);
                Velocity.X = heading.X * Def.Speed * TornadoSpeedFactor;
                Velocity.Z = heading.Z * Def.Speed * TornadoSpeedFactor;
            }
        }

        if (ActionTicks % TornadoPulseTicks == 0)
        {
            events.Fire(this, CurrentAbility!, target);
        }

        if (ActionTicks <= 0)
        {
            Action = EnemyAction.Strike;
            ActionTicks = CurrentAbility!.RecoverTicks;
            Velocity.X = 0;
            Velocity.Z = 0;
        }
    }
}
