using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// A small group that walks a route through the wild instead of keeping to one camp. Its members'
/// home is the point they are heading for, so the ordinary camp rules do the rest: they walk there,
/// wander a little round it, chase a player they notice and give up when led too far from it. Once
/// the whole group stands at a point and nobody is fighting, it rests a while, then moves on to the
/// next point and back along the route. The fallen come back at the start of the route.
/// </summary>
public sealed class Patrol
{
    /// <summary>How close the group must gather round a point before it counts as there.</summary>
    public const float ArriveRadius = 4f;

    /// <summary>The camp radius round each point, while the group walks and rests.</summary>
    public const float PointRadius = 2.5f;

    private readonly EnemySpawn _spawn;
    private readonly IReadOnlyList<Enemy> _members;
    private readonly Vector3[] _points;
    private int _index;
    private int _direction = 1;
    private int _rest;

    public Patrol(EnemySpawn spawn, IReadOnlyList<Enemy> members)
    {
        _spawn = spawn;
        _members = members;
        _points = new[] { spawn.Center }.Concat(spawn.Route!).ToArray();
        Aim();
    }

    /// <summary>The point the group is walking to or resting at.</summary>
    public Vector3 Goal => _points[_index];

    public void Step(Random random)
    {
        var living = _members.Where(m => m.Alive).ToList();
        foreach (var dead in _members.Where(m => !m.Alive))
        {
            dead.Home = _spawn;
        }

        if (living.Count == 0 || living.Any(m => m.TargetId >= 0 || m.Returning || m.Hunting))
        {
            return;
        }

        if (living.Any(m => Vector3.Distance(m.Position with { Y = 0 }, Goal with { Y = 0 }) > ArriveRadius))
        {
            Aim();
            return;
        }

        if (_rest <= 0)
        {
            _rest = 60 * 4 + random.Next(60 * 5);
        }

        if (--_rest > 0)
        {
            return;
        }

        // On to the next point; at either end of the route, turn round.
        if (_index + _direction < 0 || _index + _direction >= _points.Length)
        {
            _direction = -_direction;
        }

        _index += _direction;
        Aim();
    }

    private void Aim()
    {
        foreach (var member in _members.Where(m => m.Alive))
        {
            member.Home = _spawn with { Center = Goal, Radius = PointRadius };
        }
    }
}
