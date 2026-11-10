using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// Auren waystones: NPCs of role <see cref="Role"/> whose dialogue (world/dialogue/waystone_*.json) binds the stone
/// (flag <c>waystone.&lt;id&gt;</c>) and offers the trip to every other bound stone. Travel is only from stone to stone:
/// the server action <c>travel:&lt;id&gt;</c> works only beside a waystone and only to a bound one.
/// </summary>
public static class Waystones
{
    public const string Role = "waystone";
    public const string TravelPrefix = "travel:";

    /// <summary>How far in front of a stone a traveller arrives, clear of its base.</summary>
    public const float ArrivalDistance = 2.4f;

    public static string BoundFlag(string id) => "waystone." + id;

    /// <summary>Where a traveller steps out: in front of the stone (a figure's front is -Z at yaw 0).</summary>
    public static Vector3 Arrival(Npc stone) =>
        stone.Position + new Vector3(-MathF.Sin(stone.Yaw), 0, -MathF.Cos(stone.Yaw)) * ArrivalDistance;
}
