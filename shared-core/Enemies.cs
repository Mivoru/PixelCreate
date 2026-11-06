using System.Numerics;
using System.Reflection;
using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>An enemy type, loaded from world/enemies.json.</summary>
public sealed record EnemyDef(
    string Id, string Name, int MinLevel, int MaxLevel, int Health, int HealthPerLevel, float Speed, float Size,
    int HopTicks, float AggroRange, float LeapRange, int LeapCooldownTicks, float SplashRadius, short SplashDamage,
    byte SlowTicks, float SpitRange, short SpitDamage, int SpitCooldownTicks, double RareChance)
{
    private static readonly Lazy<IReadOnlyList<EnemyDef>> Loaded = new(Load);

    public static IReadOnlyList<EnemyDef> All => Loaded.Value;

    private static IReadOnlyList<EnemyDef> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("enemies")!;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<EnemyDef[]>(stream, options)!;
    }
}

public readonly record struct EnemySpawn(Vector3 Center, float Radius, int Count);

public enum EnemyAction : byte
{
    Idle = 0,
    Hop = 1,
    Windup = 2,
    Leap = 3,
    Splat = 4,
    Spit = 5,
    Dead = 6,
}

/// <summary>The nametag colour rule: how an enemy compares with the player.</summary>
public enum Threat
{
    Weak,
    Even,
    Strong,
}

public static class ThreatRules
{
    public static Threat Compare(int enemyLevel, int playerLevel) =>
        enemyLevel > playerLevel ? Threat.Strong : enemyLevel < playerLevel ? Threat.Weak : Threat.Even;
}

/// <summary>One enemy on the server. It hops, leaps at players, splashes on landing and may spit.</summary>
public sealed class Enemy
{
    private const int WindupTicks = 26;
    private const int SpitWindupTicks = 20;
    private const int SplatTicks = 26;
    private const int DeadTicks = 240;
    private const int LeapAirTicks = 34;
    private const float HopSpeed = 5.5f;

    public required int Id;
    public required byte Type;
    public required EnemySpawn Home;
    public byte Level;
    public Vector3 Position;
    public Vector3 Velocity;
    public short Health;
    public EnemyAction Action;
    public int ActionTicks;
    public int LeapCooldown;
    public int SpitCooldown;
    public byte HitFlash;
    public float Facing;

    public EnemyDef Def => EnemyDef.All[Type];
    public int MaxHealth => Def.Health + Def.HealthPerLevel * (Level - Def.MinLevel);
    public bool Alive => Action != EnemyAction.Dead;

    public void Reset(Random random, World world)
    {
        var rare = EnemyDef.All.FirstOrDefault(d => d.RareChance > 0);
        var chosen = rare is not null && random.NextDouble() < rare.RareChance ? rare : EnemyDef.All[0];
        Type = (byte)IndexOf(chosen);
        Level = (byte)random.Next(chosen.MinLevel, chosen.MaxLevel + 1);
        var angle = (float)(random.NextDouble() * Math.Tau);
        var reach = (float)random.NextDouble() * Home.Radius;
        var spot = Home.Center + new Vector3(MathF.Cos(angle) * reach, 0, MathF.Sin(angle) * reach);
        spot.Y = world.GroundHeight(spot.X, spot.Z);
        Position = spot;
        Velocity = Vector3.Zero;
        Health = (short)MaxHealth;
        Action = EnemyAction.Idle;
        ActionTicks = random.Next(10, 60);
        LeapCooldown = 60;
        SpitCooldown = 60;
    }

    private static int IndexOf(EnemyDef def)
    {
        for (var i = 0; i < EnemyDef.All.Count; i++)
        {
            if (EnemyDef.All[i] == def) return i;
        }

        return 0;
    }

    /// <summary>Advances the enemy and reports what it did through the callbacks.</summary>
    public void Step(World world, Random random, Vector3? target, Action<Enemy> splash, Action<Enemy, Vector3> spit)
    {
        if (HitFlash > 0) HitFlash--;
        if (LeapCooldown > 0) LeapCooldown--;
        if (SpitCooldown > 0) SpitCooldown--;
        ActionTicks--;

        switch (Action)
        {
            case EnemyAction.Dead:
                if (ActionTicks <= 0)
                {
                    Reset(random, world);
                }

                return;
            case EnemyAction.Idle:
                Velocity *= 0.8f;
                if (ActionTicks <= 0)
                {
                    Decide(world, random, target);
                }

                break;
            case EnemyAction.Windup:
                Velocity = Vector3.Zero;
                if (ActionTicks <= 0)
                {
                    Launch(world, target);
                }

                break;
            case EnemyAction.Spit:
                Velocity = Vector3.Zero;
                if (ActionTicks <= 0)
                {
                    if (target is { } aim)
                    {
                        spit(this, aim);
                    }

                    ToIdle(random);
                }

                break;
            case EnemyAction.Splat:
                Velocity = Vector3.Zero;
                if (ActionTicks <= 0)
                {
                    ToIdle(random);
                }

                break;
        }

        Fall(world, random, splash);
    }

    public void TakeHit(short damage, Vector3 from)
    {
        Health -= damage;
        HitFlash = 10;
        var away = Position - from;
        away.Y = 0;
        if (away != Vector3.Zero)
        {
            Velocity += Vector3.Normalize(away) * 5f + Vector3.UnitY * 3f;
        }

        if (Action == EnemyAction.Windup || Action == EnemyAction.Spit)
        {
            Action = EnemyAction.Hop;
        }

        if (Health <= 0)
        {
            Action = EnemyAction.Dead;
            ActionTicks = DeadTicks;
            Velocity = Vector3.Zero;
        }
    }

    private void ToIdle(Random random)
    {
        Action = EnemyAction.Idle;
        ActionTicks = random.Next(8, Def.HopTicks);
    }

    private void Decide(World world, Random random, Vector3? target)
    {
        if (target is { } aim && Vector3.Distance(aim, Position) < Def.AggroRange)
        {
            var distance = Vector3.Distance(aim, Position);
            Face(aim);
            if (distance < Def.LeapRange && LeapCooldown == 0)
            {
                Action = EnemyAction.Windup;
                ActionTicks = WindupTicks;
                LeapCooldown = Def.LeapCooldownTicks;
                return;
            }

            if (Def.SpitRange > 0 && distance < Def.SpitRange && SpitCooldown == 0)
            {
                Action = EnemyAction.Spit;
                ActionTicks = SpitWindupTicks;
                SpitCooldown = Def.SpitCooldownTicks;
                return;
            }

            Hop(world, Vector3.Normalize(Flat(aim - Position)));
            return;
        }

        var toHome = Flat(Home.Center - Position);
        var drift = toHome.Length() > Home.Radius ? Vector3.Normalize(toHome)
            : new Vector3((float)random.NextDouble() - 0.5f, 0, (float)random.NextDouble() - 0.5f);
        if (drift == Vector3.Zero)
        {
            ToIdle(random);
            return;
        }

        Hop(world, Vector3.Normalize(drift));
    }

    private void Hop(World world, Vector3 direction)
    {
        if (float.IsNaN(direction.X))
        {
            direction = Vector3.UnitX;
        }

        Facing = MathF.Atan2(-direction.X, -direction.Z);
        Action = EnemyAction.Hop;
        ActionTicks = Def.HopTicks;
        Velocity = direction * Def.Speed + Vector3.UnitY * HopSpeed;
    }

    private void Launch(World world, Vector3? target)
    {
        var aim = target ?? Position;
        Face(aim);
        var air = LeapAirTicks * Tuning.TickSeconds;
        var flat = Flat(aim - Position);
        Velocity = flat / air;
        Velocity.Y = Tuning.Gravity * air / 2f;
        Action = EnemyAction.Leap;
        ActionTicks = LeapAirTicks * 2;
    }

    private void Face(Vector3 aim)
    {
        var flat = Flat(aim - Position);
        if (flat != Vector3.Zero)
        {
            Facing = MathF.Atan2(-flat.X, -flat.Z);
        }
    }

    private static Vector3 Flat(Vector3 value) => value with { Y = 0 };

    private void Fall(World world, Random random, Action<Enemy> splash)
    {
        var next = Position + Flat(Velocity) * Tuning.TickSeconds;
        var probe = next + Vector3.UnitY * 0.6f;
        if (world.IsBlocked(probe) && next.Y >= world.GroundHeight(next.X, next.Z) - 0.05f)
        {
            Velocity.X = 0;
            Velocity.Z = 0;
            next = Position;
        }

        Velocity.Y -= Tuning.Gravity * Tuning.TickSeconds;
        next.Y = Position.Y + Velocity.Y * Tuning.TickSeconds;
        var ground = world.GroundHeight(next.X, next.Z);
        var landed = next.Y <= ground;
        if (landed)
        {
            next.Y = ground;
            Velocity.Y = 0;
        }

        Position = next;
        if (!landed)
        {
            return;
        }

        if (Action == EnemyAction.Leap)
        {
            Action = EnemyAction.Splat;
            ActionTicks = SplatTicks;
            splash(this);
        }
        else if (Action == EnemyAction.Hop)
        {
            ToIdle(random);
        }
    }
}
