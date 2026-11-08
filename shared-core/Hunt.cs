using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// Goblins and orcs feed themselves: now and then a camp or patrol that hunts goes after the
/// nearest deer or wild boar. While the hunt lasts the hunters' home follows the prey, so the camp
/// rules let them chase it. Deer run from them; a boar turns and fights. A kill is eaten where it
/// fell, then the group goes home. A player who comes close always matters more than a meal.
/// </summary>
public sealed class Hunt
{
    /// <summary>How far from the group the hunters look for prey, and how far the chase may lead.</summary>
    public const float SearchRange = 70f;
    public const float ChaseRange = 90f;

    /// <summary>The longest a chase lasts, how long a kill is eaten, and the wait between hunts.</summary>
    public const int ChaseTicks = 60 * 40;
    public const int FeastTicks = 60 * 10;
    public const int MinRestTicks = 60 * 60;
    public const int MaxRestTicks = 60 * 150;

    /// <summary>The animals worth hunting.</summary>
    public static readonly HashSet<string> Prey = new() { "deer", "pig" };

    /// <summary>Attacks that reach a running deer.</summary>
    private static readonly HashSet<string> Ranged = new() { "arrow", "volley", "powershot", "airSlash", "airSlashWide", "spearThrow", "fireball" };

    private readonly IReadOnlyList<Enemy> _members;

    /// <summary>At most this many go out; the rest of a big camp stays home.</summary>
    public const int PartySize = 3;
    private List<Enemy> _party = new();
    private readonly EnemySpawn _home;
    private Enemy? _prey;
    private Vector3 _start;
    private int _clock;
    private bool _feasting;

    public Hunt(IReadOnlyList<Enemy> members, EnemySpawn home, Random random)
    {
        _members = members;
        _home = home;
        _clock = random.Next(MinRestTicks / 2, MaxRestTicks);
    }

    /// <summary>The animal being hunted, if a chase is on.</summary>
    public Enemy? Quarry => _feasting ? null : _prey;

    /// <summary>For a test: hunt at the next step.</summary>
    public void Now() => _clock = 0;

    public void Step(IReadOnlyList<Enemy> all, Random random)
    {
        _clock--;
        if (_prey is null)
        {
            if (_clock <= 0)
            {
                Begin(all, _members.Where(m => m.Alive).ToList(), random);
            }

            return;
        }

        var hunters = _party.Where(m => m.Alive).ToList();

        // A player turned up: the hunters drop everything for the fight.
        if (hunters.Count == 0 || hunters.Any(m => m.TargetId >= 0 && m.TargetId < Enemy.CreatureBase))
        {
            End(random);
            return;
        }

        if (_feasting)
        {
            if (_clock <= 0)
            {
                End(random);
            }

            return;
        }

        if (!_prey.Alive)
        {
            // Eat where it fell.
            _feasting = true;
            _clock = FeastTicks;
            Follow(hunters, _prey.Position, 3f, false);
            return;
        }

        if (_clock <= 0 || Vector3.Distance(_prey.Position, _start) > ChaseRange)
        {
            End(random);
            return;
        }

        Follow(hunters, _prey.Position, 8f, true);
    }

    private void Begin(IReadOnlyList<Enemy> all, List<Enemy> hunters, Random random)
    {
        _clock = random.Next(MinRestTicks, MaxRestTicks);
        if (hunters.Count == 0 || hunters.Any(m => m.TargetId >= 0 || m.Returning))
        {
            return;
        }

        // Nobody outruns a deer: only a group that can shoot or throw goes after one. Boars stand and fight.
        var centre = hunters.Aggregate(Vector3.Zero, (sum, m) => sum + m.Position) / hunters.Count;
        var reach = hunters.Any(m => m.Def.Abilities?.Any(a => Ranged.Contains(a.Effect)) == true);
        _prey = all.Where(e => e.Alive && Prey.Contains(e.Def.Id) && (reach || e.Def.Id != "deer") && Vector3.Distance(e.Position, centre) < SearchRange)
            .OrderBy(e => Vector3.Distance(e.Position, centre)).FirstOrDefault();
        if (_prey is null)
        {
            return;
        }

        _start = centre;
        _clock = ChaseTicks;
        _party = hunters.OrderBy(m => Vector3.Distance(m.Position, _prey.Position)).Take(PartySize).ToList();
        Follow(_party, _prey.Position, 8f, true);
    }

    /// <summary>Points the hunters' home at a spot and, in a chase, their attention at the prey.</summary>
    private void Follow(List<Enemy> hunters, Vector3 spot, float radius, bool chase)
    {
        foreach (var hunter in hunters)
        {
            hunter.Hunting = true;
            hunter.Home = _home with { Center = spot, Radius = radius };
            if (chase && hunter.TargetId < 0 && !hunter.Returning)
            {
                hunter.TargetId = Enemy.CreatureBase + _prey!.Id;
            }
            else if (!chase && hunter.TargetId >= Enemy.CreatureBase)
            {
                hunter.TargetId = -1;
            }
        }
    }

    private void End(Random random)
    {
        foreach (var hunter in _party)
        {
            hunter.Hunting = false;
            hunter.Home = _home;
            if (hunter.TargetId >= Enemy.CreatureBase)
            {
                hunter.TargetId = -1;
            }
        }

        _prey = null;
        _party = new List<Enemy>();
        _feasting = false;
        _clock = random.Next(MinRestTicks, MaxRestTicks);
    }
}
