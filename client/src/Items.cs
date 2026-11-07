using System;
using System.Collections.Generic;
using System.Linq;
using PixelCreate.Core;

namespace PixelCreate;

public enum EquipSlot
{
    None,
    Head,
    Chest,
    Legs,
    Boots,
    Weapon,
    Ring,
}

/// <param name="Index">The item's index in items.json, or -1 for the starting sword the server does not track.</param>
/// <param name="Gear">For a piece of gear: the piece itself, with its rarity and affixes.</param>
/// <param name="Place">For a piece of gear: its place in the server's gear list, or -1.</param>
public sealed record Item(string Id, string Name, string Description, string Icon, EquipSlot Slot, int Tier = 0, string Kind = "equipment",
    int Price = 0, int Heal = 0, int Index = -1, GearPiece? Gear = null, int Place = -1)
{
    public bool IsFood => Kind == "food";

    public bool IsGear => Gear is not null;

    /// <summary>A piece of gear as the interface shows it; its tier is its rarity.</summary>
    public static Item FromGear(GearPiece piece, int place)
    {
        var def = piece.Def;
        return new Item(def.Id, def.Name, def.Description, def.Icon, SlotOf(def.Slot), (int)piece.Rarity, def.Kind, def.Price, 0,
            piece.Item, piece, place);
    }

    public static Item From(ItemDef def, int index) => new(def.Id, def.Name, def.Description, def.Icon, SlotOf(def.Slot), def.Tier, def.Kind,
        def.Price, def.Heal, index);

    public static EquipSlot SlotOf(string slot) => slot switch
    {
        "head" => EquipSlot.Head,
        "chest" => EquipSlot.Chest,
        "legs" => EquipSlot.Legs,
        "boots" => EquipSlot.Boots,
        "weapon" => EquipSlot.Weapon,
        "ring" => EquipSlot.Ring,
        _ => EquipSlot.None,
    };
}

/// <summary>
/// The player's items as the interface shows them. The server owns how many of each item the player
/// holds, every piece of gear and what is worn; this class only decides where each one sits in the
/// bag and on the hotbar. A worn piece always shows in its equipment slot; with no weapon worn the
/// player fights with the wooden sword.
/// </summary>
public sealed class Inventory
{
    public const int BagSlots = 30;
    public const int HotbarSlots = 8;

    public static readonly Item WoodenSword = new(
        "wooden_sword", "Wooden Sword", "A training blade. It still hurts.", "wooden_sword", EquipSlot.Weapon, 0);

    private static readonly Item[] Defs = ItemDef.All.Select(Item.From).ToArray();

    public Item?[] Bag { get; } = new Item?[BagSlots];
    public Item?[] Hotbar { get; } = new Item?[HotbarSlots];
    public Dictionary<EquipSlot, Item?> Equipped { get; } = new()
    {
        [EquipSlot.Head] = null,
        [EquipSlot.Chest] = null,
        [EquipSlot.Legs] = null,
        [EquipSlot.Boots] = null,
        [EquipSlot.Weapon] = WoodenSword,
        [EquipSlot.Ring] = null,
    };

    /// <summary>Counts the server reported, by item index.</summary>
    public Dictionary<int, int> Counts { get; } = new();

    public int SelectedHotbar { get; set; }

    /// <summary>The item picked up with the mouse and not yet put down.</summary>
    public Item? Held { get; set; }

    public Inventory()
    {
        Hotbar[0] = WoodenSword;
    }

    public bool HasWeapon => Equipped[EquipSlot.Weapon] is not null;

    public static Item Def(int index) => Defs[index];

    public int Count(Item item) => item.Index < 0 || item.IsGear ? 1 : Counts.GetValueOrDefault(item.Index);

    /// <summary>Gear by its place in the server's gear list.</summary>
    private readonly Dictionary<int, Item> _gear = new();

    /// <summary>
    /// Takes in gear entries from a snapshot. A new piece goes to the first free bag slot and is
    /// returned so it can be announced; a changed piece replaces the old one where it sat; a worn
    /// piece moves to its equipment slot and one taken off moves back to the bag.
    /// </summary>
    public List<Item> ApplyGear(IEnumerable<GearEntry> entries)
    {
        var found = new List<Item>();
        foreach (var entry in entries)
        {
            _gear.TryGetValue(entry.Index, out var old);
            if (entry.Piece is not { } piece)
            {
                if (old is not null)
                {
                    Forget(old);
                    _gear.Remove(entry.Index);
                }

                continue;
            }

            var item = old is not null && old.Gear == piece ? old : Item.FromGear(piece, entry.Index);
            if (old is null || !ReferenceEquals(old.Gear, piece) && !SamePiece(old.Gear!, piece))
            {
                if (old is not null)
                {
                    Forget(old);
                }
                else
                {
                    found.Add(item);
                }

                _gear[entry.Index] = item;
                Place(item);
            }
            else
            {
                item = old;
            }

            Settle(item, entry.Worn);
        }

        if (!Equipped.Values.Any(i => i?.Slot == EquipSlot.Weapon) && Held?.Slot != EquipSlot.Weapon)
        {
            Equipped[EquipSlot.Weapon] = WoodenSword;
        }

        return found;
    }

    private static bool SamePiece(GearPiece a, GearPiece b) =>
        a.Item == b.Item && a.Rarity == b.Rarity && a.Level == b.Level && a.Affixes.SequenceEqual(b.Affixes);

    /// <summary>Puts a worn piece in its equipment slot and a piece taken off back in the bag.</summary>
    private void Settle(Item item, bool worn)
    {
        var slot = item.Slot;
        if (worn && !ReferenceEquals(Equipped[slot], item))
        {
            RemoveFromView(item);
            if (Equipped[slot] is { IsGear: true } other)
            {
                Place(other);
            }

            Equipped[slot] = item;
        }
        else if (!worn && ReferenceEquals(Equipped[slot], item))
        {
            Equipped[slot] = slot == EquipSlot.Weapon ? WoodenSword : null;
            Place(item);
        }
    }

    /// <summary>Puts an item in the first free bag slot unless it already sits somewhere.</summary>
    private void Place(Item item)
    {
        if (Holds(item))
        {
            return;
        }

        var free = Array.IndexOf(Bag, null);
        if (free >= 0)
        {
            Bag[free] = item;
        }
    }

    private void RemoveFromView(Item item)
    {
        for (var i = 0; i < Bag.Length; i++)
        {
            Bag[i] = ReferenceEquals(Bag[i], item) ? null : Bag[i];
        }

        for (var i = 0; i < Hotbar.Length; i++)
        {
            Hotbar[i] = ReferenceEquals(Hotbar[i], item) ? null : Hotbar[i];
        }

        Held = ReferenceEquals(Held, item) ? null : Held;
    }

    /// <summary>
    /// Takes in counts from a snapshot. Returns the items that grew and by how much, so the interface
    /// can announce the pickups. A new item goes to the first free bag slot; one that ran out leaves
    /// every slot it sat in.
    /// </summary>
    public List<(Item Item, int Gained)> Apply(IEnumerable<BagEntry> entries)
    {
        var gained = new List<(Item, int)>();
        foreach (var entry in entries)
        {
            var before = Counts.GetValueOrDefault(entry.Item);
            Counts[entry.Item] = entry.Count;
            var item = Defs[entry.Item];
            if (entry.Count > before)
            {
                gained.Add((item, entry.Count - before));
            }

            if (entry.Count > 0 && !Holds(item))
            {
                var free = Array.IndexOf(Bag, null);
                if (free >= 0)
                {
                    Bag[free] = item;
                }
            }
            else if (entry.Count == 0)
            {
                Forget(item);
            }
        }

        return gained;
    }

    private bool Holds(Item item) => ReferenceEquals(Held, item) || Bag.Any(i => ReferenceEquals(i, item)) ||
                                     Hotbar.Any(i => ReferenceEquals(i, item)) || Equipped.Values.Any(i => ReferenceEquals(i, item));

    private void Forget(Item item)
    {
        RemoveFromView(item);
        foreach (var slot in Equipped.Keys.ToList())
        {
            Equipped[slot] = ReferenceEquals(Equipped[slot], item) ? null : Equipped[slot];
        }
    }

    /// <summary>The total the appraiser would pay for everything sellable.</summary>
    public int SaleValue() => Counts.Where(p => ItemDef.All[p.Key].Sellable).Sum(p => p.Value * ItemDef.All[p.Key].Price);
}

/// <summary>A place an item can sit in the interface.</summary>
public readonly record struct SlotRef(string Area, int Index, EquipSlot Equip = EquipSlot.None);
