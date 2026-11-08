using System.Numerics;

namespace PixelCreate.Core;

public sealed class ClientStats
{
    public long Snapshots;
    public long Corrections;
    public long ResimulatedTicks;

    /// <summary>Largest gap found between prediction and the server, in metres.</summary>
    public float MaxError;

    /// <summary>Largest jump of the shown position caused by one correction, in metres.</summary>
    public float MaxVisibleSnap;

    /// <summary>Largest move of <see cref="ClientPrediction.ShownPosition"/> in one tick, in metres.</summary>
    public float MaxShownStep;
}

/// <summary>Runs the local player ahead of the server and corrects it from snapshots.</summary>
public sealed class ClientPrediction
{
    private const int HistorySize = 256;
    private const float Tolerance = 0.001f;
    private const int ServerPeer = 0;

    /// <summary>Share of a correction that stays hidden after each tick.</summary>
    private const float SmoothingDecay = 0.85f;

    /// <summary>Change of tick length per input of difference from the target buffer.</summary>
    private const float ClockGain = 0.01f;
    private const float MaxClockChange = 0.03f;
    private const float MaxSmoothedCorrection = 8f;

    private readonly ITransport _transport;
    private readonly World _world;
    private readonly InputCmd[] _inputs = new InputCmd[HistorySize];
    private readonly PlayerState[] _states = new PlayerState[HistorySize];
    private uint _lastAck;
    private bool _spawned;
    private Vector3 _visualOffset;
    private bool _teleported;

    public uint Tick { get; private set; }
    public PlayerState State;
    public Snapshot? Latest { get; private set; }

    /// <summary>The gear list as the server last described each place of it.</summary>
    public IReadOnlyCollection<GearEntry> Gear => _gear.Values;

    private readonly Dictionary<byte, GearEntry> _gear = new();
    public ClientStats Stats { get; } = new();

    /// <summary>
    /// Multiply the tick length by this. Clocks drift apart, so the client slows down when the
    /// server holds too many of its inputs, and speeds up when it holds too few.
    /// </summary>
    public float TickScale { get; private set; } = 1f;

    /// <summary>Where to draw the player: corrections fade in instead of jumping.</summary>
    public Vector3 ShownPosition => State.Position + _visualOffset;

    public ClientPrediction(ITransport transport, World world)
    {
        _transport = transport;
        _world = world;
    }

    /// <summary>Runs one client tick: reads snapshots, predicts this input and sends it.</summary>
    /// <param name="aimYaw">The fine aim for arrows, in 65536ths of a turn; by default the coarse aim.</param>
    /// <param name="aimPitch">How far up the arrows aim, in 65536ths of a turn.</param>
    public void Step(sbyte moveX, sbyte moveZ, Buttons buttons, byte aim = 0, Command request = Command.None, ushort? aimYaw = null, short aimPitch = 0,
        ushort requestArg = 0)
    {
        var shownBefore = ShownPosition;
        var wasSpawned = _spawned;
        while (_transport.TryReceive(out _, out var data))
        {
            if (Protocol.TryDecodeSnapshot(data, out var snapshot))
            {
                Apply(snapshot);
            }
        }

        Tick++;
        var command = new InputCmd
        {
            Tick = Tick, MoveX = moveX, MoveZ = moveZ, Buttons = buttons, Aim = aim, Command = request,
            AimYaw = aimYaw ?? (ushort)(aim << 8), AimPitch = aimPitch, Arg = requestArg,
        };
        _inputs[Tick % HistorySize] = command;
        if (_spawned)
        {
            Movement.Step(ref State, command, _world);
        }

        _states[Tick % HistorySize] = State;
        _visualOffset *= SmoothingDecay;
        if (wasSpawned && !_teleported)
        {
            Stats.MaxShownStep = MathF.Max(Stats.MaxShownStep, Vector3.Distance(shownBefore, ShownPosition));
        }

        _teleported = false;
        Send();
    }

    private void Send()
    {
        var count = (int)Math.Min(Tick, Protocol.InputRedundancy);
        var commands = new InputCmd[count];
        for (var i = 0; i < count; i++)
        {
            commands[i] = _inputs[(Tick - (uint)(count - 1 - i)) % HistorySize];
        }

        _transport.Send(ServerPeer, Protocol.EncodeInputs(commands));
    }

    private void Apply(Snapshot snapshot)
    {
        Stats.Snapshots++;
        if (Latest is null || snapshot.ServerTick > Latest.ServerTick)
        {
            Latest = snapshot;
            foreach (var entry in snapshot.Gear)
            {
                _gear[entry.Index] = entry;
            }

            // Only the server changes health, so it needs no prediction and no correction.
            State.Health = snapshot.Self.Health;
            var surplus = snapshot.BufferedInputs - Tuning.InputBufferTicks;
            TickScale = 1f + Math.Clamp(surplus * ClockGain, -MaxClockChange, MaxClockChange);
        }

        // Snapshots can arrive out of order. Only a newer acknowledgement says anything new.
        if (snapshot.AckTick <= _lastAck && _spawned)
        {
            return;
        }

        if (!_spawned)
        {
            _spawned = true;
            _lastAck = snapshot.AckTick;
            Rewind(snapshot, counted: false);
            return;
        }

        _lastAck = snapshot.AckTick;
        if (snapshot.AckTick > Tick || Tick - snapshot.AckTick >= HistorySize)
        {
            return;
        }

        var predicted = _states[snapshot.AckTick % HistorySize];
        var error = Vector3.Distance(predicted.Position, snapshot.Self.Position);
        if (error > Tolerance || !SameDiscreteState(predicted, snapshot.Self))
        {
            Stats.MaxError = MathF.Max(Stats.MaxError, error);
            Rewind(snapshot, counted: true);
        }
    }

    /// <summary>Takes the server state at the acknowledged tick and replays every later input.</summary>
    private void Rewind(Snapshot snapshot, bool counted)
    {
        var before = State.Position;
        State = snapshot.Self;
        _states[snapshot.AckTick % HistorySize] = State;
        for (var tick = snapshot.AckTick + 1; tick <= Tick; tick++)
        {
            Movement.Step(ref State, _inputs[tick % HistorySize], _world);
            _states[tick % HistorySize] = State;
            if (counted)
            {
                Stats.ResimulatedTicks++;
            }
        }

        if (counted)
        {
            Stats.Corrections++;
            // A respawn is a real jump. Fading it would drag the player across the world.
            var moved = before - State.Position;
            _teleported = moved.Length() > MaxSmoothedCorrection;
            _visualOffset = _teleported ? Vector3.Zero : _visualOffset + moved;
            Stats.MaxVisibleSnap = MathF.Max(Stats.MaxVisibleSnap, Vector3.Distance(before, State.Position));
        }
    }

    private static bool SameDiscreteState(in PlayerState a, in PlayerState b) =>
        a.SlowTicks == b.SlowTicks && a.DashTicks == b.DashTicks && a.DashCooldown == b.DashCooldown &&
        a.Attack == b.Attack && a.AttackTick == b.AttackTick && a.Weapon == b.Weapon && a.DrawTicks == b.DrawTicks &&
        MathF.Abs(a.Stamina - b.Stamina) < 0.01f;
}
