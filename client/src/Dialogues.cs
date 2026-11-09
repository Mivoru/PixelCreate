using System.Collections.Generic;
using System.Linq;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>How the people of the world look.</summary>
public static class NpcLooks
{
    private static readonly Color Skin = new("f6d8c4");

    public static readonly RigLook Examiner = new("examiner", Skin, 1f, "sword")
    {
        Mood = "neutral",
        Hair = "rhea",
        Feminine = true,
        Extras = new[] { "armor" },
        Eyes = new Color("b8552a"),
        Brows = new Color("7a2420"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("a8362e"),
            ["anime_shirt"] = new("3a3440"),
            ["anime_cloak"] = new("7a2430"),
            ["anime_trousers"] = new("3a3440"),
        },
    };

    private static readonly RigLook Receptionist = new("receptionist", Skin, 0.95f, "none")
    {
        Mood = "smile",
        Hair = "mira",
        Feminine = true,
        Skirt = true,
        Cloak = false,
        Rest = "clasped",
        Extras = new[] { "vest", "bow" },
        Eyes = new Color("3f6fa8"),
        Brows = new Color("9a5028"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("c8703a"),
            ["anime_shirt"] = new("f4f0e6"),
            ["anime_vest"] = new("2f4070"),
            ["anime_cloak"] = new("2f4070"),
            ["anime_trousers"] = new("f0dcc8"),
            ["anime_dark"] = new("f4f0e6"),
            ["anime_glove"] = Skin,
            ["anime_accent"] = new("c84848"),
        },
    };

    private static readonly RigLook Villager = new("villager", new Color("e8c0a4"), 0.97f, "none")
    {
        Mood = "neutral",
        Hair = "bram",
        Cloak = false,
        Stoop = 0.16f,
        Rest = "behind",
        Extras = new[] { "robe", "beard" },
        Eyes = new Color("6a4a30"),
        Brows = new Color("d8d8e0"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("d8d8e0"),
            ["anime_shirt"] = new("7a6a4a"),
            ["anime_cloak"] = new("6a5a3e"),
            ["anime_trousers"] = new("4a4038"),
            ["anime_dark"] = new("6a5a3e"),
            ["anime_glove"] = new("e8c0a4"),
        },
    };

    private static readonly RigLook VillagerB = new("villager_b", Skin, 0.93f, "none")
    {
        Mood = "neutral",
        Hair = "tess",
        Feminine = true,
        Skirt = true,
        Cloak = false,
        Extras = new[] { "apron" },
        Eyes = new Color("3f8f6a"),
        Brows = new Color("a88838"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("e0c060"),
            ["anime_shirt"] = new("c86a5a"),
            ["anime_cloak"] = new("7a4a58"),
            ["anime_trousers"] = new("f0dcc8"),
            ["anime_dark"] = new("c86a5a"),
            ["anime_glove"] = Skin,
            ["anime_vest"] = new("f0e8d8"),
        },
    };

    private static readonly RigLook Merchant = new("merchant", Skin, 0.93f, "none")
    {
        Mood = "smile",
        Hair = "lina",
        Feminine = true,
        Skirt = true,
        Cloak = false,
        Rest = "clasped",
        Extras = new[] { "apron" },
        Eyes = new Color("7a4aa8"),
        Brows = new Color("5a4a7a"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("b8a0d8"),
            ["anime_shirt"] = new("4a9a8a"),
            ["anime_cloak"] = new("3a4a6a"),
            ["anime_trousers"] = new("f0dcc8"),
            ["anime_dark"] = new("4a9a8a"),
            ["anime_vest"] = new("f4ecd8"),
            ["anime_glove"] = Skin,
        },
    };

    private static readonly RigLook Alchemist = new("alchemist", Skin, 0.95f, "none")
    {
        Mood = "smirk",
        Hair = "lina",
        Feminine = true,
        Skirt = true,
        Cloak = true,
        Rest = "clasped",
        Extras = new[] { "apron" },
        Eyes = new Color("3e8948"),
        Brows = new Color("3a2a24"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("2e2a3a"),
            ["anime_shirt"] = new("6a3a8a"),
            ["anime_cloak"] = new("3a2450"),
            ["anime_trousers"] = new("2e2440"),
            ["anime_dark"] = new("6a3a8a"),
            ["anime_vest"] = new("c8b48c"),
            ["anime_glove"] = Skin,
        },
    };

    private static readonly RigLook Appraiser = new("appraiser", new Color("e0b494"), 1.02f, "none")
    {
        Mood = "neutral",
        Hair = "short",
        Cloak = false,
        Stoop = 0.08f,
        Rest = "behind",
        Extras = new[] { "apron", "beard" },
        Eyes = new Color("5a4a3a"),
        Brows = new Color("8a8a90"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("8a8a90"),
            ["anime_shirt"] = new("6a4a3a"),
            ["anime_cloak"] = new("3a2a24"),
            ["anime_trousers"] = new("3a3440"),
            ["anime_vest"] = new("c8b48c"),
        },
    };

    private static readonly RigLook Guard = new("guard", new Color("e8c0a4"), 1.04f, "sword")
    {
        Mood = "tired",
        Hair = "short",
        Cloak = true,
        Extras = new[] { "armor" },
        Eyes = new Color("4a6a3a"),
        Brows = new Color("3a2418"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("4a3020"),
            ["anime_shirt"] = new("3a4a6a"),
            ["anime_cloak"] = new("2f4070"),
            ["anime_trousers"] = new("2e3040"),
        },
    };

    private static readonly RigLook Baker = new("baker", new Color("f0c8b0"), 0.94f, "none")
    {
        Mood = "smile",
        Hair = "elda",
        Feminine = true,
        Skirt = true,
        Cloak = false,
        Rest = "behind",
        Extras = new[] { "apron" },
        Eyes = new Color("6a4a30"),
        Brows = new Color("7a3a20"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("9a4a2a"),
            ["anime_shirt"] = new("d89a5a"),
            ["anime_cloak"] = new("7a5a3a"),
            ["anime_trousers"] = new("f0dcc8"),
            ["anime_dark"] = new("d89a5a"),
            ["anime_vest"] = new("fbf6ec"),
            ["anime_glove"] = new("f0c8b0"),
        },
    };

    private static readonly RigLook Farmer = new("farmer", new Color("e8b08a"), 0.92f, "none")
    {
        Mood = "neutral",
        Hair = "pip",
        Cloak = false,
        Extras = new[] { "headband" },
        Eyes = new Color("3a7a4a"),
        Brows = new Color("8a6a30"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("d8b860"),
            ["anime_shirt"] = new("e8dcc0"),
            ["anime_trousers"] = new("5a6a8a"),
            ["anime_dark"] = new("6a5a3e"),
            ["anime_accent"] = new("c84848"),
            ["anime_glove"] = new("e8b08a"),
        },
    };

    private static readonly RigLook Mage = new("adventurer_a", Skin, 0.95f, "none")
    {
        Mood = "smile",
        Hair = "sora",
        Feminine = true,
        Cloak = true,
        Rest = "clasped",
        Eyes = new Color("4a8ac8"),
        Brows = new Color("6a7a9a"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("a8c0e0"),
            ["anime_shirt"] = new("f4f0f8"),
            ["anime_cloak"] = new("4a3a8a"),
            ["anime_trousers"] = new("3a3460"),
            ["anime_glove"] = Skin,
        },
    };

    private static readonly RigLook Swordsman = new("adventurer_b", new Color("e8c0a4"), 1.02f, "sword")
    {
        Mood = "smirk",
        Hair = "spiky",
        Cloak = true,
        Extras = new[] { "headband" },
        Eyes = new Color("c87a2a"),
        Brows = new Color("1a2a20"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("2a4a3a"),
            ["anime_shirt"] = new("8a3a3a"),
            ["anime_cloak"] = new("2a2a32"),
            ["anime_trousers"] = new("3a3440"),
            ["anime_accent"] = new("e8e4dc"),
        },
    };

    /// <summary>Every person with a name, for the photo studio.</summary>
    public static readonly (string Name, RigLook Look)[] All =
    {
        ("rhea", Examiner), ("mira", Receptionist), ("bram", Villager), ("tess", VillagerB), ("lina", Merchant),
        ("garrick", Guard), ("hollis", Appraiser), ("elda", Baker), ("pip", Farmer), ("sora", Mage), ("kai", Swordsman),
    };

    public static RigLook? For(string role) => role switch
    {
        "receptionist" => Receptionist,
        "villager" => Villager,
        "villager_b" => VillagerB,
        "merchant" => Merchant,
        "alchemist" => Alchemist,
        "guard" => Guard,
        "appraiser" => Appraiser,
        "baker" => Baker,
        "farmer" => Farmer,
        "adventurer_a" => Mage,
        "adventurer_b" => Swordsman,
        _ => null,
    };
}


/// <summary>
/// What people say: the dialogues in world/dialogue, shown from the client's copy of the world
/// state. A choice with effects is sent to the server, which checks it again and applies it.
/// </summary>
public static class Dialogues
{
    /// <summary>The line under a speaker's name.</summary>
    public static string Title(string role) => role switch
    {
        "receptionist" => "Guild Receptionist",
        "appraiser" => "Guild Appraiser",
        "quest_board" => "Jobs and Bounties",
        "merchant" => "Merchant",
        "alchemist" => "Alchemist",
        "guard" => "Village Guard",
        "baker" => "Baker",
        "farmer" => "Farmer",
        "adventurer_a" or "adventurer_b" => "Adventurer",
        _ => "Villager",
    };

    public static void Open(Hud hud, Npc npc, IStoryContext context, Snapshot? snapshot)
    {
        if (npc.Role == "storage")
        {
            hud.OpenChest();
            return;
        }

        if (!Story.Dialogues.TryGetValue(npc.Id, out var dialogue) || dialogue.Opening(context) is not { } node)
        {
            return;
        }

        hud.DialogRole = Title(npc.Role);
        Show(hud, npc, dialogue, node, context, snapshot);
    }

    private static void Show(Hud hud, Npc npc, DialogueDef dialogue, DialogueNode node, IStoryContext context, Snapshot? snapshot)
    {
        var choices = new List<DialogChoice>();
        if (node.Shop is { } shop)
        {
            foreach (var ware in ItemDef.Wares.Where(w => ItemDef.All[w].Shop == shop))
            {
                var def = ItemDef.All[ware];
                choices.Add(new DialogChoice($"Buy {def.Name} ({def.Price} gold)", context.Gold >= def.Price ? ShopCommands.Buy(ware) : Command.None));
            }
        }

        foreach (var option in node.Options.Where(o => o.When.Holds(context)))
        {
            var command = option.Effects.Length > 0 ? Command.Choose : Command.None;
            System.Action? then = option.Next is { } next && dialogue.Nodes.TryGetValue(next, out var nextNode)
                ? () => Show(hud, npc, dialogue, nextNode, context, snapshot)
                : null;
            choices.Add(new DialogChoice(Fill(option.Text, context, snapshot), command, (ushort)option.Index, then));
        }

        hud.ShowDialog(npc.Name, Fill(node.Text, context, snapshot), choices.ToArray());
        hud.DialogFace = node.Face;
        hud.Speaker = npc.Id;
    }

    /// <summary>Fills the {names} in a line: gold, crystal worth, sale value, the cost of an attribute reset.</summary>
    public static string Fill(string text, IStoryContext context, Snapshot? snapshot)
    {
        if (!text.Contains('{'))
        {
            return text;
        }

        var worth = 0;
        for (var i = 0; i < Crystals.Tiers; i++)
        {
            worth += (snapshot?.Crystals[i] ?? 0) * Guild.CrystalPrices[i];
        }

        return text
            .Replace("{gold}", context.Gold.ToString())
            .Replace("{crystalWorth}", worth.ToString())
            .Replace("{saleValue}", context.SaleValue.ToString())
            .Replace("{resetCost}", (Attributes.ResetGoldPerLevel * context.Level).ToString());
    }
}

/// <summary>The story as the client sees it: its copy of the world state, and the player's snapshot and bag.</summary>
public sealed class ClientStory : IStoryContext
{
    private readonly ClientPrediction _client;
    private readonly Inventory _items;

    public ClientStory(ClientPrediction client, Inventory items)
    {
        _client = client;
        _items = items;
    }

    public WorldState World => _client.Story;

    public int Rank => _client.Latest?.Rank == GuildRank.Adventurer ? 1 : 0;

    public int Gold => _client.Latest?.Gold ?? 0;

    public int Level => _client.Latest?.Level ?? 1;

    public int SaleValue => _items.SaleValue();

    public int ItemCount(string item) => ItemDef.TryIndex(item, out var index) ? _items.Counts.GetValueOrDefault(index) : 0;

    public int Attribute(string name) =>
        System.Enum.TryParse<PlayerAttribute>(name, true, out var which) && _client.Latest is { } s ? s.Attributes[(int)which] : 0;

    public bool Near(string role) => true;
}
