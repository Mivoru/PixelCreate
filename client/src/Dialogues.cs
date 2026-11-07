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
        Eyes = new Color("b8552a"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("a8362e"),
            ["anime_shirt"] = new("8b9bb4"),
            ["anime_cloak"] = new("7a2430"),
            ["anime_trousers"] = new("3a3440"),
        },
    };

    private static readonly RigLook Receptionist = new("receptionist", Skin, 0.96f, "none")
    {
        LongHair = true,
        Skirt = true,
        Cloak = false,
        Eyes = new Color("3f6fa8"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("c8703a"),
            ["anime_shirt"] = new("f0ece2"),
            ["anime_cloak"] = new("2f4070"),
            ["anime_trousers"] = new("2f4070"),
            ["anime_dark"] = new("2f4070"),
        },
    };

    private static readonly RigLook Villager = new("villager", new Color("e8c0a4"), 1f, "none")
    {
        Cloak = false,
        Eyes = new Color("6a4a30"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("a8a8b0"),
            ["anime_shirt"] = new("8a7a5a"),
            ["anime_trousers"] = new("4a4038"),
        },
    };

    private static readonly RigLook VillagerB = new("villager_b", Skin, 0.95f, "none")
    {
        LongHair = true,
        Skirt = true,
        Cloak = false,
        Eyes = new Color("3f8f6a"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("e0c060"),
            ["anime_shirt"] = new("c86a5a"),
            ["anime_cloak"] = new("6a4a70"),
        },
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
