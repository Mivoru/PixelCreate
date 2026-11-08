using System.Numerics;

namespace PixelCreate.Core;

/// <summary>Moves bytes between peers. Delivery is unreliable and packets can arrive out of order.</summary>
public interface ITransport
{
    void Send(int peer, byte[] data);
    bool TryReceive(out int peer, out byte[] data);
}

public readonly record struct RemotePlayer(int Id, Vector3 Position, Vector3 Velocity, byte Aim, AttackKind Attack, byte AttackTick, byte Weapon = 0);

public readonly record struct EnemyView(
    int Id, byte Type, byte Level, Vector3 Position, float Facing, short Health, short MaxHealth,
    EnemyAction Action, byte HitFlash, Vector3 LeapTarget, bool Fighting, byte Ability = 0);

public readonly record struct ProjectileView(int Id, ProjectileKind Kind, Vector3 Position);

/// <param name="HalfWidth">Half the width of a player's straight line of fire; 0 for the shaman's V.</param>
public readonly record struct FireWallView(int Id, Vector3 Origin, float Yaw, float Length, float HalfWidth = 0f, HazardKind Kind = HazardKind.Fire);

/// <summary>
/// Something lying on the ground for one player: a crystal or another item from items.json. Gear
/// carries its rarity, so the client can colour its glow; anything else has no rarity.
/// </summary>
public readonly record struct LootView(int Id, ushort Item, Vector3 Position, byte Rarity = LootView.NoRarity)
{
    public const byte NoRarity = 255;
}

/// <summary>
/// One place in a player's gear list: the piece there, or none when the place is empty, and
/// whether the player wears it.
/// </summary>
public readonly record struct GearEntry(byte Index, GearPiece? Piece, bool Worn);

/// <summary>How many of one item a player holds. A count of zero means the player no longer has it.</summary>
public readonly record struct BagEntry(ushort Item, ushort Count);

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

    /// <summary>Counts blows taken on the guard and blows parried, the same way.</summary>
    public byte Blocks;
    public byte Parries;
    public GuildRank Rank;
    public int Gold;
    public byte QuestId;
    public byte QuestProgress;

    /// <summary>Magic crystals held, by rarity.</summary>
    public int[] Crystals = new int[Core.Crystals.Tiers];
    public RemotePlayer[] Others = Array.Empty<RemotePlayer>();
    public EnemyView[] Enemies = Array.Empty<EnemyView>();
    public ProjectileView[] Projectiles = Array.Empty<ProjectileView>();
    public LootView[] Drops = Array.Empty<LootView>();

    /// <summary>
    /// Part of the bag: changed items first, then a rotating slice of the rest, so the whole bag
    /// reaches the client over a few snapshots without making any one of them big.
    /// </summary>
    public BagEntry[] Bag = Array.Empty<BagEntry>();

    /// <summary>Part of the gear list, sent the same way as the bag.</summary>
    public GearEntry[] Gear = Array.Empty<GearEntry>();
    public FireWallView[] FireWalls = Array.Empty<FireWallView>();

    /// <summary>
    /// The player's Guild chest, sent only while they stand by it, in slices like the bag: stacks by
    /// item, and gear pieces by their place in the chest.
    /// </summary>
    public BagEntry[] Storage = Array.Empty<BagEntry>();
    public GearEntry[] StoredGear = Array.Empty<GearEntry>();
}

/// <summary>Binary encoding of the two packets the game sends.</summary>
public static class Protocol
{
    /// <summary>Inputs repeated in every packet, so a lost packet costs nothing.</summary>
    public const int InputRedundancy = 8;

    private const byte InputPacket = 1;
    private const byte SnapshotPacket = 2;
    private const int InputSize = 15;

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
            writer.Write(command.AimYaw);
            writer.Write(command.AimPitch);
            writer.Write((byte)command.Command);
            writer.Write(command.Arg);
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
                AimYaw = reader.ReadUInt16(),
                AimPitch = reader.ReadInt16(),
                Command = (Command)reader.ReadByte(),
                Arg = reader.ReadUInt16(),
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
        writer.Write(snapshot.Blocks);
        writer.Write(snapshot.Parries);
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
            writer.Write(other.Weapon);
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
        foreach (var drop in snapshot.Drops)
        {
            writer.Write(drop.Id);
            writer.Write(drop.Item);
            WriteVector(writer, drop.Position);
            writer.Write(drop.Rarity);
        }

        writer.Write((byte)snapshot.Bag.Length);
        foreach (var entry in snapshot.Bag)
        {
            writer.Write(entry.Item);
            writer.Write(entry.Count);
        }

        WriteGear(writer, snapshot.Gear);

        writer.Write((byte)snapshot.FireWalls.Length);
        foreach (var wall in snapshot.FireWalls)
        {
            writer.Write(wall.Id);
            WriteVector(writer, wall.Origin);
            writer.Write(wall.Yaw);
            writer.Write(wall.Length);
            writer.Write(wall.HalfWidth);
            writer.Write((byte)wall.Kind);
        }

        writer.Write((byte)snapshot.Storage.Length);
        foreach (var entry in snapshot.Storage)
        {
            writer.Write(entry.Item);
            writer.Write(entry.Count);
        }

        WriteGear(writer, snapshot.StoredGear);

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
        snapshot.Blocks = reader.ReadByte();
        snapshot.Parries = reader.ReadByte();
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
                reader.ReadByte(), (AttackKind)reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
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

        snapshot.Drops = new LootView[reader.ReadByte()];
        for (var i = 0; i < snapshot.Drops.Length; i++)
        {
            snapshot.Drops[i] = new LootView(reader.ReadInt32(), reader.ReadUInt16(), ReadVector(reader), reader.ReadByte());
        }

        snapshot.Bag = new BagEntry[reader.ReadByte()];
        for (var i = 0; i < snapshot.Bag.Length; i++)
        {
            snapshot.Bag[i] = new BagEntry(reader.ReadUInt16(), reader.ReadUInt16());
        }

        snapshot.Gear = ReadGear(reader);

        snapshot.FireWalls = new FireWallView[reader.ReadByte()];
        for (var i = 0; i < snapshot.FireWalls.Length; i++)
        {
            snapshot.FireWalls[i] = new FireWallView(reader.ReadInt32(), ReadVector(reader), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), (HazardKind)reader.ReadByte());
        }

        snapshot.Storage = new BagEntry[reader.ReadByte()];
        for (var i = 0; i < snapshot.Storage.Length; i++)
        {
            snapshot.Storage[i] = new BagEntry(reader.ReadUInt16(), reader.ReadUInt16());
        }

        snapshot.StoredGear = ReadGear(reader);

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

    private static void WriteGear(BinaryWriter writer, GearEntry[] entries)
    {
        writer.Write((byte)entries.Length);
        foreach (var entry in entries)
        {
            writer.Write(entry.Index);
            writer.Write(entry.Worn);
            writer.Write(entry.Piece is null ? ushort.MaxValue : entry.Piece.Item);
            if (entry.Piece is { } piece)
            {
                writer.Write((byte)piece.Rarity);
                writer.Write(piece.Level);
                writer.Write((byte)piece.Affixes.Length);
                foreach (var affix in piece.Affixes)
                {
                    writer.Write(affix.Affix);
                    writer.Write(affix.Value);
                }
            }
        }
    }

    private static GearEntry[] ReadGear(BinaryReader reader)
    {
        var entries = new GearEntry[reader.ReadByte()];
        for (var i = 0; i < entries.Length; i++)
        {
            var index = reader.ReadByte();
            var worn = reader.ReadBoolean();
            var item = reader.ReadUInt16();
            GearPiece? piece = null;
            if (item != ushort.MaxValue)
            {
                var rarity = (Rarity)reader.ReadByte();
                var level = reader.ReadByte();
                var affixes = new AffixRoll[reader.ReadByte()];
                for (var a = 0; a < affixes.Length; a++)
                {
                    affixes[a] = new AffixRoll(reader.ReadByte(), reader.ReadInt16());
                }

                piece = new GearPiece(item, rarity, level, affixes);
            }

            entries[i] = new GearEntry(index, piece, worn);
        }

        return entries;
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
        writer.Write(state.Weapon);
        writer.Write(state.DrawTicks);
        writer.Write(state.ShotDraw);
        writer.Write(state.BlockTicks);
        writer.Write(state.BlockAim);
        writer.Write(state.SlideTicks);
        writer.Write(state.SlideCooldown);
        WriteVector(writer, state.SlideDirection);
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
        Weapon = reader.ReadByte(),
        DrawTicks = reader.ReadByte(),
        ShotDraw = reader.ReadByte(),
        BlockTicks = reader.ReadByte(),
        BlockAim = reader.ReadByte(),
        SlideTicks = reader.ReadByte(),
        SlideCooldown = reader.ReadByte(),
        SlideDirection = ReadVector(reader),
    };
}
