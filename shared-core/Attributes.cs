namespace PixelCreate.Core;

/// <summary>The five attributes a player raises with the points each level brings.</summary>
public enum PlayerAttribute : byte
{
    /// <summary>Melee damage with swords and clubs, and with bare hands.</summary>
    Strength = 0,

    /// <summary>Bow and dagger damage, and the chance of a critical hit.</summary>
    Dexterity = 1,

    /// <summary>Staff damage now; skill damage and shorter skill cooldowns once skills come.</summary>
    Intelligence = 2,

    /// <summary>Maximum health and how fast it comes back.</summary>
    Vitality = 3,

    /// <summary>Maximum stamina and how fast it comes back: more dashes, slides and blocks.</summary>
    Endurance = 4,
}

/// <summary>
/// What each attribute point gives. These are starting values, to be measured and tuned with real
/// play, not final balance. Endurance changes movement, so it travels in <see cref="PlayerState"/>
/// and both client and server read it from there.
/// </summary>
public static class Attributes
{
    public const int Count = 5;
    public const int PointsPerLevel = 3;

    /// <summary>A reset at the Guild costs this much gold for each level of the player.</summary>
    public const int ResetGoldPerLevel = 20;

    public const int DamagePctPerPoint = 3;
    public const float CritChancePerDexterity = 0.5f;
    public const int HealthPerVitality = 6;
    public const int RegenPctPerVitality = 3;
    public const float StaminaPerEndurance = 3f;
    public const float StaminaRegenPctPerEndurance = 2f;

    /// <summary>Points a player of this level has to spend in all.</summary>
    public static int Points(int level) => PointsPerLevel * (level - 1);

    /// <summary>The attribute that strengthens a weapon kind.</summary>
    public static PlayerAttribute ForWeapon(string weapon) => weapon switch
    {
        "bow" or "dagger" => PlayerAttribute.Dexterity,
        "staff" => PlayerAttribute.Intelligence,
        _ => PlayerAttribute.Strength,
    };

    public static string Name(PlayerAttribute attribute) => attribute.ToString();

    /// <summary>One line on what a point does, for the character window.</summary>
    public static string Describe(PlayerAttribute attribute) => attribute switch
    {
        PlayerAttribute.Strength => $"+{DamagePctPerPoint}% sword, club and fist damage",
        PlayerAttribute.Dexterity => $"+{DamagePctPerPoint}% bow and dagger damage, +{CritChancePerDexterity.ToString(System.Globalization.CultureInfo.InvariantCulture)}% critical chance",
        PlayerAttribute.Intelligence => $"+{DamagePctPerPoint}% staff and skill damage, -1% skill cooldowns",
        PlayerAttribute.Vitality => $"+{HealthPerVitality} health, +{RegenPctPerVitality}% health regeneration",
        _ => $"+{StaminaPerEndurance:0} stamina, +{StaminaRegenPctPerEndurance:0}% stamina regeneration",
    };

    /// <summary>The most stamina a player holds, with their endurance.</summary>
    public static float MaxStamina(in PlayerState state) => Tuning.MaxStamina + state.Endurance * StaminaPerEndurance;

    /// <summary>Stamina that comes back each tick, with their endurance.</summary>
    public static float StaminaRegen(in PlayerState state) =>
        Tuning.StaminaRegenPerTick * (1f + state.Endurance * StaminaRegenPctPerEndurance / 100f);
}
