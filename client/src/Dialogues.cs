using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>How the people of the world look.</summary>
public static class NpcLooks
{
    private static readonly Color Skin = new("f6d8c4");

    public static readonly RigLook Examiner = new("examiner", Skin, 1f, "sword")
    {
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

    private static readonly RigLook Guard = new("guard", new Color("e8c0a4"), 1.04f, "sword")
    {
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
        ("garrick", Guard), ("elda", Baker), ("pip", Farmer), ("sora", Mage), ("kai", Swordsman),
    };

    public static RigLook? For(string role) => role switch
    {
        "receptionist" => Receptionist,
        "villager" => Villager,
        "villager_b" => VillagerB,
        "merchant" => Merchant,
        "guard" => Guard,
        "baker" => Baker,
        "farmer" => Farmer,
        "adventurer_a" => Mage,
        "adventurer_b" => Swordsman,
        _ => null,
    };
}

/// <summary>What people say, which depends on how far the player has come.</summary>
public static class Dialogues
{
    public static void Open(Hud hud, Npc npc, Snapshot? snapshot)
    {
        var rank = snapshot?.Rank ?? GuildRank.Newcomer;
        switch (npc.Role)
        {
            case "receptionist":
                Receptionist(hud, npc, snapshot, rank);
                break;
            case "quest_board":
                Board(hud, npc, snapshot, rank);
                break;
            case "villager":
                hud.ShowDialog(npc.Name, "You have a strange look, stranger. Lost? The Guild in the west takes in folk like you.", ("Goodbye", Command.None));
                break;
            case "merchant":
                hud.ShowDialog(npc.Name, "Fresh from the mill and the orchard! Once you carry some gold, come back and we will talk prices.", ("Goodbye", Command.None));
                break;
            case "guard":
                hud.ShowDialog(npc.Name, "The south road leads to Goblin Hollow. Do not go there before the Guild gives you its badge.", ("Understood", Command.None));
                break;
            case "baker":
                hud.ShowDialog(npc.Name, "Another one who fell from the sky? You are the third this year. Eat something first, the Guild can wait a moment.", ("Thank you", Command.None));
                break;
            case "farmer":
                hud.ShowDialog(npc.Name, "Boars dig up the jungle edge again. And the cows are scared of anyone who runs at them, so walk slowly, please.", ("Goodbye", Command.None));
                break;
            case "adventurer_a":
                hud.ShowDialog(npc.Name, "I came from another world too. The trial looks hard, but watch the red marks on the ground and step aside.", ("Good advice", Command.None));
                break;
            case "adventurer_b":
                hud.ShowDialog(npc.Name, "Rhea once threw me across the whole arena. Dash through her heavy swing, then strike back.", ("I will try", Command.None));
                break;
            default:
                hud.ShowDialog(npc.Name, "Slimes by the road again. Mind the ground when they jump: the mark shows where they land.", ("Goodbye", Command.None));
                break;
        }
    }

    private static void Receptionist(Hud hud, Npc npc, Snapshot? snapshot, GuildRank rank)
    {
        if (rank != GuildRank.Adventurer)
        {
            hud.ShowDialog(npc.Name, "Welcome to the Adventurers Guild! To register you must pass a trial against one of our staff.",
                ("Take the trial", Command.BeginTrial), ("Not yet", Command.None));
            return;
        }

        var worth = 0;
        for (var i = 0; i < Crystals.Tiers; i++)
        {
            worth += (snapshot?.Crystals[i] ?? 0) * Guild.CrystalPrices[i];
        }

        if (snapshot is not null && Guild.Quest(snapshot.QuestId) is { } quest && snapshot.QuestProgress >= quest.Count)
        {
            hud.ShowDialog(npc.Name, $"Well done! Here is your pay for {quest.Name}: {quest.Gold} gold.",
                ("Hand in the job", Command.TurnInQuest), ($"Sell all crystals ({worth} gold)", Command.SellCrystals), ("Goodbye", Command.None));
            return;
        }

        hud.ShowDialog(npc.Name, "Good to see you. The Guild buys every magic crystal you bring.",
            ($"Sell all crystals ({worth} gold)", Command.SellCrystals), ("Goodbye", Command.None));
    }

    private static void Board(Hud hud, Npc npc, Snapshot? snapshot, GuildRank rank)
    {
        if (rank != GuildRank.Adventurer)
        {
            hud.ShowDialog(npc.Name, "Only registered adventurers may take jobs.", ("Leave", Command.None));
            return;
        }

        if (snapshot is { QuestId: > 0 })
        {
            hud.ShowDialog(npc.Name, "You already have a job. Finish it first.", ("Leave", Command.None));
            return;
        }

        var slime = Guild.Quests[0];
        var goblin = Guild.Quests[1];
        hud.ShowDialog(npc.Name, "Notices flutter on the board.",
            ($"{slime.Name}: kill {slime.Count} slimes ({slime.Gold} gold)", Command.AcceptSlimeQuest),
            ($"{goblin.Name}: kill {goblin.Count} goblins ({goblin.Gold} gold)", Command.AcceptGoblinQuest),
            ("Leave", Command.None));
    }
}
