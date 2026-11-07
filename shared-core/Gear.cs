using System.Reflection;
using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>How rare a piece of gear is. It scales the base stats and the number and strength of its affixes.</summary>
public enum Rarity : byte
{
    Common = 0,
    Rare = 1,
    Epic = 2,
    Legendary = 3,
}

/// <summary>
/// An extra property gear can roll, loaded from world/affixes.json. Group says what may carry it:
/// "weapon" any weapon, "dagger" or "club" only that kind of weapon, "armor" any armour. Rings take both.
/// Tiers hold the range rolled at each affix strength, weakest first.
/// </summary>
public sealed record AffixDef(string Id, string Name, string Group, int[][] Tiers)
{
    private static readonly Lazy<IReadOnlyList<AffixDef>> Loaded = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("affixes")!;
        return JsonSerializer.Deserialize<AffixDef[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    });

    public static IReadOnlyList<AffixDef> All => Loaded.Value;

    public static byte Index(string id)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (All[i].Id == id)
            {
                return (byte)i;
            }
        }

        throw new KeyNotFoundException($"No affix '{id}' in affixes.json");
    }

    /// <summary>The line shown on a tooltip, such as "+12% damage".</summary>
    public string Describe(int value) => "+" + Name.Replace("{0}", value.ToString());

    /// <summary>True when a piece of this base type may roll this affix.</summary>
    public bool Fits(ItemDef item)
    {
        var weapon = item.Slot == "weapon";
        var jewel = item.Slot == "ring";
        return Group switch
        {
            "weapon" => weapon || jewel,
            "armor" => !weapon,
            _ => weapon && item.Weapon == Group,
        };
    }
}

/// <summary>One rolled affix on a piece: which affix and how strong.</summary>
public readonly record struct AffixRoll(byte Affix, short Value)
{
    public AffixDef Def => AffixDef.All[Affix];
}

/// <summary>
/// One piece of gear: its base type from items.json, how rare it is, the level of the enemy that
/// dropped it, and the affixes it rolled. The server creates pieces and owns them.
/// </summary>
public sealed record GearPiece(ushort Item, Rarity Rarity, byte Level, AffixRoll[] Affixes)
{
    public ItemDef Def => ItemDef.All[Item];

    /// <summary>How much a rarity lifts the base stats.</summary>
    public static float RarityScale(Rarity rarity) => rarity switch
    {
        Rarity.Rare => 1.15f,
        Rarity.Epic => 1.3f,
        Rarity.Legendary => 1.5f,
        _ => 1f,
    };

    /// <summary>The base stats grow a little with the level of the enemy that dropped the piece.</summary>
    public float Scale => RarityScale(Rarity) * (1f + (Level - 1) * 0.05f);

    /// <summary>Weapon power after rarity and level: 1 is the wooden sword.</summary>
    public float Power => Def.Power * Scale;

    public int Defense => (int)MathF.Round(Def.Defense * Scale);

    public int Health => (int)MathF.Round(Def.Health * Scale);

    /// <summary>
    /// Rolls a new piece. Rarity decides how many affixes it gets and how strong they may be;
    /// no affix appears twice on one piece.
    /// </summary>
    public static GearPiece Roll(ushort item, Rarity rarity, byte level, Random random)
    {
        var def = ItemDef.All[item];
        var (fewest, most) = rarity switch
        {
            Rarity.Rare => (1, 2),
            Rarity.Epic => (2, 3),
            Rarity.Legendary => (3, 3),
            _ => (0, 1),
        };
        var (weakest, strongest) = rarity switch
        {
            Rarity.Rare => (0, 1),
            Rarity.Epic => (1, 2),
            Rarity.Legendary => (2, 3),
            _ => (0, 0),
        };
        var pool = Enumerable.Range(0, AffixDef.All.Count).Where(i => AffixDef.All[i].Fits(def)).ToList();
        var count = Math.Min(random.Next(fewest, most + 1), pool.Count);
        var affixes = new AffixRoll[count];
        for (var i = 0; i < count; i++)
        {
            var pick = random.Next(pool.Count);
            var affix = AffixDef.All[pool[pick]];
            pool.RemoveAt(pick);
            var range = affix.Tiers[random.Next(weakest, strongest + 1)];
            affixes[i] = new AffixRoll((byte)AffixDef.Index(affix.Id), (short)random.Next(range[0], range[1] + 1));
        }

        return new GearPiece(item, rarity, level, affixes);
    }

    /// <summary>Picks a rarity by weights, common first.</summary>
    public static Rarity RollRarity(int[] weights, Random random)
    {
        var total = weights.Sum();
        var roll = random.Next(total);
        for (var i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0)
            {
                return (Rarity)i;
            }
        }

        return Rarity.Common;
    }
}

/// <summary>
/// What a player's worn gear adds up to. Percentages are whole numbers: 12 means 12 %.
/// With no weapon worn a player fights bare-handed, at half the wooden sword's power.
/// </summary>
public sealed class GearStats
{
    public float Power = BareHands;

    /// <summary>The power of a fist, against the wooden sword's 1.</summary>
    public const float BareHands = 0.5f;
    public string Weapon = "sword";
    public int Damage;
    public int DamagePct;
    public int CritChance;
    public int CritDamage;
    public int ArmorPen;
    public int Bleed;
    public int Lifesteal;
    public int GoblinBane;
    public int BeastBane;
    public int Backstab;
    public int Crushing;
    public int Defense;
    public int DefensePct;
    public int MaxHealth;
    public int Thorns;
    public int Regen;
    public int SecondWind;

    /// <summary>Critical hits do this much more damage before affixes, in percent.</summary>
    public const int BaseCritDamage = 50;

    /// <summary>Defense K: a defense of K halves the damage taken. More defense helps less and less.</summary>
    public const float DefenseHalving = 50f;

    public static GearStats Of(IEnumerable<GearPiece> worn)
    {
        var stats = new GearStats();
        foreach (var piece in worn)
        {
            if (piece.Def.Slot == "weapon")
            {
                stats.Power = piece.Power;
                stats.Weapon = piece.Def.Weapon;
            }

            stats.Defense += piece.Defense;
            stats.MaxHealth += piece.Health;
            foreach (var affix in piece.Affixes)
            {
                stats.Add(affix.Def.Id, affix.Value);
            }
        }

        stats.Defense = (int)MathF.Round(stats.Defense * (1f + stats.DefensePct / 100f));
        return stats;
    }

    private void Add(string id, int value)
    {
        switch (id)
        {
            case "damage": Damage += value; break;
            case "damage_pct": DamagePct += value; break;
            case "crit_chance": CritChance += value; break;
            case "crit_damage": CritDamage += value; break;
            case "armor_pen": ArmorPen += value; break;
            case "bleed": Bleed += value; break;
            case "lifesteal": Lifesteal += value; break;
            case "goblin_bane": GoblinBane += value; break;
            case "beast_bane": BeastBane += value; break;
            case "backstab": Backstab += value; break;
            case "crushing": Crushing += value; break;
            case "defense": Defense += value; break;
            case "defense_pct": DefensePct += value; break;
            case "max_health": MaxHealth += value; break;
            case "thorns": Thorns += value; break;
            case "regen": Regen += value; break;
            case "second_wind": SecondWind += value; break;
        }
    }

    /// <summary>The share of incoming damage that gets through this much defense.</summary>
    public static float DamageTaken(float defense) => DefenseHalving / (DefenseHalving + MathF.Max(0f, defense));
}
