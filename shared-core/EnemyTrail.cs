using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// Where an enemy stood a few ticks back. A player sees enemies a little late (the snapshot travels, then the
/// client eases towards it), so a blow aimed at what is on screen is also tested against where the enemy stood
/// then. Without this a goblin circling the player kept stepping out of light blows that looked like hits.
/// </summary>
public sealed partial class Enemy
{
    /// <summary>How many ticks back a player's blow may find an enemy: about what the player sees late.</summary>
    public const int SeenLateTicks = 6;

    /// <summary>Farther than this from where it is now, the old position was a jump, not a move.</summary>
    private const float MaxSeenDrift = 2.5f;

    private readonly Vector3[] _trail = new Vector3[SeenLateTicks + 1];
    private int _trailAt;
    private int _trailCount;

    /// <summary>Keeps this tick's position; the server calls it once a tick after the enemy has moved.</summary>
    public void Remember()
    {
        _trail[_trailAt] = Position;
        _trailAt = (_trailAt + 1) % _trail.Length;
        _trailCount = Math.Min(_trailCount + 1, _trail.Length);
    }

    /// <summary>Where the enemy stood up to <see cref="SeenLateTicks"/> ticks ago, or where it is when it has no past yet.</summary>
    public Vector3 SeenAt(int ticks)
    {
        ticks = Math.Clamp(ticks, 0, _trailCount - 1);
        if (_trailCount == 0)
        {
            return Position;
        }

        var index = ((_trailAt - 1 - ticks) % _trail.Length + _trail.Length) % _trail.Length;

        // A jump (a respawn, a teleport, a test placing it) is not a move the player could have seen.
        return Vector3.Distance(_trail[index], Position) > MaxSeenDrift ? Position : _trail[index];
    }

    /// <summary>Forgets the past, as after a respawn or a jump that is not a move.</summary>
    public void ForgetTrail() => _trailCount = 0;
}
