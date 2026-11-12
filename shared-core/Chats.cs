using System.Reflection;
using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>
/// A little exchange two or three people have among themselves, for the player to overhear: lines in turn, each
/// said by one of them. It plays when the player stands near all of them and they are at rest (not walking), at an
/// hour the condition allows. Only for the eye and ear: it changes nothing in the game.
/// </summary>
public sealed record Chat(string[] Between, Condition When, (string Speaker, string Text)[] Lines);

public static class Chats
{
    public static IReadOnlyList<Chat> All { get; } = Load();

    private static Chat[] Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("chats");
        if (stream is null)
        {
            return Array.Empty<Chat>();
        }

        using var document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateArray().Select(c => new Chat(
            c.GetProperty("between").EnumerateArray().Select(b => b.GetString()!).ToArray(),
            c.TryGetProperty("when", out var when) ? Condition.Parse(when) : Condition.Always,
            c.GetProperty("lines").EnumerateArray().Select(l => (l[0].GetString()!, l[1].GetString()!)).ToArray())).ToArray();
    }

    /// <summary>The lines a person says in any chat, for recording their voice.</summary>
    public static IEnumerable<string> LinesOf(string id) => All.SelectMany(c => c.Lines).Where(l => l.Speaker == id).Select(l => l.Text);
}
