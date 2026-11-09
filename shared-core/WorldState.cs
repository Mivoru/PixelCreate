namespace PixelCreate.Core;

public enum NpcFate : byte
{
    Alive = 0,
    Dead = 1,
    Gone = 2,
    Moved = 3,
}

public enum QuestStatus : byte
{
    None = 0,
    Active = 1,
    Done = 2,
    Failed = 3,
}

/// <summary>Where one quest stands: its status, the step reached and the progress inside that step.</summary>
public struct QuestState
{
    public QuestStatus Status;
    public byte Step;
    public int Progress;

    /// <summary>How often a repeatable job was finished.</summary>
    public int Completions;
}

/// <summary>A player's place in a faction: rank from 0 (outsider) to 3 (inner circle), and how far they suspect betrayal.</summary>
public struct Membership
{
    public byte Rank;
    public byte Suspicion;
}

/// <summary>
/// Everything the story remembers about the world: what happened, who is alive, what the factions
/// think and which steps of their plans have run. The server owns it and saves it; the client keeps
/// a copy built from the story entries in snapshots. Keys are names from the story data.
/// </summary>
public sealed class WorldState
{
    public GameTime Time = GameTime.Start;
    public readonly HashSet<string> Flags = new(StringComparer.Ordinal);
    public readonly Dictionary<string, int> Reputation = new(StringComparer.Ordinal);
    public readonly Dictionary<string, int> Trust = new(StringComparer.Ordinal);
    public readonly Dictionary<string, NpcFate> Fates = new(StringComparer.Ordinal);
    public readonly Dictionary<string, Membership> Members = new(StringComparer.Ordinal);
    public readonly HashSet<string> AgendaDone = new(StringComparer.Ordinal);
    public readonly HashSet<string> AgendaFoiled = new(StringComparer.Ordinal);
    public readonly Dictionary<string, QuestState> Quests = new(StringComparer.Ordinal);

    public bool Has(string flag) => Flags.Contains(flag);

    public int Rep(string faction) => Reputation.GetValueOrDefault(faction);

    public int TrustOf(string npc) => Trust.GetValueOrDefault(npc);

    public NpcFate Fate(string npc) => Fates.GetValueOrDefault(npc);

    public Membership Member(string faction) => Members.GetValueOrDefault(faction);

    public QuestState Quest(string id) => Quests.GetValueOrDefault(id);

    public static int Clamp(int value) => Math.Clamp(value, -100, 100);

    /// <summary>Entries changed since the server last sent them, so they go out first.</summary>
    public readonly HashSet<(StoryKind Kind, string Key)> Changed = new();

    public void SetFlag(string flag, bool set = true)
    {
        if (set ? Flags.Add(flag) : Flags.Remove(flag))
        {
            Changed.Add((StoryKind.Flag, flag));
        }
    }

    public void AddReputation(string faction, int amount)
    {
        Reputation[faction] = Clamp(Rep(faction) + amount);
        Changed.Add((StoryKind.Reputation, faction));
    }

    public void AddTrust(string npc, int amount)
    {
        Trust[npc] = Clamp(TrustOf(npc) + amount);
        Changed.Add((StoryKind.Trust, npc));
    }

    public void SetFate(string npc, NpcFate fate)
    {
        Fates[npc] = fate;
        Changed.Add((StoryKind.Fate, npc));
    }

    public void SetMembership(string faction, Membership membership)
    {
        Members[faction] = membership;
        Changed.Add((StoryKind.Member, faction));
    }

    public void SetQuest(string quest, QuestState state)
    {
        Quests[quest] = state;
        Changed.Add((StoryKind.Quest, quest));
    }

    public void MarkAgenda(string step, bool foiled)
    {
        (foiled ? AgendaFoiled : AgendaDone).Add(step);
        Changed.Add((StoryKind.Agenda, step));
    }
}

/// <summary>What a story entry in a snapshot describes.</summary>
public enum StoryKind : byte
{
    Flag = 0,
    Reputation = 1,
    Trust = 2,
    Fate = 3,
    Member = 4,
    Quest = 5,
    Agenda = 6,
}
