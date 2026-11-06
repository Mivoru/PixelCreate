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
    EnemyAction Action, byte HitFlash, Vector3 LeapTarget, bool Fighting);

public readonly record struct SpitView(int Id, Vector3 Position);

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
    public RemotePlayer[] Others = Array.Empty<RemotePlayer>();
    public EnemyView[] Enemies = Array.Empty<EnemyView>();
    public SpitView[] Spits = Array.Empty<SpitView>();
}

/// <summary>Binary encoding of the two packets the game sends.</summary>
public static class Protocol
{
    /// <summary>Inputs repeated in every packet, so a lost packet costs nothing.</summary>
    public const int InputRedundancy = 8;

    private const byte InputPacket = 1;
    private const byte SnapshotPacket = 2;
    private const int InputSize = 8;

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
            WriteVector(writer, enemy.LeapTarget);
            writer.Write(enemy.Fighting);
        }

        writer.Write((byte)snapshot.Spits.Length);
        foreach (var spit in snapshot.Spits)
        {
            writer.Write(spit.Id);
            WriteVector(writer, spit.Position);
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
        snapshot.Others = new RemotePlayer[reader.ReadByte()];
        for (var i = 0; i < snapshot.Others.Length; i++)
        {
            snapshot.Others[i] = new RemotePlayer(reader.ReadInt32(), ReadVector(reader), ReadVector(reader),
                reader.ReadByte(), (AttackKind)reader.ReadByte(), reader.ReadByte());
        }

        snapshot.Enemies = new EnemyView[reader.ReadByte()];
        for (var i = 0; i < snapshot.Enemies.Length; i++)
        {
            snapshot.Enemies[i] = new EnemyView(reader.ReadInt32(), reader.ReadByte(), reader.ReadByte(), ReadVector(reader),
                reader.ReadSingle(), reader.ReadInt16(), reader.ReadInt16(), (EnemyAction)reader.ReadByte(), reader.ReadByte(),
                ReadVector(reader), reader.ReadBoolean());
        }

        snapshot.Spits = new SpitView[reader.ReadByte()];
        for (var i = 0; i < snapshot.Spits.Length; i++)
        {
            snapshot.Spits[i] = new SpitView(reader.ReadInt32(), ReadVector(reader));
        }

        return true;
    }

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
