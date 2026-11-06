using System.Numerics;

namespace PixelCreate.Core;

/// <summary>Gameplay constants. Client and server must use the same values.</summary>
public static class Tuning
{
    public const int TickRate = 60;
    public const float TickSeconds = 1f / TickRate;

    /// <summary>Ticks of input the server holds per player, to absorb jitter and short loss.</summary>
    public const int InputBufferTicks = 3;
    public const float MoveSpeed = 7f;
    public const float GroundAcceleration = 60f;
    public const float AirAcceleration = 20f;
    public const float Gravity = 24f;
    public const float JumpSpeed = 9f;
    public const float DashSpeed = 20f;
    public const byte DashTicks = 9;
    public const byte DashCooldownTicks = 60;
    public const byte AttackCooldownTicks = 24;
    public const float AttackRange = 2.6f;
    public const short AttackDamage = 25;
    public const short PlayerHealth = 100;
    public const short EnemyHealth = 50;
    public const float EnemySpeed = 3f;
    public const float EnemyReach = 1.1f;
    public const byte EnemyCooldownTicks = 45;
    public const short EnemyDamage = 5;
    public const byte StunTicks = 8;

    /// <summary>Time after a stun in which hits still hurt but cannot stun, so enemies cannot chain it.</summary>
    public const byte StunImmunityTicks = 90;
    public const float Knockback = 6f;
    public static readonly Vector3 PlayerHalfSize = new(0.4f, 0.9f, 0.4f);

    /// <summary>Rise per metre that a player can still walk up.</summary>
    public const float MaxSlope = 1.2f;

    /// <summary>How far above the feet the ground can be before it blocks like a wall.</summary>
    public const float StepHeight = 0.4f;

    /// <summary>Largest drop that keeps a walking player on the ground instead of falling.</summary>
    public const float GroundSnap = 0.35f;
}

[Flags]
public enum Buttons : byte
{
    None = 0,
    Jump = 1,
    Dash = 2,
    Attack = 4,
}

/// <summary>One tick of player input. Stored quantized, so both sides simulate the same values.</summary>
public struct InputCmd
{
    public uint Tick;
    public sbyte MoveX;
    public sbyte MoveZ;
    public Buttons Buttons;
}

/// <summary>Everything that decides how a player moves next. Position is the centre of the feet.</summary>
public struct PlayerState
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 DashDirection;
    public bool Grounded;
    public byte DashTicks;
    public byte DashCooldown;
    public byte AttackCooldown;
    public byte StunTicks;
    public byte StunImmunity;
    public short Health;

    public static PlayerState Spawn(Vector3 position) =>
        new() { Position = position, Health = Tuning.PlayerHealth, Grounded = true };
}

public readonly record struct Aabb(Vector3 Min, Vector3 Max);

/// <summary>Static collision geometry: boxes, and terrain or the plane y = 0 as the ground.</summary>
public sealed class World
{
    public IReadOnlyList<Aabb> Boxes { get; }
    public HeightField? Terrain { get; }
    public Vector3 Spawn { get; }

    public World(IReadOnlyList<Aabb> boxes, HeightField? terrain = null, Vector3 spawn = default)
    {
        Boxes = boxes;
        Terrain = terrain;
        Spawn = spawn;
    }

    public float GroundHeight(float x, float z) => Terrain?.At(x, z) ?? 0f;

    /// <summary>True when a player standing at this position would be inside a box.</summary>
    public bool OverlapsPlayer(Vector3 position)
    {
        var half = Tuning.PlayerHalfSize;
        foreach (var box in Boxes)
        {
            if (position.X - half.X < box.Max.X && position.X + half.X > box.Min.X &&
                position.Y < box.Max.Y && position.Y + half.Y * 2 > box.Min.Y &&
                position.Z - half.Z < box.Max.Z && position.Z + half.Z > box.Min.Z)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when a point is under the ground or inside a box. The camera uses this.</summary>
    public bool IsBlocked(Vector3 point)
    {
        if (point.Y < GroundHeight(point.X, point.Z) + 0.2f)
        {
            return true;
        }

        foreach (var box in Boxes)
        {
            if (point.X > box.Min.X && point.X < box.Max.X && point.Y > box.Min.Y && point.Y < box.Max.Y &&
                point.Z > box.Min.Z && point.Z < box.Max.Z)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A walled 40 x 40 m arena with four pillars.</summary>
    public static World Arena()
    {
        const float half = 20f;
        const float height = 6f;
        var boxes = new List<Aabb>
        {
            new(new(-half - 1, 0, -half - 1), new(half + 1, height, -half)),
            new(new(-half - 1, 0, half), new(half + 1, height, half + 1)),
            new(new(-half - 1, 0, -half), new(-half, height, half)),
            new(new(half, 0, -half), new(half + 1, height, half)),
        };
        foreach (var (x, z) in new[] { (-8f, -8f), (8f, -8f), (-8f, 8f), (8f, 8f) })
        {
            boxes.Add(new(new(x - 1.5f, 0, z - 1.5f), new(x + 1.5f, 3f, z + 1.5f)));
        }

        return new World(boxes);
    }
}

public static class Movement
{
    /// <summary>Advances one player by one tick. Returns true when an attack starts on this tick.</summary>
    public static bool Step(ref PlayerState state, in InputCmd input, World world)
    {
        var wish = new Vector2(input.MoveX, input.MoveZ) / 127f;
        if (wish.LengthSquared() > 1f)
        {
            wish = Vector2.Normalize(wish);
        }

        var buttons = input.Buttons;
        if (state.StunTicks > 0)
        {
            state.StunTicks--;
            wish = Vector2.Zero;
            buttons = Buttons.None;
        }

        if (state.StunImmunity > 0)
        {
            state.StunImmunity--;
        }

        if (state.DashCooldown > 0)
        {
            state.DashCooldown--;
        }

        if (state.AttackCooldown > 0)
        {
            state.AttackCooldown--;
        }

        var attacked = false;
        if (buttons.HasFlag(Buttons.Attack) && state.AttackCooldown == 0)
        {
            state.AttackCooldown = Tuning.AttackCooldownTicks;
            attacked = true;
        }

        if (buttons.HasFlag(Buttons.Dash) && state.DashCooldown == 0 && wish != Vector2.Zero)
        {
            var direction = Vector2.Normalize(wish);
            state.DashDirection = new Vector3(direction.X, 0, direction.Y);
            state.DashTicks = Tuning.DashTicks;
            state.DashCooldown = Tuning.DashCooldownTicks;
        }

        if (state.DashTicks > 0)
        {
            state.DashTicks--;
            state.Velocity.X = state.DashDirection.X * Tuning.DashSpeed;
            state.Velocity.Z = state.DashDirection.Z * Tuning.DashSpeed;
        }
        else
        {
            Accelerate(ref state, wish);
        }

        if (buttons.HasFlag(Buttons.Jump) && state.Grounded)
        {
            state.Velocity.Y = Tuning.JumpSpeed;
        }

        state.Velocity.Y -= Tuning.Gravity * Tuning.TickSeconds;
        var wasGrounded = state.Grounded && state.Velocity.Y <= 0;
        state.Grounded = false;
        MoveAxis(ref state, world, 0, state.Velocity.X * Tuning.TickSeconds);
        MoveAxis(ref state, world, 2, state.Velocity.Z * Tuning.TickSeconds);
        MoveAxis(ref state, world, 1, state.Velocity.Y * Tuning.TickSeconds);
        LandOnGround(ref state, world, wasGrounded);
        return attacked;
    }

    private static void Accelerate(ref PlayerState state, Vector2 wish)
    {
        var current = new Vector2(state.Velocity.X, state.Velocity.Z);
        var change = wish * Tuning.MoveSpeed - current;
        var limit = (state.Grounded ? Tuning.GroundAcceleration : Tuning.AirAcceleration) * Tuning.TickSeconds;
        if (change.LengthSquared() > limit * limit)
        {
            change = Vector2.Normalize(change) * limit;
        }

        state.Velocity.X = current.X + change.X;
        state.Velocity.Z = current.Y + change.Y;
    }

    private static void MoveAxis(ref PlayerState state, World world, int axis, float delta)
    {
        var position = state.Position;
        position[axis] += delta;
        var half = Tuning.PlayerHalfSize;
        var min = new Vector3(position.X - half.X, position.Y, position.Z - half.Z);
        var max = new Vector3(position.X + half.X, position.Y + half.Y * 2, position.Z + half.Z);

        foreach (var box in world.Boxes)
        {
            if (min.X >= box.Max.X || max.X <= box.Min.X || min.Y >= box.Max.Y || max.Y <= box.Min.Y ||
                min.Z >= box.Max.Z || max.Z <= box.Min.Z)
            {
                continue;
            }

            // A deep overlap did not come from this move. Pushing it out would be a teleport.
            var depth = delta > 0 ? max[axis] - box.Min[axis] : box.Max[axis] - min[axis];
            if (depth > MathF.Abs(delta) + 0.01f)
            {
                continue;
            }

            var bottom = axis == 1 ? 0f : half[axis];
            var top = axis == 1 ? half.Y * 2 : half[axis];
            position[axis] = delta > 0 ? box.Min[axis] - top : box.Max[axis] + bottom;
            var velocity = state.Velocity;
            velocity[axis] = 0;
            state.Velocity = velocity;
            state.Grounded |= axis == 1 && delta < 0;
            min[axis] = position[axis] - bottom;
            max[axis] = position[axis] + top;
        }

        if (axis != 1 && IsTooSteep(state, world, position, delta))
        {
            position[axis] = state.Position[axis];
            var velocity = state.Velocity;
            velocity[axis] = 0;
            state.Velocity = velocity;
        }

        state.Position = position;
    }

    private static bool IsTooSteep(in PlayerState state, World world, Vector3 next, float delta)
    {
        var groundNow = world.GroundHeight(state.Position.X, state.Position.Z);
        var groundNext = world.GroundHeight(next.X, next.Z);
        var onGround = state.Position.Y <= groundNow + 0.05f;
        return groundNext > state.Position.Y + Tuning.StepHeight ||
               (onGround && groundNext - groundNow > MathF.Abs(delta) * Tuning.MaxSlope);
    }

    private static void LandOnGround(ref PlayerState state, World world, bool wasGrounded)
    {
        var ground = world.GroundHeight(state.Position.X, state.Position.Z);
        var height = state.Position.Y - ground;
        var snap = wasGrounded && !state.Grounded && height < Tuning.GroundSnap &&
                   !world.OverlapsPlayer(state.Position with { Y = ground });
        if (height <= 0 || snap)
        {
            state.Position.Y = ground;
            state.Velocity.Y = 0;
            state.Grounded = true;
        }
    }
}
