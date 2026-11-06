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
/// Connects to the server and draws the arena from behind the player.
/// Arguments after "--": [host] [port] [capture folder]. With a capture folder a simple bot plays,
/// saves frames, prints network stats and quits.
/// </summary>
public partial class GameClient : Node
{
    private const float CameraDistance = 6.5f;
    private const float CameraHeight = 2.6f;
    private const float MouseSensitivity = 0.0035f;
    private const float RemoteSmoothing = 18f;
    private const int CaptureFrames = 8;
    private const int CaptureEveryTicks = 45;

    private readonly Dictionary<int, Node3D> _remotePlayers = new();
    private readonly Dictionary<int, Node3D> _enemies = new();
    private PixelStage _stage = null!;
    private UdpTransport _transport = null!;
    private ClientPrediction _client = null!;
    private Node3D _player = null!;
    private Label _status = null!;
    private Vector3 _shownBefore;
    private Vector3 _shownNow;
    private float _yaw;
    private double _clock;
    private string? _captureDir;
    private int _captured;
    private readonly Random _botRandom = new(5);

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        var host = args.ElementAtOrDefault(0) ?? "127.0.0.1";
        var port = int.Parse(args.ElementAtOrDefault(1) ?? "7777");
        _captureDir = args.ElementAtOrDefault(2);

        var world = World.Arena();
        _stage = new PixelStage(this);
        _stage.View.AddChild(ArenaView.Build(world));
        _player = ArenaView.BuildPlayer(new Color("0099db"));
        _stage.View.AddChild(_player);

        _status = new Label { Position = new Vector2(12, 8) };
        AddChild(_status);

        _transport = UdpTransport.Connect(host, port);
        _client = new ClientPrediction(_transport, world);
        if (_captureDir is null)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
        {
            _yaw -= motion.Relative.X * MouseSensitivity;
        }
        else if (@event is InputEventKey { Keycode: Key.Escape, Pressed: true })
        {
            GetTree().Quit();
        }
    }

    public override void _Process(double delta)
    {
        _transport.Poll();
        if (!_transport.IsConnected)
        {
            _status.Text = "Connecting...";
            return;
        }

        _clock += delta;
        var tickLength = Tuning.TickSeconds * _client.TickScale;
        while (_clock >= tickLength)
        {
            _clock -= tickLength;
            var (x, z, buttons) = _captureDir is null ? ReadInput() : BotInput();
            _shownBefore = _shownNow;
            _client.Step(x, z, buttons);
            _shownNow = ToGodot(_client.ShownPosition);
            if (_captureDir is not null && _client.Tick % CaptureEveryTicks == 0)
            {
                Capture();
            }
        }

        var shown = _shownBefore.Lerp(_shownNow, (float)(_clock / tickLength));
        DrawPlayer(shown);
        DrawSnapshot((float)delta);
        PlaceCamera(shown);
        _status.Text = $"HP {_client.State.Health}   corrections {_client.Stats.Corrections}";
    }

    private (sbyte X, sbyte Z, Buttons Buttons) ReadInput()
    {
        var side = (Input.IsKeyPressed(Key.D) ? 1f : 0f) - (Input.IsKeyPressed(Key.A) ? 1f : 0f);
        var forward = (Input.IsKeyPressed(Key.W) ? 1f : 0f) - (Input.IsKeyPressed(Key.S) ? 1f : 0f);
        var buttons = Buttons.None;
        if (Input.IsKeyPressed(Key.Space)) buttons |= Buttons.Jump;
        if (Input.IsKeyPressed(Key.Shift)) buttons |= Buttons.Dash;
        if (Input.IsMouseButtonPressed(MouseButton.Left)) buttons |= Buttons.Attack;
        return ToMove(side, forward, buttons);
    }

    private (sbyte X, sbyte Z, Buttons Buttons) BotInput()
    {
        if (_client.Tick % 60 == 0)
        {
            _yaw += (float)(_botRandom.NextDouble() - 0.5) * 2f;
        }

        var buttons = _botRandom.Next(30) == 0 ? Buttons.Attack : Buttons.None;
        if (_botRandom.Next(80) == 0) buttons |= Buttons.Jump;
        if (_botRandom.Next(120) == 0) buttons |= Buttons.Dash;
        return ToMove(0, 1, buttons);
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

    private void DrawPlayer(Vector3 shown)
    {
        _player.Position = shown;
        var velocity = _client.State.Velocity;
        if (new Vector2(velocity.X, velocity.Z).LengthSquared() > 0.5f)
        {
            _player.Rotation = new Vector3(0, Mathf.Atan2(-velocity.X, -velocity.Z), 0);
        }
    }

    private void DrawSnapshot(float delta)
    {
        if (_client.Latest is not { } snapshot)
        {
            return;
        }

        var blend = 1f - Mathf.Exp(-RemoteSmoothing * delta);
        foreach (var other in snapshot.Others)
        {
            var node = GetOrAdd(_remotePlayers, other.Id, () => ArenaView.BuildPlayer(new Color("f77622")));
            node.Position = node.Position.Lerp(ToGodot(other.Position), blend);
        }

        foreach (var enemy in snapshot.Enemies)
        {
            var node = GetOrAdd(_enemies, enemy.Id, ArenaView.BuildEnemy);
            var target = ToGodot(enemy.Position);
            // A respawn is a jump across the arena, not a move.
            node.Position = node.Position.DistanceTo(target) > 4f ? target : node.Position.Lerp(target, blend);
        }
    }

    private Node3D GetOrAdd(Dictionary<int, Node3D> nodes, int id, Func<Node3D> build)
    {
        if (!nodes.TryGetValue(id, out var node))
        {
            node = build();
            nodes.Add(id, node);
            _stage.View.AddChild(node);
        }

        return node;
    }

    private void PlaceCamera(Vector3 shown)
    {
        var behind = new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw)) * CameraDistance;
        var target = shown + new Vector3(0, 1.4f, 0);
        _stage.Camera.LookAtFromPosition(target + behind + new Vector3(0, CameraHeight, 0), target);
    }

    private void Capture()
    {
        _stage.View.GetTexture().GetImage().SavePng($"{_captureDir}/arena-{_captured}.png");
        if (++_captured < CaptureFrames)
        {
            return;
        }

        var stats = _client.Stats;
        GD.Print($"STATS snapshots={stats.Snapshots} corrections={stats.Corrections} " +
                 $"maxError={stats.MaxError:F3} buffered={_client.Latest?.BufferedInputs} " +
                 $"tickScale={_client.TickScale:F3} enemies={_enemies.Count} hp={_client.State.Health}");
        GetTree().Quit();
    }

    private static Vector3 ToGodot(System.Numerics.Vector3 value) => new(value.X, value.Y, value.Z);
}
