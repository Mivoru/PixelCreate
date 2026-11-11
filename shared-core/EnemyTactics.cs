using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// How fighters move round a player when they are not striking. Each enemy keeps clear of its
/// allies, so a group spreads out instead of piling up. Only a few may come in for a close blow at
/// once; they take turns from the server, and the rest circle and feint until a turn is free.
/// The role in enemies.json shapes the rest: a brute presses close, a flanker and a pack wolf work
/// round to the player's back, a skirmisher strikes and falls back, ranged fighters keep their
/// distance and shift sideways, and support stays behind its own front line.
/// </summary>
public sealed partial class Enemy
{
    /// <summary>Blows, and the longest time, one turn at a player lasts before the fighter makes way for another.</summary>
    public const int BlowsPerTurn = 2;
    public const int MaxTurnTicks = 60 * 5;

    /// <summary>How close two allies may come before they push apart, as a share of their size, and how hard.</summary>
    private const float PersonalSpace = 1.9f;
    private const float SpacingWeight = 1.4f;

    /// <summary>How long a skirmisher falls back after a blow.</summary>
    public const int RetreatTicks = 70;

    /// <summary>Every enemy in the zone, so each can keep its distance from the others. The server sets it.</summary>
    public IReadOnlyList<Enemy> Crowd { get; set; } = Array.Empty<Enemy>();

    /// <summary>Whether this fighter holds a turn to come in close at its target.</summary>
    public bool HasTurn;
    private int _turnTicks;
    private int _turnBlows;
    private int _retreat;
    private float _orbitSide = 1f;

    /// <summary>The fighting role: brute, flanker, skirmisher, ranged, support, pack or duelist.</summary>
    public string Role => Def.Behavior ?? (Def.PreferredRange >= 6f ? "ranged" : "brute");

    /// <summary>A blow that needs the fighter next to the player, and so a turn. Shots and summons do not.</summary>
    public static bool IsClose(Ability ability) => ability.Effect switch
    {
        "pounce" or "dashStrike" or "shadowRun" or "tornado" or "combo" or "superJump" or "dive" => true,
        "arrow" or "volley" or "powershot" or "fireball" or "firewall" or "airSlash" or "airSlashWide"
            or "spearThrow" or "summonSpears" or "shadowClone" or "earthCrack" or "fangSlash" or "rookCall" => false,
        _ => ability.Range <= 4.5f,
    };

    /// <summary>A push away from every ally that stands too close, stronger the closer it is.</summary>
    private Vector3 AllySpacing()
    {
        var push = Vector3.Zero;
        foreach (var other in Crowd)
        {
            if (other == this || !other.Alive || other.IsAnimal || other.InShadow)
            {
                continue;
            }

            var away = Flat(Position - other.Position);
            var room = PersonalSpace * (Def.Size + other.Def.Size) * 0.5f;
            var distance = away.Length();
            if (distance >= room)
            {
                continue;
            }

            // Two on the same spot split along their ids, so they never stay stuck together.
            var direction = distance > 0.01f ? away / distance : new Vector3(Id % 2 == 0 ? 1f : -1f, 0, 0);
            push += direction * (1f - distance / room);
        }

        return push * SpacingWeight;
    }

    /// <summary>Asks for a turn at the target, or keeps the one it has.</summary>
    private bool TakeTurn(IEnemyEvents events, Target target)
    {
        if (HasTurn)
        {
            return true;
        }

        if (_retreat > 0 || !events.TryTakeTurn(this, target.Id))
        {
            return false;
        }

        HasTurn = true;
        _turnTicks = 0;
        _turnBlows = 0;
        return true;
    }

    /// <summary>Gives the turn back so another fighter can come in.</summary>
    public void EndTurn(IEnemyEvents events)
    {
        if (HasTurn)
        {
            HasTurn = false;
            events.EndTurn(this);
        }
    }

    /// <summary>Counts time and blows on a turn and ends it when it has run its course.</summary>
    private void StepTurn(IEnemyEvents events, bool struck)
    {
        if (_retreat > 0)
        {
            _retreat--;
        }

        if (!HasTurn)
        {
            return;
        }

        _turnTicks++;
        if (struck)
        {
            _turnBlows++;
        }

        // A turn ends only once the blow is over, so a fighter still recovering from it never
        // overlaps with the next one coming in.
        if (Action is EnemyAction.Cast or EnemyAction.Strike or EnemyAction.Leap or EnemyAction.Tornado
            or EnemyAction.Shadow or EnemyAction.Surface)
        {
            return;
        }

        if (_turnBlows > 0 && Role == "skirmisher")
        {
            // Strike and fall back: the turn goes to someone else while it gets clear.
            _retreat = RetreatTicks;
            EndTurn(events);
            return;
        }

        if (_turnBlows >= BlowsPerTurn || _turnTicks > MaxTurnTicks)
        {
            EndTurn(events);
        }
    }

    /// <summary>Where to go when not striking: in close on a turn, else round the player by role.</summary>
    private void Manoeuvre(World world, Random random, Target target, float distance)
    {
        var toward = Flat(target.Position - Position);
        var role = Role;
        if (_retreat > 0)
        {
            Walk(world, -toward + AllySpacing() * toward.Length(), 1.1f);
            return;
        }

        if (role == "flock")
        {
            Orbit(world, target);
            return;
        }

        if (role is "ranged" or "support")
        {
            KeepRange(world, random, target, distance, role == "support");
            return;
        }

        if (role == "duelist")
        {
            // A blow ready: step in to strike. Otherwise wait on the ring for the next opening.
            if (distance > DuelRing + 2.5f || ReadyToStrike())
            {
                Walk(world, toward, 1f);
            }
            else
            {
                Circle(world, random, target, distance);
            }

            return;
        }

        if (HasTurn)
        {
            Close(world, toward, distance);
            return;
        }

        // Waiting for a turn: hold a ring round the player, at the back for flankers and wolves.
        var ring = role == "brute" ? 3.8f : 5f;
        var angle = MathF.Atan2(-toward.X, -toward.Z) + MathF.PI;
        if (role is "flanker" or "pack")
        {
            angle = Behind(target) + (Id % 3 - 1) * 0.9f;
        }
        else
        {
            angle += _orbitSide * 0.35f;
        }

        var spot = target.Position + new Vector3(-MathF.Sin(angle), 0, -MathF.Cos(angle)) * ring;
        var path = Flat(spot - Position);
        if (random.Next(40) == 0)
        {
            _orbitSide = -_orbitSide;
        }

        // Now and then a feint: a quick step in that stops short.
        if (path.Length() < 1.2f && random.Next(12) == 0)
        {
            Walk(world, toward, 1.3f);
            ActionTicks = 8;
            return;
        }

        Walk(world, path.Length() < 0.6f ? Vector3.Cross(toward, Vector3.UnitY) * _orbitSide : path, path.Length() < 2f ? 0.6f : 1f);
    }

    /// <summary>On a turn: close to striking distance, sidestepping a little once there.</summary>
    private void Close(World world, Vector3 toward, float distance)
    {
        var gap = distance - Def.PreferredRange;
        if (MathF.Abs(gap) < 1f)
        {
            var side = Vector3.Cross(toward, Vector3.UnitY) * (Id % 2 == 0 ? 1f : -1f);
            Walk(world, side, 0.7f);
        }
        else
        {
            Walk(world, gap > 0 ? toward : -toward, 1f);
        }
    }

    /// <summary>
    /// Ranged fighters hold their distance and shift sideways now and then so they are not an easy
    /// mark; support also keeps a friendly fighter between itself and the player.
    /// </summary>
    private void KeepRange(World world, Random random, Target target, float distance, bool behindFriends)
    {
        var toward = Flat(target.Position - Position);
        if (behindFriends && FrontLine(target) is { } guard)
        {
            var away = Flat(guard.Position - target.Position);
            var shelter = guard.Position + (away == Vector3.Zero ? -toward : Vector3.Normalize(away)) * 3.5f;
            var path = Flat(shelter - Position);
            if (path.Length() > 1f)
            {
                Walk(world, path, 1f);
                return;
            }
        }

        var gap = distance - Def.PreferredRange;
        if (MathF.Abs(gap) < 1.5f)
        {
            if (random.Next(30) == 0)
            {
                _orbitSide = -_orbitSide;
            }

            Walk(world, Vector3.Cross(toward, Vector3.UnitY) * _orbitSide, 0.6f);
            return;
        }

        Walk(world, gap > 0 ? toward : -toward, gap < 0 ? 1.1f : 1f);
    }

    /// <summary>The nearest fighting ally that stands closer to the player than this one does.</summary>
    private Enemy? FrontLine(Target target)
    {
        Enemy? best = null;
        var mine = Flat(target.Position - Position).Length();
        var bestDistance = float.MaxValue;
        foreach (var other in Crowd)
        {
            if (other == this || !other.Alive || other.TargetId != target.Id || other.Role is "ranged" or "support")
            {
                continue;
            }

            var theirs = Flat(target.Position - other.Position).Length();
            var apart = Flat(other.Position - Position).Length();
            if (theirs < mine && apart < bestDistance)
            {
                best = other;
                bestDistance = apart;
            }
        }

        return best;
    }

    /// <summary>
    /// The angle, seen from the player, of the side away from the fighters already at them, or from
    /// where the player is heading when nobody is: the side they are not watching.
    /// </summary>
    private float Behind(Target target)
    {
        var front = Vector3.Zero;
        foreach (var other in Crowd)
        {
            if (other != this && other.Alive && other.HasTurn && other.TargetId == target.Id)
            {
                front += Flat(other.Position - target.Position);
            }
        }

        if (front == Vector3.Zero)
        {
            front = Flat(target.Velocity);
        }

        if (front == Vector3.Zero)
        {
            front = Flat(Position - target.Position);
            return MathF.Atan2(-front.X, -front.Z);
        }

        return MathF.Atan2(-front.X, -front.Z) + MathF.PI;
    }
}
