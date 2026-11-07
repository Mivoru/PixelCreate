using System.Numerics;

namespace PixelCreate.Core;

/// <summary>Gameplay constants. Client and server must use the same values.</summary>
public static class Tuning
{
    public const int TickRate = 60;
    public const float TickSeconds = 1f / TickRate;

    /// <summary>Ticks of input the server holds per player, to absorb jitter and short loss.</summary>
    public const int InputBufferTicks = 3;

    public const float MoveSpeed = 9f;
    public const float SlowFactor = 0.5f;
    public const float GroundAcceleration = 60f;
    public const float AirAcceleration = 20f;
    public const float Gravity = 24f;
    public const float JumpSpeed = 9f;
    public const float DashSpeed = 20f;
    public const byte DashTicks = 9;
    public const byte DashCooldownTicks = 45;
    public const short PlayerHealth = 100;

    public const float MaxStamina = 100f;
    public const float StaminaRegenPerTick = 24f / TickRate;
    public const byte StaminaRegenDelayTicks = 40;
    public const float DashStamina = 22f;
    public const float JumpStamina = 8f;

    public static readonly Vector3 PlayerHalfSize = new(0.4f, 0.9f, 0.4f);

    /// <summary>Rise per metre that a player can still walk up.</summary>
    public const float MaxSlope = 1.2f;

    /// <summary>How far above the feet the ground can be before it blocks like a wall.</summary>
    public const float StepHeight = 0.4f;

    /// <summary>Largest drop that keeps a walking player on the ground instead of falling.</summary>
    public const float GroundSnap = 0.35f;

    public static AttackDef Attack(AttackKind kind) => kind switch
    {
        AttackKind.Heavy => HeavyAttack,
        AttackKind.DaggerFlurry => DaggerFlurry,
        AttackKind.DaggerLeap => DaggerLeap,
        _ => LightAttack,
    };

    private static readonly AttackDef LightAttack = new(22, 9, 15, 2.8f, 0.3f, 8f, 0.55f);
    private static readonly AttackDef HeavyAttack = new(46, 26, 40, 3.3f, 0.1f, 25f, 0.25f);

    /// <summary>Two quick stabs, right hand then left, each landing on its own.</summary>
    private static readonly AttackDef DaggerFlurry = new(24, 6, 9, 2.3f, 0.35f, 9f, 0.75f, 13);

    /// <summary>
    /// A leap at the enemy with both daggers raised, driving them down on landing. The blow lands
    /// on everything close round the landing spot.
    /// </summary>
    private static readonly AttackDef DaggerLeap = new(50, LeapHitAt, 32, 2.4f, -1f, 28f, 0.1f);

    /// <summary>The leap flies for this many ticks, at this speed along the aim, after this lift.</summary>
    public const int LeapHitAt = 30;
    public const float LeapSpeed = 10f;
    public const float LeapLift = 6.2f;
}

/// <summary>The kind of weapon a player holds, which decides the attacks. Without gear it is the sword.</summary>
public static class Weapons
{
    public const byte Sword = 0;
    public const byte Dagger = 1;
    public const byte Club = 2;
    public const byte Bow = 3;
    public const byte Staff = 4;

    public static byte Of(string weapon) => weapon switch
    {
        "dagger" => Dagger,
        "club" => Club,
        "bow" => Bow,
        "staff" => Staff,
        _ => Sword,
    };

    public static string Name(byte weapon) => weapon switch
    {
        Dagger => "dagger",
        Club => "club",
        Bow => "bow",
        Staff => "staff",
        _ => "sword",
    };
}

public enum AttackKind : byte
{
    None = 0,
    Light = 1,
    Heavy = 2,
    DaggerFlurry = 3,
    DaggerLeap = 4,
}

/// <param name="Total">Length of the attack in ticks.</param>
/// <param name="HitAt">Tick of the attack on which the blow lands.</param>
/// <param name="Damage">Damage of the wooden sword.</param>
/// <param name="Range">Reach in metres.</param>
/// <param name="MinDot">Smallest cosine of the angle to the target: lower is a wider swing.</param>
/// <param name="Stamina">Stamina the attack costs.</param>
/// <param name="MoveScale">Share of normal speed while attacking.</param>
/// <param name="SecondHitAt">Tick of a second blow in the same attack, or 0.</param>
public sealed record AttackDef(int Total, int HitAt, short Damage, float Range, float MinDot, float Stamina, float MoveScale, int SecondHitAt = 0);

[Flags]
public enum Buttons : byte
{
    None = 0,
    Jump = 1,
    Dash = 2,
    Light = 4,
    Heavy = 8,
}

/// <summary>One tick of player input. Stored quantized, so both sides simulate the same values.</summary>
public struct InputCmd
{
    public uint Tick;
    public sbyte MoveX;
    public sbyte MoveZ;
    public Buttons Buttons;

    /// <summary>Where the player aims, in 256ths of a full turn.</summary>
    public byte Aim;

    public Command Command;
}

/// <summary>Everything that decides how a player moves next. Position is the centre of the feet.</summary>
public struct PlayerState
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 DashDirection;
    public float Stamina;
    public bool Grounded;
    public byte DashTicks;
    public byte DashCooldown;
    public byte StaminaDelay;
    public byte SlowTicks;
    public AttackKind Attack;
    public byte AttackTick;
    public byte AttackAim;
    public short Health;

    /// <summary>The kind of weapon held (see <see cref="Weapons"/>). The server sets it when gear changes.</summary>
    public byte Weapon;

    public static PlayerState Spawn(Vector3 position, short health = Tuning.PlayerHealth) => new()
    {
        Position = position,
        Health = health,
        Stamina = Tuning.MaxStamina,
        Grounded = true,
    };
}

public readonly record struct Aabb(Vector3 Min, Vector3 Max);

/// <summary>Static collision geometry: boxes, and terrain or the plane y = 0 as the ground.</summary>
public sealed class World
{
    public IReadOnlyList<Aabb> Boxes { get; }
    public HeightField? Terrain { get; }
    public Vector3 Spawn { get; }
    public IReadOnlyList<EnemySpawn> EnemySpawns { get; }
    public IReadOnlyList<Platform> Platforms { get; init; } = Array.Empty<Platform>();
    public IReadOnlyList<Portal> Portals { get; init; } = Array.Empty<Portal>();
    public IReadOnlyList<Npc> Npcs { get; init; } = Array.Empty<Npc>();

    /// <summary>Where a trial takes place, and where the examiner waits.</summary>
    public Vector3 TrialGround { get; init; }

    public World(IReadOnlyList<Aabb> boxes, HeightField? terrain = null, Vector3 spawn = default,
                 IReadOnlyList<EnemySpawn>? enemySpawns = null)
    {
        Boxes = boxes;
        Terrain = terrain;
        Spawn = spawn;
        EnemySpawns = enemySpawns ?? Array.Empty<EnemySpawn>();
        foreach (var box in boxes)
        {
            var (x0, z0, x1, z1) = Cells(box.Min.X, box.Min.Z, box.Max.X, box.Max.Z);
            if ((long)(x1 - x0 + 1) * (z1 - z0 + 1) > HugeCells)
            {
                _huge.Add(box);
                continue;
            }

            for (var x = x0; x <= x1; x++)
            {
                for (var z = z0; z <= z1; z++)
                {
                    if (!_grid.TryGetValue((x, z), out var list))
                    {
                        _grid[(x, z)] = list = new List<Aabb>();
                    }

                    list.Add(box);
                }
            }
        }
    }

    /// <summary>The boxes are filed by area in a grid of this cell size, so a mover only checks those near it.</summary>
    private const float GridCell = 4f;

    /// <summary>Boxes covering more cells than this (the walls round the map) are always checked instead.</summary>
    private const int HugeCells = 64;

    private readonly Dictionary<(int X, int Z), List<Aabb>> _grid = new();
    private readonly List<Aabb> _huge = new();

    private static (int X0, int Z0, int X1, int Z1) Cells(float minX, float minZ, float maxX, float maxZ) =>
        ((int)MathF.Floor(minX / GridCell), (int)MathF.Floor(minZ / GridCell), (int)MathF.Floor(maxX / GridCell), (int)MathF.Floor(maxZ / GridCell));

    /// <summary>Every box that may touch this area seen from above. A box can come up more than once.</summary>
    public IEnumerable<Aabb> BoxesNear(float minX, float minZ, float maxX, float maxZ)
    {
        foreach (var box in _huge)
        {
            yield return box;
        }

        var (x0, z0, x1, z1) = Cells(minX, minZ, maxX, maxZ);
        for (var x = x0; x <= x1; x++)
        {
            for (var z = z0; z <= z1; z++)
            {
                if (_grid.TryGetValue((x, z), out var list))
                {
                    foreach (var box in list)
                    {
                        yield return box;
                    }
                }
            }
        }
    }

    public float GroundHeight(float x, float z)
    {
        foreach (var platform in Platforms)
        {
            if (x >= platform.MinX && x <= platform.MaxX && z >= platform.MinZ && z <= platform.MaxZ)
            {
                return platform.Height;
            }
        }

        return Terrain?.At(x, z) ?? 0f;
    }

    /// <summary>True when a platform, not the terrain, is the ground at this point.</summary>
    public bool OnPlatform(float x, float z)
    {
        foreach (var platform in Platforms)
        {
            if (x >= platform.MinX && x <= platform.MaxX && z >= platform.MinZ && z <= platform.MaxZ)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when a player standing at this position would be inside a box.</summary>
    public bool OverlapsPlayer(Vector3 position)
    {
        var half = Tuning.PlayerHalfSize;
        foreach (var box in BoxesNear(position.X - half.X, position.Z - half.Z, position.X + half.X, position.Z + half.Z))
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

    /// <summary>True when a point is under the ground or inside a box.</summary>
    public bool IsBlocked(Vector3 point)
    {
        return point.Y < GroundHeight(point.X, point.Z) + 0.2f || IsInsideBox(point);
    }

    /// <summary>True when a point is inside a solid box. The ground does not count.</summary>
    public bool IsInsideBox(Vector3 point)
    {
        foreach (var box in BoxesNear(point.X, point.Z, point.X, point.Z))
        {
            if (point.X > box.Min.X && point.X < box.Max.X && point.Y > box.Min.Y && point.Y < box.Max.Y &&
                point.Z > box.Min.Z && point.Z < box.Max.Z)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The largest footprint, seen from above, of the boxes a point is inside; 0 when it is free.</summary>
    public float FootprintAt(Vector3 point)
    {
        var largest = 0f;
        foreach (var box in BoxesNear(point.X, point.Z, point.X, point.Z))
        {
            if (point.X > box.Min.X && point.X < box.Max.X && point.Y > box.Min.Y && point.Y < box.Max.Y &&
                point.Z > box.Min.Z && point.Z < box.Max.Z)
            {
                largest = MathF.Max(largest, MathF.Max(box.Max.X - box.Min.X, box.Max.Z - box.Min.Z));
            }
        }

        return largest;
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
    /// <summary>Unit vector for an aim value. Aim 0 looks along -Z, the way a Godot camera does.</summary>
    public static Vector3 AimDirection(byte aim)
    {
        var angle = aim / 256f * MathF.Tau;
        return new Vector3(-MathF.Sin(angle), 0, -MathF.Cos(angle));
    }

    /// <summary>Advances one player by one tick. Returns the attack whose blow lands on this tick.</summary>
    public static AttackKind Step(ref PlayerState state, in InputCmd input, World world)
    {
        var wish = new Vector2(input.MoveX, input.MoveZ) / 127f;
        if (wish.LengthSquared() > 1f)
        {
            wish = Vector2.Normalize(wish);
        }

        if (state.DashCooldown > 0) state.DashCooldown--;
        if (state.SlowTicks > 0) state.SlowTicks--;
        if (state.StaminaDelay > 0) state.StaminaDelay--;

        var landed = StepAttack(ref state, input);
        StepDash(ref state, input, wish);

        if (state.StaminaDelay == 0)
        {
            state.Stamina = MathF.Min(Tuning.MaxStamina, state.Stamina + Tuning.StaminaRegenPerTick);
        }

        if (state.DashTicks > 0)
        {
            state.DashTicks--;
            state.Velocity.X = state.DashDirection.X * Tuning.DashSpeed;
            state.Velocity.Z = state.DashDirection.Z * Tuning.DashSpeed;
        }
        else if (state.Attack == AttackKind.DaggerLeap && state.AttackTick < Tuning.LeapHitAt)
        {
            // The leap: a spring off the ground, then a flight along the aim until the daggers land.
            var aim = AimDirection(state.AttackAim);
            if (state.AttackTick == 1 && state.Grounded)
            {
                state.Velocity.Y = Tuning.LeapLift;
            }

            state.Velocity.X = aim.X * Tuning.LeapSpeed;
            state.Velocity.Z = aim.Z * Tuning.LeapSpeed;
        }
        else
        {
            Accelerate(ref state, wish);
        }

        if (input.Buttons.HasFlag(Buttons.Jump) && state.Grounded && state.Stamina >= Tuning.JumpStamina)
        {
            state.Velocity.Y = Tuning.JumpSpeed;
            Spend(ref state, Tuning.JumpStamina);
        }

        state.Velocity.Y -= Tuning.Gravity * Tuning.TickSeconds;
        var wasGrounded = state.Grounded && state.Velocity.Y <= 0;
        state.Grounded = false;
        MoveAxis(ref state, world, 0, state.Velocity.X * Tuning.TickSeconds);
        MoveAxis(ref state, world, 2, state.Velocity.Z * Tuning.TickSeconds);
        MoveAxis(ref state, world, 1, state.Velocity.Y * Tuning.TickSeconds);
        LandOnGround(ref state, world, wasGrounded);
        return landed;
    }

    private static void Spend(ref PlayerState state, float amount)
    {
        state.Stamina = MathF.Max(0f, state.Stamina - amount);
        state.StaminaDelay = Tuning.StaminaRegenDelayTicks;
    }

    private static AttackKind StepAttack(ref PlayerState state, in InputCmd input)
    {
        var landed = AttackKind.None;
        if (state.Attack != AttackKind.None)
        {
            var current = Tuning.Attack(state.Attack);
            state.AttackTick++;
            if (state.AttackTick == current.HitAt || state.AttackTick == current.SecondHitAt)
            {
                landed = state.Attack;
            }

            if (state.AttackTick >= current.Total)
            {
                state.Attack = AttackKind.None;
                state.AttackTick = 0;
            }
        }

        var wanted = input.Buttons.HasFlag(Buttons.Heavy) ? AttackKind.Heavy
            : input.Buttons.HasFlag(Buttons.Light) ? AttackKind.Light : AttackKind.None;
        if (state.Weapon == Weapons.Dagger && wanted != AttackKind.None)
        {
            wanted = wanted == AttackKind.Heavy ? AttackKind.DaggerLeap : AttackKind.DaggerFlurry;
        }
        if (wanted == AttackKind.None || !CanStartAttack(state))
        {
            return landed;
        }

        var next = Tuning.Attack(wanted);
        if (state.Stamina >= next.Stamina)
        {
            state.Attack = wanted;
            state.AttackTick = 0;
            state.AttackAim = input.Aim;
            Spend(ref state, next.Stamina);
        }

        return landed;
    }

    /// <summary>An attack can follow another once the blow has landed and the swing is recovering.</summary>
    private static bool CanStartAttack(in PlayerState state)
    {
        if (state.Attack == AttackKind.None)
        {
            return true;
        }

        var current = Tuning.Attack(state.Attack);
        return state.AttackTick > Math.Max(current.HitAt, current.SecondHitAt) + 5;
    }

    private static void StepDash(ref PlayerState state, in InputCmd input, Vector2 wish)
    {
        if (!input.Buttons.HasFlag(Buttons.Dash) || state.DashCooldown > 0 || state.Stamina < Tuning.DashStamina)
        {
            return;
        }

        var direction = wish != Vector2.Zero ? Vector2.Normalize(wish) : Vector2.Zero;
        if (direction == Vector2.Zero)
        {
            var aim = AimDirection(input.Aim);
            direction = new Vector2(aim.X, aim.Z);
        }

        state.DashDirection = new Vector3(direction.X, 0, direction.Y);
        state.DashTicks = Tuning.DashTicks;
        state.DashCooldown = Tuning.DashCooldownTicks;
        state.Attack = AttackKind.None;
        state.AttackTick = 0;
        Spend(ref state, Tuning.DashStamina);
    }

    private static void Accelerate(ref PlayerState state, Vector2 wish)
    {
        var speed = Tuning.MoveSpeed;
        if (state.SlowTicks > 0)
        {
            speed *= Tuning.SlowFactor;
        }

        if (state.Attack != AttackKind.None)
        {
            speed *= Tuning.Attack(state.Attack).MoveScale;
        }

        var current = new Vector2(state.Velocity.X, state.Velocity.Z);
        var change = wish * speed - current;
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

        foreach (var box in world.BoxesNear(min.X, min.Z, max.X, max.Z))
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
        // The join between a platform and the terrain is a step, not a slope: the coarse ground mesh
        // never meets a deck exactly, and a small lip there must not stop a walker.
        var crossesEdge = world.OnPlatform(state.Position.X, state.Position.Z) != world.OnPlatform(next.X, next.Z);
        return groundNext > state.Position.Y + Tuning.StepHeight ||
               (onGround && !crossesEdge && groundNext - groundNow > MathF.Abs(delta) * Tuning.MaxSlope);
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
