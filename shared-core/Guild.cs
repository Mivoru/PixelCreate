using System.Numerics;

namespace PixelCreate.Core;

/// <summary>What a player asks for beyond moving. Sent with an input, checked by the server.</summary>
public enum Command : byte
{
    None = 0,
    UseDoor = 1,
    BeginTrial = 2,
    SellCrystals = 3,
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

    /// <summary>Takes a node of a weapon mastery tree, named by its place in <see cref="Mastery.Nodes"/>.</summary>
    TakeMastery = 208,

    /// <summary>
    /// Puts a learned skill in one of the <see cref="Skills.Slots"/> skill slots: the low byte names the
    /// skill (<see cref="Skills.NoSkill"/> empties the slot), the high byte the slot.
    /// </summary>
    SlotSkill = 209,

    /// <summary>Picks a dialogue option; the argument is its place in <see cref="Story.Options"/>. The server checks it again.</summary>
    Choose = 210,

    /// <summary>Sleeps until six in the morning, when the evening has come.</summary>
    Sleep = 211,

    /// <summary>The player leaves the game: the server saves at once.</summary>
    Quit = 212,

    /// <summary>Changes one part of the player's look: the high byte names the <see cref="AppearanceField"/>, the low byte the choice.</summary>
    SetAppearance = 213,

    /// <summary>Moves a scene past a line or a slide the player has read; the argument is the step, so a late repeat skips nothing.</summary>
    SceneNext = 214,
}

/// <summary>A newcomer must beat the guild examiner to become an adventurer.</summary>
public enum GuildRank : byte
{
    Newcomer = 0,
    InTrial = 1,
    Adventurer = 2,
}

public static class Guild
{
    public const string ExaminerId = "guild_examiner";
    public const float TalkRange = 3.6f;
    public const float DoorRange = 3f;

    public static readonly int[] CrystalPrices = { 5, 30, 150, 800 };

}

public readonly record struct Portal(Vector3 From, Vector3 To);

public readonly record struct Npc(string Id, string Name, string Role, Vector3 Position, float Yaw);

/// <summary>A flat area that replaces the ground: a bridge deck or the floor of an interior.</summary>
public readonly record struct Platform(float MinX, float MinZ, float MaxX, float MaxZ, float Height);
