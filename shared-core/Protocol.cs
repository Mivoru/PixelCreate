using System.Numerics;

namespace PixelCreate.Core;

/// <summary>Moves bytes between peers. Delivery is unreliable and packets can arrive out of order.</summary>
public interface ITransport
{
    void Send(int peer, byte[] data);
    bool TryReceive(out int peer, out byte[] data);
}

public readonly record struct RemotePlayer(int Id, Vector3 Position, Vector3 Velocity, byte Aim, AttackKind Attack, byte AttackTick);

public readonly record struct EnemyView(
    int Id, byte Type, byte Level, Vector3 Position, float Facing, short Health, short MaxHealth,
    EnemyAction Action, byte HitFlash, Vector3 LeapTarget, bool Fighting, byte Ability = 0);

public readonly record struct ProjectileView(int Id, ProjectileKind Kind, Vector3 Position);

public readonly record struct FireWallView(int Id, Vector3 Origin, float Yaw, float Length);

public readonly record struct CrystalView(int Id, CrystalRarity Rarity, Vector3 Position);

public sealed class Snapshot
{
    public uint ServerTick;
    public int YourId;

    /// <summary>The newest client tick that is included in <see cref="Self"/>.</summary>
    public uint AckTick;

    /// <summary>Inputs the server holds for this client. The client runs faster or slower to keep it steady.</summary>
    public byte BufferedInputs;
    public PlayerState Self;
    public byte Level = 1;
    public int Xp;
    public int XpToNext;
    public short MaxHealth = Tuning.PlayerHealth;

    /// <summary>Counts attacks avoided with a dash, so the client can show each one.</summary>
    public byte Dodges;
    public GuildRank Rank;
    public int Gold;
    public byte QuestId;
    public byte QuestProgress;

    /// <summary>Magic crystals held, by rarity.</summary>
    public int[] Crystals = new int[Core.Crystals.Tiers];
    public RemotePlayer[] Others = Array.Empty<RemotePlayer>();
    public EnemyView[] Enemies = Array.Empty<EnemyView>();
    public ProjectileView[] Projectiles = Array.Empty<ProjectileView>();
    public CrystalView[] Drops = Array.Empty<CrystalView>();
    public FireWallView[] FireWalls = Array.Empty<FireWallView>();
}

/// <summary>Binary encoding of the two packets the game sends.</summary>
public static class Protocol
{
    /// <summary>Inputs repeated in every packet, so a lost packet costs nothing.</summary>
    public const int InputRedundancy = 8;

    private const byte InputPacket = 1;
    private const byte SnapshotPacket = 2;
    private const int InputSize = 9;

    public static byte[] EncodeInputs(ReadOnlySpan<InputCmd> commands)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(InputPacket);
        writer.Write((byte)commands.Length);
        foreach (var command in commands)
        {
            writer.Write(command.Tick);
            writer.Write(command.MoveX);
            writer.Write(command.MoveZ);
            writer.Write((byte)command.Buttons);
            writer.Write(command.Aim);
            writer.Write((byte)command.Command);
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static bool TryDecodeInputs(byte[] data, out InputCmd[] commands)
    {
        commands = Array.Empty<InputCmd>();
        if (data.Length < 2 || data[0] != InputPacket || data.Length != 2 + data[1] * InputSize)
        {
            return false;
        }

        using var reader = new BinaryReader(new MemoryStream(data, 2, data.Length - 2));
        commands = new InputCmd[data[1]];
        for (var i = 0; i < commands.Length; i++)
        {
            commands[i] = new InputCmd
            {
                Tick = reader.ReadUInt32(),
                MoveX = reader.ReadSByte(),
                MoveZ = reader.ReadSByte(),
                Buttons = (Buttons)reader.ReadByte(),
                Aim = reader.ReadByte(),
                Command = (Command)reader.ReadByte(),
            };
        }

        return true;
    }

    public static byte[] EncodeSnapshot(Snapshot snapshot)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(SnapshotPacket);
        writer.Write(snapshot.ServerTick);
        writer.Write(snapshot.YourId);
        writer.Write(snapshot.AckTick);
        writer.Write(snapshot.BufferedInputs);
        WriteState(writer, snapshot.Self);
        writer.Write(snapshot.Level);
        writer.Write(snapshot.Xp);
        writer.Write(snapshot.XpToNext);
        writer.Write(snapshot.MaxHealth);
        writer.Write(snapshot.Dodges);
        writer.Write((byte)snapshot.Rank);
        writer.Write(snapshot.Gold);
        writer.Write(snapshot.QuestId);
        writer.Write(snapshot.QuestProgress);
        foreach (var count in snapshot.Crystals)
        {
            writer.Write((ushort)count);
        }

        writer.Write((byte)snapshot.Others.Length);
        foreach (var other in snapshot.Others)
        {
            writer.Write(other.Id);
            WriteVector(writer, other.Position);
            WriteVector(writer, other.Velocity);
            writer.Write(other.Aim);
            writer.Write((byte)other.Attack);
            writer.Write(other.AttackTick);
        }

        writer.Write((byte)snapshot.Enemies.Length);
        foreach (var enemy in snapshot.Enemies)
        {
            writer.Write(enemy.Id);
            writer.Write(enemy.Type);
            writer.Write(enemy.Level);
            WriteVector(writer, enemy.Position);
            writer.Write(enemy.Facing);
            writer.Write(enemy.Health);
            writer.Write(enemy.MaxHealth);
            writer.Write((byte)enemy.Action);
            writer.Write(enemy.HitFlash);
            writer.Write(enemy.Fighting);
            writer.Write(enemy.Ability);

            // Only an enemy about to strike needs its aim point, which saves most of the packet.
            if (ShowsTarget(enemy.Action))
            {
                WriteVector(writer, enemy.LeapTarget);
            }
        }

        writer.Write((byte)snapshot.Projectiles.Length);
        foreach (var shot in snapshot.Projectiles)
        {
            writer.Write(shot.Id);
            writer.Write((byte)shot.Kind);
            WriteVector(writer, shot.Position);
        }

        writer.Write((byte)snapshot.Drops.Length);
        foreach (var crystal in snapshot.Drops)
        {
            writer.Write(crystal.Id);
            writer.Write((byte)crystal.Rarity);
            WriteVector(writer, crystal.Position);
        }

        writer.Write((byte)snapshot.FireWalls.Length);
        foreach (var wall in snapshot.FireWalls)
        {
            writer.Write(wall.Id);
            WriteVector(writer, wall.Origin);
            writer.Write(wall.Yaw);
            writer.Write(wall.Length);
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static bool TryDecodeSnapshot(byte[] data, out Snapshot snapshot)
    {
        snapshot = new Snapshot();
        if (data.Length == 0 || data[0] != SnapshotPacket)
        {
            return false;
        }

        using var reader = new BinaryReader(new MemoryStream(data, 1, data.Length - 1));
        snapshot.ServerTick = reader.ReadUInt32();
        snapshot.YourId = reader.ReadInt32();
        snapshot.AckTick = reader.ReadUInt32();
        snapshot.BufferedInputs = reader.ReadByte();
        snapshot.Self = ReadState(reader);
        snapshot.Level = reader.ReadByte();
        snapshot.Xp = reader.ReadInt32();
        snapshot.XpToNext = reader.ReadInt32();
        snapshot.MaxHealth = reader.ReadInt16();
        snapshot.Dodges = reader.ReadByte();
        snapshot.Rank = (GuildRank)reader.ReadByte();
        snapshot.Gold = reader.ReadInt32();
        snapshot.QuestId = reader.ReadByte();
        snapshot.QuestProgress = reader.ReadByte();
        for (var i = 0; i < snapshot.Crystals.Length; i++)
        {
            snapshot.Crystals[i] = reader.ReadUInt16();
        }

        snapshot.Others = new RemotePlayer[reader.ReadByte()];
        for (var i = 0; i < snapshot.Others.Length; i++)
        {
            snapshot.Others[i] = new RemotePlayer(reader.ReadInt32(), ReadVector(reader), ReadVector(reader),
                reader.ReadByte(), (AttackKind)reader.ReadByte(), reader.ReadByte());
        }

        snapshot.Enemies = new EnemyView[reader.ReadByte()];
        for (var i = 0; i < snapshot.Enemies.Length; i++)
        {
            var id = reader.ReadInt32();
            var type = reader.ReadByte();
            var level = reader.ReadByte();
            var position = ReadVector(reader);
            var facing = reader.ReadSingle();
            var health = reader.ReadInt16();
            var maxHealth = reader.ReadInt16();
            var action = (EnemyAction)reader.ReadByte();
            var flash = reader.ReadByte();
            var fighting = reader.ReadBoolean();
            var ability = reader.ReadByte();
            var target = ShowsTarget(action) ? ReadVector(reader) : Vector3.Zero;
            snapshot.Enemies[i] = new EnemyView(id, type, level, position, facing, health, maxHealth, action, flash, target, fighting, ability);
        }

        snapshot.Projectiles = new ProjectileView[reader.ReadByte()];
        for (var i = 0; i < snapshot.Projectiles.Length; i++)
        {
            snapshot.Projectiles[i] = new ProjectileView(reader.ReadInt32(), (ProjectileKind)reader.ReadByte(), ReadVector(reader));
        }

        snapshot.Drops = new CrystalView[reader.ReadByte()];
        for (var i = 0; i < snapshot.Drops.Length; i++)
        {
            snapshot.Drops[i] = new CrystalView(reader.ReadInt32(), (CrystalRarity)reader.ReadByte(), ReadVector(reader));
        }

        snapshot.FireWalls = new FireWallView[reader.ReadByte()];
        for (var i = 0; i < snapshot.FireWalls.Length; i++)
        {
            snapshot.FireWalls[i] = new FireWallView(reader.ReadInt32(), ReadVector(reader), reader.ReadSingle(), reader.ReadSingle());
        }

        return true;
    }

    private static bool ShowsTarget(EnemyAction action) =>
        action is EnemyAction.Windup or EnemyAction.Leap or EnemyAction.Cast;

    private static void WriteVector(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    private static Vector3 ReadVector(BinaryReader reader) =>
        new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    private static void WriteState(BinaryWriter writer, in PlayerState state)
    {
        WriteVector(writer, state.Position);
        WriteVector(writer, state.Velocity);
        WriteVector(writer, state.DashDirection);
        writer.Write(state.Stamina);
        writer.Write(state.Grounded);
        writer.Write(state.DashTicks);
        writer.Write(state.DashCooldown);
        writer.Write(state.StaminaDelay);
        writer.Write(state.SlowTicks);
        writer.Write((byte)state.Attack);
        writer.Write(state.AttackTick);
        writer.Write(state.AttackAim);
        writer.Write(state.Health);
    }

    private static PlayerState ReadState(BinaryReader reader) => new()
    {
        Position = ReadVector(reader),
        Velocity = ReadVector(reader),
        DashDirection = ReadVector(reader),
        Stamina = reader.ReadSingle(),
        Grounded = reader.ReadBoolean(),
        DashTicks = reader.ReadByte(),
        DashCooldown = reader.ReadByte(),
        StaminaDelay = reader.ReadByte(),
        SlowTicks = reader.ReadByte(),
        Attack = (AttackKind)reader.ReadByte(),
        AttackTick = reader.ReadByte(),
        AttackAim = reader.ReadByte(),
        Health = reader.ReadInt16(),
    };
}
