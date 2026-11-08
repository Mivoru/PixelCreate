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

    /// <summary>
    /// Blocking: the share of normal speed while guarding, how wide the guard covers to each side of
    /// the aim (as the cosine of the angle), and the first ticks of a guard that count as a parry.
    /// A guard takes all of a light blow and lets the given share of a heavy one through, for stamina.
    /// </summary>
    public const float BlockMoveScale = 0.45f;

    /// <summary>
    /// The slide: a low burst along the way the player runs that bleeds speed as it goes. It needs a
    /// running start on the ground, costs stamina, can be steered a little and jumped out of, keeping
    /// the speed. Speeds are metres a second at the start and at the end of the slide.
    /// </summary>
    public const byte SlideTicks = 30;
    public const byte SlideCooldownTicks = 24;
    public const float SlideStartSpeed = 17f;
    public const float SlideEndSpeed = 7f;
    public const float SlideStamina = 12f;
    public const float SlideMinSpeed = 5.5f;
    public const float SlideSteer = 0.05f;
    public const float BlockMinDot = 0.34f;
    public const byte ParryTicks = 10;
    public const float BlockHeavyShare = 0.4f;
    public const float BlockLightStamina = 8f;
    public const float BlockHeavyStamina = 18f;

    /// <summary>How long a parried enemy reels, by how its level compares with the player's, and how much more it takes meanwhile.</summary>
    public const int ParryStaggerTicks = 90;
    public const int ParryStaggerTicksStronger = 36;
    public const int ParryLevelReach = 2;
    public const float StaggerDamageScale = 1.5f;
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
        AttackKind.BowShot => BowShot,
        AttackKind.ClubSwing => ClubSwing,
        AttackKind.ClubSpin => ClubSpin,
        AttackKind.StaffBolt => StaffBolt,
        AttackKind.StaffWall => StaffWall,
        _ => LightAttack,
    };

    /// <summary>
    /// Loosing an arrow: the shot leaves on the first tick, then the hand fetches and nocks the next
    /// arrow; no new draw starts before that is done. Damage is for a full draw; the arrow itself is
    /// the server's (see GameServer.Loose).
    /// </summary>
    private static readonly AttackDef BowShot = new(26, 1, 30, 0f, 1f, 0f, 0.6f);

    /// <summary>A bow reaches full draw after this many ticks of holding the attack button.</summary>
    public const byte FullDraw = 54;

    /// <summary>
    /// No arrow leaves before the bow is drawn this far: letting go sooner finishes the short draw
    /// first. With the nocking time this caps the bow at about 1.4 shots a second.
    /// </summary>
    public const byte MinDraw = 18;

    /// <summary>How fast an arrow leaves the bow, how hard it falls, and how far it flies before it is spent.</summary>
    public static float ArrowSpeed(float drawn) => 22f + 28f * drawn;
    public const float ArrowGravity = 9.8f;
    public static float ArrowRange(float drawn) => 18f + 22f * drawn;

    /// <summary>How far the bow is drawn, from nothing to a full draw.</summary>
    public static float Drawn(byte draw) => Math.Min(draw, FullDraw) / (float)FullDraw;

    /// <summary>The steepest an arrow can be aimed up or down, in radians.</summary>
    public const float MaxShotPitch = 1.4f;

    /// <summary>A quick shot still carries this share of a full draw's damage and speed.</summary>
    public const float MinDrawShare = 0.3f;

    /// <summary>Holding a drawn bow slows the walk to this share, and costs this stamina when loosed.</summary>
    public const float DrawMoveScale = 0.55f;
    public const float ShotStamina = 6f;

    /// <summary>The share of a full draw's power a shot of this draw carries.</summary>
    public static float DrawShare(byte draw) => MinDrawShare + (1f - MinDrawShare) * Math.Min(draw, FullDraw) / FullDraw;

    private static readonly AttackDef LightAttack = new(22, 9, 15, 2.8f, 0.3f, 8f, 0.55f);
    private static readonly AttackDef HeavyAttack = new(46, 26, 40, 3.3f, 0.1f, 25f, 0.25f);

    /// <summary>Two quick stabs, right hand then left, each landing on its own.</summary>
    private static readonly AttackDef DaggerFlurry = new(24, 6, 9, 2.3f, 0.35f, 9f, 0.75f, 13);

    /// <summary>
    /// A leap at the enemy with both daggers raised, driving them down on landing. The blow lands
    /// on everything close round the landing spot.
    /// </summary>
    private static readonly AttackDef DaggerLeap = new(50, LeapHitAt, 32, 2.4f, -1f, 28f, 0.1f);

    /// <summary>A club's swing: slower than a sword's, but it sweeps a wide arc in front.</summary>
    private static readonly AttackDef ClubSwing = new(34, 15, 22, 3.2f, -0.15f, 12f, 0.45f);

    /// <summary>The club whirled round once at arm's length: the blow lands on everything around.</summary>
    private static readonly AttackDef ClubSpin = new(56, 26, 36, 3.4f, -1f, 28f, 0.35f);

    /// <summary>
    /// A fireball from the staff, loosed at the hit tick along the aim. Range and arc are the projectile's.
    /// It is slow and costly on purpose: about 60 % of a sword's damage a second, from 30 m away, and
    /// it drains stamina a little faster than it comes back.
    /// </summary>
    private static readonly AttackDef StaffBolt = new(54, 12, 22, 0f, 1f, 16f, 0.5f);

    /// <summary>The staff driven into the ground, raising a straight line of fire ahead.</summary>
    private static readonly AttackDef StaffWall = new(50, 22, 26, 0f, 1f, 30f, 0.2f);

    /// <summary>How fast a staff's fireball flies, how far, and how wide it bursts.</summary>
    public const float StaffBoltSpeed = 20f;
    public const float StaffBoltRange = 30f;
    public const float StaffBoltSplash = 1.6f;

    /// <summary>The staff's line of fire: its length, half its width, and how long it grows and burns.</summary>
    public const float StaffWallLength = 9f;
    public const float StaffWallHalfWidth = 0.9f;
    public const int StaffWallGrowTicks = 24;
    public const int StaffWallBurnTicks = 90;

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
    BowShot = 5,
    ClubSwing = 6,
    ClubSpin = 7,
    StaffBolt = 8,
    StaffWall = 9,
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
    Block = 16,
    Slide = 32,
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

    /// <summary>
    /// The aim to the finer grain an arrow needs: the turn in 65536ths, and how far up (positive) or
    /// down, in 65536ths of a turn. Only the server's arrows read them; movement uses Aim.
    /// </summary>
    public ushort AimYaw;
    public short AimPitch;

    public Command Command;

    /// <summary>What a command acts on, for commands that need more than the command byte (the Guild chest).</summary>
    public ushort Arg;
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

    /// <summary>Ticks the bow has been drawn so far, and how far it was drawn when the last arrow left.</summary>
    public byte DrawTicks;
    public byte ShotDraw;

    /// <summary>Endurance points: more stamina and faster stamina regeneration. The server sets it.</summary>
    public byte Endurance;

    /// <summary>Ticks of slide left, the wait before the next, and the way the slide goes.</summary>
    public byte SlideTicks;
    public byte SlideCooldown;
    public Vector3 SlideDirection;

    /// <summary>Ticks the guard has been up (0 when not blocking), and where it faces.</summary>
    public byte BlockTicks;
    public byte BlockAim;

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
        if (state.SlideCooldown > 0) state.SlideCooldown--;
        if (state.SlowTicks > 0) state.SlowTicks--;
        if (state.StaminaDelay > 0) state.StaminaDelay--;

        var landed = StepAttack(ref state, input);
        StepDash(ref state, input, wish);
        StepBlock(ref state, input);
        StartSlide(ref state, input);

        if (state.StaminaDelay == 0)
        {
            state.Stamina = MathF.Min(Attributes.MaxStamina(state), state.Stamina + Attributes.StaminaRegen(state));
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
        else if (state.SlideTicks > 0)
        {
            Slide(ref state, wish);
        }
        else
        {
            Accelerate(ref state, wish);
        }

        if (input.Buttons.HasFlag(Buttons.Jump) && state.Grounded && state.Stamina >= Tuning.JumpStamina)
        {
            // A jump out of a slide keeps its speed, so the two chain into a long, fast leap.
            state.Velocity.Y = Tuning.JumpSpeed;
            Spend(ref state, Tuning.JumpStamina);
            EndSlide(ref state);
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

    /// <summary>Ctrl while running on the ground drops into a slide along the way the player is going.</summary>
    private static void StartSlide(ref PlayerState state, in InputCmd input)
    {
        var flat = new Vector3(state.Velocity.X, 0, state.Velocity.Z);
        if (!input.Buttons.HasFlag(Buttons.Slide) || state.SlideTicks > 0 || state.SlideCooldown > 0 || !state.Grounded
            || state.DashTicks > 0 || state.Attack != AttackKind.None || state.BlockTicks > 0 || state.DrawTicks > 0
            || state.Stamina < Tuning.SlideStamina || flat.Length() < Tuning.SlideMinSpeed)
        {
            return;
        }

        state.SlideTicks = Tuning.SlideTicks;
        state.SlideDirection = Vector3.Normalize(flat);
        Spend(ref state, Tuning.SlideStamina);
    }

    /// <summary>One tick of a slide: fast at first and slowing, nudged a little towards where the player steers.</summary>
    private static void Slide(ref PlayerState state, Vector2 wish)
    {
        if (state.Attack != AttackKind.None || state.DashTicks > 0)
        {
            EndSlide(ref state);
            return;
        }

        if (wish != Vector2.Zero)
        {
            var steer = state.SlideDirection + new Vector3(wish.X, 0, wish.Y) * Tuning.SlideSteer;
            state.SlideDirection = Vector3.Normalize(steer);
        }

        var done = 1f - state.SlideTicks / (float)Tuning.SlideTicks;
        var speed = Tuning.SlideStartSpeed + (Tuning.SlideEndSpeed - Tuning.SlideStartSpeed) * done;
        state.Velocity.X = state.SlideDirection.X * speed;
        state.Velocity.Z = state.SlideDirection.Z * speed;
        state.SlideTicks--;
        if (state.SlideTicks == 0)
        {
            state.SlideCooldown = Tuning.SlideCooldownTicks;
        }
    }

    private static void EndSlide(ref PlayerState state)
    {
        if (state.SlideTicks > 0)
        {
            state.SlideTicks = 0;
            state.SlideCooldown = Tuning.SlideCooldownTicks;
        }
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

        if (state.Weapon == Weapons.Bow)
        {
            return StepBow(ref state, input, landed);
        }

        var wanted = input.Buttons.HasFlag(Buttons.Heavy) ? AttackKind.Heavy
            : input.Buttons.HasFlag(Buttons.Light) ? AttackKind.Light : AttackKind.None;
        if (wanted != AttackKind.None)
        {
            var heavy = wanted == AttackKind.Heavy;
            wanted = state.Weapon switch
            {
                Weapons.Dagger => heavy ? AttackKind.DaggerLeap : AttackKind.DaggerFlurry,
                Weapons.Club => heavy ? AttackKind.ClubSpin : AttackKind.ClubSwing,
                Weapons.Staff => heavy ? AttackKind.StaffWall : AttackKind.StaffBolt,
                _ => wanted,
            };
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

    /// <summary>
    /// The bow: holding the attack button draws it, up to a full draw; letting go looses the arrow
    /// with the draw it had, in the direction the player aims.
    /// </summary>
    private static AttackKind StepBow(ref PlayerState state, in InputCmd input, AttackKind landed)
    {
        var holding = input.Buttons.HasFlag(Buttons.Light);
        if (state.DrawTicks == 0)
        {
            // A new draw waits until the last arrow's follow-through and nocking are over.
            if (holding && state.Attack == AttackKind.None && state.Stamina >= Tuning.ShotStamina)
            {
                state.DrawTicks = 1;
            }
        }
        else if (holding || state.DrawTicks < Tuning.MinDraw)
        {
            state.DrawTicks = (byte)Math.Min(state.DrawTicks + 1, Tuning.FullDraw);
        }
        else
        {
            state.Attack = AttackKind.BowShot;
            state.AttackTick = 0;
            state.AttackAim = input.Aim;
            state.ShotDraw = state.DrawTicks;
            state.DrawTicks = 0;
            Spend(ref state, Tuning.ShotStamina);
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

    /// <summary>
    /// Holding block raises the guard, unless the player is mid-attack, dashing or out of stamina. It
    /// faces where the player aims and counts the ticks it has been up, for the parry window.
    /// </summary>
    private static void StepBlock(ref PlayerState state, in InputCmd input)
    {
        var free = state.Attack == AttackKind.None && state.DashTicks == 0 && state.DrawTicks == 0 && state.Stamina > 0f;
        if (!input.Buttons.HasFlag(Buttons.Block) || !free)
        {
            state.BlockTicks = 0;
            return;
        }

        state.BlockAim = input.Aim;
        if (state.BlockTicks < byte.MaxValue)
        {
            state.BlockTicks++;
        }
    }

    private static void Accelerate(ref PlayerState state, Vector2 wish)
    {
        var speed = Tuning.MoveSpeed;
        if (state.SlowTicks > 0)
        {
            speed *= Tuning.SlowFactor;
        }

        if (state.BlockTicks > 0)
        {
            speed *= Tuning.BlockMoveScale;
        }

        if (state.Attack != AttackKind.None)
        {
            speed *= Tuning.Attack(state.Attack).MoveScale;
        }

        if (state.DrawTicks > 0)
        {
            speed *= Tuning.DrawMoveScale;
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
