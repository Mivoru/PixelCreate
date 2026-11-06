using System;
using System.Collections.Generic;

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

public sealed record Item(string Id, string Name, string Description, string Icon, EquipSlot Slot);

/// <summary>
/// The player's items. This is a local model for the interface: the server does not know about
/// items yet, so it treats every player as holding the wooden sword.
/// </summary>
public sealed class Inventory
{
    public const int BagSlots = 20;
    public const int HotbarSlots = 8;

    public static readonly Item WoodenSword = new(
        "wooden_sword", "Wooden Sword", "A training blade. It still hurts.", "wooden_sword", EquipSlot.Weapon);

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

    public int SelectedHotbar { get; set; }

    public Inventory()
    {
        Hotbar[0] = WoodenSword;
    }

    public bool HasWeapon => Equipped[EquipSlot.Weapon] is not null;
}

/// <summary>A place an item can sit in the interface.</summary>
public readonly record struct SlotRef(string Area, int Index, EquipSlot Equip = EquipSlot.None);
