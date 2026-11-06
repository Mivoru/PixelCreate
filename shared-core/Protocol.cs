using System.Numerics;

namespace PixelCreate.Core;

/// <summary>Moves bytes between peers. Delivery is unreliable and packets can arrive out of order.</summary>
public interface ITransport
{
    void Send(int peer, byte[] data);
    bool TryReceive(out int peer, out byte[] data);
}

public readonly record struct RemotePlayer(int Id, Vector3 Position);

public readonly record struct EnemyView(int Id, Vector3 Position, short Health);

public sealed class Snapshot
{
    public uint ServerTick;
    public int YourId;

    /// <summary>The newest client tick that is included in <see cref="Self"/>.</summary>
    public uint AckTick;

    /// <summary>Inputs the server holds for this client. The client runs faster or slower to keep it steady.</summary>
    public byte BufferedInputs;
    public PlayerState Self;
    public RemotePlayer[] Others = Array.Empty<RemotePlayer>();
    public EnemyView[] Enemies = Array.Empty<EnemyView>();
}

/// <summary>Binary encoding of the two packets the game sends.</summary>
public static class Protocol
{
    /// <summary>Inputs repeated in every packet, so a lost packet costs nothing.</summary>
    public const int InputRedundancy = 8;

    private const byte InputPacket = 1;
    private const byte SnapshotPacket = 2;

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
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static bool TryDecodeInputs(byte[] data, out InputCmd[] commands)
    {
        commands = Array.Empty<InputCmd>();
        if (data.Length < 2 || data[0] != InputPacket || data.Length != 2 + data[1] * 7)
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
        writer.Write((byte)snapshot.Others.Length);
        foreach (var other in snapshot.Others)
        {
            writer.Write(other.Id);
            WriteVector(writer, other.Position);
        }

        writer.Write((byte)snapshot.Enemies.Length);
        foreach (var enemy in snapshot.Enemies)
        {
            writer.Write(enemy.Id);
            WriteVector(writer, enemy.Position);
            writer.Write(enemy.Health);
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
        snapshot.Others = new RemotePlayer[reader.ReadByte()];
        for (var i = 0; i < snapshot.Others.Length; i++)
        {
            snapshot.Others[i] = new RemotePlayer(reader.ReadInt32(), ReadVector(reader));
        }

        snapshot.Enemies = new EnemyView[reader.ReadByte()];
        for (var i = 0; i < snapshot.Enemies.Length; i++)
        {
            snapshot.Enemies[i] = new EnemyView(reader.ReadInt32(), ReadVector(reader), reader.ReadInt16());
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
        writer.Write(state.Grounded);
        writer.Write(state.DashTicks);
        writer.Write(state.DashCooldown);
        writer.Write(state.AttackCooldown);
        writer.Write(state.StunTicks);
        writer.Write(state.Health);
    }

    private static PlayerState ReadState(BinaryReader reader) => new()
    {
        Position = ReadVector(reader),
        Velocity = ReadVector(reader),
        DashDirection = ReadVector(reader),
        Grounded = reader.ReadBoolean(),
        DashTicks = reader.ReadByte(),
        DashCooldown = reader.ReadByte(),
        AttackCooldown = reader.ReadByte(),
        StunTicks = reader.ReadByte(),
        Health = reader.ReadInt16(),
    };
}
