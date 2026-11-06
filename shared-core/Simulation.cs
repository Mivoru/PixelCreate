using System.Numerics;

namespace PixelCreate.Core;

/// <summary>Gameplay constants. Client and server must use the same values.</summary>
public static class Tuning
{
    public const int TickRate = 60;
    public const float TickSeconds = 1f / TickRate;
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
    public const byte StunTicks = 14;
    public const float Knockback = 8f;
    public static readonly Vector3 PlayerHalfSize = new(0.4f, 0.9f, 0.4f);
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
    public short Health;

    public static PlayerState Spawn(Vector3 position) =>
        new() { Position = position, Health = Tuning.PlayerHealth, Grounded = true };
}

public readonly record struct Aabb(Vector3 Min, Vector3 Max);

/// <summary>Static collision geometry. The floor is the plane y = 0.</summary>
public sealed class World
{
    public IReadOnlyList<Aabb> Boxes { get; }

    public World(IReadOnlyList<Aabb> boxes) => Boxes = boxes;

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
        state.Grounded = false;
        MoveAxis(ref state, world, 0, state.Velocity.X * Tuning.TickSeconds);
        MoveAxis(ref state, world, 2, state.Velocity.Z * Tuning.TickSeconds);
        MoveAxis(ref state, world, 1, state.Velocity.Y * Tuning.TickSeconds);
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

        if (axis == 1 && position.Y <= 0)
        {
            position.Y = 0;
            state.Velocity.Y = 0;
            state.Grounded = true;
        }

        state.Position = position;
    }
}
