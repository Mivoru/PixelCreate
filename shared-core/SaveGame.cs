using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixelCreate.Core;

/// <summary>One death of the player: where, when, at what level and with which weapon. The echoes of act III read these.</summary>
public sealed record DeathRecord(int Day, float[] Position, int Level, string Weapon);

public sealed record SavedGear(string Item, byte Rarity, byte Level, short[] Affixes);

public sealed class SavedPlayer
{
    public string Name { get; set; } = "";
    public byte[] Appearance { get; set; } = Array.Empty<byte>();
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public int Gold { get; set; }
    public byte Rank { get; set; }
    public int[] Crystals { get; set; } = Array.Empty<int>();
    public byte[] Attributes { get; set; } = Array.Empty<byte>();
    public int[] Skills { get; set; } = Array.Empty<int>();
    public byte[] SkillSlots { get; set; } = Array.Empty<byte>();
    public byte[] MasteryLevels { get; set; } = Array.Empty<byte>();
    public int[] MasteryXp { get; set; } = Array.Empty<int>();
    public ulong MasteryTaken { get; set; }
    public Dictionary<string, int> Bag { get; set; } = new();
    public SavedGear?[] Gear { get; set; } = Array.Empty<SavedGear?>();
    public int[] Worn { get; set; } = Array.Empty<int>();
    public Dictionary<string, int> Storage { get; set; } = new();
    public SavedGear?[] StoredGear { get; set; } = Array.Empty<SavedGear?>();
    public float[] Position { get; set; } = Array.Empty<float>();
    public List<DeathRecord> Deaths { get; set; } = new();
}

public sealed class SavedQuest
{
    public QuestStatus Status { get; set; }
    public byte Step { get; set; }
    public int Progress { get; set; }
    public int Completions { get; set; }
}

public sealed class SavedWorld
{
    public int Day { get; set; } = GameTime.Start.Day;
    public int Minute { get; set; } = GameTime.Start.Minute;
    public string[] Flags { get; set; } = Array.Empty<string>();
    public Dictionary<string, int> Reputation { get; set; } = new();
    public Dictionary<string, int> Trust { get; set; } = new();
    public Dictionary<string, NpcFate> Fates { get; set; } = new();
    public Dictionary<string, byte[]> Members { get; set; } = new();
    public string[] AgendaDone { get; set; } = Array.Empty<string>();
    public string[] AgendaFoiled { get; set; } = Array.Empty<string>();
    public Dictionary<string, SavedQuest> Quests { get; set; } = new();

    public static SavedWorld From(WorldState world) => new()
    {
        Day = world.Time.Day,
        Minute = world.Time.Minute,
        Flags = world.Flags.Order(StringComparer.Ordinal).ToArray(),
        Reputation = new Dictionary<string, int>(world.Reputation),
        Trust = new Dictionary<string, int>(world.Trust),
        Fates = new Dictionary<string, NpcFate>(world.Fates),
        Members = world.Members.ToDictionary(p => p.Key, p => new[] { p.Value.Rank, p.Value.Suspicion }),
        AgendaDone = world.AgendaDone.Order(StringComparer.Ordinal).ToArray(),
        AgendaFoiled = world.AgendaFoiled.Order(StringComparer.Ordinal).ToArray(),
        Quests = world.Quests.ToDictionary(p => p.Key, p => new SavedQuest
        {
            Status = p.Value.Status, Step = p.Value.Step, Progress = p.Value.Progress, Completions = p.Value.Completions,
        }),
    };

    /// <summary>Fills a fresh world state. Every entry is marked changed, so clients hear all of it.</summary>
    public void Into(WorldState world)
    {
        world.Time = new GameTime(Day, Minute);
        world.Judging = false;
        foreach (var flag in Flags)
        {
            world.SetFlag(flag);
        }

        world.Judging = true;

        foreach (var (key, value) in Reputation)
        {
            world.AddReputation(key, value - world.Rep(key));
        }

        foreach (var (key, value) in Trust)
        {
            world.AddTrust(key, value - world.TrustOf(key));
        }

        foreach (var (key, value) in Fates)
        {
            world.SetFate(key, value);
        }

        foreach (var (key, value) in Members)
        {
            world.SetMembership(key, new Membership { Rank = value.ElementAtOrDefault(0), Suspicion = value.ElementAtOrDefault(1) });
        }

        foreach (var step in AgendaDone)
        {
            world.MarkAgenda(step, false);
        }

        foreach (var step in AgendaFoiled)
        {
            world.MarkAgenda(step, true);
        }

        foreach (var (key, quest) in Quests)
        {
            world.SetQuest(key, new QuestState { Status = quest.Status, Step = quest.Step, Progress = quest.Progress, Completions = quest.Completions });
        }
    }
}

/// <summary>A saved game: the world and the player in it.</summary>
public sealed class SaveData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public SavedWorld World { get; set; } = new();
    public SavedPlayer? Player { get; set; }
}

/// <summary>One save as the main menu lists it. <paramref name="Broken"/> when neither the save nor its backup could be read.</summary>
public sealed record SaveSummary(string Slot, DateTime Saved, int Day, int Minute, int Level, float[] Position, bool PrologueDone, bool Broken);

/// <summary>
/// Reads and writes saves as JSON. A save is written to a temporary file and then moved into place,
/// and the previous save is kept as a backup, so a crash in the middle never destroys both.
/// </summary>
public static class SaveFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The save folder: PIXEL_SAVE when set, else the local application data folder.</summary>
    public static string Folder =>
        Environment.GetEnvironmentVariable("PIXEL_SAVE") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelCreate", "saves");

    public static string PathFor(string slot) => Path.Combine(Folder, slot + ".json");

    public static string BackupFor(string slot) => Path.Combine(Folder, slot + ".bak.json");

    public static string Serialize(SaveData data) => JsonSerializer.Serialize(data, Options);

    public static SaveData? Deserialize(string json) => JsonSerializer.Deserialize<SaveData>(json, Options);

    public static void Write(string slot, SaveData data)
    {
        Directory.CreateDirectory(Folder);
        var path = PathFor(slot);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, Serialize(data));
        if (File.Exists(path))
        {
            File.Copy(path, BackupFor(slot), true);
        }

        File.Move(temporary, path, true);
    }

    /// <summary>Every save in the folder, newest first: what the main menu lists and Continue picks.</summary>
    public static List<SaveSummary> List()
    {
        var found = new List<SaveSummary>();
        if (!Directory.Exists(Folder))
        {
            return found;
        }

        foreach (var path in Directory.GetFiles(Folder, "*.json"))
        {
            var slot = Path.GetFileNameWithoutExtension(path);
            if (slot.EndsWith(".bak", StringComparison.Ordinal))
            {
                continue;
            }

            var data = Read(slot);
            found.Add(new SaveSummary(
                slot,
                File.GetLastWriteTime(path),
                data is null ? 0 : data.World.Day,
                data is null ? 0 : data.World.Minute,
                data?.Player?.Level ?? 0,
                data?.Player?.Position ?? Array.Empty<float>(),
                data is not null && data.World.Flags.Contains("prologue.done"),
                data is null));
        }

        return found.OrderByDescending(s => s.Saved).ToList();
    }

    /// <summary>Deletes a save and its backup. Cannot be undone; the menu asks first.</summary>
    public static void Delete(string slot)
    {
        foreach (var path in new[] { PathFor(slot), BackupFor(slot), PathFor(slot) + ".tmp" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>
    /// The slot a name the player typed is saved under: letters, digits, spaces, dashes and underscores,
    /// trimmed, at most 32 characters. Empty when nothing usable is left.
    /// </summary>
    public static string SlotFor(string name)
    {
        var kept = new string(name.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').ToArray()).Trim();
        return kept.Length > 32 ? kept[..32].TrimEnd() : kept;
    }

    /// <summary>Whether a slot already holds a save (or its backup).</summary>
    public static bool Exists(string slot) => File.Exists(PathFor(slot)) || File.Exists(BackupFor(slot));

    /// <summary>A free default name for a new game: "Save 1", "Save 2" and so on.</summary>
    public static string NextFreeName()
    {
        for (var i = 1; ; i++)
        {
            if (!Exists($"Save {i}"))
            {
                return $"Save {i}";
            }
        }
    }

    /// <summary>Reads a slot, falling back to its backup when the save is missing or broken. Null for a new game.</summary>
    public static SaveData? Read(string slot)
    {
        foreach (var path in new[] { PathFor(slot), BackupFor(slot) })
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                if (Deserialize(File.ReadAllText(path)) is { } data)
                {
                    return data;
                }
            }
            catch (JsonException)
            {
                // A broken file: try the backup.
            }
        }

        return null;
    }
}
