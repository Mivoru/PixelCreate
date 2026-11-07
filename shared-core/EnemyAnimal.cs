using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// Animals. A passive one grazes and wanders near home and runs from anyone who comes too close
/// or hurts it. A neutral one ignores players until hit, then fights back like a goblin, and
/// calms down once it has chased the attacker off.
/// </summary>
public sealed partial class Enemy
{
    private const int FleeTicks = 150;
    private const float FleeLeash = 2.5f;

    private int _fleeTicks;
    private Vector3 _fleeFrom;

    public bool IsAnimal => Def.Kind == "animal";

    private void StepAnimal(World world, Random random, IReadOnlyList<Target> targets, IEnemyEvents events)
    {
        if (Def.Temper == "neutral" && TaggedBy >= 0)
        {
            if (TargetId < 0 && !Returning)
            {
                TargetId = TaggedBy;
            }

            StepGoblin(world, random, targets, events);
            if (TargetId < 0 && !Returning && Action == EnemyAction.Idle)
            {
                TaggedBy = -1;
            }

            return;
        }

        var homeDistance = Flat(Home.Center - Position).Length();
        if (Scare(targets) is { } danger)
        {
            _fleeTicks = FleeTicks;
            _fleeFrom = danger;
        }

        if (_fleeTicks > 0)
        {
            _fleeTicks--;
            var away = Flat(Position - _fleeFrom);
            if (homeDistance > Home.Radius * FleeLeash)
            {
                away = Flat(Home.Center - Position);
            }

            Walk(world, away, 1f);
        }
        else if (ActionTicks <= 0)
        {
            Patrol(world, random, homeDistance);
            if (Action == EnemyAction.Idle)
            {
                // Animals spend most of their time standing and grazing.
                ActionTicks += random.Next(60, 200);
            }
        }

        Fall(world, random, events);
    }

    /// <summary>Where the animal should run from: a fresh wound, or a player inside its comfort range.</summary>
    private Vector3? Scare(IReadOnlyList<Target> targets)
    {
        Target? closest = null;
        var best = float.MaxValue;
        foreach (var target in targets)
        {
            var distance = Flat(target.Position - Position).Length();
            if (distance < best)
            {
                best = distance;
                closest = target;
            }
        }

        if (closest is not { } near)
        {
            return null;
        }

        return HitFlash > 0 || best < Def.AggroRange ? near.Position : null;
    }
}
