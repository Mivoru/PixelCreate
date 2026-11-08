namespace PixelCreate.Core;

/// <summary>Levels and experience. Levels raise only base maximum health and base damage for now.</summary>
public static class Progression
{
    public const int MaxLevel = 60;

    /// <summary>Experience needed to go from this level to the next.</summary>
    /// <remarks>About 5 slimes for level 2, 16 even-level kills at level 5 and 34 at level 10.</remarks>
    public static int XpToNext(int level) => 100 + level * level * 40;

    public static short MaxHealth(int level) => (short)(Tuning.PlayerHealth + (level - 1) * 15);

    /// <summary>Base damage multiplier: 10 % more for each level.</summary>
    public static float DamageScale(int level) => 1f + (level - 1) * 0.1f;

    /// <summary>
    /// Experience for a kill. Enemies far below the player give little, as in most MMORPGs,
    /// so that progress comes from fighting at your own level.
    /// </summary>
    public static int KillXp(int enemyLevel, int playerLevel, bool rare)
    {
        var baseXp = 20 + enemyLevel * 10;
        var gap = playerLevel - enemyLevel;
        var scale = gap <= 0 ? 1f + MathF.Min(-gap, 3) * 0.1f : MathF.Max(0.1f, 1f - gap * 0.25f);
        return (int)MathF.Ceiling(baseXp * scale * (rare ? 3f : 1f));
    }
}
