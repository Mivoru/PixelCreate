using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using PixelCreate.Core;
using PixelCreate.Transport;
using Buttons = PixelCreate.Core.Buttons;
using Vector3 = Godot.Vector3;

namespace PixelCreate;

/// <summary>
/// Connects to the server and draws the zone from behind the player.
/// Arguments after "--": [host] [port] [capture folder] [frames]. With a capture folder a bot plays,
/// saves frames, prints network stats and quits. PIXEL_BOT=idle stands still; PIXEL_BOT=keys
/// presses real key events.
/// </summary>
public partial class GameClient : Node
{
    private const float CameraDistance = 5.2f;
    private const float CameraStep = 0.25f;
    private const float MinPitch = -1.1f;
    private const float MaxPitch = 0.35f;
    private const float MouseSensitivity = 0.0035f;
    private const float RemoteSmoothing = 18f;
    private const int CaptureEveryTicks = 45;

    private readonly Dictionary<int, PlayerRig> _remotes = new();
    private readonly Dictionary<int, SlimeView> _slimes = new();
    private readonly Dictionary<int, MeshInstance3D> _spits = new();
    private PixelStage _stage = null!;
    private UdpTransport _transport = null!;
    private ClientPrediction _client = null!;
    private PlayerRig _player = null!;
    private Hud _hud = null!;
    private World _world = null!;
    private Vector3 _shownBefore;
    private Vector3 _shownNow;
    private float _yaw;
    private float _pitch = -0.32f;
    private float _facing;
    private double _clock;
    private string? _captureDir;
    private int _captureFrames = 8;
    private int _captured;
    private float _maxHeight;
    private float _travelled;
    private readonly Random _botRandom = new(5);

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        var host = args.ElementAtOrDefault(0) ?? "127.0.0.1";
        var port = int.Parse(args.ElementAtOrDefault(1) ?? "7777");
        _captureDir = args.ElementAtOrDefault(2);
        _captureFrames = int.Parse(args.ElementAtOrDefault(3) ?? "8");

        var zone = Zone.Load("meadow-valley");
        _world = zone.World;
        _stage = new PixelStage(this);
        _stage.View.AddChild(ZoneView.Build(zone));
        _player = new PlayerRig(new Color("0099db"));
        _stage.View.AddChild(_player.Root);

        _hud = new Hud { Camera = _stage.Camera };
        AddChild(_hud);

        _transport = UdpTransport.Connect(host, port);
        _client = new ClientPrediction(_transport, _world);
        if (_captureDir is null)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && !_hud.InventoryOpen)
        {
            _yaw -= motion.Relative.X * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * MouseSensitivity, MinPitch, MaxPitch);
        }
        else if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Escape)
            {
                GetTree().Quit();
            }
            else if (key.Keycode == Key.I || key.Keycode == Key.Tab)
            {
                _hud.ToggleInventory();
            }
            else if (key.Keycode >= Key.Key1 && key.Keycode <= Key.Key8)
            {
                _hud.SelectHotbar((int)(key.Keycode - Key.Key1));
            }
        }
    }

    public override void _Process(double delta)
    {
        _transport.Poll();
        if (!_transport.IsConnected)
        {
            _hud.Notice = "Connecting...";
            return;
        }

        _hud.Notice = "";
        _clock += delta;
        var tickLength = Tuning.TickSeconds * _client.TickScale;
        while (_clock >= tickLength)
        {
            _clock -= tickLength;
            var (x, z, buttons) = _captureDir is null ? ReadInput() : BotInput();
            _shownBefore = _shownNow;
            _client.Step(x, z, buttons, AimByte());
            _shownNow = ToGodot(_client.ShownPosition);
            var position = _client.State.Position;
            _maxHeight = Mathf.Max(_maxHeight, position.Y - _world.GroundHeight(position.X, position.Z));
            _travelled += _shownBefore.DistanceTo(_shownNow);
            if (_captureDir is not null && _client.Tick % CaptureEveryTicks == 0)
            {
                Capture();
            }
        }

        var d = (float)delta;
        var shown = _shownBefore.Lerp(_shownNow, (float)(_clock / tickLength));
        DrawPlayer(shown, d);
        DrawSnapshot(d);
        PlaceCamera(shown);
        _hud.Player = _client.State;
        _hud.Snapshot = _client.Latest;
    }

    private byte AimByte() => (byte)Mathf.PosMod(Mathf.Round(_yaw / Mathf.Tau * 256f), 256f);

    private (sbyte X, sbyte Z, Buttons Buttons) ReadInput()
    {
        if (_hud.InventoryOpen)
        {
            return (0, 0, Buttons.None);
        }

        var side = (Input.IsKeyPressed(Key.D) ? 1f : 0f) - (Input.IsKeyPressed(Key.A) ? 1f : 0f);
        var forward = (Input.IsKeyPressed(Key.W) ? 1f : 0f) - (Input.IsKeyPressed(Key.S) ? 1f : 0f);
        var buttons = Buttons.None;
        if (Input.IsKeyPressed(Key.Space)) buttons |= Buttons.Jump;
        if (Input.IsKeyPressed(Key.Shift)) buttons |= Buttons.Dash;
        if (Input.IsMouseButtonPressed(MouseButton.Left)) buttons |= Buttons.Light;
        if (Input.IsMouseButtonPressed(MouseButton.Right)) buttons |= Buttons.Heavy;
        return ToMove(side, forward, buttons);
    }

    private (sbyte X, sbyte Z, Buttons Buttons) BotInput()
    {
        var mode = OS.GetEnvironment("PIXEL_BOT");
        if (mode == "idle")
        {
            return (0, 0, Buttons.None);
        }

        if (mode == "keys")
        {
            // Press real key events, so this checks the same path as a person at the keyboard.
            PressKey(Key.W, true);
            PressKey(Key.Space, _client.Tick % 90 < 2);
            PressKey(Key.Shift, _client.Tick % 170 < 2);
            return ReadInput();
        }

        if (_client.Tick % 60 == 0)
        {
            _yaw += (float)(_botRandom.NextDouble() - 0.5) * 2f;
        }

        var buttons = _botRandom.Next(30) == 0 ? Buttons.Light : Buttons.None;
        if (_botRandom.Next(90) == 0) buttons |= Buttons.Heavy;
        if (_botRandom.Next(80) == 0) buttons |= Buttons.Jump;
        if (_botRandom.Next(120) == 0) buttons |= Buttons.Dash;
        return ToMove(0, 1, buttons);
    }

    private static void PressKey(Key key, bool pressed)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed });
    }

    /// <summary>Turns input relative to the camera into world directions.</summary>
    private (sbyte X, sbyte Z, Buttons Buttons) ToMove(float side, float forward, Buttons buttons)
    {
        var sin = Mathf.Sin(_yaw);
        var cos = Mathf.Cos(_yaw);
        var x = side * cos - forward * sin;
        var z = -side * sin - forward * cos;
        return ((sbyte)(Mathf.Clamp(x, -1, 1) * 127), (sbyte)(Mathf.Clamp(z, -1, 1) * 127), buttons);
    }

    private void DrawPlayer(Vector3 shown, float delta)
    {
        var state = _client.State;
        _player.Root.Position = shown;
        var flat = new Vector2(state.Velocity.X, state.Velocity.Z);
        if (state.Attack != AttackKind.None)
        {
            _facing = LerpFacing(_facing, state.AttackAim / 256f * Mathf.Tau, 30f * delta);
        }
        else if (flat.LengthSquared() > 0.5f)
        {
            _facing = LerpFacing(_facing, Mathf.Atan2(-flat.X, -flat.Y), 14f * delta);
        }

        _player.Root.Rotation = new Vector3(0, _facing, 0);
        _player.SwordVisible = _hud.Items.HasWeapon;
        _player.Animate(new MotionState(flat.Length(), state.Grounded, state.Velocity.Y,
            state.DashTicks > 0, state.Attack, state.AttackTick), delta);
    }

    private static float LerpFacing(float from, float to, float amount) =>
        from + Mathf.Wrap(to - from, -Mathf.Pi, Mathf.Pi) * Mathf.Clamp(amount, 0f, 1f);

    private void DrawSnapshot(float delta)
    {
        if (_client.Latest is not { } snapshot)
        {
            return;
        }

        var blend = 1f - Mathf.Exp(-RemoteSmoothing * delta);
        foreach (var other in snapshot.Others)
        {
            if (!_remotes.TryGetValue(other.Id, out var rig))
            {
                rig = new PlayerRig(new Color("f77622"));
                _remotes.Add(other.Id, rig);
                _stage.View.AddChild(rig.Root);
            }

            rig.Root.Position = rig.Root.Position.Lerp(ToGodot(other.Position), blend);
            var flat = new Vector2(other.Velocity.X, other.Velocity.Z);
            if (flat.LengthSquared() > 0.5f)
            {
                rig.Root.Rotation = new Vector3(0, Mathf.Atan2(-flat.X, -flat.Y), 0);
            }

            rig.Animate(new MotionState(flat.Length(), Mathf.Abs(other.Velocity.Y) < 0.5f, other.Velocity.Y,
                flat.Length() > Tuning.MoveSpeed + 1f, other.Attack, other.AttackTick), delta);
        }

        _hud.EnemyPositions.Clear();
        foreach (var enemy in snapshot.Enemies)
        {
            if (!_slimes.TryGetValue(enemy.Id, out var slime))
            {
                slime = new SlimeView();
                _slimes.Add(enemy.Id, slime);
                _stage.View.AddChild(slime.Root);
            }

            var target = ToGodot(enemy.Position);
            var position = slime.Root.Position;
            // A respawn is a jump across the map, not a move.
            position = position.DistanceTo(target) > 6f ? target : position.Lerp(target, blend);
            slime.Update(enemy, position, delta);
            _hud.EnemyPositions[enemy.Id] = position;
        }

        DrawSpits(snapshot, blend);
    }

    private void DrawSpits(Snapshot snapshot, float blend)
    {
        var alive = new HashSet<int>();
        foreach (var spit in snapshot.Spits)
        {
            alive.Add(spit.Id);
            if (!_spits.TryGetValue(spit.Id, out var ball))
            {
                ball = new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = 0.22f, Height = 0.44f, RadialSegments = 8, Rings = 4 },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("f6757a"), EmissionEnabled = true, Emission = new Color("b55088") },
                    Position = ToGodot(spit.Position),
                };
                _spits.Add(spit.Id, ball);
                _stage.View.AddChild(ball);
            }

            ball.Position = ball.Position.Lerp(ToGodot(spit.Position), blend);
        }

        foreach (var id in _spits.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _spits[id].QueueFree();
            _spits.Remove(id);
        }
    }

    private void PlaceCamera(Vector3 shown)
    {
        var target = shown + new Vector3(0, 1.75f, 0);
        var flat = Mathf.Cos(_pitch);
        var back = new Vector3(Mathf.Sin(_yaw) * flat, -Mathf.Sin(_pitch), Mathf.Cos(_yaw) * flat);

        // Walk out from the player and stop before the first thing in the way.
        var distance = CameraStep;
        while (distance < CameraDistance)
        {
            var next = target + back * (distance + CameraStep);
            if (_world.IsBlocked(new System.Numerics.Vector3(next.X, next.Y, next.Z)))
            {
                break;
            }

            distance += CameraStep;
        }

        _stage.Camera.LookAtFromPosition(target + back * distance, target);
    }

    private void Capture()
    {
        GetViewport().GetTexture().GetImage().SavePng($"{_captureDir}/window-{_captured}.png");
        if (++_captured < _captureFrames)
        {
            return;
        }

        var stats = _client.Stats;
        var enemies = _client.Latest?.Enemies ?? Array.Empty<EnemyView>();
        GD.Print($"STATS snapshots={stats.Snapshots} corrections={stats.Corrections} " +
                 $"maxError={stats.MaxError:F3} buffered={_client.Latest?.BufferedInputs} " +
                 $"tickScale={_client.TickScale:F3} enemies={enemies.Length} rare={enemies.Count(e => e.Type == 1)} " +
                 $"fps={Engine.GetFramesPerSecond()} hp={_client.State.Health} stamina={_client.State.Stamina:F0} maxHeight={_maxHeight:F2} travelled={_travelled:F0}");
        GetTree().Quit();
    }

    private static Vector3 ToGodot(System.Numerics.Vector3 value) => new(value.X, value.Y, value.Z);
}
