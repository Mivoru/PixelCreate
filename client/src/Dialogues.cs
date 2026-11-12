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
    private static readonly RigLook FinderCaptain = new("finder_captain", new Color("e0b494"), 1.08f, "bow")
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

    /// <summary>A finder: one of Vosk's hired men, in drab hooded leathers. (Any finder without a look of their own.)</summary>
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

    /// <summary>Gil: lean and quick, the finder who throws the net; a knife at hand, a weasel's face, a rope coat.</summary>
    private static readonly RigLook FinderGil = new("finder_gil", new Color("e8c0a4"), 0.96f, "dagger")
    {
        Hair = "undercut",
        Cloak = false,
        FaceWidth = 0.88f,
        Mood = "smirk",
        Extras = new[] { "vest" },
        Eyes = new Color("6a5a2a"),
        Brows = new Color("5a3a1a"),
        FaceStyle = new FaceStyle(2, 1, 1, "none", true, new Color("6a4a2a")),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("8a5a2a"),
            ["anime_shirt"] = new("7a6a4a"),
            ["anime_vest"] = new("c8b48c"),
            ["anime_trousers"] = new("4a4038"),
        },
    };

    /// <summary>Hanne: a woman in a mail shirt under a dark cloak, a short sword at her hip; the steadiest of them.</summary>
    private static readonly RigLook FinderHanne = new("finder_hanne", new Color("f0c8b0"), 0.97f, "sword")
    {
        Hair = "rhea",
        Feminine = true,
        Cloak = true,
        Mood = "neutral",
        Item = "hannes_shortsword",
        Extras = new[] { "armor" },
        Eyes = new Color("3a5a7a"),
        Brows = new Color("2a2a2a"),
        FaceStyle = new FaceStyle(1, 2, 0, "cheek"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("2a2a30"),
            ["anime_shirt"] = new("4a4a52"),
            ["anime_cloak"] = new("2e3a34"),
            ["anime_trousers"] = new("3a3440"),
            ["anime_armor"] = new("8a909a"),
            ["anime_armor_dark"] = new("5a606a"),
        },
    };

    /// <summary>Corb: the big one, shaved head and a red beard, a club over his shoulder, slow to talk.</summary>
    private static readonly RigLook FinderCorb = new("finder_corb", new Color("e0a888"), 1.13f, "club")
    {
        Hair = "shaved",
        Cloak = false,
        FaceWidth = 1.12f,
        Mood = "neutral",
        Extras = new[] { "beard", "vest" },
        Eyes = new Color("4a4a3a"),
        Brows = new Color("8a3a1a"),
        FaceStyle = new FaceStyle(0, 0, 2, "eye"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("9a4a20"),
            ["anime_shirt"] = new("5a3a2a"),
            ["anime_vest"] = new("3a2a20"),
            ["anime_trousers"] = new("3a3440"),
        },
    };

    /// <summary>Corwin Hale: a big man gone grey, an old adventurer's coat, a scar over the eye.</summary>
    private static readonly RigLook BranchMaster = new("branch_master", new Color("e0b494"), 1.06f, "none")
    {
        Hair = "swept",
        Cloak = true,
        Stoop = 0.06f,
        FaceWidth = 1.06f,
        Mood = "tired",
        Extras = new[] { "beard_short", "vest" },
        Eyes = new Color("5a4a3a"),
        Brows = new Color("6a6a70"),
        FaceStyle = new FaceStyle(2, 0, 2, "eye"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("8a8a90"),
            ["anime_shirt"] = new("6a5a4a"),
            ["anime_cloak"] = new("4a2a2a"),
            ["anime_vest"] = new("3a3440"),
            ["anime_trousers"] = new("3a3440"),
        },
    };

    /// <summary>Tam: a boy of fifteen in an apron too long for him.</summary>
    private static readonly RigLook Apprentice = new("apprentice", new Color("f0c8b0"), 0.86f, "none")
    {
        Hair = "pip",
        Cloak = false,
        Rest = "clasped",
        Mood = "neutral",
        Extras = new[] { "apron" },
        Eyes = new Color("6a4a30"),
        Brows = new Color("6a4a30"),
        FaceStyle = new FaceStyle(0, 1, 1),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("4a3020"),
            ["anime_shirt"] = new("e8dcc0"),
            ["anime_trousers"] = new("5a4a3a"),
            ["anime_dark"] = new("6a5a3e"),
            ["anime_vest"] = new("c8b48c"),
            ["anime_glove"] = new("f0c8b0"),
        },
    };

    /// <summary>Edwin: nineteen, fair-haired, in a blue cloak cut down from his father's.</summary>
    private static readonly RigLook PartySword = new("party_sword", new Color("e8c0a4"), 1.0f, "sword")
    {
        Hair = "hero",
        Cloak = true,
        Mood = "smile",
        Eyes = new Color("3f6fa8"),
        Brows = new Color("a88838"),
        FaceStyle = new FaceStyle(1, 1, 1),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("e0c060"),
            ["anime_shirt"] = new("e8dcc0"),
            ["anime_cloak"] = new("3a5a8a"),
            ["anime_trousers"] = new("4a4038"),
            ["anime_accent"] = new("e8e4dc"),
        },
    };

    /// <summary>Moss: lean, dark, in hunter's green.</summary>
    private static readonly RigLook PartyBow = new("party_bow", new Color("c08a64"), 0.99f, "bow")
    {
        Hair = "undercut",
        Cloak = true,
        FaceWidth = 0.94f,
        Mood = "neutral",
        Extras = new[] { "vest" },
        Eyes = new Color("3e8948"),
        Brows = new Color("1c1a24"),
        FaceStyle = new FaceStyle(1, 0, 0),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("1c1a24"),
            ["anime_shirt"] = new("5a6a4a"),
            ["anime_cloak"] = new("3a4a32"),
            ["anime_trousers"] = new("3a3440"),
            ["anime_vest"] = new("5a4a3a"),
        },
    };

    /// <summary>Fen: small, brown-haired, a healer's white apron over a green dress.</summary>
    private static readonly RigLook PartyHealer = new("party_healer", new Color("f6d8c4"), 0.92f, "none")
    {
        Hair = "mira",
        Feminine = true,
        Skirt = true,
        Cloak = false,
        Rest = "clasped",
        Mood = "smile",
        Extras = new[] { "apron" },
        Eyes = new Color("3e8948"),
        Brows = new Color("4a3020"),
        FaceStyle = new FaceStyle(0, 1, 0),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("6a4a30"),
            ["anime_shirt"] = new("5a8a6a"),
            ["anime_cloak"] = new("3a5a4a"),
            ["anime_trousers"] = new("f0dcc8"),
            ["anime_dark"] = new("5a8a6a"),
            ["anime_vest"] = new("f4f0e6"),
            ["anime_glove"] = new("f6d8c4"),
        },
    };

    /// <summary>Rook: broad, shaven-headed, in the only armour the Four own.</summary>
    private static readonly RigLook PartyShield = new("party_shield", new Color("e0b494"), 1.08f, "sword")
    {
        Hair = "shaved",
        Cloak = false,
        FaceWidth = 1.06f,
        Mood = "smile",
        Extras = new[] { "armor" },
        Eyes = new Color("5a4a3a"),
        Brows = new Color("3a2418"),
        FaceStyle = new FaceStyle(2, 1, 2, "none", true, new Color("3a2418")),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("4a3020"),
            ["anime_shirt"] = new("7a5a3a"),
            ["anime_cloak"] = new("5a3a2a"),
            ["anime_trousers"] = new("3a3440"),
        },
    };

    /// <summary>Osric: a balding miller dusted with flour, moustache and apron.</summary>
    private static readonly RigLook Miller = new("miller", new Color("e8c0a4"), 1.0f, "none")
    {
        Hair = "bram",
        Cloak = false,
        Rest = "behind",
        Stoop = 0.05f,
        FaceWidth = 1.06f,
        Mood = "neutral",
        Extras = new[] { "apron", "mustache" },
        Eyes = new Color("5a4a3a"),
        Brows = new Color("6a6a70"),
        FaceStyle = new FaceStyle(2, 0, 2),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("8a8a90"),
            ["anime_shirt"] = new("d8d0c0"),
            ["anime_cloak"] = new("6a5a3e"),
            ["anime_trousers"] = new("5a4a3a"),
            ["anime_dark"] = new("6a5a3e"),
            ["anime_vest"] = new("f4f0e6"),
            ["anime_glove"] = new("e8c0a4"),
        },
    };

    /// <summary>Anwen: twenty, fair hair tied back, a blue dress.</summary>
    private static readonly RigLook MillerDaughter = new("miller_daughter", new Color("f6d8c4"), 0.93f, "none")
    {
        Hair = "rhea",
        Feminine = true,
        Skirt = true,
        Cloak = false,
        Rest = "clasped",
        Mood = "smile",
        Extras = new[] { "apron" },
        Eyes = new Color("3f6fa8"),
        Brows = new Color("a88838"),
        FaceStyle = new FaceStyle(0, 1, 0),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("e0c060"),
            ["anime_shirt"] = new("6a8ac0"),
            ["anime_cloak"] = new("3a4a6a"),
            ["anime_trousers"] = new("f0dcc8"),
            ["anime_dark"] = new("6a8ac0"),
            ["anime_vest"] = new("f4ecd8"),
            ["anime_glove"] = new("f6d8c4"),
        },
    };

    /// <summary>Hamm: Pip's father, sun-browned, greying, a farmer's short beard.</summary>
    private static readonly RigLook FarmElder = new("farm_elder", new Color("e8b08a"), 1.02f, "none")
    {
        Hair = "short",
        Cloak = false,
        Rest = "behind",
        Stoop = 0.04f,
        Mood = "tired",
        Extras = new[] { "beard_short" },
        Eyes = new Color("3a7a4a"),
        Brows = new Color("6a5a40"),
        FaceStyle = new FaceStyle(1, 0, 2),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("8a7a60"),
            ["anime_shirt"] = new("c8b48c"),
            ["anime_trousers"] = new("5a4a3a"),
            ["anime_dark"] = new("6a5a3e"),
            ["anime_glove"] = new("e8b08a"),
        },
    };

    /// <summary>Maddy: seventeen, red-haired, sleeves rolled.</summary>
    private static readonly RigLook FarmGirl = new("farm_girl", new Color("f0c8b0"), 0.9f, "none")
    {
        Hair = "tess",
        Feminine = true,
        Skirt = true,
        Cloak = false,
        Mood = "smirk",
        Extras = new[] { "apron" },
        Eyes = new Color("3a7a4a"),
        Brows = new Color("7a2420"),
        FaceStyle = new FaceStyle(0, 0, 1),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("a8362e"),
            ["anime_shirt"] = new("e8dcc0"),
            ["anime_cloak"] = new("7a5a3a"),
            ["anime_trousers"] = new("8a6a4a"),
            ["anime_dark"] = new("e8dcc0"),
            ["anime_vest"] = new("c8b48c"),
            ["anime_glove"] = new("f0c8b0"),
        },
    };

    /// <summary>Tobias: thirty, weathered, hair tied up out of the way.</summary>
    private static readonly RigLook Farmhand = new("farmhand", new Color("c08a64"), 1.03f, "none")
    {
        Hair = "topknot",
        Cloak = false,
        Mood = "neutral",
        Eyes = new Color("5a4a3a"),
        Brows = new Color("1c1a24"),
        FaceStyle = new FaceStyle(1, 0, 1, "none", true, new Color("2b3044")),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("2b3044"),
            ["anime_shirt"] = new("8a7a5a"),
            ["anime_trousers"] = new("4a4038"),
            ["anime_dark"] = new("4a4038"),
            ["anime_glove"] = new("c08a64"),
        },
    };
    /// <summary>Oswin: a broad greying woodcutter with a full beard and rolled sleeves, a felling axe always in hand.</summary>
    private static readonly RigLook Woodcutter = new("woodcutter", new Color("d8a07a"), 1.06f, "axe")
    {
        Hair = "short",
        Cloak = false,
        Mood = "neutral",
        Item = "wood_axe",
        WorkEvery = 2.1f,
        WorkHeavy = true,
        Extras = new[] { "beard", "vest" },
        Eyes = new Color("4a5a3a"),
        Brows = new Color("5a5a5a"),
        FaceStyle = new FaceStyle(2, 0, 1),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("6a6a6a"),
            ["anime_shirt"] = new("a84a3a"),
            ["anime_trousers"] = new("4a4038"),
            ["anime_dark"] = new("3a2a20"),
            ["anime_vest"] = new("5a4a30"),
            ["anime_glove"] = new("6a4a30"),
        },
    };

    /// <summary>Jory: Oswin's lanky young nephew, freckled and quick, swinging smaller and faster.</summary>
    private static readonly RigLook WoodcutterB = new("woodcutter_b", new Color("f0c8a0"), 0.96f, "axe")
    {
        Hair = "spiky",
        Cloak = false,
        Mood = "happy",
        Item = "wood_axe",
        WorkEvery = 1.5f,
        WorkHeavy = true,
        Eyes = new Color("3a6a8a"),
        Brows = new Color("a85a2a"),
        FaceStyle = new FaceStyle(0, 1, 0),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("c86a3a"),
            ["anime_shirt"] = new("6a8a5a"),
            ["anime_trousers"] = new("5a4a3a"),
            ["anime_dark"] = new("3a2a20"),
            ["anime_glove"] = new("f0c8a0"),
        },
    };

    /// <summary>Tobb: a weathered old fisherman in an oilskin-coloured coat, white whiskers, a rod always out over the water.</summary>
    private static readonly RigLook Fisher = new("fisher", new Color("c89070"), 0.94f, "staff")
    {
        Hair = "shaved",
        Cloak = false,
        Mood = "tired",
        Item = "fishing_rod",
        WorkEvery = 7f,
        Stoop = 0.05f,
        Extras = new[] { "beard_short", "apron" },
        Eyes = new Color("4a6a8a"),
        Brows = new Color("d8d8d8"),
        FaceStyle = new FaceStyle(2, 0, 2, "none", true, new Color("d8d8d8")),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("d8d8d8"),
            ["anime_shirt"] = new("5a6a5a"),
            ["anime_trousers"] = new("3a4a5a"),
            ["anime_dark"] = new("2a2a2a"),
            ["anime_apron"] = new("8a7a4a"),
            ["anime_glove"] = new("c89070"),
        },
    };

    /// <summary>Brin: a sturdy young shepherdess with a long braid, a wool shawl over her shoulders and a crook in hand.</summary>
    private static readonly RigLook Shepherd = new("shepherd", new Color("e8b896"), 0.95f, "staff")
    {
        Hair = "lina",
        Feminine = true,
        Cloak = true,
        Mood = "neutral",
        Item = "shepherd_crook",
        WorkEvery = 6f,
        Extras = new[] { "apron" },
        Eyes = new Color("6a4a2a"),
        Brows = new Color("8a5a2a"),
        FaceStyle = new FaceStyle(0, 1, 1, "none"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("a86a3a"),
            ["anime_shirt"] = new("e8dcc0"),
            ["anime_trousers"] = new("6a5a4a"),
            ["anime_dark"] = new("4a3a2a"),
            ["anime_cloak"] = new("c8b890"),
            ["anime_apron"] = new("8a6a4a"),
            ["anime_glove"] = new("e8b896"),
        },
    };

    /// <summary>Marta: the round, brisk innkeeper of the Last Lantern, hair up in a bun, sleeves rolled, apron on.</summary>
    private static readonly RigLook Innkeeper = new("innkeeper", new Color("e8b090"), 0.98f, "none")
    {
        Hair = "elda",
        Feminine = true,
        Cloak = false,
        Mood = "happy",
        Rest = "clasped",
        Extras = new[] { "apron" },
        Eyes = new Color("5a3a2a"),
        Brows = new Color("6a4a2a"),
        FaceWidth = 1.08f,
        FaceStyle = new FaceStyle(1, 1, 1, "cheek"),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("8a5a3a"),
            ["anime_shirt"] = new("c8a888"),
            ["anime_trousers"] = new("6a3a3a"),
            ["anime_dark"] = new("3a2a2a"),
            ["anime_apron"] = new("f0e8d8"),
            ["anime_glove"] = new("e8b090"),
        },
    };

    /// <summary>Aldous: a thin old pilgrim in a grey robe with a long white beard, bound for the shrine each day.</summary>
    private static readonly RigLook Pilgrim = new("pilgrim", new Color("d8a888"), 0.97f, "staff")
    {
        Hair = "shaved",
        Cloak = true,
        Mood = "tired",
        Item = "shepherd_crook",
        Stoop = 0.06f,
        Extras = new[] { "robe", "beard" },
        Eyes = new Color("6a7a8a"),
        Brows = new Color("e0e0e0"),
        FaceStyle = new FaceStyle(2, 0, 2),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("e8e8e8"),
            ["anime_shirt"] = new("7a7a80"),
            ["anime_trousers"] = new("5a5a60"),
            ["anime_dark"] = new("3a3a40"),
            ["anime_robe"] = new("8a8a90"),
            ["anime_cloak"] = new("5a5a60"),
            ["anime_glove"] = new("d8a888"),
        },
    };

    /// <summary>Fennick: a quick-talking young peddler in a patched green coat, a satchel of odds and ends.</summary>
    private static readonly RigLook Peddler = new("peddler", new Color("f0c8a8"), 0.93f, "none")
    {
        Hair = "swept",
        Cloak = false,
        Mood = "happy",
        Extras = new[] { "vest" },
        Eyes = new Color("4a7a3a"),
        Brows = new Color("3a2a1a"),
        FaceStyle = new FaceStyle(0, 2, 0),
        Palette = new Dictionary<string, Color>
        {
            ["anime_hair"] = new("3a2a1a"),
            ["anime_shirt"] = new("4a6a3a"),
            ["anime_trousers"] = new("6a5a3a"),
            ["anime_dark"] = new("2a2a1a"),
            ["anime_vest"] = new("8a6a3a"),
            ["anime_glove"] = new("f0c8a8"),
        },
    };

    /// <summary>A bandit of the south woods: a hooded cutthroat in drab, patched leathers.</summary>
    private static RigLook Bandit(string weapon, Color hood, Color coat, Color skin, string hair, bool feminine = false) =>
        new("bandit", skin, feminine ? 0.96f : 1f, weapon)
        {
            Hair = hair,
            Feminine = feminine,
            Cloak = true,
            Mood = "angry",
            Extras = new[] { "vest" },
            Eyes = new Color("3a3a2a"),
            Brows = new Color("2a2218"),
            FaceStyle = new FaceStyle(2, 0, 2, feminine ? "none" : "cheek", !feminine),
            Palette = new Dictionary<string, Color>
            {
                ["anime_hair"] = new("3a2a1e"),
                ["anime_shirt"] = coat,
                ["anime_trousers"] = new("3a3428"),
                ["anime_dark"] = new("22201a"),
                ["anime_vest"] = new("5a4630"),
                ["anime_cloak"] = hood,
                ["anime_glove"] = new("3a2a1e"),
            },
        };

    /// <summary>The look of a human enemy by its id, or null when it has none of its own.</summary>
    public static RigLook? Enemy(string id) => id switch
    {
        "bandit_cutthroat" => Bandit("sword", new Color("4a4a3a"), new Color("6a5a40"), new Color("d8a888"), "undercut"),
        "bandit_archer" => Bandit("bow", new Color("3a4a3a"), new Color("5a6a4a"), new Color("e8c0a0"), "lina", true),
        "bandit_chief" => Bandit("sword", new Color("8a2a2a"), new Color("4a3a2a"), new Color("c89070"), "topknot") with { Scale = 1.08f, Item = "orc_longsword" },
        _ => null,
    };

    /// <summary>Every person with a name, for the photo studio.</summary>
    public static readonly (string Name, RigLook Look)[] All =
    {
        ("rhea", Examiner), ("mira", Receptionist), ("bram", Villager), ("tess", VillagerB), ("lina", Merchant),
        ("garrick", Guard), ("hollis", Appraiser), ("elda", Baker), ("pip", Farmer), ("sora", Mage), ("kai", Swordsman),
        ("nell", Captive), ("haruto", Lost), ("vosk", FinderCaptain), ("gil", FinderGil), ("hanne", FinderHanne), ("corb", FinderCorb),
        ("corwin", BranchMaster), ("tam", Apprentice), ("edwin", PartySword), ("moss", PartyBow), ("fen", PartyHealer),
        ("rook", PartyShield), ("osric", Miller), ("anwen", MillerDaughter), ("hamm", FarmElder), ("maddy", FarmGirl),
        ("tobias", Farmhand), ("oswin", Woodcutter), ("jory", WoodcutterB), ("tobb", Fisher), ("brin", Shepherd), ("marta", Innkeeper), ("aldous", Pilgrim), ("fennick", Peddler),
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
        "finder_gil" => FinderGil,
        "finder_hanne" => FinderHanne,
        "finder_corb" => FinderCorb,
        "branch_master" => BranchMaster,
        "apprentice" => Apprentice,
        "party_sword" => PartySword,
        "party_bow" => PartyBow,
        "party_healer" => PartyHealer,
        "party_shield" => PartyShield,
        "miller" => Miller,
        "miller_daughter" => MillerDaughter,
        "farm_elder" => FarmElder,
        "farm_girl" => FarmGirl,
        "farmhand" => Farmhand,
        "woodcutter" => Woodcutter,
        "woodcutter_b" => WoodcutterB,
        "fisher" => Fisher,
        "shepherd" => Shepherd,
        "innkeeper" => Innkeeper,
        "pilgrim" => Pilgrim,
        "peddler" => Peddler,
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
        "adventurer_a" or "adventurer_b" or "party_sword" or "party_bow" or "party_healer" or "party_shield" => "Adventurer",
        "branch_master" => "Branch Master",
        "apprentice" => "Appraiser's Apprentice",
        "miller" => "Miller",
        "miller_daughter" => "Miller's Daughter",
        "farm_elder" => "Farmer",
        "farm_girl" or "farmhand" => "Farmhand",
        "woodcutter" or "woodcutter_b" => "Woodcutter",
        "fisher" => "Fisherman",
        "shepherd" => "Shepherd",
        "innkeeper" => "Innkeeper",
        "pilgrim" => "Pilgrim",
        "peddler" => "Peddler",
        "captive" => "Bonded Girl",
        "lost" => "Lightborn",
        "finder_captain" => "Finder Captain",
        "finder" or "finder_gil" or "finder_hanne" or "finder_corb" => "Finder",
        "object" or "anchor_stone" => "",
        Waystones.Role => "Auren Waystone",
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

    /// <summary>Rumours heard in this run of the game, before the server's word of it has come back.</summary>
    private static readonly HashSet<string> Heard = new();

    private static Rumor? News(Npc npc, IStoryContext context) => Story.NewsFrom(npc.Id, context).FirstOrDefault(r => !Heard.Contains(r.Id));

    /// <summary>
    /// "Heard anything?": the newest thing this person has to tell that the player has not heard, in
    /// their own words. The server is told, checks they do tell it, and the journal keeps a note.
    /// </summary>
    private static void ShowRumor(Hud hud, Npc npc, Rumor rumor, System.Action back, IStoryContext context)
    {
        var role = hud.DialogRole;
        Heard.Add(rumor.Id);
        var choices = new List<DialogChoice>();
        if (News(npc, context) is { } more)
        {
            choices.Add(new DialogChoice("Anything else?", Command.HearRumor, (ushort)more.Index, () => ShowRumor(hud, npc, more, back, context)));
        }

        choices.Add(new DialogChoice("I see.", Command.None, 0, back, Muted: true));
        hud.ShowDialog(npc.Name, rumor.TextFor(npc.Id), choices.ToArray());
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

        if (opening && News(npc, context) is { } rumor)
        {
            // Asked at the start of a talk, before the goodbye: what is being said in the village.
            var ask = new DialogChoice(npc.Role == "quest_board" ? "Read the notices." : "Heard anything?", Command.HearRumor, (ushort)rumor.Index,
                () => ShowRumor(hud, npc, rumor, () => Show(hud, npc, dialogue, node, context, snapshot, true), context));
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
