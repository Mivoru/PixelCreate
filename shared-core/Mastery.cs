using System.Reflection;
using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>One step of a weapon's mastery tree: stats or a passive it adds, or at the end of a branch a new move.</summary>
/// <param name="Unlock">The new ability or combo at the end of a branch. Until it is built it shows as coming soon.</param>
public sealed record MasteryNode(string Name, string Text, Dictionary<string, int>? Stats = null, string? Unlock = null, int Cost = 1)
{
    public string Weapon { get; init; } = "";
    public int Branch { get; init; }
    public int Tier { get; init; }

    /// <summary>The node's place in <see cref="Mastery.Nodes"/>, which commands and snapshots use.</summary>
    public int Index { get; init; }

    /// <summary>Unlocks for moves that exist in the game; others cannot be taken yet.</summary>
    public static readonly HashSet<string> BuiltUnlocks = new()
    {
        "riposte", "triple_shot", "piercing_arrow", "stunning_bash", "shockwave", "chain_lightning",
        "fan_of_knives", "meteor", "arcane_barrier", "rain_of_arrows",
        "rising_slash", "whirlwind", "ground_pound", "blade_dance", "shadow_step",
    };

    public bool Ready => Unlock is null || BuiltUnlocks.Contains(Unlock);
}

/// <summary>
/// Weapon mastery, from world/mastery.json. Kills with a kind of weapon earn that kind mastery
/// experience; each mastery level gives a point to spend in the weapon's tree. A branch is taken in
/// order, and a node's stats count only while a weapon of its kind is in hand.
/// </summary>
public static class Mastery
{
    public static readonly string[] Weapons = { "sword", "dagger", "club", "bow", "staff" };
    public const int MaxLevel = 30;

    private sealed record WeaponData(string Weapon, BranchData[] Branches);

    private sealed record BranchData(string Name, MasteryNode[] Nodes);

    private static readonly Lazy<(IReadOnlyList<MasteryNode> Nodes, Dictionary<string, string[]> Branches)> Loaded = new(Load);

    /// <summary>Every node of every tree, in file order.</summary>
    public static IReadOnlyList<MasteryNode> Nodes => Loaded.Value.Nodes;

    /// <summary>The branch names of a weapon's tree.</summary>
    public static IReadOnlyList<string> Branches(string weapon) => Loaded.Value.Branches.GetValueOrDefault(weapon, Array.Empty<string>());

    public static int WeaponIndex(string weapon) => Array.IndexOf(Weapons, weapon);

    /// <summary>Experience to go from this mastery level to the next.</summary>
    public static int XpToNext(int level) => 80 + level * level * 40;

    /// <summary>A node can be taken when it is built, its weapon has a free point for it, and the one before it in the branch is taken.</summary>
    public static bool CanTake(MasteryNode node, ulong taken, int freePoints) =>
        node.Ready && (taken & (1ul << node.Index)) == 0 && freePoints >= node.Cost
        && (node.Tier == 0 || Nodes.Any(n => n.Weapon == node.Weapon && n.Branch == node.Branch && n.Tier == node.Tier - 1 && (taken & (1ul << n.Index)) != 0));

    /// <summary>True when a move at the end of a branch is taken and a weapon of its kind is in hand.</summary>
    public static bool Has(string weaponInHand, ulong taken, string unlock) =>
        Nodes.Any(n => n.Unlock == unlock && n.Weapon == weaponInHand && (taken & (1ul << n.Index)) != 0);

    /// <summary>The moves that change movement, taken for the weapon in hand, for <see cref="PlayerState.Moves"/>.</summary>
    public static MasteryMoves MovesFor(string weaponInHand, ulong taken)
    {
        var moves = MasteryMoves.None;
        foreach (var (unlock, move) in new[]
        {
            ("rising_slash", MasteryMoves.RisingSlash), ("whirlwind", MasteryMoves.Whirlwind), ("blade_dance", MasteryMoves.BladeDance),
            ("shadow_step", MasteryMoves.ShadowStep), ("ground_pound", MasteryMoves.GroundPound), ("rain_of_arrows", MasteryMoves.RainOfArrows),
        })
        {
            if (Has(weaponInHand, taken, unlock))
            {
                moves |= move;
            }
        }

        return moves;
    }

    /// <summary>Points spent in one weapon's tree.</summary>
    public static int Spent(string weapon, ulong taken) => Nodes.Where(n => n.Weapon == weapon && (taken & (1ul << n.Index)) != 0).Sum(n => n.Cost);

    /// <summary>Adds the stats of every taken node of this weapon kind to the gear stats.</summary>
    public static void AddTo(GearStats stats, ulong taken)
    {
        foreach (var node in Nodes.Where(n => n.Weapon == stats.Weapon && (taken & (1ul << n.Index)) != 0 && n.Stats is not null))
        {
            foreach (var (stat, value) in node.Stats!)
            {
                switch (stat)
                {
                    case "damagePct": stats.DamagePct += value; break;
                    case "critChance": stats.CritChance += value; break;
                    case "critDamage": stats.CritDamage += value; break;
                    case "armorPen": stats.ArmorPen += value; break;
                    case "bleed": stats.Bleed += value; break;
                    case "lifesteal": stats.Lifesteal += value; break;
                    case "beastBane": stats.BeastBane += value; break;
                    case "backstab": stats.Backstab += value; break;
                    case "crushing": stats.Crushing += value; break;
                    default: throw new InvalidDataException($"mastery.json names an unknown stat '{stat}'.");
                }
            }
        }
    }

    private static (IReadOnlyList<MasteryNode>, Dictionary<string, string[]>) Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("mastery")!;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        using var document = JsonDocument.Parse(stream);
        var weapons = document.RootElement.GetProperty("weapons").Deserialize<WeaponData[]>(options)!;
        var nodes = new List<MasteryNode>();
        var branches = new Dictionary<string, string[]>();
        foreach (var weapon in weapons)
        {
            branches[weapon.Weapon] = weapon.Branches.Select(b => b.Name).ToArray();
            for (var b = 0; b < weapon.Branches.Length; b++)
            {
                for (var t = 0; t < weapon.Branches[b].Nodes.Length; t++)
                {
                    nodes.Add(weapon.Branches[b].Nodes[t] with { Weapon = weapon.Weapon, Branch = b, Tier = t, Index = nodes.Count });
                }
            }
        }

        if (nodes.Count > 64)
        {
            throw new InvalidDataException("mastery.json holds more than 64 nodes; snapshots carry them in one 64-bit mask.");
        }

        return (nodes, branches);
    }
}
