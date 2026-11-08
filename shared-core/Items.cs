using System.Reflection;
using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>
/// An item type, loaded from world/items.json. Kind is crystal, material, food or equipment.
/// Crystals come first in the file, so a crystal's index is its rarity.
/// </summary>
/// <param name="Crystal">The crystal rarity this item stands for, or -1.</param>
/// <param name="Heal">Health that eating it restores; only food has it.</param>
/// <param name="Weapon">For a weapon: sword, dagger, club, bow or staff.</param>
/// <param name="Power">For a weapon: damage compared with the wooden sword, which is 1.</param>
/// <param name="Defense">For armour: the defense it gives before rarity and level.</param>
/// <param name="Health">Extra maximum health it gives before rarity and level.</param>
/// <param name="Shop">The role of the shopkeeper who sells it, such as alchemist, or empty.</param>
public sealed record ItemDef(
    string Id, string Name, string Kind, int Tier, string Icon, string Description, int Price = 0, string Slot = "",
    int Crystal = -1, int Heal = 0, string Weapon = "", float Power = 0f, int Defense = 0, int Health = 0, string Shop = "",
    string Skill = "")
{
    private static readonly Lazy<IReadOnlyList<ItemDef>> Loaded = new(Load);
    private static readonly Lazy<Dictionary<string, ushort>> Indices = new(() =>
        All.Select((def, index) => (def.Id, (ushort)index)).ToDictionary(p => p.Id, p => p.Item2));

    public static IReadOnlyList<ItemDef> All => Loaded.Value;

    public bool IsCrystal => Crystal >= 0;
    public bool IsFood => Kind == "food";

    /// <summary>A potion heals at once like food, but shares a short cooldown with every other potion.</summary>
    public bool IsPotion => Kind == "potion";

    /// <summary>A book that teaches the skill it names when read.</summary>
    public bool IsSkillBook => Kind == "skillbook";

    /// <summary>Every item a shop sells, in file order; a buy command names one by its place here.</summary>
    public static IReadOnlyList<ushort> Wares => LoadedWares.Value;

    private static readonly Lazy<IReadOnlyList<ushort>> LoadedWares = new(() =>
        All.Select((def, index) => (def, (ushort)index)).Where(p => p.def.Shop != "").Select(p => p.Item2).ToList());
    public bool IsEquipment => Kind == "equipment";

    /// <summary>Materials and food can be sold to the appraiser; equipment and crystals cannot.</summary>
    public bool Sellable => Kind is "material" or "food";

    public static ushort Index(string id) =>
        Indices.Value.TryGetValue(id, out var index) ? index : throw new KeyNotFoundException($"No item '{id}' in items.json");

    public static ushort CrystalIndex(CrystalRarity rarity) => (ushort)rarity;

    private static IReadOnlyList<ItemDef> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("items")!;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var items = JsonSerializer.Deserialize<ItemDef[]>(stream, options)!;
        for (var i = 0; i < Crystals.Tiers; i++)
        {
            if (items[i].Crystal != i)
            {
                throw new InvalidDataException("items.json must list the four crystals first, by rarity.");
            }
        }

        return items;
    }
}

/// <summary>One line of an enemy's loot table, rolled on its own.</summary>
/// <param name="Chance">From 0 to 1.</param>
public sealed record LootEntry(string Item, double Chance, int Min = 1, int Max = 1);

/// <summary>
/// The command byte doubles as "eat item": a command at or above <see cref="EatBase"/> eats the item
/// whose index is the command minus the base, so no packet layout changes.
/// </summary>
public static class ItemCommands
{
    public const int EatBase = 64;

    public static Command Eat(ushort item) => (Command)(EatBase + item);

    public static bool TryEat(Command command, out ushort item)
    {
        item = (ushort)Math.Max(0, (int)command - EatBase);
        return (int)command >= EatBase && (int)command < GearCommands.ToggleBase && item < ItemDef.All.Count;
    }
}

/// <summary>
/// Buying from a shop: commands from <see cref="BuyBase"/> up buy one of the item at that place in
/// <see cref="ItemDef.Wares"/>, if the player stands at the right shopkeeper and has the gold.
/// </summary>
public static class ShopCommands
{
    public const int BuyBase = 8;
    public const int MaxWares = 24;

    public static Command Buy(ushort item) => (Command)(BuyBase + ItemDef.Wares.ToList().IndexOf(item));

    public static bool TryBuy(Command command, out ushort item)
    {
        var place = (int)command - BuyBase;
        item = place >= 0 && place < Math.Min(MaxWares, ItemDef.Wares.Count) ? ItemDef.Wares[place] : (ushort)0;
        return place >= 0 && place < Math.Min(MaxWares, ItemDef.Wares.Count);
    }
}

/// <summary>
/// Gear commands share the command byte with the rest: a command from <see cref="ToggleBase"/> puts
/// on or takes off the piece at that place in the gear list; one from <see cref="DiscardBase"/>
/// throws the piece away.
/// </summary>
public static class GearCommands
{
    public const int ToggleBase = 128;
    public const int DiscardBase = ToggleBase + GameServer.GearSlots;

    public static Command Toggle(int place) => (Command)(ToggleBase + place);

    public static Command Discard(int place) => (Command)(DiscardBase + place);

    public static bool TryToggle(Command command, out int place)
    {
        place = (int)command - ToggleBase;
        return place >= 0 && place < GameServer.GearSlots;
    }

    public static bool TryDiscard(Command command, out int place)
    {
        place = (int)command - DiscardBase;
        return place >= 0 && place < GameServer.GearSlots;
    }
}
