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
    SellLoot = 7,

    /// <summary>
    /// The Guild chest. The input's argument names what: an item index for a stack (all of it moves),
    /// a place in the gear list to store a piece, or a place in the chest to take one back.
    /// </summary>
    StoreItem = 200,
    TakeItem = 201,
    StoreGear = 202,
    TakeGear = 203,

    /// <summary>Spends one attribute point; the argument names the <see cref="PlayerAttribute"/>.</summary>
    RaiseAttribute = 204,

    /// <summary>Takes every attribute point back for gold, at the Guild receptionist.</summary>
    ResetAttributes = 205,

    /// <summary>Uses an item from the bag, named by the argument: eats food, drinks a potion, reads a skill book.</summary>
    UseItem = 206,

    /// <summary>Casts a learned skill, named by its place in <see cref="SkillDef.All"/>, along the player's aim.</summary>
    CastSkill = 207,
}

/// <summary>A newcomer must beat the guild examiner to become an adventurer.</summary>
public enum GuildRank : byte
{
    Newcomer = 0,
    InTrial = 1,
    Adventurer = 2,
}

/// <param name="Target">
/// The enemy id to kill, a kind prefix ending in * such as goblin_*, or item:id for a job that asks
/// the player to bring items, which the receptionist takes at the end.
/// </param>
public sealed record QuestDef(byte Id, string Name, string Target, int Count, int Gold, int Xp, string Brief)
{
    public bool Matches(string enemyId) =>
        !IsDelivery && (Target.EndsWith('*') ? enemyId.StartsWith(Target[..^1], StringComparison.Ordinal) : enemyId == Target);

    public bool IsDelivery => Target.StartsWith("item:", StringComparison.Ordinal);

    public string Item => IsDelivery ? Target[5..] : "";
}

/// <summary>Taking a job: commands from <see cref="AcceptBase"/> up name the quest by its id.</summary>
public static class QuestCommands
{
    public const int AcceptBase = 32;

    public static Command Accept(byte quest) => (Command)(AcceptBase + quest);

    public static bool TryAccept(Command command, out byte quest)
    {
        quest = (byte)Math.Max(0, (int)command - AcceptBase);
        return (int)command > AcceptBase && (int)command < ItemCommands.EatBase && Guild.Quest(quest) is not null;
    }
}

public static class Guild
{
    public const string ExaminerId = "guild_examiner";
    public const float TalkRange = 3.6f;
    public const float DoorRange = 3f;

    public static readonly int[] CrystalPrices = { 5, 30, 150, 800 };

    public static readonly QuestDef[] Quests =
    {
        new(1, "Clear the Slimes", "slime", 5, 40, 60, "kill 5 slimes"),
        new(2, "Goblin Bounty", "goblin_*", 3, 120, 180, "kill 3 goblins"),
        new(3, "Gel for the Alchemist", "item:slime_gel", 6, 45, 50, "bring 6 Slime Gel"),
        new(4, "Hides for the Tanner", "item:deer_hide", 2, 60, 70, "bring 2 Deer Hides"),
        new(5, "Proof of the Hunt", "item:goblin_tooth", 4, 90, 120, "bring 4 Goblin Teeth"),
    };

    public static QuestDef? Quest(byte id) => Quests.FirstOrDefault(q => q.Id == id);
}

public readonly record struct Portal(Vector3 From, Vector3 To);

public readonly record struct Npc(string Id, string Name, string Role, Vector3 Position, float Yaw);

/// <summary>A flat area that replaces the ground: a bridge deck or the floor of an interior.</summary>
public readonly record struct Platform(float MinX, float MinZ, float MaxX, float MaxZ, float Height);
