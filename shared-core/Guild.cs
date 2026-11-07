using System.Numerics;

namespace PixelCreate.Core;

/// <summary>What a player asks for beyond moving. Sent with an input, checked by the server.</summary>
public enum Command : byte
{
    None = 0,
    UseDoor = 1,
    BeginTrial = 2,
    SellCrystals = 3,
    AcceptSlimeQuest = 4,
    AcceptGoblinQuest = 5,
    TurnInQuest = 6,
}

/// <summary>A newcomer must beat the guild examiner to become an adventurer.</summary>
public enum GuildRank : byte
{
    Newcomer = 0,
    InTrial = 1,
    Adventurer = 2,
}

/// <param name="Target">The enemy id to kill, or a kind prefix ending in * such as goblin_*.</param>
public sealed record QuestDef(byte Id, string Name, string Target, int Count, int Gold, int Xp)
{
    public bool Matches(string enemyId) =>
        Target.EndsWith('*') ? enemyId.StartsWith(Target[..^1], StringComparison.Ordinal) : enemyId == Target;
}

public static class Guild
{
    public const string ExaminerId = "guild_examiner";
    public const float TalkRange = 3.6f;
    public const float DoorRange = 3f;

    public static readonly int[] CrystalPrices = { 5, 30, 150, 800 };

    public static readonly QuestDef[] Quests =
    {
        new(1, "Clear the Slimes", "slime", 5, 40, 60),
        new(2, "Goblin Bounty", "goblin_*", 3, 120, 180),
    };

    public static QuestDef? Quest(byte id) => Quests.FirstOrDefault(q => q.Id == id);
}

public readonly record struct Portal(Vector3 From, Vector3 To);

public readonly record struct Npc(string Id, string Name, string Role, Vector3 Position, float Yaw);

/// <summary>A flat area that replaces the ground: a bridge deck or the floor of an interior.</summary>
public readonly record struct Platform(float MinX, float MinZ, float MaxX, float MaxZ, float Height);
