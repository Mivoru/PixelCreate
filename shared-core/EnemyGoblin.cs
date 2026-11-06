using System.Numerics;

namespace PixelCreate.Core;

/// <summary>One thing an enemy can do. Effects: melee, dashStrike, slam, spin, arrow, volley, powershot, fireball, firewall.</summary>
public sealed record Ability(
    string Effect, float Range, int WindupTicks, int RecoverTicks, int CooldownTicks, short Damage,
    float Radius = 0f, float MinRange = 0f, string Telegraph = "none");

/// <summary>Goblins walk and fight with abilities, unlike slimes, which hop and leap.</summary>
public sealed partial class Enemy
{
    private const int LungeTicks = 12;
    private const float LungeSpeed = 15f;
    private readonly int[] _cooldowns = new int[4];

    /// <summary>The ability being used, while the action is Cast, Leap or Strike.</summary>
    public byte AbilityIndex;

    public Ability? CurrentAbility => Def.Abilities is { } list && AbilityIndex < list.Length ? list[AbilityIndex] : null;

    private void StepGoblin(World world, Random random, IReadOnlyList<Target> targets, IEnemyEvents events)
    {
        for (var i = 0; i < _cooldowns.Length; i++)
        {
            if (_cooldowns[i] > 0) _cooldowns[i]--;
        }

        switch (Action)
        {
            case EnemyAction.Idle:
            case EnemyAction.Walk:
                if (ActionTicks <= 0)
                {
                    DecideGoblin(world, random, targets, events);
                }

                break;
            case EnemyAction.Cast:
                Velocity.X = 0;
                Velocity.Z = 0;
                if (ActionTicks <= 0)
                {
                    Release(events, targets);
                }

                break;
            case EnemyAction.Leap:
                if (ActionTicks <= 0)
                {
                    events.Fire(this, CurrentAbility!, null);
                    Action = EnemyAction.Strike;
                    ActionTicks = CurrentAbility!.RecoverTicks;
                    Velocity.X = 0;
                    Velocity.Z = 0;
                }

                break;
            case EnemyAction.Strike:
                Velocity.X = 0;
                Velocity.Z = 0;
                if (ActionTicks <= 0)
                {
                    Action = EnemyAction.Idle;
                    ActionTicks = random.Next(4, 14);
                }

                break;
        }

        Fall(world, random, events);
    }

    private void DecideGoblin(World world, Random random, IReadOnlyList<Target> targets, IEnemyEvents events)
    {
        var homeDistance = Flat(Home.Center - Position).Length();
        if (Returning)
        {
            if (homeDistance < Home.Radius * 0.6f)
            {
                Returning = false;
                Action = EnemyAction.Idle;
                ActionTicks = 30;
                return;
            }

            Walk(world, Home.Center - Position, 1.6f);
            return;
        }

        if (TargetId >= 0 && (FindTarget(targets) is null || homeDistance > Home.Radius + LeashExtra * 1.5f))
        {
            TargetId = -1;
            Returning = homeDistance > Home.Radius;
            Action = EnemyAction.Idle;
            ActionTicks = 10;
            return;
        }

        if (TargetId < 0)
        {
            Notice(targets, events);
        }

        if (FindTarget(targets) is not { } target)
        {
            Patrol(world, random, homeDistance);
            return;
        }

        var distance = Flat(target.Position - Position).Length();
        Face(target.Position);
        for (var i = (Def.Abilities?.Length ?? 0) - 1; i >= 0; i--)
        {
            var ability = Def.Abilities![i];
            if (_cooldowns[i] == 0 && distance <= ability.Range && distance >= ability.MinRange)
            {
                Cast((byte)i, ability, target, world, random);
                return;
            }
        }

        // Keep to the preferred distance: archers back away, fighters close in.
        var toward = target.Position - Position;
        var gap = distance - Def.PreferredRange;
        if (MathF.Abs(gap) < 1f)
        {
            var side = Vector3.Cross(Flat(toward), Vector3.UnitY) * (Id % 2 == 0 ? 1f : -1f);
            Walk(world, side, 0.7f);
        }
        else
        {
            Walk(world, gap > 0 ? toward : -toward, 1f);
        }
    }

    private void Patrol(World world, Random random, float homeDistance)
    {
        if (homeDistance > Home.Radius)
        {
            Walk(world, Home.Center - Position, 0.7f);
        }
        else if (random.Next(3) == 0)
        {
            var angle = (float)(random.NextDouble() * Math.Tau);
            Walk(world, new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)), 0.5f);
            ActionTicks = random.Next(40, 90);
        }
        else
        {
            Action = EnemyAction.Idle;
            Velocity.X = 0;
            Velocity.Z = 0;
            ActionTicks = random.Next(50, 160);
        }
    }

    private void Walk(World world, Vector3 direction, float speedFactor)
    {
        var flat = Flat(direction);
        if (flat == Vector3.Zero)
        {
            return;
        }

        var heading = Steer(world, Vector3.Normalize(flat));
        Facing = MathF.Atan2(-heading.X, -heading.Z);
        Action = EnemyAction.Walk;
        ActionTicks = 14;
        Velocity.X = heading.X * Def.Speed * speedFactor;
        Velocity.Z = heading.Z * Def.Speed * speedFactor;
    }

    private void Cast(byte index, Ability ability, Target target, World world, Random random)
    {
        AbilityIndex = index;
        Action = EnemyAction.Cast;
        ActionTicks = ability.WindupTicks;
        _cooldowns[index] = ability.CooldownTicks + random.Next(30);
        LeapTarget = ability.Effect == "spin" ? Position : target.Position;
        LeapTarget.Y = world.GroundHeight(LeapTarget.X, LeapTarget.Z);
    }

    private void Release(IEnemyEvents events, IReadOnlyList<Target> targets)
    {
        var ability = CurrentAbility!;
        if (ability.Effect == "dashStrike")
        {
            var flat = Flat(LeapTarget - Position);
            if (flat != Vector3.Zero)
            {
                var direction = Vector3.Normalize(flat);
                Velocity.X = direction.X * LungeSpeed;
                Velocity.Z = direction.Z * LungeSpeed;
            }

            Action = EnemyAction.Leap;
            ActionTicks = LungeTicks;
            return;
        }

        events.Fire(this, ability, FindTarget(targets));
        Action = EnemyAction.Strike;
        ActionTicks = ability.RecoverTicks;
    }
}
