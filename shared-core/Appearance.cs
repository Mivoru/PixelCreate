using System.Reflection;
using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>The parts of the player's look, each a place in its list in world/appearance.json.</summary>
public enum AppearanceField : byte
{
    Face = 0,
    Nose = 1,
    Skin = 2,
    Eyes = 3,
    EyeShape = 4,
    Brows = 5,
    Hair = 6,
    HairColor = 7,
    Beard = 8,
    Scar = 9,
    Height = 10,
}

public sealed record AppearanceOption(string Id, string Name);

/// <summary>The choices for the player's look, from world/appearance.json. Colours are hex strings.</summary>
public sealed class AppearanceOptions
{
    public AppearanceOption[] Hair { get; init; } = Array.Empty<AppearanceOption>();
    public string[] HairColors { get; init; } = Array.Empty<string>();
    public string[] Skin { get; init; } = Array.Empty<string>();
    public string[] Eyes { get; init; } = Array.Empty<string>();
    public AppearanceOption[] Beards { get; init; } = Array.Empty<AppearanceOption>();
    public AppearanceOption[] Scars { get; init; } = Array.Empty<AppearanceOption>();
    public string[] Faces { get; init; } = Array.Empty<string>();
    public string[] Noses { get; init; } = Array.Empty<string>();
    public string[] EyeShapes { get; init; } = Array.Empty<string>();
    public string[] Brows { get; init; } = Array.Empty<string>();
    public float[] Heights { get; init; } = Array.Empty<float>();
}

/// <summary>
/// How the player looks: one choice per <see cref="AppearanceField"/>. The player is always a man
/// (decided 2026-11-09); everything else is theirs to pick. The server keeps it in the save and
/// sends it in the snapshot; the client builds the figure from it.
/// </summary>
public static class Appearance
{
    public static readonly int FieldCount = Enum.GetValues<AppearanceField>().Length;

    /// <summary>A look not yet chosen: the client opens the character creator.</summary>
    public const byte Unset = 255;

    public static AppearanceOptions Options { get; } = Load();

    /// <summary>The first look, before the player picks: the hero as he was drawn.</summary>
    public static byte[] Default => new byte[] { 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 2 };

    public static byte[] NotChosen => Enumerable.Repeat(Unset, FieldCount).ToArray();

    public static bool IsChosen(byte[] look) => look.Length == FieldCount && look.All(v => v != Unset);

    /// <summary>How many choices a field has.</summary>
    public static int Choices(AppearanceField field) => field switch
    {
        AppearanceField.Face => Options.Faces.Length,
        AppearanceField.Nose => Options.Noses.Length,
        AppearanceField.Skin => Options.Skin.Length,
        AppearanceField.Eyes => Options.Eyes.Length,
        AppearanceField.EyeShape => Options.EyeShapes.Length,
        AppearanceField.Brows => Options.Brows.Length,
        AppearanceField.Hair => Options.Hair.Length,
        AppearanceField.HairColor => Options.HairColors.Length,
        AppearanceField.Beard => Options.Beards.Length,
        AppearanceField.Scar => Options.Scars.Length,
        AppearanceField.Height => Options.Heights.Length,
        _ => 0,
    };

    /// <summary>The name of a choice, for the creator screen.</summary>
    public static string Describe(AppearanceField field, int value) => field switch
    {
        AppearanceField.Face => Options.Faces[value],
        AppearanceField.Nose => Options.Noses[value],
        AppearanceField.EyeShape => Options.EyeShapes[value],
        AppearanceField.Brows => Options.Brows[value],
        AppearanceField.Hair => Options.Hair[value].Name,
        AppearanceField.Beard => Options.Beards[value].Name,
        AppearanceField.Scar => Options.Scars[value].Name,
        AppearanceField.Height => $"{Options.Heights[value]:0.00}",
        _ => $"{value + 1} of {Choices(field)}",
    };

    public static bool Valid(AppearanceField field, int value) => value >= 0 && value < Choices(field);

    /// <summary>The argument of a <see cref="Command.SetAppearance"/>: the field in the high byte, the choice in the low.</summary>
    public static ushort Arg(AppearanceField field, int value) => (ushort)(((int)field << 8) | (value & 0xff));

    /// <summary>The argument that confirms the look as it stands (the creator's "This is me").</summary>
    public const ushort Confirm = 0xff00;

    private static AppearanceOptions Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("appearance")!;
        return JsonSerializer.Deserialize<AppearanceOptions>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
