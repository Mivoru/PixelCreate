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

        // The hotbar's weapon slot follows the worn weapon through every swap.
        Assert.Same(inventory.Equipped[EquipSlot.Weapon], inventory.Hotbar[0]);
        Assert.Equal(daggers.Item, inventory.Hotbar[0]!.Gear!.Item);
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
}
