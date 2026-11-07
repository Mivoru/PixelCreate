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
public sealed record Item(string Id, string Name, string Description, string Icon, EquipSlot Slot, int Tier = 0, string Kind = "equipment",
    int Price = 0, int Heal = 0, int Index = -1)
{
    public bool IsFood => Kind == "food";

    public static Item From(ItemDef def, int index) => new(def.Id, def.Name, def.Description, def.Icon, SlotOf(def.Slot), def.Tier, def.Kind,
        def.Price, def.Heal, index);

    private static EquipSlot SlotOf(string slot) => slot switch
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
/// holds; this class only decides where each one sits: bag, hotbar or an equipment slot. Equipping
/// is a look for now: the server still fights with the wooden sword.
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

    public int Count(Item item) => item.Index < 0 ? 1 : Counts.GetValueOrDefault(item.Index);

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

    private bool Holds(Item item) => Held == item || Bag.Contains(item) || Hotbar.Contains(item) || Equipped.ContainsValue(item);

    private void Forget(Item item)
    {
        for (var i = 0; i < Bag.Length; i++)
        {
            Bag[i] = Bag[i] == item ? null : Bag[i];
        }

        for (var i = 0; i < Hotbar.Length; i++)
        {
            Hotbar[i] = Hotbar[i] == item ? null : Hotbar[i];
        }

        Held = Held == item ? null : Held;
        foreach (var slot in Equipped.Keys.ToList())
        {
            Equipped[slot] = Equipped[slot] == item ? null : Equipped[slot];
        }
    }

    /// <summary>The total the appraiser would pay for everything sellable.</summary>
    public int SaleValue() => Counts.Where(p => ItemDef.All[p.Key].Sellable).Sum(p => p.Value * ItemDef.All[p.Key].Price);
}

/// <summary>A place an item can sit in the interface.</summary>
public readonly record struct SlotRef(string Area, int Index, EquipSlot Equip = EquipSlot.None);
