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

    /// <summary>Nell: a thin girl of thirteen in a worn dress, sold for her father's debt.</summary>
    private static readonly RigLook Captive = new("captive", new Color("f0c8b0"), 0.8f, "none")
    {
        Hair = "lina",
        Feminine = true,
        Skirt = true,
        Cloak = false,
        Rest = "clasped",
        Mood = "afraid",
        Extras = new[] { "apron" },
        Eyes = new Color("6a4a30"),
        Brows = new Color("4a3020"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("6a4a30"),
            ["anime_shirt"] = new("9a8a6a"),
            ["anime_cloak"] = new("6a5a3e"),
            ["anime_trousers"] = new("8a7a5a"),
            ["anime_dark"] = new("6a5a3e"),
            ["anime_vest"] = new("7a6a4a"),
            ["anime_glove"] = new("f0c8b0"),
        },
    };

    /// <summary>Haruto: a Skyfallen office worker, still in the white shirt he died in.</summary>
    private static readonly RigLook Lost = new("lost", Skin, 0.97f, "none")
    {
        Hair = "short",
        Cloak = false,
        Stoop = 0.1f,
        Rest = "relaxed",
        Mood = "tired",
        Eyes = new Color("2a2a32"),
        Brows = new Color("1c1a24"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("1c1a24"),
            ["anime_shirt"] = new("f4f0e6"),
            ["anime_trousers"] = new("2e3040"),
            ["anime_dark"] = new("2a2a32"),
            ["anime_glove"] = Skin,
        },
    };

    /// <summary>Brann Vosk: the finders' captain, broad, scarred, in a worn leather coat.</summary>
    private static readonly RigLook FinderCaptain = new("finder_captain", new Color("e0b494"), 1.08f, "club")
    {
        Hair = "swept",
        Cloak = true,
        Mood = "smirk",
        Extras = new[] { "beard_short", "vest" },
        Eyes = new Color("5a4a3a"),
        Brows = new Color("3a2418"),
        FaceStyle = new FaceStyle(2, 0, 2, "cheek"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("4a3020"),
            ["anime_shirt"] = new("5a4a3a"),
            ["anime_cloak"] = new("3a2a24"),
            ["anime_vest"] = new("6a4a3a"),
            ["anime_trousers"] = new("3a3440"),
        },
    };

    /// <summary>A finder: one of Vosk's hired men, in drab hooded leathers.</summary>
    private static readonly RigLook Finder = new("finder", new Color("e8c0a4"), 1.0f, "club")
    {
        Hair = "pip",
        Cloak = true,
        Mood = "neutral",
        Extras = new[] { "vest" },
        Eyes = new Color("4a6a3a"),
        Brows = new Color("3a2418"),
        FaceStyle = new FaceStyle(1, 0, 1, "none", true, new Color("3a2418")),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("5a4a3a"),
            ["anime_shirt"] = new("6a6a5a"),
            ["anime_cloak"] = new("4a4a3e"),
            ["anime_vest"] = new("5a4a3a"),
            ["anime_trousers"] = new("3a3440"),
        },
    };

    /// <summary>Every person with a name, for the photo studio.</summary>
    public static readonly (string Name, RigLook Look)[] All =
    {
        ("rhea", Examiner), ("mira", Receptionist), ("bram", Villager), ("tess", VillagerB), ("lina", Merchant),
        ("garrick", Guard), ("hollis", Appraiser), ("elda", Baker), ("pip", Farmer), ("sora", Mage), ("kai", Swordsman),
        ("nell", Captive), ("haruto", Lost), ("vosk", FinderCaptain), ("finder", Finder),
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
        "captive" => Captive,
        "lost" => Lost,
        "finder_captain" => FinderCaptain,
        "finder" => Finder,
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
        "captive" => "Bonded Girl",
        "lost" => "Lightborn",
        "finder_captain" => "Finder Captain",
        "finder" => "Finder",
        "object" or "anchor_stone" => "",
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
        Show(hud, npc, dialogue, node, context, snapshot, true);
    }

    /// <summary>Everyone's name by id, set by the game, for asking one person about another.</summary>
    public static Dictionary<string, string> Names { get; } = new();

    /// <summary>
    /// "What do you make of ...?": what this person thinks of their neighbours, from world/people.json.
    /// Nothing is sent to the server: it is only talk. The dead and the gone are not asked about here;
    /// what people say of them is the story's to write.
    /// </summary>
    private static void ShowAbout(Hud hud, Npc npc, PersonDef person, System.Action back, IStoryContext context)
    {
        var role = hud.DialogRole;
        var choices = new List<DialogChoice>();
        foreach (var (other, line) in person.About)
        {
            if (!Names.TryGetValue(other, out var name) || context.World.Fate(other) is not (NpcFate.Alive or NpcFate.Moved))
            {
                continue;
            }

            choices.Add(new DialogChoice(name, Command.None, 0, () =>
            {
                hud.ShowDialog(npc.Name, line, new[]
                {
                    new DialogChoice("Ask about someone else.", Command.None, 0, () => ShowAbout(hud, npc, person, back, context)),
                    new DialogChoice("That's all.", Command.None, 0, back, Muted: true),
                });
                hud.DialogRole = role;
                hud.Speaker = npc.Id;
            }));
        }

        choices.Add(new DialogChoice("Never mind.", Command.None, 0, back, Muted: true));
        hud.ShowDialog(npc.Name, "Who?", choices.ToArray());
        hud.DialogRole = role;
        hud.Speaker = npc.Id;
    }

    /// <summary>Opens a given node of someone's dialogue, as a scene asks.</summary>
    public static void OpenNode(Hud hud, Npc npc, string node, IStoryContext context, Snapshot? snapshot)
    {
        if (Story.Dialogues.TryGetValue(npc.Id, out var dialogue) && dialogue.Nodes.TryGetValue(node, out var start))
        {
            hud.DialogRole = Title(npc.Role);
            Show(hud, npc, dialogue, start, context, snapshot);
        }
    }

    private static void Show(Hud hud, Npc npc, DialogueDef dialogue, DialogueNode node, IStoryContext context, Snapshot? snapshot, bool opening = false)
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
            // A choice with effects, or one that ends the talk, goes to the server: a scene waits for
            // the talk to end. Goodbyes stay grey all the same.
            var command = option.Effects.Length > 0 || option.Next is null ? Command.Choose : Command.None;
            System.Action? then = option.Next is { } next && dialogue.Nodes.TryGetValue(next, out var nextNode)
                ? () => Show(hud, npc, dialogue, nextNode, context, snapshot)
                : null;
            choices.Add(new DialogChoice(Fill(option.Text, context, snapshot), command, (ushort)option.Index, then, Muted: option.Effects.Length == 0 && option.Next is null));
        }

        if (opening && People.Of(npc.Id) is { About.Count: > 0 } person)
        {
            // Asked at the start of a talk, before the goodbye: what they make of the others.
            var ask = new DialogChoice("What do you make of...?", Command.None, 0,
                () => ShowAbout(hud, npc, person, () => Show(hud, npc, dialogue, node, context, snapshot, true), context));
            var goodbye = choices.FindLastIndex(c => c.Muted);
            choices.Insert(goodbye < 0 ? choices.Count : goodbye, ask);
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

    public System.Numerics.Vector3 Position => _client.State.Position;
}
