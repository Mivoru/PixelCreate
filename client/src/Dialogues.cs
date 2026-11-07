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

    /// <summary>Every person with a name, for the photo studio.</summary>
    public static readonly (string Name, RigLook Look)[] All =
    {
        ("rhea", Examiner), ("mira", Receptionist), ("bram", Villager), ("tess", VillagerB),
    };

    public static RigLook? For(string role) => role switch
    {
        "receptionist" => Receptionist,
        "villager" => Villager,
        "villager_b" => VillagerB,
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
