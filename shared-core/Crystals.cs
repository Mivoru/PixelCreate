namespace PixelCreate.Core;

public enum CrystalRarity : byte
{
    Common = 0,
    Rare = 1,
    Epic = 2,
    Legendary = 3,
}

/// <param name="Weight">Weight of one crystal in kilograms.</param>
public sealed record CrystalDef(CrystalRarity Rarity, string Name, float Weight, string Color);

/// <summary>Magic crystals: every enemy drops them, and they will later be sold or spent on skills.</summary>
public static class Crystals
{
    public const int Tiers = 4;

    public static readonly CrystalDef[] All =
    {
        new(CrystalRarity.Common, "Common Magic Crystal", 0.2f, "8b9bb4"),
        new(CrystalRarity.Rare, "Rare Magic Crystal", 0.5f, "0099db"),
        new(CrystalRarity.Epic, "Epic Magic Crystal", 1.2f, "b55088"),
        new(CrystalRarity.Legendary, "Legendary Magic Crystal", 3f, "feae34"),
    };
}

/// <param name="Rarity">The crystal tier.</param>
/// <param name="Chance">Chance from 0 to 1, rolled separately for each entry.</param>
public sealed record DropEntry(CrystalRarity Rarity, double Chance);
