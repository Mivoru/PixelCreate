using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// Animals live in herds. A passive animal spends most of its time grazing, now and then ambles to a
/// spot near the rest of its herd, and keeps a little space from its neighbours. Someone coming
/// close first makes it look up and watch; coming closer, running at it or hurting it makes it bolt,
/// and the scare spreads through the herd. A neutral animal watches but never runs: once hit it
/// fights back like a goblin, and calms down after it has chased the attacker off.
/// </summary>
public sealed partial class Enemy
{
    private const int FleeTicks = 150;
    private const float FleeLeash = 2.5f;

    /// <summary>A scared animal warns every herd mate this close.</summary>
    private const float PanicReach = 12f;

    /// <summary>A player moving faster than this looks like a hunter, and scares animals from further away.</summary>
    private const float RunningSpeed = 4.5f;
    private const float RunningScare = 1.6f;
    private const float Personal = 1.3f;

    private int _fleeTicks;
    private Vector3 _fleeFrom;
    private int _alertTicks;
    private Vector3 _alertAt;
    private Vector3 _goal;
    private int _goalTicks;
    private bool _rallied;

    public bool IsAnimal => Def.Kind == "animal";

    /// <summary>The animals of the same camp, this one included. The server fills it in.</summary>
    public IReadOnlyList<Enemy> Herd { get; set; } = Array.Empty<Enemy>();

    public bool Fleeing => _fleeTicks > 0;

    private void StepAnimal(World world, Random random, IReadOnlyList<Target> targets, IEnemyEvents events)
    {
        if (Def.Temper == "neutral" && (TaggedBy >= 0 || TargetId >= 0))
        {
            if (TargetId < 0 && !Returning)
            {
                TargetId = TaggedBy;
            }

            // Hurting one boar brings the whole sounder down on the attacker.
            if (!_rallied && TargetId >= 0)
            {
                _rallied = true;
                foreach (var mate in Herd)
                {
                    if (mate != this && mate.Alive && mate.Def.Temper == "neutral" && mate.TargetId < 0 &&
                        Flat(mate.Position - Position).Length() < PanicReach)
                    {
                        mate.TargetId = TargetId;
                    }
                }
            }

            StepGoblin(world, random, targets, events);
            if (TargetId < 0 && !Returning && Action == EnemyAction.Idle)
            {
                TaggedBy = -1;
                _rallied = false;
            }

            return;
        }

        Sense(targets);
        if (_fleeTicks > 0)
        {
            Flee(world);
        }
        else if (_alertTicks > 0)
        {
            Watch();
        }
        else
        {
            Graze(world, random);
        }

        Fall(world, random, events);
    }

    /// <summary>Looks for danger: a fresh wound, someone too close, or someone watching from further off.</summary>
    private void Sense(IReadOnlyList<Target> targets)
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
            return;
        }

        var running = Flat(near.Velocity).Length() > RunningSpeed;
        var flee = Def.AggroRange * (running ? RunningScare : 1f);
        var neutral = Def.Temper == "neutral";
        if (!neutral && (HitFlash > 0 || best < flee))
        {
            Panic(near.Position, true);
        }
        else if (_fleeTicks == 0 && best < Def.AlertRange * (running ? RunningScare : 1f))
        {
            _alertTicks = Math.Max(_alertTicks, 45);
            _alertAt = near.Position;
        }
    }

    /// <summary>Starts running from a spot, and with a warning makes nearby herd mates run too.</summary>
    private void Panic(Vector3 from, bool warn)
    {
        var fresh = _fleeTicks < FleeTicks / 2;
        _fleeTicks = FleeTicks;
        _fleeFrom = from;
        _alertTicks = 0;
        if (!warn || !fresh)
        {
            return;
        }

        foreach (var mate in Herd)
        {
            if (mate != this && mate.Alive && mate._fleeTicks == 0 && mate.Def.Temper != "neutral" &&
                Flat(mate.Position - Position).Length() < PanicReach)
            {
                mate.Panic(from, false);
            }
        }
    }

    private void Flee(World world)
    {
        _fleeTicks--;
        var away = Flat(Position - _fleeFrom);
        if (Flat(Home.Center - Position).Length() > Home.Radius * FleeLeash)
        {
            away = Flat(Home.Center - Position);
        }

        // A bolt starts at full speed and eases off as the animal tires.
        Walk(world, away + Spacing() * 2f, _fleeTicks > FleeTicks / 3 ? 1f : 0.65f);
        if (_fleeTicks == 0)
        {
            _goalTicks = 0;
            _alertTicks = 60;
            _alertAt = _fleeFrom;
        }
    }

    /// <summary>Stands still with the head up, facing whatever caught its attention.</summary>
    private void Watch()
    {
        _alertTicks--;
        Velocity.X = 0;
        Velocity.Z = 0;
        var look = Flat(_alertAt - Position);
        if (look != Vector3.Zero)
        {
            Facing = MathF.Atan2(-look.X, -look.Z);
        }

        Action = EnemyAction.Alert;
    }

    /// <summary>Grazes in place, or ambles to a new patch near the herd.</summary>
    private void Graze(World world, Random random)
    {
        _goalTicks--;
        if (_goalTicks <= 0)
        {
            PickGoal(world, random);
        }

        var toGoal = Flat(_goal - Position);
        if (Action == EnemyAction.Walk && toGoal.Length() > 0.6f)
        {
            Walk(world, Vector3.Normalize(toGoal) + Spacing(), Def.Id == "chicken" ? 0.6f : 0.35f);
            return;
        }

        Velocity.X = 0;
        Velocity.Z = 0;
        if (Action == EnemyAction.Walk)
        {
            // Arrived: put the head down and eat for a while.
            Action = EnemyAction.Graze;
            _goalTicks = random.Next(150, 420);
        }
    }

    /// <summary>
    /// Chooses what to do next. Mostly grazing; sometimes a walk to a spot near the middle of the
    /// herd, which keeps a herd together without any animal leading it. Goats prefer high ground.
    /// </summary>
    private void PickGoal(World world, Random random)
    {
        var roll = random.NextDouble();
        if (roll < 0.5)
        {
            Action = EnemyAction.Graze;
            _goalTicks = random.Next(150, 420);
            return;
        }

        if (roll < 0.62)
        {
            Action = EnemyAction.Idle;
            _goalTicks = random.Next(60, 150);
            return;
        }

        var centre = HerdCentre();
        var best = Vector3.Zero;
        var bestScore = float.MinValue;
        for (var i = 0; i < (Def.Id == "goat" ? 4 : 1); i++)
        {
            var angle = (float)(random.NextDouble() * Math.Tau);
            var reach = 1.5f + (float)random.NextDouble() * Math.Min(4f, Home.Radius * 0.5f);
            var spot = centre + new Vector3(MathF.Cos(angle) * reach, 0, MathF.Sin(angle) * reach);
            if (Flat(spot - Home.Center).Length() > Home.Radius)
            {
                spot = Home.Center + Vector3.Normalize(Flat(spot - Home.Center)) * Home.Radius * 0.8f;
            }

            var score = world.GroundHeight(spot.X, spot.Z);
            if (score > bestScore)
            {
                bestScore = score;
                best = spot;
            }
        }

        _goal = best;
        _goalTicks = random.Next(300, 500);
        Action = EnemyAction.Walk;
    }

    /// <summary>Where the rest of the herd stands, or home when it is alone.</summary>
    private Vector3 HerdCentre()
    {
        var sum = Vector3.Zero;
        var count = 0;
        foreach (var mate in Herd)
        {
            if (mate != this && mate.Alive)
            {
                sum += mate.Position;
                count++;
            }
        }

        return count == 0 ? Home.Center : Vector3.Lerp(sum / count, Home.Center, 0.25f);
    }

    /// <summary>A push away from herd mates that stand too close.</summary>
    private Vector3 Spacing()
    {
        var push = Vector3.Zero;
        foreach (var mate in Herd)
        {
            var gap = Flat(Position - mate.Position);
            var distance = gap.Length();
            if (mate != this && mate.Alive && distance is > 0.001f and < Personal)
            {
                push += gap / distance * (Personal - distance);
            }
        }

        return push;
    }
}
