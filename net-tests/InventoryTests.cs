using PixelCreate.Core;
using Xunit;

using PixelCreate;

namespace PixelCreate.NetTests;

/// <summary>The client's inventory as the server's gear entries change it: nothing may vanish from view.</summary>
public sealed class InventoryTests
{
    private static GearPiece Piece(string id) => new(ItemDef.Index(id), Rarity.Rare, 3, Array.Empty<AffixRoll>());

    /// <summary>Every piece the server lists shows exactly once: worn in its slot, or else in the bag.</summary>
    private static void AssertShown(Inventory inventory, IEnumerable<GearEntry> server)
    {
        foreach (var entry in server.Where(e => e.Piece is not null))
        {
            var inSlot = inventory.Equipped.Values.Count(i => i?.Place == entry.Index);
            var elsewhere = inventory.Bag.Count(i => i?.Place == entry.Index) + inventory.Hotbar.Count(i => i?.Place == entry.Index && i.IsGear);
            Assert.True(entry.Worn ? inSlot == 1 && inventory.Bag.All(i => i?.Place != entry.Index) : inSlot == 0 && elsewhere >= 1,
                $"place {entry.Index} ({entry.Piece!.Def.Id}, worn {entry.Worn}) shows {inSlot} times worn and {elsewhere} times in the bag");
        }
    }

    [Fact]
    public void WearingANewWeapon_PutsTheOldOneBackInTheBag()
    {
        var inventory = new Inventory();
        var daggers = Piece("twin_daggers");
        var bow = Piece("hunters_longbow");
        var state = new Dictionary<byte, GearEntry> { [0] = new(0, daggers, false), [1] = new(1, bow, false) };
        inventory.ApplyGear(state.Values);
        AssertShown(inventory, state.Values);

        // Wear the daggers, then the bow: the server takes the daggers off in the same step.
        state[0] = new(0, daggers, true);
        inventory.ApplyGear(new[] { state[0] });
        AssertShown(inventory, state.Values);
        state[0] = new(0, daggers, false);
        state[1] = new(1, bow, true);
        inventory.ApplyGear(new[] { state[1], state[0] });
        AssertShown(inventory, state.Values);

        // The same, with the entries arriving the other way round and in separate snapshots.
        state[1] = new(1, bow, false);
        state[0] = new(0, daggers, true);
        inventory.ApplyGear(new[] { state[1] });
        inventory.ApplyGear(new[] { state[0] });
        AssertShown(inventory, state.Values);

        // The rotating slice resends places that did not change.
        inventory.ApplyGear(state.Values);
        inventory.ApplyGear(state.Values);
        AssertShown(inventory, state.Values);

        // The first weapon worn went on the hotbar once, and stays a shortcut to that piece.
        Assert.Equal(daggers.Item, inventory.Hotbar[0]!.Gear!.Item);
    }

    [Fact]
    public void TheHotbar_HoldsShortcutsAndLosesNothing()
    {
        var inventory = new Inventory();
        var sword = new GearPiece(ItemDef.Index("wooden_sword"), Rarity.Common, 1, Array.Empty<AffixRoll>());
        var daggers = Piece("twin_daggers");
        var bow = Piece("hunters_longbow");
        var state = new[] { new GearEntry(0, sword, true), new GearEntry(1, daggers, false), new GearEntry(2, bow, false) };
        inventory.ApplyGear(state);

        // Lift the daggers out of the bag and put them on the third hotbar slot.
        var from = Array.FindIndex(inventory.Bag, i => i?.Gear?.Item == daggers.Item);
        inventory.Held = inventory.Bag[from];
        inventory.HeldFrom = from;
        inventory.Bag[from] = null;
        inventory.PutOnHotbar(2, inventory.Held!);
        Assert.Null(inventory.Held);
        Assert.Equal(daggers.Item, inventory.Bag[from]!.Gear!.Item);
        Assert.Equal(daggers.Item, inventory.Hotbar[2]!.Gear!.Item);

        // The server resends everything; nothing turns into the worn sword.
        inventory.ApplyGear(state);
        Assert.Equal(daggers.Item, inventory.Hotbar[2]!.Gear!.Item);
        Assert.Equal(sword.Item, inventory.Hotbar[0]!.Gear!.Item);
        AssertShown(inventory, state);

        // Wearing the daggers keeps the shortcut and marks it worn.
        var worn = new[] { new GearEntry(1, daggers, true), new GearEntry(0, sword, false), new GearEntry(2, bow, false) };
        inventory.ApplyGear(worn);
        Assert.True(inventory.IsWorn(inventory.Hotbar[2]!));
        Assert.False(inventory.IsWorn(inventory.Hotbar[0]!));
        AssertShown(inventory, worn);

        // Throwing the bow away leaves no shortcut behind.
        inventory.PutOnHotbar(4, inventory.Bag.First(i => i?.Gear?.Item == bow.Item)!);
        inventory.ApplyGear(new[] { new GearEntry(2, null, false) });
        Assert.Null(inventory.Hotbar[4]);
    }

    [Fact]
    public void SwappingArmour_KeepsEveryPiece()
    {
        var inventory = new Inventory();
        var helm = Piece("iron_helm");
        var cap = Piece("goblin_cap");
        inventory.ApplyGear(new[] { new GearEntry(0, helm, true), new GearEntry(1, cap, false) });
        inventory.ApplyGear(new[] { new GearEntry(1, cap, true), new GearEntry(0, helm, false) });
        AssertShown(inventory, new[] { new GearEntry(1, cap, true), new GearEntry(0, helm, false) });
        Assert.Equal(2, inventory.Bag.Count(i => i is not null) + inventory.Equipped.Values.Count(i => i is { IsGear: true }));
    }

    [Fact]
    public void TheWoodenSword_IsAWeaponLikeAnyOther()
    {
        var inventory = new Inventory();
        var sword = new GearPiece(ItemDef.Index("wooden_sword"), Rarity.Common, 1, Array.Empty<AffixRoll>());
        var daggers = Piece("twin_daggers");
        inventory.ApplyGear(new[] { new GearEntry(0, sword, true), new GearEntry(1, daggers, false) });
        Assert.Equal(sword.Item, inventory.Equipped[EquipSlot.Weapon]!.Gear!.Item);
        Assert.Same(inventory.Equipped[EquipSlot.Weapon], inventory.Hotbar[0]);

        // Wearing the daggers sends the sword to the bag, from where it can be worn again.
        var after = new[] { new GearEntry(1, daggers, true), new GearEntry(0, sword, false) };
        inventory.ApplyGear(after);
        AssertShown(inventory, after);
        Assert.Contains(inventory.Bag, i => i?.Gear?.Item == sword.Item);
        Assert.Equal(sword.Item, inventory.Hotbar[0]!.Gear!.Item);
    }
}
