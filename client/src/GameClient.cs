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
    private readonly Dictionary<int, IEnemyView> _enemies = new();
    private readonly Dictionary<int, byte> _enemyTypes = new();
    private readonly Dictionary<int, TelegraphView> _telegraphs = new();
    private readonly Dictionary<int, MeshInstance3D> _spits = new();
    private readonly Dictionary<int, ProjectileKind> _spitKinds = new();
    private readonly Dictionary<int, Node3D> _crystals = new();
    private readonly int[] _lastCrystals = new int[Crystals.Tiers];
    private readonly Dictionary<int, FireWallNode> _walls = new();
    private Zone _zone = null!;
    private string _region = "";
    private PixelStage _stage = null!;
    private UdpTransport _transport = null!;
    private ClientPrediction _client = null!;
    private PlayerRig _player = null!;
    private Hud _hud = null!;
    private Sfx _sfx = null!;
    private Fx _fx = null!;
    private PlayerState _last;
    private byte _lastLevel = 1;
    private byte _lastDodges;
    private int _lastXp;
    private bool _rareSeen;
    private float _ghostClock;
    private readonly Dictionary<int, Vector3> _spitPositions = new();
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
        _zone = zone;
        _stage = new PixelStage(this);
        _stage.View.AddChild(ZoneView.Build(zone));
        _player = new PlayerRig(new Color("0099db"));
        _stage.View.AddChild(_player.Root);
        _sfx = new Sfx(_stage.View);
        var effects = new Node3D();
        _stage.View.AddChild(effects);
        _fx = new Fx(effects);

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
            else if (key.Keycode == Key.M)
            {
                _sfx.Muted = !_sfx.Muted;
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
        PlayerEvents(shown, d);
        _fx.Update(d);
        PlaceCamera(shown);
        _hud.WorldScale = _stage.PixelScale;
        _hud.Player = _client.State;
        _hud.Snapshot = _client.Latest;
    }

    /// <summary>Turns changes in the player state into sounds and effects.</summary>
    private void PlayerEvents(Vector3 shown, float delta)
    {
        var now = _client.State;
        var chest = shown + Vector3.Up;
        if (_player.TakeFootDown())
        {
            _sfx.Play($"step_{Random.Shared.Next(1, 5)}", shown, -6f);
        }

        if (now.DashTicks > 0 && _last.DashTicks == 0)
        {
            _sfx.Play("dash", chest);
            _fx.Burst(shown + Vector3.Up * 0.1f, new Color("c0cbdc"), 12, 3f, 0.35f, 0.1f, -2f, 80f);
        }

        if (now.DashTicks > 0)
        {
            _ghostClock -= delta;
            if (_ghostClock <= 0f)
            {
                _ghostClock = 0.035f;
                _fx.Fade(new CapsuleMesh { Radius = 0.3f, Height = 1.8f, RadialSegments = 8, Rings = 3 },
                         shown + Vector3.Up * 0.95f, Vector3.Zero, new Color("2ce8f5"), 0.28f, 0.55f);
            }
        }

        if (_last.Grounded && !now.Grounded && now.Velocity.Y > 1f)
        {
            _sfx.Play("jump", shown);
        }
        else if (!_last.Grounded && now.Grounded)
        {
            _sfx.Play("land", shown);
            _fx.Burst(shown + Vector3.Up * 0.05f, new Color("b8a58a"), 8, 2.5f, 0.3f, 0.07f, -6f, 85f);
        }

        if (now.Attack != AttackKind.None && now.AttackTick < _last.AttackTick | now.Attack != _last.Attack)
        {
            _sfx.Play(now.Attack == AttackKind.Heavy ? "swing_heavy" : "swing_light", chest);
        }

        if (now.Health < _last.Health && _last.Health > 0)
        {
            _sfx.Play("hurt", chest);
            _hud.AddFloating($"-{_last.Health - now.Health}", chest + Vector3.Up, new Color("e43b44"));
        }

        _last = now;
        if (_client.Latest is not { } snapshot)
        {
            return;
        }

        if (snapshot.Dodges != _lastDodges)
        {
            _lastDodges = snapshot.Dodges;
            _sfx.Play("dodge", chest);
            _hud.AddFloating("Dodged!", chest + Vector3.Up, new Color("2ce8f5"));
        }

        if (snapshot.Level > _lastLevel)
        {
            _lastLevel = snapshot.Level;
            _lastXp = snapshot.Xp;
            _sfx.Play("level_up", chest);
            _fx.LevelUp(shown);
            _hud.ShowBanner($"Level Up!  Lv {snapshot.Level}", new Color("fee761"));
        }
        else if (snapshot.Xp > _lastXp)
        {
            _hud.AddFloating($"+{snapshot.Xp - _lastXp} XP", chest + Vector3.Up * 1.4f, new Color("feae34"));
            _lastXp = snapshot.Xp;
        }

        var rare = snapshot.Enemies.FirstOrDefault(e => e.Action != EnemyAction.Dead && EnemyDef.All[e.Type].Rare);
        var rareAlive = EnemyDef.All[rare.Type].Rare && rare.Action != EnemyAction.Dead && snapshot.Enemies.Length > 0;
        if (rareAlive && !_rareSeen)
        {
            _hud.ShowBanner($"A {EnemyDef.All[rare.Type].Name} is near!", new Color("f6757a"));
        }

        _rareSeen = rareAlive;
        for (var i = 0; i < Crystals.Tiers; i++)
        {
            if (snapshot.Crystals[i] > _lastCrystals[i])
            {
                _sfx.Play("crystal", chest);
                _hud.AddFloating($"+{Crystals.All[i].Name}", chest + Vector3.Up * 1.7f, new Color(Crystals.All[i].Color));
            }

            _lastCrystals[i] = snapshot.Crystals[i];
        }

        var region = _zone.RegionAt(shown.X, shown.Z);
        if (region != _region && region.Length > 0)
        {
            _hud.ShowBanner(region, new Color("ead4aa"));
        }

        _region = region.Length > 0 ? region : _region;
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
            var def = EnemyDef.All[enemy.Type];

            // A respawn can bring back another type under the same id, with another weapon and moves.
            if (_enemies.TryGetValue(enemy.Id, out var old) && _enemyTypes[enemy.Id] != enemy.Type)
            {
                old.Root.QueueFree();
                _enemies.Remove(enemy.Id);
            }

            if (!_enemies.TryGetValue(enemy.Id, out var slime))
            {
                slime = def.Kind == "goblin" ? new GoblinView(def) : new SlimeView();
                _enemies.Add(enemy.Id, slime);
                _enemyTypes[enemy.Id] = enemy.Type;
                _stage.View.AddChild(slime.Root);
                if (!_telegraphs.ContainsKey(enemy.Id))
                {
                    _telegraphs.Add(enemy.Id, new TelegraphView(_world, _fx, _stage.View));
                }
            }

            var target = ToGodot(enemy.Position);
            var position = slime.Root.Position;
            // A respawn is a jump across the map, not a move.
            position = position.DistanceTo(target) > 6f ? target : position.Lerp(target, blend);
            slime.Update(enemy, position, delta, _sfx);
            _hud.EnemyPositions[enemy.Id] = position;
            ShowTelegraph(enemy, def, position);
            if (slime.DamageTaken > 0)
            {
                var heavy = _client.State.Attack == AttackKind.Heavy;
                _sfx.Play(heavy ? "hit_heavy" : "hit", position + Vector3.Up, -3f);
                _hud.AddFloating(slime.DamageTaken.ToString(), position + Vector3.Up * 1.6f, heavy ? new Color("feae34") : Colors.White);
                var blood = def.Kind == "goblin" ? "a22633" : def.Rare ? "f6757a" : "63c74d";
                _fx.Burst(position + Vector3.Up * 0.6f, new Color(blood), heavy ? 22 : 12, 6f, 0.5f, 0.09f);
            }
        }

        // Enemies outside the player's area are not in the snapshot, so they must not linger on screen.
        var near = snapshot.Enemies.Select(e => e.Id).ToHashSet();
        foreach (var (id, view) in _enemies)
        {
            view.Root.Visible &= near.Contains(id);
            if (!near.Contains(id))
            {
                _telegraphs[id].Hide();
            }
        }

        DrawSpits(snapshot, blend);
        DrawCrystals(snapshot, delta);
        DrawFireWalls(snapshot, delta);
    }

    private void DrawFireWalls(Snapshot snapshot, float delta)
    {
        var alive = new HashSet<int>();
        foreach (var wall in snapshot.FireWalls)
        {
            alive.Add(wall.Id);
            if (!_walls.TryGetValue(wall.Id, out var node))
            {
                node = new FireWallNode(_world, _fx, ToGodot(wall.Origin), wall.Yaw);
                _walls.Add(wall.Id, node);
                _stage.View.AddChild(node.Root);
                _sfx.Play("fire_wall", ToGodot(wall.Origin));
            }

            node.Update(wall.Length, delta);
        }

        foreach (var id in _walls.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _walls[id].Root.QueueFree();
            _walls.Remove(id);
        }
    }

    private void ShowTelegraph(in EnemyView enemy, EnemyDef def, Vector3 position)
    {
        var telegraph = _telegraphs[enemy.Id];
        var target = ToGodot(enemy.LeapTarget);
        if (def.Kind != "goblin")
        {
            if (enemy.Action is EnemyAction.Windup or EnemyAction.Leap)
            {
                telegraph.ShowCircle(target, def.SplashRadius, enemy.Action == EnemyAction.Leap ? 1f : 0.3f);
            }
            else
            {
                telegraph.Hide();
            }

            return;
        }

        var ability = def.Abilities![enemy.Ability];
        if (enemy.Action != EnemyAction.Cast || ability.Telegraph == "none")
        {
            telegraph.Hide();
        }
        else if (ability.Telegraph == "wedge")
        {
            var flat = new Vector2(target.X - position.X, target.Z - position.Z);
            telegraph.ShowWedge(position, Mathf.Atan2(-flat.X, -flat.Y), FireWallHazard.MaxLength, 0.42f);
        }
        else if (ability.Telegraph == "circle")
        {
            telegraph.ShowCircle(ability.Effect == "spin" ? position : target, ability.Radius, 0.5f);
        }
        else
        {
            var along = (target - position).Normalized();
            telegraph.ShowLine(position, position + along * ability.Range, ability.Effect == "powershot" ? 0.5f : 0.9f, 0f);
        }
    }

    /// <summary>Crystals on the ground: they float and spin, and better ones throw a beam of light.</summary>
    private void DrawCrystals(Snapshot snapshot, float delta)
    {
        var alive = new HashSet<int>();
        foreach (var drop in snapshot.Drops)
        {
            alive.Add(drop.Id);
            if (!_crystals.TryGetValue(drop.Id, out var node))
            {
                node = BuildCrystal(drop);
                _crystals.Add(drop.Id, node);
                _stage.View.AddChild(node);
            }

            node.Rotation += new Vector3(0, delta * 2.2f, 0);
            node.Position = ToGodot(drop.Position) + Vector3.Up * (0.55f + Mathf.Sin(Time.GetTicksMsec() / 300f + drop.Id) * 0.12f);
        }

        foreach (var id in _crystals.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _fx.Burst(_crystals[id].Position, Colors.White, 8, 3f, 0.3f, 0.05f, -3f);
            _crystals[id].QueueFree();
            _crystals.Remove(id);
        }
    }

    private Node3D BuildCrystal(in CrystalView drop)
    {
        var color = new Color(Crystals.All[(int)drop.Rarity].Color);
        var root = new Node3D();
        var size = 0.26f + (int)drop.Rarity * 0.06f;
        var material = new StandardMaterial3D { AlbedoColor = color, EmissionEnabled = true, Emission = color, EmissionEnergyMultiplier = 0.4f, Roughness = 0.2f };
        var top = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = size, Height = size * 1.6f, RadialSegments = 5 }, MaterialOverride = material, Position = new Vector3(0, size * 0.8f, 0) };
        var bottom = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = size, BottomRadius = 0f, Height = size * 1.1f, RadialSegments = 5 }, MaterialOverride = material, Position = new Vector3(0, -size * 0.55f, 0) };
        root.AddChild(top);
        root.AddChild(bottom);
        if (drop.Rarity != CrystalRarity.Common)
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.32f, Height = 6f, RadialSegments = 8 },
                MaterialOverride = _fx.Stipple(color, 0.3f),
                Position = new Vector3(0, 3f, 0),
            });
        }

        return root;
    }

    private void DrawSpits(Snapshot snapshot, float blend)
    {
        var alive = new HashSet<int>();
        foreach (var spit in snapshot.Projectiles)
        {
            alive.Add(spit.Id);
            var target = ToGodot(spit.Position);
            if (!_spits.TryGetValue(spit.Id, out var ball))
            {
                ball = BuildProjectile(spit.Kind, target);
                _spits.Add(spit.Id, ball);
                _spitKinds.Add(spit.Id, spit.Kind);
                _stage.View.AddChild(ball);
                _sfx.Play(spit.Kind switch
                {
                    ProjectileKind.Spit => "slime_spit",
                    ProjectileKind.Arrow => "bow_shot",
                    ProjectileKind.PowerArrow => "bow_power",
                    _ => "fireball",
                }, ball.Position);
            }

            var motion = target - ball.Position;
            ball.Position = ball.Position.Lerp(target, spit.Kind == ProjectileKind.PowerArrow ? 1f : blend);
            if (spit.Kind is ProjectileKind.Arrow or ProjectileKind.PowerArrow && motion.LengthSquared() > 0.0001f)
            {
                ball.LookAt(ball.GlobalPosition + motion, Vector3.Up);
            }
            else if (spit.Kind == ProjectileKind.Spit)
            {
                ball.Rotation += new Vector3(0.2f, 0.3f, 0f);
            }

            _spitPositions[spit.Id] = ball.Position;
        }

        foreach (var id in _spits.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            ImpactEffect(_spitKinds[id], _spitPositions[id]);
            _spits[id].QueueFree();
            _spits.Remove(id);
            _spitKinds.Remove(id);
            _spitPositions.Remove(id);
        }
    }

    private void ImpactEffect(ProjectileKind kind, Vector3 last)
    {
        var ground = new Vector3(last.X, _world.GroundHeight(last.X, last.Z), last.Z);
        switch (kind)
        {
            case ProjectileKind.Spit:
                _sfx.Play("spit_splash", last);
                _fx.Burst(last, new Color("f6757a"), 26, 7f, 0.6f, 0.11f);
                _fx.Burst(last, new Color("b55088"), 14, 4f, 0.5f, 0.08f);
                _fx.Puddle(ground, new Color("b55088"), 1.6f, 1.6f);
                break;
            case ProjectileKind.Fireball:
                _sfx.Play("explosion", last);
                _fx.Burst(last, new Color("f77622"), 34, 9f, 0.6f, 0.13f, -5f, 90f);
                _fx.Burst(last, new Color("fee761"), 18, 5f, 0.45f, 0.09f, -3f, 90f);
                _fx.Puddle(ground, new Color("3e2731"), 1.8f, 1.2f);
                break;
            default:
                _sfx.Play("arrow_hit", last, -5f);
                _fx.Burst(last, new Color("b8a58a"), 5, 3f, 0.25f, 0.05f);
                break;
        }
    }

    private MeshInstance3D BuildProjectile(ProjectileKind kind, Vector3 position)
    {
        if (kind == ProjectileKind.Spit)
        {
            return BuildSpit(position);
        }

        if (kind is ProjectileKind.Arrow or ProjectileKind.PowerArrow)
        {
            var power = kind == ProjectileKind.PowerArrow;
            var arrow = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.06f, 0.06f, power ? 1.6f : 0.95f) },
                MaterialOverride = _fx.Stipple(new Color(power ? "f6757a" : "ead4aa"), 1f),
                Position = position,
            };
            if (power)
            {
                arrow.AddChild(Trail(new Color("e43b44"), 22, 0.28f, 0.06f));
            }

            return arrow;
        }

        const float size = 0.42f;
        var ball = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = size, Height = size * 2f, RadialSegments = 8, Rings = 5 },
            MaterialOverride = _fx.Stipple(new Color("feae34"), 1f),
            Position = position,
        };
        ball.AddChild(Trail(new Color("f77622"), 30, 0.5f, 0.15f, 4f));
        ball.AddChild(Trail(new Color("e43b44"), 14, 0.65f, 0.1f, 5f));
        return ball;
    }

    private CpuParticles3D Trail(Color color, int amount, float life, float size, float rise = 0f) => new()
    {
        Amount = amount,
        Lifetime = life,
        LocalCoords = false,
        Spread = 180f,
        InitialVelocityMax = 1.2f,
        Gravity = new Vector3(0, rise, 0),
        ScaleAmountMin = 0.5f,
        ScaleAmountMax = 1.2f,
        Mesh = new BoxMesh { Size = Vector3.One * size },
        MaterialOverride = _fx.Stipple(color, 1f),
    };

    /// <summary>A wet ball of slime that sheds drops along its path.</summary>
    private static MeshInstance3D BuildSpit(Vector3 position)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/slime.gdshader") };
        material.SetShaderParameter("body", new Color("b55088"));
        material.SetShaderParameter("rim", new Color("fbb9b7"));
        material.SetShaderParameter("core", new Color("68386c"));
        material.SetShaderParameter("glow", 0.5f);
        material.SetShaderParameter("wobble", 0.05f);
        var ball = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.3f, Height = 0.6f, RadialSegments = 10, Rings = 6 },
            MaterialOverride = material,
            Position = position,
        };
        ball.AddChild(new CpuParticles3D
        {
            Amount = 26,
            Lifetime = 0.45f,
            LocalCoords = false,
            Spread = 180f,
            InitialVelocityMax = 1f,
            Gravity = new Vector3(0, -9f, 0),
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.2f,
            Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f, RadialSegments = 5, Rings = 3 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("f6757a"), Roughness = 0.3f },
        });
        return ball;
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
                 $"fps={Engine.GetFramesPerSecond()} sounds={_sfx.Played} level={_client.Latest?.Level} xp={_client.Latest?.Xp} hp={_client.State.Health} stamina={_client.State.Stamina:F0} maxHeight={_maxHeight:F2} travelled={_travelled:F0}");
        GetTree().Quit();
    }

    private static Vector3 ToGodot(System.Numerics.Vector3 value) => new(value.X, value.Y, value.Z);
}
