using System.Reflection;
using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>
/// One facial expression from world/faces.json: where the brows sit, how far the lids close and
/// which mouth shows. The client's face rig eases towards it.
/// </summary>
/// <param name="BrowRaise">-1 pulled down to 1 raised high.</param>
/// <param name="BrowTilt">-1 inner ends down (anger) to 1 inner ends up (sorrow).</param>
/// <param name="LidUpper">0 open to 1 shut.</param>
/// <param name="LidLower">0 down to 1 raised (a smile or a squint).</param>
/// <param name="Mouth">line, smile, grin, open, o, frown, teeth or smirk.</param>
public sealed record FacePreset(
    float BrowRaise, float BrowTilt, float LidUpper, float LidLower, string Mouth,
    bool HappyEyes = false, bool Wide = false, bool Blush = false, bool Tear = false, bool Sweat = false);

public static class Faces
{
    public static readonly string[] Mouths = { "line", "smile", "grin", "open", "o", "frown", "teeth", "smirk" };

    public static IReadOnlyDictionary<string, FacePreset> All { get; } = Load();

    public static FacePreset Neutral => All["neutral"];

    public static FacePreset Get(string? name) => name is not null && All.TryGetValue(name, out var face) ? face : Neutral;

    private static Dictionary<string, FacePreset> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("faces")!;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<Dictionary<string, FacePreset>>(stream, options)!;
    }
}
