namespace PixelCreate.Core;

/// <summary>
/// Packs world state values into story entries and unpacks them again. The server packs what
/// changed and a rotating slice of the rest; the client applies them to its own copy.
/// </summary>
public static class StoryMirror
{
    /// <summary>Entries in each snapshot: changed ones first, the rest of the room for the rotation.</summary>
    public const int EntriesPerSnapshot = 8;

    public static int Pack(WorldState world, StoryKind kind, string key) => kind switch
    {
        StoryKind.Flag => world.Has(key) ? 1 : 0,
        StoryKind.Reputation => world.Rep(key),
        StoryKind.Trust => world.TrustOf(key),
        StoryKind.Fate => (int)world.Fate(key),
        StoryKind.Member => world.Member(key).Rank | (world.Member(key).Suspicion << 8),
        StoryKind.Quest => PackQuest(world.Quest(key)),
        StoryKind.Agenda => world.AgendaDone.Contains(key) ? 1 : world.AgendaFoiled.Contains(key) ? 2 : 0,
        _ => 0,
    };

    private static int PackQuest(QuestState state) =>
        (int)state.Status | (state.Step << 8) | (Math.Min(state.Progress, short.MaxValue) << 16);

    /// <summary>Applies one entry to the client's copy. Entries for unknown keys are ignored.</summary>
    public static void Apply(WorldState world, StoryEntry entry)
    {
        if (Story.KeyName(entry.Kind, entry.Key) is not { } key)
        {
            return;
        }

        var v = entry.Value;
        switch (entry.Kind)
        {
            case StoryKind.Flag:
                if (v != 0)
                {
                    world.Flags.Add(key);
                }
                else
                {
                    world.Flags.Remove(key);
                }

                break;
            case StoryKind.Reputation:
                world.Reputation[key] = v;
                break;
            case StoryKind.Trust:
                world.Trust[key] = v;
                break;
            case StoryKind.Fate:
                world.Fates[key] = (NpcFate)v;
                break;
            case StoryKind.Member:
                world.Members[key] = new Membership { Rank = (byte)(v & 0xff), Suspicion = (byte)((v >> 8) & 0xff) };
                break;
            case StoryKind.Quest:
                world.Quests[key] = new QuestState { Status = (QuestStatus)(v & 0xff), Step = (byte)((v >> 8) & 0xff), Progress = v >> 16 };
                break;
            case StoryKind.Agenda:
                world.AgendaDone.Remove(key);
                world.AgendaFoiled.Remove(key);
                if (v == 1)
                {
                    world.AgendaDone.Add(key);
                }
                else if (v == 2)
                {
                    world.AgendaFoiled.Add(key);
                }

                break;
        }
    }

    /// <summary>Every key of every kind in a fixed order, for the rotating slice.</summary>
    public static IReadOnlyList<(StoryKind Kind, string Key)> AllKeys { get; } =
        Story.Keys.OrderBy(k => k.Key).SelectMany(k => k.Value.Select(name => (k.Key, name))).ToArray();

    /// <summary>
    /// Picks the entries for one snapshot: what changed for this player first, then the rotation.
    /// </summary>
    public static StoryEntry[] Slice(WorldState world, HashSet<(StoryKind Kind, string Key)> changed, ref int cursor)
    {
        var entries = new List<StoryEntry>(EntriesPerSnapshot);
        foreach (var item in changed.Take(EntriesPerSnapshot - 2).ToList())
        {
            changed.Remove(item);
            if (Story.TryKey(item.Kind, item.Key, out var index))
            {
                entries.Add(new StoryEntry(item.Kind, index, Pack(world, item.Kind, item.Key)));
            }
        }

        var all = AllKeys;
        for (var i = 0; entries.Count < EntriesPerSnapshot && i < all.Count; i++)
        {
            var (kind, key) = all[cursor++ % all.Count];
            if (Story.TryKey(kind, key, out var index))
            {
                entries.Add(new StoryEntry(kind, index, Pack(world, kind, key)));
            }
        }

        return entries.ToArray();
    }
}
