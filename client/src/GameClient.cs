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
/// saves frames, prints network stats and quits. PIXEL_BOT=idle stands still; talk opens a conversation; trial starts the Guild trial; keys
/// presses real key events.
/// </summary>
public partial class GameClient : Node
{
    private const float CameraDistance = 5.2f;
    private const float CameraStep = 0.25f;
    private const float CameraClearance = 0.4f;
    private const float CameraMargin = 0.6f;

    /// <summary>Closer than this behind the player, the camera rises to find more room.</summary>
    private const float CameraComfort = 2.6f;

    /// <summary>Blocked stretches shorter than this along the camera's line are looked past.</summary>
    private const float ThinObstacle = 0.9f;

    /// <summary>Only boxes no wider than this from above (trunks, posts, bits of fence) count as thin.</summary>
    private const float ThinFootprint = 1.6f;

    /// <summary>How fast the camera closes in on an obstacle, and how slowly it backs out again.</summary>
    private const float CameraPullIn = 18f;
    private const float CameraEaseOut = 2.5f;

    private Vector3? _cameraTarget;
    private float _cameraReach = -1f;
    private float _cameraRaise;
    private float _cameraLift;

    /// <summary>How far the camera has moved in to aim, from 0 to 1, and how long it stays after a shot.</summary>
    private float _aimBlend;
    private float _aimHold;

    /// <summary>A bot's own shot aim, used instead of the crosshair.</summary>
    private (ushort Yaw, short Pitch)? _botShot;

    /// <summary>How far up the last aim pointed, in radians, for the archer's arms.</summary>
    private float _shotPitch;

    /// <summary>How dark and misty the world is drawn, following the blight under the player.</summary>
    private float _gloom;
    private const int BotDialogTicks = 70;
    private const int HuntTicks = 1400;
    private const int MaxTicksPerFrame = 6;
    private const float FigureRange = 60f;
    private const float GuildInteriorX = 400f;
    private const float ArenaX = 450f;
    private const float ArchiveX = 530f;
    private const float MinPitch = -1.1f;
    private const float MaxPitch = 0.35f;

    /// <summary>
    /// Aiming a bow: the camera moves in over the right shoulder, the view narrows as the draw
    /// fills, and the player may look further up, to shoot at what stands above them.
    /// </summary>
    private const float AimMaxPitch = 0.95f;
    private const float AimReach = 3.1f;
    private const float AimShoulder = 0.85f;
    private const float AimSeconds = 0.6f;
    private const float BaseFov = 58f;

    /// <summary>The furthest the aim looks for something under the crosshair.</summary>
    private const float AimRayLength = 90f;
    private const float MouseSensitivity = 0.0035f;
    private const float RemoteSmoothing = 18f;
    /// <summary>Ticks between captured frames; PIXEL_CAPTURE_EVERY changes it to catch a short effect.</summary>
    private static readonly int CaptureEveryTicks = int.TryParse(OS.GetEnvironment("PIXEL_CAPTURE_EVERY"), out var every) && every > 0 ? every : 45;

    private readonly Dictionary<int, PlayerRig> _remotes = new();
    private readonly Dictionary<int, IEnemyView> _enemies = new();
    private readonly Dictionary<int, byte> _enemyTypes = new();

    /// <summary>
    /// Where each enemy is shown, eased toward the server's position. Kept apart from the view's root,
    /// which a leaping werewolf tips round its middle, so the tip never feeds back into the easing.
    /// </summary>
    private readonly Dictionary<int, Vector3> _enemyShown = new();
    private readonly Dictionary<int, TelegraphView> _telegraphs = new();
    private readonly Dictionary<int, MeshInstance3D> _spits = new();
    private readonly Dictionary<int, ProjectileKind> _spitKinds = new();
    private readonly Dictionary<int, Node3D> _crystals = new();
    private readonly int[] _lastCrystals = new int[Crystals.Tiers];
    private Snapshot? _lastSnapshot;
    private readonly Dictionary<int, IGroundHazard> _walls = new();
    private Zone _zone = null!;
    private Minimap _minimap = null!;

    // Things scenes move about that are not people (the finders' net), by actor id.
    private readonly Dictionary<string, Node3D> _propViews = new();
    private IntroVideo _introVideo = null!;
    private Navigator _navigator = null!;
    private WorldMap _worldMap = null!;

    // Set while a thrown net holds the player (a scene's throw has landed), for the objective.
    private bool _sceneRooted;

    // The scene step whose film is showing, or -1: Space, Enter or Esc skips it.
    private int _videoStep = -1;
    private string _region = "";
    private PixelStage _stage = null!;
    private UdpTransport _transport = null!;
    private ClientPrediction _client = null!;
    private PlayerRig _player = null!;
    private Hud _hud = null!;
    private Sfx _sfx = null!;
    private Voices _voices = null!;
    private (string Speaker, string Text) _voiced;
    private Fx _fx = null!;
    private PlayerState _last;
    private byte _lastLevel = 1;
    private ushort _pendingArg;
    private Command _pendingCommand;
    private GuildRank _lastRank;
    private readonly Dictionary<string, PlayerRig> _npcs = new();
    private byte _lastDodges;
    private byte _lastBlocks;
    private byte _lastParries;
    private byte _lastRipostes;
    private MeshInstance3D _barrier = null!;

    /// <summary>True when the player has taken a mastery move and holds a weapon of its kind.</summary>
    private bool HasMove(string unlock) =>
        _hud.Snapshot is { } snapshot && Mastery.Has(Weapons.Name(_client.State.Weapon), snapshot.MasteryTaken, unlock);
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
    private string _botStage = "";
    private int _sinceCapture;
    private int _measuredFrames;
    private double _frameSeconds;
    private int _botWait;
    private string? _captureDir;
    private int _captureFrames = 8;
    private int _captured;
    private float _maxHeight;
    private float _travelled;
    private readonly Random _botRandom = new(5);

    private bool _closing;
    private int _quitFrames;

    /// <summary>Closing the window quits like the menu does, so the server can save first.</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            _closing = true;
        }
    }

    public override void _Ready()
    {
        GetTree().AutoAcceptQuit = false;
        // The main menu sets where to connect; a host on the command line (scripts, bots) wins over it.
        var args = Launch.FromMenu ? Array.Empty<string>() : OS.GetCmdlineUserArgs();
        var host = args.ElementAtOrDefault(0) ?? Launch.Host;
        var port = int.Parse(args.ElementAtOrDefault(1) ?? Launch.Port.ToString());
        _captureDir = args.ElementAtOrDefault(2);
        _captureFrames = int.Parse(args.ElementAtOrDefault(3) ?? "8");
        _yaw = float.TryParse(OS.GetEnvironment("PIXEL_YAW"), System.Globalization.CultureInfo.InvariantCulture, out var yaw) ? yaw : 0f;

        Settings.Load();
        var zone = Zone.Load("meadow-valley");
        _world = zone.World;
        _zone = zone;
        _stage = new PixelStage(this);
        _stage.View.AddChild(ZoneView.Build(zone));
        _player = new PlayerRig(new Color("0099db"));
        _stage.View.AddChild(_player.Root);
        _sfx = new Sfx(_stage.View);
        _voices = new Voices(_stage.View);
        var effects = new Node3D();
        _stage.View.AddChild(effects);
        _fx = new Fx(effects);

        // Staff mastery Arcane Barrier: a faint blue shell round the player while it holds.
        _barrier = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.85f, Height = 2.1f, RadialSegments = 16, Rings = 8 },
            MaterialOverride = _fx.Stipple(new Color("2ce8f5"), 0.22f),
            Position = Vector3.Up * 0.95f,
            Visible = false,
        };
        _player.Root.AddChild(_barrier);

        _hud = new Hud { Camera = _stage.Camera };
        AddChild(_hud);

        // The dialogue portrait: a second camera in the same world, close on the speaker's face.
        _portrait = new SubViewport { Size = new Vector2I(256, 256), World3D = _stage.View.FindWorld3D(), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        // It sees only the speaker's layer, against a calm backdrop of its own (no sky, no fog over the face).
        _portraitCamera = new Camera3D
        {
            Fov = 14f, Near = 0.05f, Far = 40f, CullMask = PortraitLayer,
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("2a2c3a"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("c8c4d8"),
                AmbientLightEnergy = 0.6f,
            },
        };
        _portrait.AddChild(_portraitCamera);
        AddChild(_portrait);
        _portraitCamera.Current = true;
        _hud.Portrait = _portrait.GetTexture();
        _minimap = new Minimap(zone);
        _hud.AddChild(_minimap);
        _introVideo = new IntroVideo();
        _hud.AddChild(_introVideo);
        _navigator = new Navigator(_world, _stage.View) { TrailEnabled = !PixelStage.Off("trail") };
        _worldMap = new WorldMap(zone, _navigator);
        _hud.AddChild(_worldMap);
        _actors = Actors.For(_world);
        foreach (var actor in _actors)
        {
            Dialogues.Names[actor.Id] = actor.Name;
        }

        _actorAt = _actors.Select(a => ToGodot(a.Home)).ToArray();
        _actorPresent = _actors.Select(a => a.Resident).ToArray();
        // Sized here too: E pressed before the first actor update once read an empty array.
        _actorWalking = new bool[_actors.Count];
        _actorPose = new ActorPose[_actors.Count];
        _actorStrike = new float[_actors.Count];
        foreach (var actor in _actors)
        {
            if (NpcLooks.For(actor.Role) is { } look)
            {
                var rig = new PlayerRig(new Color("0099db"), look);
                rig.Root.Position = ToGodot(actor.Home);
                rig.Root.Rotation = new Vector3(0, actor.HomeYaw, 0);
                rig.Root.Visible = actor.Resident;
                PixelStage.Limit(rig.Root, FigureRange);
                _stage.View.AddChild(rig.Root);
                _npcs.Add(actor.Id, rig);
            }
            else if (actor.Role.StartsWith("prop:", StringComparison.Ordinal))
            {
                // A thing a scene moves (the finders' net): its model, placed like a person.
                var model = actor.Role["prop:".Length..];
                if (ResourceLoader.Exists($"res://assets/{model}.glb"))
                {
                    var node = GD.Load<PackedScene>($"res://assets/{model}.glb").Instantiate<Node3D>();
                    node.Visible = actor.Resident;
                    _stage.View.AddChild(node);
                    _propViews.Add(actor.Id, node);
                }
            }
            else if (actor.Role is "storage" or "anchor_stone" or "quest_board" or Waystones.Role)
            {
                // Things you can use, like the Guild chest and the job board, carry a floating sign so players see them.
                var height = actor.Role == "quest_board" ? 2.7f : actor.Role == Waystones.Role ? 3.7f : 1.25f;
                _hud.Signs.Add((ToGodot(actor.Home) + Vector3.Up * height, actor.Role == "quest_board" ? "Quest Board: jobs" : actor.Name));
            }
        }


        _transport = UdpTransport.Connect(host, port);
        _client = new ClientPrediction(_transport, _world);

        // Props shown or hidden by a story flag block with it, from the story copy the server sends.
        _world.HasFlag = flag => _client.Story.Has(flag);
        if (_captureDir is null)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && !_hud.WindowOpen && !_hud.DialogOpen && !_hud.MenuOpen)
        {
            // A drawn bow turns at its own speed, for fine aim.
            var speed = MouseSensitivity * Settings.Sensitivity * Mathf.Lerp(1f, Settings.AimSensitivity, _aimBlend);
            _yaw -= motion.Relative.X * speed;
            _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * speed, MinPitch, Mathf.Lerp(MaxPitch, AimMaxPitch, _aimBlend));
        }
        else if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (_videoStep >= 0 && key.Keycode is Key.Escape or Key.Space or Key.Enter or Key.KpEnter)
            {
                // A film is skipped, not paused: the scene goes on.
                (_pendingCommand, _pendingArg) = (Command.SceneNext, (ushort)_videoStep);
                return;
            }

            if (key.Keycode == Key.Escape)
            {
                if (_hud.DialogOpen)
                {
                    _hud.CloseDialog();
                    return;
                }

                if (_hud.WindowOpen && !_hud.MenuOpen)
                {
                    _hud.CloseWindows();
                    return;
                }

                _hud.ToggleMenu();
            }
            else if (key.Keycode == Key.E)
            {
                Interact();
            }
            else if (_hud.DialogOpen && key.Keycode >= Key.Key1 && key.Keycode <= Key.Key9)
            {
                var choice = _hud.Choose((int)(key.Keycode - Key.Key1));
                if (choice.Command != Command.None)
                {
                    (_pendingCommand, _pendingArg) = choice;
                }
            }
            else if (key.Keycode == Key.Backspace && _hud.TutorialShown && !_hud.DialogOpen)
            {
                (_pendingCommand, _pendingArg) = (Command.SkipTutorial, 0);
            }
            else if (key.Keycode == Key.F3)
            {
                _hud.ShowPosition = !_hud.ShowPosition;
            }
            else if (key.Keycode == Key.M && !_hud.DialogOpen && _videoStep < 0)
            {
                _hud.ToggleMap();
            }
            else if (key.Keycode == Key.I || key.Keycode == Key.Tab)
            {
                _hud.ToggleInventory();
            }
            else if (key.Keycode == Key.K)
            {
                _hud.ToggleCharacter();
            }
            else if (key.Keycode == Key.J)
            {
                _hud.ToggleMastery();
            }
            else if (key.Keycode == Key.L)
            {
                _hud.ToggleJournal();
            }
            else if (key.Keycode is Key.Q or Key.R or Key.F && !_hud.DialogOpen && !_hud.WindowOpen)
            {
                _hud.CastSlot(Hud.SkillKeys.IndexOf(key.Keycode.ToString()[0]));
            }
            else if (key.Keycode is Key.Z or Key.X or Key.C && !_hud.DialogOpen)
            {
                _hud.DrinkPotion(key.Keycode == Key.Z ? 0 : key.Keycode == Key.X ? 1 : 2);
            }
            else if (key.Keycode >= Key.Key1 && key.Keycode <= Key.Key8)
            {
                _hud.SelectHotbar((int)(key.Keycode - Key.Key1));
            }
        }
    }

    /// <summary>Snapshots seen so far, when the last one came, and how long a silence means the server is gone.</summary>
    private long _seenSnapshots;
    private ulong _lastSnapshotMs;
    private const ulong SilenceMs = 4000;

    /// <summary>
    /// Leaves the game after the quit command went out: a server the menu started saves and stops by
    /// itself (we wait for that), then the game closes or goes back to the main menu.
    /// </summary>
    private async System.Threading.Tasks.Task Leave(bool toMenu)
    {
        if (Launch.FromMenu)
        {
            await LocalServer.StopAfterSave();
        }

        if (toMenu)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            GetTree().ChangeSceneToFile("res://scenes/menu.tscn");
        }
        else
        {
            GetTree().Quit();
        }
    }

    public override void _Process(double delta)
    {
        // Frame times are measured away from the frames that save a picture, which stall on purpose.
        if (_captureDir is not null && _sinceCapture++ > 20)
        {
            _frameSeconds += delta;
            _measuredFrames++;
        }

        if (Launch.FromMenu && OS.GetEnvironment("PIXEL_MENU_CHECK") == "1" && _client.Tick == 900)
        {
            // The menu check: play a little, then save and go back to the menu, which looks for the save.
            _hud.RequestMainMenu();
        }

        if (_hud.QuitRequested || _hud.MenuRequested || _closing)
        {
            // Tell the server first, so it saves; the command rides in the next few input packets.
            if (_quitFrames == 0)
            {
                _pendingCommand = Command.Quit;
            }

            if (++_quitFrames > 12 || !_transport.IsConnected)
            {
                if (_quitFrames < 1000)
                {
                    _quitFrames = 1000;
                    _ = Leave(_hud.MenuRequested && !_closing);
                }

                return;
            }
        }

        _transport.Poll();
        if (!_transport.IsConnected)
        {
            _hud.Notice = _transport.LastDisconnect.Length > 0
                ? $"Lost the connection to the server ({_transport.LastDisconnect}). Restart the game to rejoin."
                : "Connecting...";
            return;
        }

        // A server that dies without a goodbye stays "connected" until the timeout; silence says it sooner.
        if (_client.Stats.Snapshots != _seenSnapshots)
        {
            _seenSnapshots = _client.Stats.Snapshots;
            _lastSnapshotMs = Time.GetTicksMsec();
        }

        var silent = _seenSnapshots > 0 && Time.GetTicksMsec() - _lastSnapshotMs > SilenceMs;
        _hud.Notice = silent ? "Connection lost: the server stopped answering. Restart the game to rejoin." : "";
        // A long frame, such as the first one while shaders compile, must not turn into a burst of
        // simulated ticks: the time beyond a few ticks is simply dropped.
        _clock = Math.Min(_clock + delta, Tuning.TickSeconds * MaxTicksPerFrame);
        var tickLength = Tuning.TickSeconds * _client.TickScale;
        while (_clock >= tickLength)
        {
            _clock -= tickLength;
            var (x, z, buttons) = _captureDir is null ? ReadInput() : BotInput();
            _shownBefore = _shownNow;
            if (_pendingCommand == Command.None)
            {
                (_pendingCommand, _pendingArg) = _hud.TakeCommand();
            }

            var (aim, aimYaw, aimPitch) = ShotAim();
            _client.Step(x, z, buttons, aim, _pendingCommand, aimYaw, aimPitch, _pendingArg);
            _pendingCommand = Command.None;
            _pendingArg = 0;
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
        UpdateNpcs(shown, d);
        _fx.Update(d);
        UpdateAim(d);
        PlaceCamera(shown, d);
        // Cursed ground darkens the world as the player walks into it, and lifts as they leave.
        var (_, blight, _) = _zone.GroundAt(shown.X, shown.Z);
        _gloom = Mathf.MoveToward(_gloom, blight, d * 0.6f);
        if (_client.Latest is { } now && !PixelStage.Off("daytime"))
        {
            _stage.SetTime(now.Minute);
        }

        _stage.Underground = shown.X > ArchiveX ? 1f : 0f;
        // Indoors the outdoor world is not drawn, and outdoors the interiors on their platforms off the map are not.
        var inside = shown.X > ZoneView.InteriorX;
        _stage.Camera.CullMask = (_stage.Camera.CullMask | ZoneView.OutdoorLayer | ZoneView.InteriorLayer) & ~(inside ? ZoneView.OutdoorLayer : ZoneView.InteriorLayer);
        _stage.SetGloom(_gloom);
        _stage.PlaceMoon();
        // A staff shows the crosshair all the time, as its fireballs fly to whatever lies under it.
        // So does any weapon once a skill is known, since skills fly through it too.
        var skills = (_client.Latest?.KnownSkills ?? 0) != 0;
        _hud.Aiming = Mathf.Max(_aimBlend, _client.State.Weapon == Weapons.Staff ? 0.7f : skills ? 0.45f : 0f);
        _hud.WorldScale = _stage.PixelScale;
        _hud.Player = _client.State;
        _hud.Snapshot = _client.Latest;
    }

    private byte[] _lookShown = Appearance.NotChosen;
    private bool _creatorOffered;

    /// <summary>
    /// Builds the player's figure again when the look in the snapshot changes, and opens the
    /// character creator once for a new game whose look is not yet chosen.
    /// </summary>
    private void UpdateLook()
    {
        if (_client.Latest is not { } snapshot)
        {
            return;
        }

        // In the prologue the creator waits for its moment after the intro; elsewhere it opens at once.
        var scene = snapshot.Scene != Snapshot.NoScene && snapshot.Scene < Story.Scenes.Count ? Story.Scenes[snapshot.Scene] : null;
        var moment = scene is null
            ? !_client.Story.Has("prologue.new") && _client.Tick > 90
            : scene.Steps[Math.Min(snapshot.SceneStep, scene.Steps.Length - 1)] is { Kind: "waitFor" } wait && wait.When.References().Any(r => r.Name == "look.chosen");
        if (!_creatorOffered && ((!Appearance.IsChosen(snapshot.Appearance) && moment) || OS.GetEnvironment("PIXEL_BOT") == "creator"))
        {
            _creatorOffered = true;
            _hud.OpenCreator();
        }

        var look = Appearance.IsChosen(snapshot.Appearance) ? snapshot.Appearance : Appearance.Default;
        if (look.AsSpan().SequenceEqual(_lookShown))
        {
            return;
        }

        _lookShown = look.ToArray();
        var rig = new PlayerRig(new Color("0099db"), PlayerLook.From(look));
        rig.Root.Transform = _player.Root.Transform;
        _player.Root.RemoveChild(_barrier);
        rig.Root.AddChild(_barrier);
        _stage.View.AddChild(rig.Root);
        _player.Root.QueueFree();
        _player = rig;
    }

    private (int Scene, int Step) _sceneShown = (-1, -1);
    private Vector3[]? _sceneCamera;
    private bool _sceneLocked;

    /// <summary>The speaker a scene's line waits for the player to meet, or null.</summary>
    private Actor? _sceneApart;
    private readonly Dictionary<string, Texture2D?> _slides = new();

    /// <summary>
    /// Shows the scene the server plays: a line in the dialogue box, a talk, the camera, a fade, the
    /// intro's slides. Everything is worked out from the steps up to the current one, so a lost
    /// snapshot changes nothing.
    /// </summary>
    private void UpdateScene()
    {
        var snapshot = _client.Latest;
        if (snapshot is null || snapshot.Scene == Snapshot.NoScene || snapshot.Scene >= Story.Scenes.Count)
        {
            if (_sceneShown.Scene >= 0 && _hud.DialogOpen && _hud.SceneDialog)
            {
                _hud.CloseDialog();
            }

            _sceneShown = (-1, -1);
            _sceneCamera = null;
            _sceneLocked = false;
            _sceneRooted = false;
            _sceneApart = null;
            _hud.Fade = 0f;
            _hud.SlideShown = false;
            _introVideo.Hide();
            _videoStep = -1;
            return;
        }

        var scene = Story.Scenes[snapshot.Scene];
        var index = Math.Min((int)snapshot.SceneStep, scene.Steps.Length - 1);
        var step = scene.Steps[index];
        var elapsed = Math.Max(0, (long)snapshot.ServerTick - snapshot.SceneStepTick) * Tuning.TickSeconds;

        _sceneCamera = null;
        _sceneLocked = false;
        _hud.Fade = 0f;
        var rooted = false;
        var sceneStory = new ClientStory(_client, _hud.Items);
        for (var i = 0; i <= index; i++)
        {
            var past = scene.Steps[i];
            if (i < index && past.If is { } only && !only.Holds(sceneStory))
            {
                // The server skipped it: its condition reads only story flags, so the copy here agrees.
                continue;
            }

            switch (past.Kind)
            {
                case "camera":
                    _sceneCamera = new[] { ToGodot(past.From), ToGodot(past.To) };
                    break;
                case "playerCamera":
                    _sceneCamera = null;
                    break;
                case "lockInput":
                    _sceneLocked = true;
                    break;
                case "unlockInput":
                    _sceneLocked = false;
                    break;
                case "fade":
                    var progress = i == index ? Mathf.Clamp(elapsed / Mathf.Max(past.Seconds, 0.01f), 0f, 1f) : 1f;
                    _hud.Fade = past.Out ? progress : 1f - progress;
                    break;
                case "throw" when i < index:
                    // The net has landed: held, even while someone walks up to talk.
                    rooted = true;
                    break;
                case "unroot":
                    rooted = false;
                    break;
            }
        }

        _sceneLocked |= rooted;
        _sceneRooted = rooted;
        if (step.Kind == "video")
        {
            _introVideo.Show(step.Item, step.Seconds, step.Text, step.CaptionFrom, GetProcessDeltaTime());
            _videoStep = index;
            if (_introVideo.Done && _pendingCommand == Command.None)
            {
                // The film ran on its own clock; the scene goes on when it has played here.
                (_pendingCommand, _pendingArg) = (Command.SceneNext, (ushort)index);
            }
        }
        else
        {
            _introVideo.Hide();
            _videoStep = -1;
        }

        _hud.SlideShown = step.Kind == "slides";
        if (_hud.SlideShown)
        {
            var slide = Math.Min(Math.Max(snapshot.SceneSlide, (int)(elapsed / Mathf.Max(step.Seconds, 0.1f))), step.Slides.Length - 1);
            _hud.Slide = SlideTexture(step.Slides[slide]);
            _hud.SlideCaption = slide == step.Slides.Length - 1 ? step.Text : "";
        }

        var actor = _actors.FirstOrDefault(a => a.Id == step.Actor);
        var showing = _hud.DialogOpen && _hud.SceneDialog && _sceneShown == (snapshot.Scene, index);
        if (actor is not null && _actorPresent[actor.Index] && !showing &&
            SceneData.Apart(step, FromGodot(_actorAt[actor.Index]), _client.State.Position))
        {
            // People speak face to face: until the two meet, the player walks freely and no words show.
            if (_hud.DialogOpen && _hud.SceneDialog)
            {
                _hud.CloseDialog();
            }

            _sceneLocked = rooted;
            _sceneRooted = rooted;
            _sceneApart = actor;
            return;
        }

        _sceneApart = null;
        var fresh = _sceneShown != (snapshot.Scene, index);
        _sceneShown = (snapshot.Scene, index);
        if (step.Kind == "say" && step.Bark && fresh)
        {
            // Called out while the player plays: a banner, not a dialogue box.
            _hud.ShowBanner($"{actor?.Name ?? step.Name}: {step.Text}", new Color("f4f0e6"));

            // Called out loud, from where they stand, when the line is recorded.
            var speaker = actor?.Id ?? step.Name.ToLowerInvariant();
            _voices.SayAt(speaker, step.Text, actor is not null ? _actorAt[actor.Index] + Vector3.Up * 1.5f : ToGodot(_client.State.Position) + Vector3.Up * 1.5f);
        }
        else if (step.Kind == "say" && fresh)
        {
            _hud.ShowDialog(actor?.Name ?? step.Name, step.Text, new[] { new DialogChoice("Continue", Command.SceneNext, (ushort)index, null, true) });
            _hud.DialogRole = actor is null ? "" : Dialogues.Title(actor.Role);
            _hud.Speaker = step.Actor;
            _hud.DialogFace = step.Face;
            _hud.SceneDialog = true;
        }
        else if (step.Kind == "talk" && actor is not null && (fresh || !_hud.DialogOpen))
        {
            var npc = new Npc(actor.Id, actor.Name, actor.Role, FromGodot(_actorAt[actor.Index]), actor.HomeYaw);
            Dialogues.OpenNode(_hud, npc, step.Node, new ClientStory(_client, _hud.Items), snapshot);
            _hud.SceneDialog = true;
        }
        else if (fresh && _hud.SceneDialog && (step.Kind is not ("say" or "talk") || step.Bark))
        {
            _hud.CloseDialog();
        }
    }

    private Texture2D? SlideTexture(string name)
    {
        if (!_slides.TryGetValue(name, out var texture))
        {
            var path = $"res://assets/intro/{name}.png";
            texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            _slides[name] = texture;
        }

        return texture;
    }

    private List<Actor> _actors = null!;
    private Vector3[] _actorAt = null!;
    private bool[] _actorPresent = null!;
    private bool[] _actorWalking = Array.Empty<bool>();
    private ActorPose[] _actorPose = Array.Empty<ActorPose>();
    private float[] _actorStrike = Array.Empty<float>();

    /// <summary>
    /// Places everyone from the server's actor views: those it sends stand where it says; the rest
    /// rest at home (the zone's NPCs) or are away (scene actors). The dead and the gone are hidden.
    /// </summary>
    private void UpdateActors(float delta)
    {
        if (_actorWalking.Length != _actors.Count)
        {
            _actorWalking = new bool[_actors.Count];
            _actorPose = new ActorPose[_actors.Count];
        _actorStrike = new float[_actors.Count];
        }

        var views = new Dictionary<ushort, ActorView>();
        foreach (var view in _client.Latest?.Actors ?? Array.Empty<ActorView>())
        {
            views[view.Index] = view;
        }

        foreach (var actor in _actors)
        {
            var index = actor.Index;
            var fate = _client.Story.Fate(actor.Id);
            var (active, target, yaw, pose) = views.TryGetValue(index, out var v)
                ? (v.Active, ToGodot(v.Position), v.Yaw, v.Pose)
                : Resting(actor);
            var present = active && fate is NpcFate.Alive or NpcFate.Moved;
            var before = _actorAt[index];
            var appeared = present && !_actorPresent[index];
            _actorAt[index] = appeared || before.DistanceTo(target) > 8f ? target : before.Lerp(target, 1f - Mathf.Exp(-delta * 14f));
            var speed = before.DistanceTo(_actorAt[index]) / Mathf.Max(delta, 0.001f);
            _actorWalking[index] = speed > 0.4f;
            if (!present && _actorPresent[index] && actor.Role == "lost")
            {
                // An unbound Lightborn who dies goes out in light, which rises and is gone.
                var chest = before + Vector3.Up * 1.1f;
                _fx.Burst(chest, new Color("ffffff"), 46, 2.2f, 1.6f, 0.09f, 2.6f, 180f);
                _fx.Burst(chest, new Color("2ce8f5"), 30, 1.2f, 2.2f, 0.07f, 3.4f, 180f);
                _fx.Ring(before + Vector3.Up * 0.1f, new Color("c0cbdc"), 2.4f, 0.7f);
            }

            if (present && _actorPresent[index] && pose != _actorPose[index])
            {
                // A fight is heard: a blow lands with a sound and now and then a shout; the fallen say their last.
                if (pose == ActorPose.Strike)
                {
                    FightShout(actor, index);
                }
                else if (pose == ActorPose.Fallen && _actorPose[index] is ActorPose.Stand or ActorPose.Strike)
                {
                    LastWords(actor, index);
                }
            }

            _actorPresent[index] = present;
            _actorPose[index] = pose;
            if (_propViews.TryGetValue(actor.Id, out var propView))
            {
                propView.Visible = present;
                propView.Position = _actorAt[index];
                propView.Rotation = new Vector3(0, yaw, 0);
                continue;
            }

            if (!_npcs.TryGetValue(actor.Id, out var rig))
            {
                continue;
            }

            rig.Root.Visible = present;
            if (!present)
            {
                continue;
            }

            Temper(actor, rig, index, delta);
            rig.Root.Position = _actorAt[index];
            rig.Sitting = pose == ActorPose.Sit && !_actorWalking[index];
            // Someone at rest looks the way their day says, unless the player stands by (then they turn to them, below).
            var alone = new Vector2(_actorAt[index].X - _client.State.Position.X, _actorAt[index].Z - _client.State.Position.Z).Length() > 6f;
            if (_actorWalking[index] || views.ContainsKey(index) || alone || pose != ActorPose.Stand)
            {
                rig.Root.Rotation = new Vector3(pose == ActorPose.Fallen ? -Mathf.Pi / 2f : 0f, LerpFacing(rig.Root.Rotation.Y, yaw, 10f * delta), 0);
            }

            if (pose == ActorPose.Fallen)
            {
                rig.Root.Position += Vector3.Up * 0.15f;
            }

            // A blow in a scene's fight: the weapon in hand swings once while the pose lasts.
            _actorStrike[index] = pose == ActorPose.Strike ? _actorStrike[index] + delta : 0f;
            var strike = pose == ActorPose.Strike ? AttackKind.Light : AttackKind.None;
            rig.Animate(new MotionState(_actorWalking[index] ? Mathf.Min(speed, Tuning.MoveSpeed) : 0f, true, 0f, false, strike,
                (int)(_actorStrike[index] * Tuning.TickRate)), delta);
        }

        // Lines said in passing float over their speaker's head for a few seconds, fading at both ends.
        _hud.Barks.Clear();
        for (var i = _barks.Count - 1; i >= 0; i--)
        {
            var (who, text, left) = _barks[i];
            left -= delta;
            if (left <= 0f || !_actorPresent[who] || _hud.DialogOpen)
            {
                _barks.RemoveAt(i);
                continue;
            }

            _barks[i] = (who, text, left);
            var height = _actorPose[who] == ActorPose.Sit ? 1.55f : 2.05f;
            _hud.Barks.Add((_actorAt[who] + Vector3.Up * height, text, Mathf.Clamp(Mathf.Min(left, 4.5f - left) * 3f, 0f, 1f)));
        }
    }

    // When each actor may shout again in a fight, in seconds of game time.
    private readonly Dictionary<int, double> _shoutAt = new();

    /// <summary>A fighting actor's blow: the weapon's sound, and now and then one of their shouts (voiced when recorded).</summary>
    private void FightShout(Actor actor, int index)
    {
        var at = _actorAt[index] + Vector3.Up * 1.4f;
        var look = NpcLooks.For(actor.Role);
        _sfx.Play(look?.Weapon == "club" ? "club_thud" : "blade_hit", at);
        var now = Time.GetTicksMsec() / 1000.0;
        if (People.Of(actor.Id) is not { Fight.Length: > 0 } person || now < _shoutAt.GetValueOrDefault(index) || Random.Shared.NextDouble() > 0.45)
        {
            return;
        }

        _shoutAt[index] = now + 3.0;
        var line = person.Fight[Random.Shared.Next(person.Fight.Length)];
        _barks.Add((index, line, 2.6f));
        if (!_voices.SayAt(actor.Id, line, at))
        {
            _sfx.Play(look?.Female == true ? "woman_shout" : "man_shout", at);
        }
    }

    /// <summary>Someone a fight puts down: their last words over them, and a cry.</summary>
    private void LastWords(Actor actor, int index)
    {
        var at = _actorAt[index] + Vector3.Up * 0.8f;
        var female = NpcLooks.For(actor.Role)?.Female == true;
        if (People.Of(actor.Id) is { Death.Length: > 0 } person)
        {
            _barks.Add((index, person.Death, 3.5f));
            if (_voices.SayAt(actor.Id, person.Death, at))
            {
                return;
            }
        }

        _sfx.Play(female ? "woman_death" : "man_death", at);
    }

    private float[] _barkWait = Array.Empty<float>();
    private readonly Dictionary<int, string> _drawnMood = new();
    private readonly List<(int Actor, string Text, float Left)> _barks = new();

    /// <summary>
    /// What makes one person unlike the next, from world/people.json: the face they wear for how things
    /// stand, and what they mutter as the player passes. Checked now and then, not every frame.
    /// </summary>
    private void Temper(Actor actor, PlayerRig rig, int index, float delta)
    {
        if (_barkWait.Length != _actors.Count)
        {
            _barkWait = _actors.Select(_ => 4f + Random.Shared.NextSingle() * 20f).ToArray();
        }

        if (People.Of(actor.Id) is not { } person)
        {
            return;
        }

        _barkWait[index] -= delta;
        if (_barkWait[index] > 0f)
        {
            return;
        }

        var story = _restStory ??= new ClientStory(_client, _hud.Items);
        if (rig.Face is { } face)
        {
            _drawnMood.TryAdd(index, face.Resting);
            face.Resting = person.Mood(story) ?? _drawnMood[index];
            if (_speaking != rig)
            {
                face.Expression = face.Resting;
            }
        }

        // A line in passing, when the player is near, nobody is talking, and they are not asleep on their feet.
        var near = new Vector2(_actorAt[index].X - _client.State.Position.X, _actorAt[index].Z - _client.State.Position.Z).Length() < 9f;
        var lines = person.BarksNow(story).ToArray();
        if (near && lines.Length > 0 && !_hud.DialogOpen && _barks.Count < 2 && _client.Latest?.Scene == Snapshot.NoScene)
        {
            var said = lines[Random.Shared.Next(lines.Length)];
            _barks.Add((index, said, 4.5f));
            _voices.SayAt(actor.Id, said, _actorAt[index] + Vector3.Up * 1.5f);
            _barkWait[index] = 28f + Random.Shared.NextSingle() * 30f;
        }
        else
        {
            _barkWait[index] = 3f + Random.Shared.NextSingle() * 3f;
        }
    }

    /// <summary>Where someone the server does not send is: resting by their day, which both sides work out alike.</summary>
    private (bool Active, Vector3 Target, float Yaw, ActorPose Pose) Resting(Actor actor)
    {
        var spot = Schedules.RestOf(actor, _restStory ??= new ClientStory(_client, _hud.Items), _client.Story.Time.Hour, _world);
        return (spot.Present, ToGodot(spot.At), spot.Yaw, spot.Pose);
    }

    private ClientStory? _restStory;

    private SubViewport _portrait = null!;
    private Camera3D _portraitCamera = null!;
    private PlayerRig? _speaking;

    /// <summary>
    /// Points the portrait camera at whoever speaks, sets their expression from the line and moves
    /// their mouth while the words appear. Everyone else goes back to a calm face.
    /// </summary>
    private void UpdateSpeaker()
    {
        // A new line on the screen is said aloud, if it has a recording; anything else cuts the last one off.
        var line = _hud.DialogOpen ? (_hud.Speaker, _hud.DialogText) : default;
        if (line != _voiced)
        {
            _voiced = line;
            _voices.Say(line.Item1 ?? "", line.Item2 ?? "");
        }

        var rig = _hud.DialogOpen && _npcs.TryGetValue(_hud.Speaker, out var found) ? found : null;
        if (_speaking is not null && _speaking != rig)
        {
            if (_speaking.Face is { } old)
            {
                old.Talking = false;
                old.Expression = old.Resting;
            }

            OnPortraitLayer(_speaking.Root, false);
        }

        if (rig is not null && _speaking != rig)
        {
            // The portrait sees only the speaker: nothing in the world (a table, a wall, the player) can stand in the way.
            OnPortraitLayer(rig.Root, true);
            _portraitHead = null;
        }

        _speaking = rig;
        _hud.PortraitShown = rig?.FaceAnchor is not null;
        if (rig?.FaceAnchor is not { } head || rig.Face is not { } face)
        {
            return;
        }

        face.Expression = _hud.DialogFace ?? face.Resting;
        face.Talking = _hud.DialogTyping || _voices.Talking;

        // Framed from the front of the body, on a head position that is eased, so a nod or a breath does not shake it.
        var target = head.GlobalPosition + Vector3.Down * 0.02f;
        _portraitHead = _portraitHead is { } eased ? eased.Lerp(target, 1f - Mathf.Exp(-(float)GetProcessDeltaTime() * 6f)) : target;
        var yaw = rig.Root.GlobalRotation.Y;
        // A figure's front is -Z at yaw 0.
        var front = new Vector3(-Mathf.Sin(yaw), 0, -Mathf.Cos(yaw));
        var eye = _portraitHead.Value + front * 2.4f + Vector3.Up * 0.02f;
        _portraitCamera.LookAtFromPosition(eye, _portraitHead.Value);
    }

    /// <summary>The render layer only the portrait camera sees, besides the world's own.</summary>
    private const uint PortraitLayer = 1u << 19;

    private Vector3? _portraitHead;

    /// <summary>Puts every visible part of a figure on the portrait's layer as well as its own, or takes it off again.</summary>
    private static void OnPortraitLayer(Node node, bool on)
    {
        if (node is VisualInstance3D visual)
        {
            visual.Layers = on ? visual.Layers | PortraitLayer : visual.Layers & ~PortraitLayer;
        }

        foreach (var child in node.GetChildren())
        {
            OnPortraitLayer(child, on);
        }
    }

    private void UpdateNpcs(Vector3 shown, float delta)
    {
        UpdateLook();
        foreach (var (node, flag, unless) in ZoneView.Conditional)
        {
            node.Visible = (flag is null || _client.Story.Has(flag)) && (unless is null || !_client.Story.Has(unless));
        }

        UpdateActors(delta);
        UpdateScene();
        UpdateSpeaker();
        Npc? nearest = null;
        var best = Guild.TalkRange;
        foreach (var actor in _actors)
        {
            // The fallen are not spoken to, nor things a scene moves about (the net).
            // Only people with something to say are spoken to: Mira, the traders, the board. The rest only talk among themselves.
            if (!_actorPresent[actor.Index] || _actorPose[actor.Index] == ActorPose.Fallen || _propViews.ContainsKey(actor.Id) ||
                (!Story.Dialogues.ContainsKey(actor.Id) && actor.Role != "storage"))
            {
                continue;
            }

            var position = _actorAt[actor.Index];
            var npc = new Npc(actor.Id, actor.Name, actor.Role, FromGodot(position), actor.HomeYaw);
            var distance = new Vector2(position.X - shown.X, position.Z - shown.Z).Length();
            if (_npcs.TryGetValue(actor.Id, out var rig) && distance < 6f && !_actorWalking[actor.Index] && _actorPose[actor.Index] == ActorPose.Stand)
            {
                rig.Root.Rotation = new Vector3(0, LerpFacing(rig.Root.Rotation.Y, Mathf.Atan2(position.X - shown.X, position.Z - shown.Z), 6f * delta), 0);
            }

            if (distance < best)
            {
                best = distance;
                nearest = npc;
            }
        }

        var doorAt = DoorDistance(shown);
        var door = doorAt < Guild.DoorRange;
        _hud.Prompt = door && (nearest is null || doorAt < best) ? "[E] Use door"
            : nearest is { Role: "storage" } chest ? $"[E] Open the {chest.Name}"
            : nearest is { Role: Waystones.Role } stone ? $"[E] Touch the {stone.Name}"
            : nearest is { } target ? $"[E] Talk to {target.Name}" : "";
        if (_hud.DialogOpen && nearest is null && !_hud.SceneDialog)
        {
            _hud.CloseDialog();
        }

        var snapshot = _client.Latest;
        if (snapshot is not null && snapshot.Rank != _lastRank)
        {
            if (snapshot.Rank == GuildRank.Adventurer)
            {
                _hud.ShowBanner("You are now an Adventurer!", new Color("fee761"));
                _sfx.Play("level_up", shown);
                _fx.LevelUp(shown);
            }
            else if (snapshot.Rank == GuildRank.InTrial)
            {
                _hud.ShowBanner("Trial: defeat Examiner Rhea", new Color("f6757a"));
            }
            else if (_lastRank == GuildRank.InTrial)
            {
                _hud.ShowBanner("Trial failed. Try again at the Guild.", new Color("e43b44"));
            }

            _lastRank = snapshot.Rank;
        }

        _hud.Objective = Objective(snapshot, shown);
        _hud.Story = new ClientStory(_client, _hud.Items);
        _hud.MarkAwake = !_client.Story.Has("prologue.started") || _client.Story.Has("hud.mark") || _client.Story.Has("prologue.done");
    }

    private string Objective(Snapshot? snapshot, Vector3 shown)
    {
        if (snapshot is null)
        {
            return "";
        }

        if (_sceneApart is { } speaker)
        {
            return $"Go to {speaker.Name}";
        }

        if (_hud.TutorialShown)
        {
            return "Training with Rhea in the yard";
        }

        // On the shore, before the net, the only people in sight are the finders.
        if (_client.Story.Has("prologue.started") && !_client.Story.Has("prologue.captured"))
        {
            return _sceneRooted ? "Caught in the finders' net" : "Go to the men by the shore";
        }

        // The prologue tells its own way; the Guild's hints begin once it is over.
        if (snapshot.Rank != GuildRank.InTrial && _client.Story.Quest("prologue_fallen") is { Status: QuestStatus.Active } fallen &&
            Story.Quests.FirstOrDefault(q => q.Id == "prologue_fallen") is { } prologue)
        {
            return prologue.Steps[Math.Min(fallen.Step, prologue.Steps.Length - 1)].Text;
        }

        if (snapshot.Rank == GuildRank.Newcomer)
        {
            return shown.X > GuildInteriorX && shown.X < ArenaX ? "Talk to Mira at the counter" : "Find the Adventurers Guild in the west";
        }

        if (snapshot.Rank == GuildRank.InTrial)
        {
            return "Defeat Examiner Rhea. Block her quick blows; when the ground goes red, dash (Shift) just as it falls";
        }

        var story = new ClientStory(_client, _hud.Items);
        foreach (var quest in Story.Quests.Where(q => _client.Story.Quest(q.Id).Status == QuestStatus.Active))
        {
            if (quest.Kind == "job" && Story.QuestStatusName(story, quest.Id) == "ready")
            {
                return $"{quest.Name}: report to Mira";
            }

            var (done, needed) = Story.StepProgress(story, quest.Id);
            return needed > 0 ? $"{quest.Name}: {done} / {needed}" : $"{quest.Name}: {quest.Steps[_client.Story.Quest(quest.Id).Step].Text}";
        }

        if (shown.X > ArchiveX)
        {
            return "Explore the Sunken Archive, or leave by the cave mouth";
        }

        if (shown.X > ArenaX)
        {
            return "Leave through the gate";
        }

        return shown.X > GuildInteriorX ? "Take a job from the Quest Board" : "Hunt, or return to the Guild for a job";
    }

    /// <summary>E talks to whoever is close, or goes through a door.</summary>
    private void Interact()
    {
        if (_hud.DialogOpen && _hud.SceneDialog && _hud.DialogLabels.FirstOrDefault() == "Continue")
        {
            // E moves a scene's line on, as a click on Continue would.
            (_pendingCommand, _pendingArg) = _hud.Choose(0);
            return;
        }

        if (_hud.DialogOpen)
        {
            _hud.CloseDialog();
            return;
        }

        var position = ToGodot(_client.State.Position);
        var npc = _actors.Where(a => _actorPresent[a.Index] && _actorPose[a.Index] != ActorPose.Fallen)
            .Select(a => (Actor: a, At: _actorAt[a.Index]))
            .Where(p => new Vector2(p.At.X - position.X, p.At.Z - position.Z).Length() < Guild.TalkRange)
            .OrderBy(p => new Vector2(p.At.X - position.X, p.At.Z - position.Z).Length())
            .Select(p => (Npc?)new Npc(p.Actor.Id, p.Actor.Name, p.Actor.Role, FromGodot(p.At), p.Actor.HomeYaw)).FirstOrDefault();
        // A door closer than anyone standing by wins: someone who walks along must not stand in its way.
        var doorAt = DoorDistance(position);
        if (npc is { } target && !(doorAt < Guild.DoorRange && doorAt < new Vector2(target.Position.X - position.X, target.Position.Z - position.Z).Length()))
        {
            Dialogues.Open(_hud, target, new ClientStory(_client, _hud.Items), _client.Latest);
            return;
        }

        _pendingCommand = Command.UseDoor;
    }

    private float DoorDistance(Vector3 position) => _world.Portals.Count == 0 ? float.MaxValue
        : _world.Portals.Min(p => new Vector2(p.From.X - position.X, p.From.Z - position.Z).Length());

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

        if (now.Attack == AttackKind.BowShot && _last.Attack != AttackKind.BowShot)
        {
            // A full draw thrums; a quick shot only twangs.
            _sfx.Play(now.ShotDraw >= Tuning.FullDraw * 0.8f ? "bow_power" : "bow_shot", chest);
        }
        else if (now.Attack != AttackKind.None && now.AttackTick < _last.AttackTick | now.Attack != _last.Attack)
        {
            _sfx.Play(now.Attack is AttackKind.Heavy or AttackKind.DaggerLeap ? "swing_heavy" : "swing_light", chest);
            if (now.Attack is AttackKind.Heavy or AttackKind.DaggerLeap or AttackKind.ClubSpin)
            {
                _player.Face?.Flash("effort", 0.5f);
            }
        }

        // The face shows how the fight goes: worn out with little stamina left, calm otherwise.
        if (_player.Face is { } face)
        {
            face.Expression = now.Stamina < Attributes.MaxStamina(now) * 0.25f ? "tired" : face.Resting;
        }

        // The second stab of a flurry has its own swish; a leap ends in a thump and a ring of dust.
        var attack = Tuning.Attack(now.Attack);
        if (now.Attack == AttackKind.DaggerFlurry && now.AttackTick >= attack.SecondHitAt - 3 && _last.AttackTick < attack.SecondHitAt - 3)
        {
            _sfx.Play("swing_light", chest, -2f);
        }

        if (now.Attack == AttackKind.ClubSpin && now.AttackTick >= attack.HitAt && _last.AttackTick < attack.HitAt && HasMove("shockwave"))
        {
            // Club mastery Shockwave: a ring of force races out from the spin.
            _sfx.Play("shockwave", shown);
            _fx.Ring(shown, new Color("ead4aa"), Tuning.ShockwaveRadius, 0.45f);
            _fx.Burst(shown + Vector3.Up * 0.1f, new Color("b8a58a"), 30, 7f, 0.4f, 0.09f, -6f, 88f);
        }

        if (now.Attack == AttackKind.DaggerLeap && now.AttackTick >= attack.HitAt && _last.AttackTick < attack.HitAt)
        {
            _sfx.Play("hit_heavy", shown);
            _fx.Burst(shown + Vector3.Up * 0.05f, new Color("b8a58a"), 26, 5f, 0.45f, 0.09f, -5f, 85f);
            if (HasMove("fan_of_knives"))
            {
                _sfx.Play("air_slash", chest, -3f);
            }
        }

        // Runs of blows (Whirlwind, Blade Dance) swish on every blow after the first.
        if (attack.HitEvery > 0 && now.Attack == _last.Attack && now.AttackTick > attack.HitAt && attack.HitsOn(now.AttackTick) && !attack.HitsOn(_last.AttackTick))
        {
            _sfx.Play("swing_light", chest, -3f);
        }

        if (now.Attack == AttackKind.GroundPound && now.AttackTick >= Tuning.GroundPoundLandTick && _last.AttackTick < Tuning.GroundPoundLandTick)
        {
            // Ground Pound: the club meets the ground in a ring of force and dust.
            _sfx.Play("club_slam", shown);
            _sfx.Play("shockwave", shown, -4f);
            _fx.Ring(shown, new Color("ead4aa"), Tuning.Attack(AttackKind.GroundPound).Range, 0.4f);
            _fx.Burst(shown + Vector3.Up * 0.1f, new Color("b8a58a"), 40, 7f, 0.5f, 0.1f, -6f, 88f);
        }

        if (now.Attack == AttackKind.ShadowStep && _last.Attack != AttackKind.ShadowStep)
        {
            // Shadow Step: the player melts into shadow here and comes out behind the enemy.
            _sfx.Play("shadow_sink", shown);
            _fx.Burst(shown + Vector3.Up * 0.8f, new Color("181425"), 30, 4f, 0.5f, 0.12f, 1f, 120f);
        }
        else if (now.Attack == AttackKind.ShadowStep && now.AttackTick >= 4 && _last.AttackTick < 4)
        {
            _sfx.Play("shadow_rise", shown);
            _fx.Burst(shown + Vector3.Up * 0.8f, new Color("3a4466"), 24, 3f, 0.45f, 0.1f, 1f, 120f);
        }

        if (now.Attack == AttackKind.RisingSlash && now.AttackTick >= attack.HitAt && _last.AttackTick < attack.HitAt)
        {
            _fx.Burst(chest + Vector3.Up * 0.3f, new Color("feae34"), 22, 8f, 0.35f, 0.07f, -4f, 25f);
        }

        if (now.Health < _last.Health && _last.Health > 0)
        {
            _sfx.Play("hurt", chest);
            _player.Face?.Flash("pain", 0.45f);
            _hud.AddFloating($"-{_last.Health - now.Health}", chest + Vector3.Up, new Color("e43b44"));
        }
        else if (now.Health > _last.Health + 3 && _last.Health > 0)
        {
            // Regeneration adds one point at a time; a bigger jump is a meal.
            _hud.AddFloating($"+{now.Health - _last.Health}", chest + Vector3.Up, new Color("63c74d"));
            _fx.Burst(chest, new Color("63c74d"), 10, 2f, 0.5f, 0.06f, 1f);
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

        if (snapshot.Blocks != _lastBlocks)
        {
            _lastBlocks = snapshot.Blocks;
            _sfx.Play("block", chest);
            _fx.Burst(chest + Vector3.Up * 0.3f, new Color("ead4aa"), 10, 4f, 0.25f, 0.05f);
        }

        if (snapshot.Parries != _lastParries)
        {
            // A parry: a bright ring of sparks and a clear note.
            _lastParries = snapshot.Parries;
            _sfx.Play("parry", chest);
            _fx.Burst(chest + Vector3.Up * 0.3f, new Color("fee761"), 26, 7f, 0.35f, 0.07f, 0f, 180f);
            _fx.Burst(chest + Vector3.Up * 0.3f, new Color("ffffff"), 12, 5f, 0.25f, 0.05f, 0f, 180f);
            _hud.AddFloating("Parry!", chest + Vector3.Up, new Color("fee761"));
        }

        _barrier.Visible = snapshot.Barrier > 0;

        if (snapshot.Ripostes != _lastRipostes)
        {
            // Riposte: the sword answers the parry with a fast cut across.
            _lastRipostes = snapshot.Ripostes;
            _castTime = 0.3f;
            _castMove = WeaponMove.SwingAcross;
            _sfx.Play("air_slash", chest);
            _hud.AddFloating("Riposte!", chest + Vector3.Up * 1.4f, new Color("ffffff"));
        }

        if (snapshot.Level > _lastLevel)
        {
            _lastLevel = snapshot.Level;
            _lastXp = snapshot.Xp;
            _sfx.Play("level_up", chest);
            _fx.LevelUp(shown);
            _hud.ShowBanner($"Level Up!  Lv {snapshot.Level}  +{Attributes.PointsPerLevel} attribute points", new Color("fee761"));
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
            if (snapshot.Crystals[i] > _lastCrystals[i] && _lastSnapshot is not null)
            {
                _sfx.Play("crystal", chest);
                _hud.AddLoot(Inventory.Def(i), snapshot.Crystals[i] - _lastCrystals[i]);
            }

            _lastCrystals[i] = snapshot.Crystals[i];
        }

        // Each snapshot carries part of the bag; new items join the bag and are announced.
        if (!ReferenceEquals(snapshot, _lastSnapshot))
        {
            var first = _lastSnapshot is null;
            foreach (var piece in _hud.Items.ApplyGear(snapshot.Gear))
            {
                _hud.AddLoot(piece, 1);
            }

            _hud.Items.ApplyChest(snapshot.Storage, snapshot.StoredGear);
            _hud.Items.ApplySkills(snapshot.KnownSkills);
            foreach (var (item, gained) in _hud.Items.Apply(snapshot.Bag))
            {
                if (!first)
                {
                    _hud.AddLoot(item, gained);
                    _sfx.Play("crystal", chest, -8f);
                }
            }

            _lastSnapshot = snapshot;
        }

        var region = _zone.RegionAt(shown.X, shown.Z);
        if (region != _region && region.Length > 0)
        {
            _hud.ShowPlace(region);
            Settings.Discover(region);
        }

        _minimap.Size = new Vector2(Hud.MinimapSize, Hud.MinimapSize);
        _minimap.Position = new Vector2(_hud.Size.X - Hud.MinimapSize - 28f, 28f);
        // Nothing of the world shows over a slide or a full fade, the map included.
        _minimap.Visible = !_hud.SlideShown && _hud.Fade < 0.95f;
        _minimap.Player = new Vector2(shown.X, shown.Z);
        _minimap.Yaw = _yaw;

        // Where the player is led: the own mark or the quest's target, by the roads, on both maps and on the ground.
        var seen = _client.Latest is { } shownNow
            ? shownNow.Enemies.Where(e => e.Health > 0).Select(e => (EnemyDef.All[e.Type], e.Position)).ToList()
            : null;
        _navigator.Update(delta, _client.State.Position, new ClientStory(_client, _hud.Items), seen);
        _minimap.Route = _navigator.Route;
        _minimap.Target = _navigator.Target;
        _worldMap.Player = new Vector2(shown.X, shown.Z);
        _worldMap.Yaw = _yaw;
        if (_worldMap.Visible != _hud.MapOpen)
        {
            if (_hud.MapOpen)
            {
                _worldMap.Open();
            }
            else
            {
                _worldMap.Visible = false;
            }
        }

        _region = region.Length > 0 ? region : _region;
    }

    private byte AimByte() => (byte)Mathf.PosMod(Mathf.Round(_yaw / Mathf.Tau * 256f), 256f);

    /// <summary>
    /// Where the next arrow would go. With a bow the arrow flies from the bow to whatever lies under
    /// the crosshair, so the aim is worked out from that point, not from the camera's turn alone.
    /// Other weapons aim along the camera.
    /// </summary>
    private (byte Aim, ushort Yaw, short Pitch) ShotAim()
    {
        if (_client.State.Weapon is not (Weapons.Bow or Weapons.Staff))
        {
            // Movement and blows use the camera's turn; skills fly through the crosshair.
            var (_, crossYaw, crossPitch) = CrosshairAim();
            return (AimByte(), crossYaw, crossPitch);
        }

        if (_botShot is { } bot)
        {
            _shotPitch = bot.Pitch / 65536f * Mathf.Tau;
            return ((byte)((bot.Yaw + 128) >> 8), bot.Yaw, bot.Pitch);
        }

        var (byteAim, fineYaw, finePitch) = CrosshairAim();
        _shotPitch = Mathf.Clamp(finePitch / 65536f * Mathf.Tau, -Tuning.MaxShotPitch, Tuning.MaxShotPitch);
        return (byteAim, fineYaw, finePitch);
    }

    /// <summary>The aim from the bow (in front of the chest) to whatever lies under the crosshair.</summary>
    private (byte Aim, ushort Yaw, short Pitch) CrosshairAim()
    {
        var camera = _stage.Camera.GlobalTransform;
        var point = AimPoint(camera.Origin, -camera.Basis.Z);
        var feet = _client.State.Position;
        var flatYaw = new System.Numerics.Vector3(-Mathf.Sin(_yaw), 0, -Mathf.Cos(_yaw));
        var origin = GameServer.ArrowOrigin(feet, flatYaw);
        var to = new Vector3(point.X - origin.X, point.Y - origin.Y, point.Z - origin.Z);
        var yaw = Mathf.Atan2(-to.X, -to.Z);
        var pitch = Mathf.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
        var fineYaw = (ushort)((int)Mathf.Round(Mathf.PosMod(yaw / Mathf.Tau, 1f) * 65536f) & 0xFFFF);
        return ((byte)((fineYaw + 128) >> 8), fineYaw, (short)Mathf.Round(pitch / Mathf.Tau * 65536f));
    }

    /// <summary>The first thing along a ray from the camera: the ground, a solid box or an enemy, or the ray's end.</summary>
    private Vector3 AimPoint(Vector3 from, Vector3 direction)
    {
        var enemies = _client.Latest?.Enemies ?? System.Array.Empty<EnemyView>();
        for (var along = 0.5f; along < AimRayLength; along += 0.2f)
        {
            var p = from + direction * along;
            var point = new System.Numerics.Vector3(p.X, p.Y, p.Z);
            if (point.Y < _world.GroundHeight(point.X, point.Z) || _world.IsInsideBox(point))
            {
                return p;
            }

            foreach (var enemy in enemies)
            {
                var size = EnemyDef.All[enemy.Type].Size;
                var chest = enemy.Position + new System.Numerics.Vector3(0, size * 0.6f, 0);
                if (enemy.Action != EnemyAction.Dead && System.Numerics.Vector3.Distance(chest, point) < size * 0.5f)
                {
                    return p;
                }
            }
        }

        return from + direction * AimRayLength;
    }

    /// <summary>Eases the camera into the aim while a bow is drawn, and out again a moment after the last shot.</summary>
    private void UpdateAim(float delta)
    {
        var state = _client.State;
        var aiming = state.Weapon == Weapons.Bow && (state.DrawTicks > 0 || state.Attack == AttackKind.BowShot);
        _aimHold = aiming ? AimSeconds : Mathf.Max(0f, _aimHold - delta);
        _aimBlend = Mathf.MoveToward(_aimBlend, _aimHold > 0f ? 1f : 0f, delta * (_aimHold > 0f ? 5f : 3f));
        _pitch = Mathf.Min(_pitch, Mathf.Lerp(MaxPitch, AimMaxPitch, _aimBlend));
        var eased = _aimBlend * _aimBlend * (3f - 2f * _aimBlend);
        _stage.Camera.Fov = BaseFov - eased * (7f + 7f * Tuning.Drawn(state.DrawTicks));
    }

    /// <summary>A mouse button pressed while a window or dialogue was open, still held: it is not an attack until let go.</summary>
    private bool _mouseHeldFromUi;

    private (sbyte X, sbyte Z, Buttons Buttons) ReadInput()
    {
        var mouseDown = Input.IsMouseButtonPressed(MouseButton.Left) || Input.IsMouseButtonPressed(MouseButton.Right);
        if (_hud.WindowOpen || _hud.DialogOpen || _hud.MenuOpen)
        {
            // The click that picks a dialogue choice or closes a window must not land as a blow once it closes.
            _mouseHeldFromUi = true;
            return (0, 0, _hud.DialogOpen || _hud.MenuOpen || _hud.CreatorOpen ? Buttons.Reading : Buttons.None);
        }

        if (!mouseDown)
        {
            _mouseHeldFromUi = false;
        }

        if (_sceneLocked)
        {
            return (0, 0, Buttons.None);
        }

        var side = (Input.IsKeyPressed(Key.D) ? 1f : 0f) - (Input.IsKeyPressed(Key.A) ? 1f : 0f);
        var forward = (Input.IsKeyPressed(Key.W) ? 1f : 0f) - (Input.IsKeyPressed(Key.S) ? 1f : 0f);
        var buttons = Buttons.None;
        if (Input.IsKeyPressed(Key.Space)) buttons |= Buttons.Jump;
        if (Input.IsKeyPressed(Key.Shift)) buttons |= Buttons.Dash;
        if (Input.IsMouseButtonPressed(MouseButton.Left) && !_mouseHeldFromUi) buttons |= Buttons.Light;
        if (Input.IsMouseButtonPressed(MouseButton.Right) && !_mouseHeldFromUi) buttons |= Buttons.Heavy;
        if (Input.IsMouseButtonPressed(MouseButton.Middle)) buttons |= Buttons.Block;
        if (Input.IsKeyPressed(Key.Ctrl)) buttons |= Buttons.Slide;
        return ToMove(side, forward, buttons);
    }

    private (sbyte X, sbyte Z, Buttons Buttons) BotInput()
    {
        var mode = OS.GetEnvironment("PIXEL_BOT");
        if (_hud.TutorialShown && mode != "tutorial")
        {
            // Bots test other things: they skip Rhea's training (PIXEL_BOT=tutorial plays it).
            BotStage("skip the training");
            if (_client.Tick % 30 == 0 && !_hud.DialogOpen)
            {
                (_pendingCommand, _pendingArg) = (Command.SkipTutorial, 0);
            }

            return (0, 0, Buttons.None);
        }

        if (mode == "guild")
        {
            return GuildBot();
        }

        if (mode == "hunt")
        {
            return HuntBot();
        }

        if (mode == "gear")
        {
            return GearBot();
        }

        if (mode == "bow")
        {
            return BowBot();
        }

        if (mode is "club" or "staff")
        {
            return MeleeBot(mode);
        }

        if (mode == "prologue")
        {
            return PrologueBot();
        }

        if (mode == "map")
        {
            return MapBot();
        }

        if (mode == "tutorial")
        {
            return TutorialBot();
        }

        if (mode == "creator")
        {
            // The creator opens by itself for this bot; halfway through, every part is picked at random.
            if (_client.Tick == 150)
            {
                _hud.RandomLook();
            }

            return (0, 0, Buttons.None);
        }

        if (mode is "character" or "mastery" or "journal")
        {
            // K, J or L as a real key opens the character, the mastery or the journal window.
            if (_client.Tick is 60 or 61)
            {
                PressKey(mode == "character" ? Key.K : mode == "mastery" ? Key.J : Key.L, _client.Tick == 60);
            }

            return (0, 0, Buttons.None);
        }

        if (mode == "menu")
        {
            // Esc as a real key opens the menu; then the settings page shows.
            if (_client.Tick is 60 or 61)
            {
                PressKey(Key.Escape, _client.Tick == 60);
            }

            if (_client.Tick == 200)
            {
                _hud.ShowMenuPage("settings");
            }

            return (0, 0, Buttons.None);
        }

        if ((mode == "trial" && _client.Tick == 60) || (mode is "talk" or "rumor" && _client.Tick % 60 == 0 && !_hud.DialogOpen))
        {
            Interact();
        }

        // The rumor bot asks whoever it talks to for news, and for more while there is any.
        if (mode == "rumor" && _client.Tick % 60 == 40 && _hud.DialogOpen &&
            _hud.DialogLabels.FirstOrDefault(l => l is "Heard anything?" or "Read the notices." or "Anything else?") is { } ask)
        {
            (_pendingCommand, _pendingArg) = _hud.ChooseLabel(ask);
            GD.Print($"BOT rumor: {_hud.DialogText}");
        }

        if (mode == "trial" && _client.Tick == 90)
        {
            (_pendingCommand, _pendingArg) = _hud.Choose(0);
        }

        if (mode == "trial" && _client.Tick > 90)
        {
            return (0, 0, _client.Tick % 40 == 0 ? Buttons.Light : Buttons.None);
        }

        if (mode is "idle" or "talk" or "trial" or "rumor")
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

    /// <summary>
    /// Plays the Guild through the real interface: sign with Mira, take a job at the board and sell
    /// any crystals. (Rhea's trial is the step to rank E from level five.) Each stage is printed as BOT.
    /// </summary>
    private (sbyte X, sbyte Z, Buttons Buttons) GuildBot()
    {
        var snapshot = _client.Latest;
        if (snapshot is null || _client.Tick < 60)
        {
            return (0, 0, Buttons.None);
        }

        var mira = _world.Npcs.First(n => n.Role == "receptionist").Position;
        var board = _world.Npcs.First(n => n.Role == "quest_board").Position;
        if (snapshot.Rank == GuildRank.Newcomer)
        {
            return BotConverse("sign with Mira", new Vector3(mira.X, 0, mira.Z + 2.6f), "Sign me up.");
        }

        if (snapshot.Rank == GuildRank.InTrial)
        {
            BotStage("fight the examiner");
            if (snapshot.Enemies.Length == 0)
            {
                return (0, 0, Buttons.None);
            }

            var foe = snapshot.Enemies[0].Position;
            return BotWalk(new Vector3(foe.X, 0, foe.Z), 1.6f, _client.Tick % 40 == 0 ? Buttons.Light : Buttons.None);
        }

        var position = _client.State.Position;
        if (position.X > ArenaX)
        {
            BotStage("leave the arena");
            var gate = _world.Portals.OrderBy(p => System.Numerics.Vector3.DistanceSquared(p.From, position)).First().From;
            var walk = BotWalk(new Vector3(gate.X, 0, gate.Z), 1f, Buttons.None);
            if (walk == (0, 0, Buttons.None) && _client.Tick % 60 == 0)
            {
                Interact();
            }

            return walk;
        }

        if (Story.ActiveJobs(_client.Story) == 0)
        {
            return BotConverse("take a job", new Vector3(board.X + 2.2f, 0, board.Z), "Clear the Slimes");
        }

        return snapshot.Crystals.Sum() > 0
            ? BotConverse("sell crystals", new Vector3(mira.X, 0, mira.Z + 2.6f), "Sell all crystals")
            : BotDone();
    }

    /// <summary>
    /// Checks gear through the real interface (start the server with PIXEL_TEST=1 for a starting
    /// kit): opens the bag, wears the legendary piece with a right click, then points at the epic one
    /// so the capture shows its tooltip and the worn piece in its slot.
    /// </summary>
    private (sbyte X, sbyte Z, Buttons Buttons) GearBot()
    {
        var tick = _client.Tick;
        if (tick < 90)
        {
            return (0, 0, Buttons.None);
        }

        if (tick < 300)
        {
            if (!_hud.InventoryOpen)
            {
                _hud.ToggleInventory();
                BotStage("open the bag");
            }

            var legendary = _hud.Items.Bag.FirstOrDefault(i => i?.Gear?.Rarity == Rarity.Legendary);
            if (legendary is not null && tick == 120)
            {
                BotStage("wear the legendary piece");
                _pendingCommand = GearCommands.Toggle(legendary.Place);
            }

            var daggers = _hud.Items.Bag.FirstOrDefault(i => i?.Gear?.Def.Weapon == "dagger");
            if (daggers is not null && tick == 160)
            {
                BotStage("take up the daggers");
                _pendingCommand = GearCommands.Toggle(daggers.Place);
            }

            // Swapping weapons must put the daggers back in the bag, not lose them.
            var bow = _hud.Items.Bag.FirstOrDefault(i => i?.Gear?.Def.Weapon == "bow");
            if (bow is not null && tick == 200)
            {
                BotStage("swap to the bow");
                _pendingCommand = GearCommands.Toggle(bow.Place);
            }

            if (daggers is not null && tick == 235)
            {
                BotStage("swap back to the daggers");
                _pendingCommand = GearCommands.Toggle(daggers.Place);
            }

            var epic = Array.FindIndex(_hud.Items.Bag, i => i?.Gear?.Rarity == Rarity.Epic);
            if (epic >= 0 && _hud.SlotCentre("bag", epic) is { } slot)
            {
                GetViewport().WarpMouse(slot);
            }

            return (0, 0, Buttons.None);
        }

        // Then close the bag and fight with the daggers: flurries, and a leap now and then.
        if (_hud.InventoryOpen)
        {
            _hud.ToggleInventory();
            BotStage("fight with the daggers");
        }

        var buttons = tick % 90 == 0 ? Buttons.Heavy : tick % 30 == 0 ? Buttons.Light : Buttons.None;
        return (0, 0, buttons);
    }

    /// <summary>
    /// Checks the bow (start the server with PIXEL_TEST=1): takes up the longbow, then turns towards
    /// the nearest enemy and draws and looses again and again, some quick shots and some full draws.
    /// </summary>
    private (sbyte X, sbyte Z, Buttons Buttons) BowBot()
    {
        var tick = _client.Tick;
        if (tick == 100 && _hud.Items.Bag.FirstOrDefault(i => i?.Gear?.Def.Weapon == "bow") is { } bow)
        {
            BotStage("take up the bow");
            _pendingCommand = GearCommands.Toggle(bow.Place);
        }

        if (tick < 160 || _client.Latest is not { } snapshot)
        {
            return (0, 0, Buttons.None);
        }

        BotStage("shoot");
        var me = _client.State.Position;

        // Every other shot is a quick one.
        var cycle = (int)(tick % 150);
        var drawFor = tick / 150 % 2 == 0 ? 80 : 15;
        if (snapshot.Enemies.Where(e => e.Action != EnemyAction.Dead).OrderBy(e => (e.Position - me).Length()).FirstOrDefault() is { Id: > 0 } target)
        {
            var to = target.Position - me;
            _yaw = Mathf.Atan2(-to.X, -to.Z);
            _botShot = BotAim(me, target.Position + new System.Numerics.Vector3(0, EnemyDef.All[target.Type].Size * 0.6f, 0), drawFor);
        }

        return (0, 0, cycle < drawFor ? Buttons.Light : Buttons.None);
    }

    /// <summary>
    /// Checks the club or the staff (start the server with PIXEL_TEST=1): takes the weapon up, turns
    /// to the nearest enemy and walks up to it, then uses the light attack and the heavy one in turn.
    /// </summary>
    private (sbyte X, sbyte Z, Buttons Buttons) MeleeBot(string weapon)
    {
        var tick = _client.Tick;
        var item = weapon == "club" ? "spiked_club" : "bone_staff";
        if (tick == 100 && _hud.Items.Bag.FirstOrDefault(i => i?.Gear?.Def.Id == item) is { } piece)
        {
            BotStage($"take up the {weapon}");
            _pendingCommand = GearCommands.Toggle(piece.Place);
        }

        if (tick < 150 || _client.Latest is not { } snapshot)
        {
            return (0, 0, Buttons.None);
        }

        BotStage("fight");
        var me = _client.State.Position;
        if (snapshot.Enemies.Where(e => e.Action != EnemyAction.Dead).OrderBy(e => (e.Position - me).Length()).FirstOrDefault() is not { Id: > 0 } target)
        {
            return (0, 0, Buttons.None);
        }

        var to = target.Position - me;
        _yaw = Mathf.Atan2(-to.X, -to.Z);
        var reach = weapon == "club" ? 2.2f : 7f;
        _botShot = weapon == "staff" ? BotAim(me, target.Position + new System.Numerics.Vector3(0, EnemyDef.All[target.Type].Size * 0.6f, 0), Tuning.FullDraw) : null;
        if (new Vector2(to.X, to.Z).Length() > reach)
        {
            return ToMove(0f, 1f, Buttons.None);
        }

        var cycle = (int)(tick % 120);
        return (0, 0, cycle == 0 ? (tick / 120 % 2 == 0 ? Buttons.Light : Buttons.Heavy) : Buttons.None);
    }

    /// <summary>The fine aim from the bow at a point, lifted by the drop, as an archer would aim.</summary>
    private static (ushort Yaw, short Pitch) BotAim(System.Numerics.Vector3 feet, System.Numerics.Vector3 point, int draw)
    {
        var flat = System.Numerics.Vector3.Normalize((point - feet) with { Y = 0 });
        var to = point - GameServer.ArrowOrigin(feet, flat);
        var time = to.Length() / Tuning.ArrowSpeed(Tuning.Drawn((byte)Math.Min(Math.Max(draw, Tuning.MinDraw), 255)));
        to.Y += 0.5f * Tuning.ArrowGravity * time * time;
        var yaw = MathF.Atan2(-to.X, -to.Z);
        var pitch = MathF.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
        return ((ushort)((int)MathF.Round(((yaw / MathF.Tau) % 1f + 1f) % 1f * 65536f) & 0xFFFF), (short)MathF.Round(pitch / MathF.Tau * 65536f));
    }

    /// <summary>
    /// Hunts whatever lives nearby, walks over the loot, eats once, then opens the bag and points at
    /// the first slot so the capture shows the inventory and a tooltip. Start it near a camp.
    /// </summary>
    private (sbyte X, sbyte Z, Buttons Buttons) HuntBot()
    {
        var snapshot = _client.Latest;
        if (snapshot is null || _client.Tick < 60)
        {
            return (0, 0, Buttons.None);
        }

        var position = ToGodot(_client.State.Position);
        if (_client.Tick > HuntTicks)
        {
            BotStage("show the bag");
            if (!_hud.InventoryOpen)
            {
                _hud.ToggleInventory();
            }

            if (_hud.SlotCentre("bag", 0) is { } slot)
            {
                GetViewport().WarpMouse(slot);
            }

            return (0, 0, Buttons.None);
        }

        if (_hud.Items.Counts.FirstOrDefault(p => p.Value > 0 && ItemDef.All[p.Key].IsFood) is { Value: > 0 } food &&
            _client.State.Health < snapshot.MaxHealth && _botWait++ % 120 == 0)
        {
            _pendingCommand = ItemCommands.Eat((ushort)food.Key);
        }

        if (snapshot.Drops.Length > 0)
        {
            BotStage("pick up loot");
            var drop = snapshot.Drops.OrderBy(d => ToGodot(d.Position).DistanceTo(position)).First();
            return BotWalk(ToGodot(drop.Position), 0.3f, Buttons.None);
        }

        var prey = snapshot.Enemies.Where(e => e.Action != EnemyAction.Dead)
            .OrderBy(e => ToGodot(e.Position).DistanceTo(position)).Cast<EnemyView?>().FirstOrDefault();
        if (prey is not { } target)
        {
            BotStage("look for prey");
            return ToMove(0, 1, Buttons.None);
        }

        BotStage($"hunt {EnemyDef.All[target.Type].Id}");
        var at = ToGodot(target.Position);
        return BotWalk(at, 1.5f, at.DistanceTo(position) < 2.2f && _client.Tick % 18 == 0 ? Buttons.Light : Buttons.None);
    }

    /// <summary>
    /// Plays the prologue through the real interface: picks a look, reads every line, takes the first
    /// choice of every talk, fights what the scenes send, and walks to where the next scene begins.
    /// Start a new game for it (no save, not test mode); PIXEL_FRAIL=1 skips the trial's lessons.
    /// </summary>
    private (int Scene, int Step) _botScene = (-2, 0);
    private RoadMap? _botRoads;
    private List<System.Numerics.Vector3> _botRoute = new();
    private Vector3 _botRouteGoal = new(float.MaxValue, 0, 0);
    private int _botLeg;
    private Vector3 _botStuckAt;
    private uint _botStuckTick;

    /// <summary>
    /// Walks to a place the way people do: along the roads and round what stands in between
    /// (<see cref="RoadMap.Route"/>), not in a straight line into a wall. A bot that has not moved for a
    /// second and a half jumps and plans again from where it stands.
    /// </summary>
    private (sbyte X, sbyte Z, Buttons Buttons) BotGo(Vector3 goal, float reach, Buttons buttons)
    {
        var position = ToGodot(_client.State.Position);
        _botRoads ??= new RoadMap(_world.Roads);
        var stuck = false;
        if (_client.Tick - _botStuckTick >= 90)
        {
            stuck = position.DistanceTo(_botStuckAt) < 0.5f && new Vector2(goal.X - position.X, goal.Z - position.Z).Length() > reach;
            _botStuckAt = position;
            _botStuckTick = _client.Tick;
        }

        if (stuck || _botRouteGoal.DistanceTo(goal) > 1.5f)
        {
            _botRoute = _botRoads.Route(_client.State.Position, FromGodot(goal), _world);
            _botRouteGoal = goal;
            _botLeg = 0;
        }

        while (_botLeg < _botRoute.Count - 1 &&
               new Vector2(_botRoute[_botLeg].X - position.X, _botRoute[_botLeg].Z - position.Z).Length() < 1.3f)
        {
            _botLeg++;
        }

        var last = _botLeg >= _botRoute.Count - 1;
        var walk = BotWalk(last ? goal : ToGodot(_botRoute[_botLeg]), last ? reach : 0.2f, buttons);
        return stuck ? (walk.X, walk.Z, walk.Buttons | Buttons.Jump) : walk;
    }

    private (sbyte X, sbyte Z, Buttons Buttons) PrologueBot()
    {
        var snapshot = _client.Latest;
        if (snapshot is null || _client.Tick < 60)
        {
            return (0, 0, Buttons.None);
        }

        var story = _client.Story;
        var sceneNow = (snapshot.Scene == Snapshot.NoScene ? -1 : snapshot.Scene, (int)snapshot.SceneStep);
        if (sceneNow != _botScene)
        {
            // Each scene step in the log shows where a stuck run stopped.
            _botScene = sceneNow;
            var at = _client.State.Position;
            GD.Print(sceneNow.Item1 < 0 ? $"BOT scene none at tick {_client.Tick}"
                : $"BOT scene {Story.Scenes[sceneNow.Item1].Id} step {sceneNow.Item2} {Story.Scenes[sceneNow.Item1].Steps[Math.Min(sceneNow.Item2, Story.Scenes[sceneNow.Item1].Steps.Length - 1)].Kind} at tick {_client.Tick}, player {at.X:0.0},{at.Z:0.0}, dialog {_hud.DialogOpen}, locked {_sceneLocked}");
        }

        if (_hud.CreatorOpen)
        {
            BotStage("pick a look");
            if (++_botWait == 40)
            {
                _hud.RandomLook();
            }
            else if (_botWait > 90)
            {
                _hud.ConfirmLook();
            }

            return (0, 0, Buttons.None);
        }

        if (_videoStep >= 0)
        {
            // Watch a little, then skip: the run proves the skip works and saves the rest of the film.
            BotStage("watch the intro film");
            if (++_botWait == 120 && OS.GetEnvironment("PIXEL_FILM") != "full")
            {
                (_pendingCommand, _pendingArg) = (Command.SceneNext, (ushort)_videoStep);
            }

            return (0, 0, Buttons.None);
        }

        if (_hud.SlideShown)
        {
            BotStage("watch the intro");
            return (0, 0, Buttons.None);
        }

        if (_hud.DialogOpen)
        {
            if (++_botWait > BotDialogTicks)
            {
                (_pendingCommand, _pendingArg) = _hud.Choose(0);
                _botWait = 0;
            }

            return (0, 0, Buttons.None);
        }

        if (_sceneLocked)
        {
            return (0, 0, Buttons.None);
        }

        if (_sceneApart is { } speaker)
        {
            BotStage($"walk up to {speaker.Name}");
            return BotGo(_actorAt[speaker.Index], 2.4f, Buttons.None);
        }

        // Things on the ground come first: the dagger by the cage is the only weapon there is.
        var position = ToGodot(_client.State.Position);
        if (story.Has("prologue.free") && !story.Has("prologue.slept") && position.X > Guild.InteriorX && _client.Tick % 300 == 0)
        {
            GD.Print("BOT ERROR inside the Guild before the night at Elda's");
        }

        if (snapshot.Drops.Where(d => ToGodot(d.Position).DistanceTo(position) < 9f).OrderBy(d => ToGodot(d.Position).DistanceTo(position)).Cast<LootView?>().FirstOrDefault() is { } drop)
        {
            BotStage("pick up what lies there");
            return BotWalk(ToGodot(drop.Position), 0.3f, Buttons.None);
        }

        // Fight anything the scenes sent, close by.
        var foe = snapshot.Enemies.Where(e => e.Health > 0 && EnemyDef.All[e.Type].Kind != "animal" && ToGodot(e.Position).DistanceTo(position) < (snapshot.Rank == GuildRank.InTrial ? 40f : 10f))
            .OrderBy(e => ToGodot(e.Position).DistanceTo(position)).Cast<EnemyView?>().FirstOrDefault();
        if (foe is { } enemy)
        {
            BotStage(snapshot.Rank == GuildRank.InTrial ? "fight the examiner" : $"fight {EnemyDef.All[enemy.Type].Id}");
            var at = ToGodot(enemy.Position);
            return BotWalk(at, 1.6f, at.DistanceTo(position) < 2.4f && _client.Tick % 16 == 0 ? Buttons.Light : Buttons.None);
        }

        // A scene waiting for the player to come to a place.
        if (snapshot.Scene != Snapshot.NoScene && snapshot.Scene < Story.Scenes.Count)
        {
            var step = Story.Scenes[snapshot.Scene].Steps[Math.Min(snapshot.SceneStep, Story.Scenes[snapshot.Scene].Steps.Length - 1)];
            if (step.Kind == "waitFor" && Story.Scenes[snapshot.Scene].Id == "prologue_1_intro")
            {
                // The contrary player runs from the finders instead: the net must still catch them.
                if (Contrary("flee"))
                {
                    BotStage("run from the finders");
                    return BotGo(new Vector3(90, 0, 118), 1f, Buttons.None);
                }

                BotStage("walk to the finders");
                return BotGo(new Vector3(70, 0, 108), 2f, Buttons.None);
            }
        }

        // After the ambush: the road to Hearthmere, the Guild, and Mira, who signs the player up.
        (string Stage, Vector3 Goal)? next =
            !story.Has("prologue.done") ? null
            : !story.Has("guild.registered") && position.X < 300 ? ("go to the Guild door", new Vector3(-36, 0, 23.5f))
            : !story.Has("guild.registered") ? ("register with Mira", new Vector3(420, 0, -4.2f))
            : null;
        if (next is not { } goal)
        {
            return story.Has("guild.registered") ? BotDone() : ((sbyte)0, (sbyte)0, Buttons.None);
        }

        if (goal.Stage == "register with Mira")
        {
            // Up to the counter, within talking range, and sign.
            return BotConverse(goal.Stage, new Vector3(420, 0, -4.0f), "Sign me up.");
        }

        BotStage(goal.Stage);
        if (_client.Tick % 300 == 0)
        {
            GD.Print($"BOT at {position.X:0.0},{position.Z:0.0} leg {_botLeg} tick {_client.Tick}");
        }

        var use = goal.Stage is "go to the Guild door" or "register with Mira";
        var walk = BotGo(goal.Goal, use ? 2.2f : 1.2f, Buttons.None);
        if (walk == (0, 0, Buttons.None) && _client.Tick % 45 == 0 && use)
        {
            Interact();
        }

        return walk;
    }

    /// <summary>Whether the prologue bot plays contrary this way (<c>PIXEL_CONTRARY=flee</c>: runs from the finders).</summary>
    private static bool Contrary(string way) =>
        (OS.GetEnvironment("PIXEL_CONTRARY") ?? "").Split(',', StringSplitOptions.TrimEntries).Contains(way);

    /// <summary>
    /// Plays Rhea's training (PIXEL_BOT=tutorial, start in the Guild hall): signs with Mira, then each
    /// lesson in turn: light blows, heavy blows, the guard held up to her, dashes, and slides on the run.
    /// </summary>
    private (sbyte X, sbyte Z, Buttons Buttons) TutorialBot()
    {
        var snapshot = _client.Latest;
        if (snapshot is null || _client.Tick < 60)
        {
            return (0, 0, Buttons.None);
        }

        var story = _client.Story;
        if (_hud.DialogOpen)
        {
            if (++_botWait > BotDialogTicks)
            {
                (_pendingCommand, _pendingArg) = _hud.Choose(0);
                _botWait = 0;
            }

            return (0, 0, Buttons.None);
        }

        if (!story.Has(Tutorial.Started))
        {
            var mira = _world.Npcs.First(n => n.Role == "receptionist").Position;
            return BotConverse("sign with Mira", new Vector3(mira.X, 0, mira.Z + 2.6f), "Sign me up.");
        }

        if (story.Has(Tutorial.Finished))
        {
            return BotDone();
        }

        var lesson = Array.FindIndex(Tutorial.Lessons, l => !story.Has(l.Flag));
        var examiner = snapshot.Enemies.Where(e => EnemyDef.All[e.Type].Id == Guild.ExaminerId).Cast<EnemyView?>().FirstOrDefault();
        if (lesson < 0 || examiner is not { } rhea)
        {
            BotStage("wait for Rhea's last word");
            return (0, 0, Buttons.None);
        }

        var at = ToGodot(rhea.Position);
        var tick = _client.Tick;
        BotStage($"lesson {Tutorial.Lessons[lesson].Counts}");
        return Tutorial.Lessons[lesson].Counts switch
        {
            "light" => BotWalk(at, 1.6f, tick % 24 == 0 ? Buttons.Light : Buttons.None),
            "heavy" => BotWalk(at, 1.6f, tick % 90 < 30 ? Buttons.Heavy : Buttons.None),
            "block" => BotWalk(at, 2.4f, Buttons.Block),
            "dodge" => BotWalk(at, 2.6f, tick % 45 == 0 ? Buttons.Dash : Buttons.None),
            // Run out and back across the yard, sliding as it goes.
            _ => BotWalk(at + new Vector3(tick % 360 < 180 ? 9f : -9f, 0, 0), 1f, tick % 50 == 0 ? Buttons.Slide : Buttons.None),
        };
    }

    /// <summary>
    /// Checks the way-finding (PIXEL_BOT=map): sets a mark at the Old Ruins, follows the trail on the
    /// ground for a while (the minimap shows the way), then opens the big map with the mark and the way.
    /// </summary>
    private (sbyte X, sbyte Z, Buttons Buttons) MapBot()
    {
        var tick = _client.Tick;
        if (tick < 60)
        {
            return (0, 0, Buttons.None);
        }

        if (_navigator.Waypoint is null && tick < 70)
        {
            var ruins = _zone.Places.First(p => p.Name == "Old Ruins");
            _navigator.SetWaypoint(new Vector2(ruins.X, ruins.Z));
            BotStage("mark the Old Ruins");
        }

        if (tick < 600)
        {
            // Walk the way the trail shows (the first frames work it out).
            if (_navigator.Route.Count < 2)
            {
                return (0, 0, Buttons.None);
            }

            if (tick < 300 && _navigator.Target is { } mark)
            {
                // First cut straight across country, off the roads: the trail must lead on from there, not back.
                BotStage("cut across country");
                return BotWalk(new Vector3(mark.At.X, 0, mark.At.Y), 0.5f, Buttons.None);
            }

            BotStage("follow the trail");
            var next = _navigator.Route[1];
            return BotWalk(new Vector3(next.X, 0, next.Y), 0.5f, Buttons.None);
        }

        if (!_hud.MapOpen && tick < 760)
        {
            _hud.ToggleMap();
            BotStage("open the map");
        }
        else if (_hud.MapOpen && tick >= 760)
        {
            _hud.ToggleMap();
            return BotDone();
        }

        return (0, 0, Buttons.None);
    }

    private (sbyte X, sbyte Z, Buttons Buttons) BotDone()
    {
        BotStage("done");
        return (0, 0, Buttons.None);
    }

    private void BotStage(string stage)
    {
        if (stage != _botStage)
        {
            _botStage = stage;
            _botWait = 0;
            GD.Print($"BOT {stage} at tick {_client.Tick}");
        }
    }

    /// <summary>Walks to a spot, opens the conversation, holds it for a moment, then picks the wanted choice.</summary>
    private (sbyte X, sbyte Z, Buttons Buttons) BotConverse(string stage, Vector3 spot, string wanted)
    {
        BotStage(stage);
        var walk = BotWalk(spot, 0.5f, Buttons.None);
        if (walk != (0, 0, Buttons.None))
        {
            return walk;
        }

        if (!_hud.DialogOpen)
        {
            Interact();
            _botWait = 0;
        }
        else if (++_botWait > BotDialogTicks)
        {
            (_pendingCommand, _pendingArg) = _hud.ChooseLabel(wanted);
            _botWait = 0;
        }

        return (0, 0, Buttons.None);
    }

    private (sbyte X, sbyte Z, Buttons Buttons) BotWalk(Vector3 target, float reach, Buttons buttons)
    {
        var position = _client.State.Position;
        var dx = target.X - position.X;
        var dz = target.Z - position.Z;
        var distance = Mathf.Sqrt(dx * dx + dz * dz);
        _yaw = distance < reach ? Mathf.Atan2(-dx, -dz) : Steer(position, Mathf.Atan2(-dx, -dz));
        return distance < reach ? ((sbyte)0, (sbyte)0, buttons) : ToMove(0, 1, buttons);
    }

    // The side the bot last went round something on, so it does not dither left and right.
    private float _botSide = 1f;

    /// <summary>
    /// The heading nearest the wanted one along which the next metre and a half is clear of solid
    /// boxes: the bot walks round a cart or a fence instead of pressing into it.
    /// </summary>
    private float Steer(System.Numerics.Vector3 position, float wanted)
    {
        bool Clear(float heading)
        {
            foreach (var reach in new[] { 0.6f, 1.5f })
            {
                var spot = position + new System.Numerics.Vector3(-Mathf.Sin(heading) * reach, 0, -Mathf.Cos(heading) * reach);
                spot.Y = _world.GroundHeight(spot.X, spot.Z) + 0.05f;
                if (_world.OverlapsPlayer(spot))
                {
                    return false;
                }
            }

            return true;
        }

        if (Clear(wanted))
        {
            return wanted;
        }

        for (var turn = 0.35f; turn < 2.6f; turn += 0.35f)
        {
            foreach (var side in new[] { _botSide, -_botSide })
            {
                if (Clear(wanted + side * turn))
                {
                    _botSide = side;
                    return wanted + side * turn;
                }
            }
        }

        return wanted;
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
        if (state.DrawTicks > 0)
        {
            // A drawn bow points where the camera aims.
            _facing = LerpFacing(_facing, _yaw, 30f * delta);
        }
        else if (state.BlockTicks > 0)
        {
            // The guard faces where it covers.
            _facing = LerpFacing(_facing, state.BlockAim / 256f * Mathf.Tau, 30f * delta);
        }
        else if (state.Attack != AttackKind.None)
        {
            _facing = LerpFacing(_facing, state.AttackAim / 256f * Mathf.Tau, 30f * delta);
        }
        else if (flat.LengthSquared() > 0.5f)
        {
            _facing = LerpFacing(_facing, Mathf.Atan2(-flat.X, -flat.Y), 14f * delta);
        }

        _player.Root.Rotation = new Vector3(0, _facing, 0);
        _player.SetWeapon(Weapons.Name(_client.State.Weapon), _hud.Items.Equipped[EquipSlot.Weapon]?.Gear?.Def.Id);
        _player.SwordVisible = _hud.Items.HasWeapon;
        // A skill cast from the hotbar: a short gesture and its sound, here on the client only.
        if (_hud.SkillCast >= 0)
        {
            var effect = SkillDef.All[_hud.SkillCast].Effect;
            _castTime = 0.35f;
            _castMove = effect is "earthCrack" ? WeaponMove.ClubSmash : effect is "airSlash" or "fangSlash" ? WeaponMove.SwingAcross : WeaponMove.StaffThrust;
            _sfx.Play(effect switch
            {
                "fireball" => "fireball",
                "airSlash" => "air_slash",
                "fangSlash" => "fang_slash",
                "summonSpears" => "spear_summon",
                _ => "earth_crack",
            }, shown + Vector3.Up);
            _hud.SkillCast = -1;
        }

        _castTime = Mathf.Max(0f, _castTime - (float)GetProcessDeltaTime());
        var bow = state.DrawTicks > 0 ? WeaponMove.BowDraw : state.Attack == AttackKind.BowShot ? WeaponMove.BowRelease
            : _castTime > 0f ? _castMove
            : state.BlockTicks > 0 ? WeaponMove.Guard : state.SlideTicks > 0 ? WeaponMove.Slide : WeaponMove.None;
        SlideEffects(state, shown);
        _player.Animate(new MotionState(flat.Length(), state.Grounded, state.Velocity.Y,
            state.DashTicks > 0, state.Attack, state.AttackTick, bow, Tuning.Drawn(state.DrawTicks), _shotPitch), delta);
    }

    /// <summary>A slide kicks up dust from under the feet, with a scrape when it starts.</summary>
    private void SlideEffects(in PlayerState state, Vector3 feet)
    {
        if (state.SlideTicks > 0 && _lastSlideTicks == 0)
        {
            _sfx.Play("slide", feet);
        }

        if (state.SlideTicks > 0 && state.SlideTicks % 3 == 0 && state.SlideTicks != _lastSlideTicks)
        {
            _fx.Burst(feet + Vector3.Up * 0.1f, new Color("b86f50"), 4, 2.5f, 0.4f, 0.08f, -4f, 60f);
        }

        _lastSlideTicks = state.SlideTicks;
    }

    private byte _lastSlideTicks;
    private float _castTime;
    private WeaponMove _castMove;

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

            rig.SetWeapon(Weapons.Name(other.Weapon));
            rig.Animate(new MotionState(flat.Length(), Mathf.Abs(other.Velocity.Y) < 0.5f, other.Velocity.Y,
                flat.Length() > Tuning.MoveSpeed + 1f, other.Attack, other.AttackTick,
                other.Attack == AttackKind.BowShot ? WeaponMove.BowRelease : WeaponMove.None), delta);
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
                slime = def.Kind switch
                {
                    "slime" => new SlimeView(),
                    "animal" or "wolf" => new AnimalView(def, enemy.Id),
                    "wisp" => new WispView(def),
                    "crow" => new CrowView(def),
                    _ => new GoblinView(def),
                };
                _enemies.Add(enemy.Id, slime);
                PixelStage.Limit(slime.Root, FigureRange);
                _enemyTypes[enemy.Id] = enemy.Type;
                _stage.View.AddChild(slime.Root);
                if (!_telegraphs.ContainsKey(enemy.Id))
                {
                    _telegraphs.Add(enemy.Id, new TelegraphView(_world, _fx, _stage.View));
                }
            }

            var target = ToGodot(enemy.Position);
            // A respawn is a jump across the map, not a move.
            var position = _enemyShown.TryGetValue(enemy.Id, out var shownAt) && shownAt.DistanceTo(target) <= 6f ? shownAt.Lerp(target, blend) : target;
            _enemyShown[enemy.Id] = position;
            slime.Update(enemy, position, delta, _sfx);
            if (slime is GoblinView { Staggered: true })
            {
                _hud.AddFloating("Staggered!", position + Vector3.Up * (def.TagHeight > 0 ? def.TagHeight : 2f), new Color("feae34"));
            }

            if (slime is GoblinView { DrumStarted: true })
            {
                _hud.AddFloating("War drum!", position + Vector3.Up * (def.TagHeight > 0 ? def.TagHeight + 0.4f : 2.4f), new Color("f77622"));
            }

            if (slime is GoblinView { Enraged: true })
            {
                _hud.AddFloating("Enraged!", position + Vector3.Up * (def.TagHeight > 0 ? def.TagHeight : 2f), new Color("e43b44"));
                _fx.Burst(position + Vector3.Up * 1.2f, new Color("e43b44"), 24, 5f, 0.6f, 0.08f, -4f, 80f);
            }

            // A shaman's dance shows as green flames licking round the feet of every goblin it stirred,
            // and a rage as red embers rising off the shoulders, so the player sees who is dangerous now.
            if (enemy.Hasted && GD.Randf() < delta * 14f)
            {
                _fx.Burst(position + Vector3.Up * 0.15f, new Color("63c74d"), 3, 2.2f, 0.45f, 0.08f, 4f, 30f);
            }

            if (enemy.Enraged && enemy.Action != EnemyAction.Dead && GD.Randf() < delta * 9f)
            {
                _fx.Burst(position + Vector3.Up * def.Size * 1.3f, new Color("f77622"), 2, 1.6f, 0.6f, 0.06f, 3f, 35f);
            }

            if (slime is GoblinView { Launch: { } launch } leaper)
            {
                // The ground bursts where a werewolf springs for its great leap.
                leaper.Launch = null;
                var ground = new Vector3(launch.X, _world.GroundHeight(launch.X, launch.Z), launch.Z);
                _sfx.Play("super_jump", ground);
                _fx.Puddle(ground, new Color("181425"), 2.2f, 2.5f);
                _fx.Burst(ground + Vector3.Up * 0.2f, new Color("5a6988"), 22, 8f, 0.7f, 0.14f, -18f, 70f);
                _fx.Burst(ground + Vector3.Up * 0.2f, new Color("b86f50"), 28, 5f, 0.8f, 0.1f, -6f, 85f);
            }

            _hud.EnemyPositions[enemy.Id] = position;
            ShowTelegraph(enemy, def, position);
            if (slime.DamageTaken > 0)
            {
                var heavy = _client.State.Attack == AttackKind.Heavy;
                _sfx.Play(heavy ? "hit_heavy" : "hit", position + Vector3.Up, -3f);
                _hud.AddFloating(slime.DamageTaken.ToString(), position + Vector3.Up * 1.6f, heavy ? new Color("feae34") : Colors.White);
                var blood = def.Kind is "goblin" or "animal" or "wolf" or "orc" or "werewolf" ? "a22633" : def.Kind == "sentinel" ? "c8c8d0" : def.Kind == "wisp" ? "2ce8f5" : def.Kind == "crow" ? "262b44" : def.Rare ? "f6757a" : "63c74d";
                _fx.Burst(position + Vector3.Up * 0.6f, new Color(blood), heavy ? 22 : 12, 6f, 0.5f, 0.09f);
                // A blow on a guardian's armoured front rings off it in sparks; one on its back cracks the stone.
                if (def.FrontGuard < 1f)
                {
                    var me = ToGodot(_client.State.Position);
                    var toward = new Vector3(me.X - position.X, 0, me.Z - position.Z);
                    var facing = new Vector3(-Mathf.Sin(enemy.Facing), 0, -Mathf.Cos(enemy.Facing));
                    var dot = toward.LengthSquared() > 0.01f ? toward.Normalized().Dot(facing) : 0f;
                    if (dot > Enemy.GuardCone)
                    {
                        _sfx.Play("block", position + Vector3.Up);
                        _fx.Burst(position + Vector3.Up * 1.3f + facing * 0.6f, new Color("feae34"), 14, 7f, 0.3f, 0.05f, -10f, 60f);
                        _hud.AddFloating("Guarded", position + Vector3.Up * 2.6f, new Color("8b9bb4"));
                    }
                    else if (dot < -Enemy.GuardCone)
                    {
                        _fx.Burst(position + Vector3.Up * 1.2f - facing * 0.4f, new Color("2ce8f5"), 16, 5f, 0.45f, 0.07f, -8f, 70f);
                    }
                }
            }
        }

        // Enemies outside the player's area are not in the snapshot, so they must not linger on screen.
        var near = snapshot.Enemies.Select(e => e.Id).ToHashSet();
        foreach (var (id, view) in _enemies)
        {
            if (!near.Contains(id))
            {
                view.Root.Visible = false;
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
                var crack = wall.Kind == HazardKind.EarthCrack;
                var runes = wall.Kind == HazardKind.RuneWave;
                node = crack ? new EarthCrackNode(_world, _fx, ToGodot(wall.Origin), wall.Yaw)
                    : runes ? new RuneWaveNode(_world, _fx, ToGodot(wall.Origin), wall.Yaw)
                    : new FireWallNode(_world, _fx, ToGodot(wall.Origin), wall.Yaw, wall.HalfWidth);
                _walls.Add(wall.Id, node);
                _stage.View.AddChild(node.Root);
                if (runes)
                {
                    _sfx.Play("shockwave", ToGodot(wall.Origin));
                }
                else if (!crack)
                {
                    _sfx.Play("fire_wall", ToGodot(wall.Origin));
                }
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
        if (def.Kind == "slime")
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

        // Animals that only wander have no abilities at all.
        if (def.Abilities is not { } abilities || enemy.Ability >= abilities.Length)
        {
            telegraph.Hide();
            return;
        }

        var ability = abilities[enemy.Ability];
        if (enemy.Action != EnemyAction.Cast || ability.Telegraph == "none")
        {
            telegraph.Hide();
        }
        else if (ability.Telegraph == "wedge")
        {
            var flat = new Vector2(target.X - position.X, target.Z - position.Z);
            var crack = ability.Effect == "earthCrack";
            telegraph.ShowWedge(position, Mathf.Atan2(-flat.X, -flat.Y), crack ? EarthCrack.Length + 0.8f : FireWallHazard.MaxLength, 0.42f,
                crack ? EarthCrack.HalfAngle : FireWallHazard.HalfAngle);
        }
        else if (ability.Telegraph == "lanes")
        {
            var along = (target - position) with { Y = 0 };
            along = along.LengthSquared() < 0.01f ? new Vector3(-Mathf.Sin(enemy.Facing), 0, -Mathf.Cos(enemy.Facing)) : along.Normalized();
            telegraph.ShowLanes(position, position + along * RuneWave.Length, RuneWave.HalfWidth * 2f, RuneWave.Spacing, RuneWave.Lanes, 0.5f);
        }
        else if (ability.Telegraph == "circle")
        {
            telegraph.ShowCircle(ability.Effect is "spin" or "tornado" ? position : target, ability.Radius, 0.5f);
        }
        else
        {
            var along = (target - position).Normalized();
            var width = ability.Effect switch { "powershot" => 0.5f, "airSlash" => 2.8f, "airSlashWide" => 4.2f, _ => 0.9f };
            telegraph.ShowLine(position, position + along * ability.Range, width, 0f);
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

    /// <summary>
    /// A crystal is a spinning gem. Any other item floats as its own pixel icon, always facing the
    /// camera, over a small glowing ring in its tier colour; good finds also throw a beam of light.
    /// </summary>
    private Node3D BuildCrystal(in LootView drop)
    {
        var def = ItemDef.All[drop.Item];
        if (!def.IsCrystal)
        {
            return BuildItemDrop(def, drop.Rarity);
        }

        var rarity = def.Crystal;
        var color = new Color(Crystals.All[rarity].Color);
        var root = new Node3D();
        var size = 0.26f + rarity * 0.06f;
        var material = new StandardMaterial3D { AlbedoColor = color, EmissionEnabled = true, Emission = color, EmissionEnergyMultiplier = 0.4f, Roughness = 0.2f };
        var top = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = size, Height = size * 1.6f, RadialSegments = 5 }, MaterialOverride = material, Position = new Vector3(0, size * 0.8f, 0) };
        var bottom = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = size, BottomRadius = 0f, Height = size * 1.1f, RadialSegments = 5 }, MaterialOverride = material, Position = new Vector3(0, -size * 0.55f, 0) };
        root.AddChild(top);
        root.AddChild(bottom);
        if (rarity != (int)CrystalRarity.Common)
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

    private static readonly Color[] TierGlow = { new("e8eef6"), new("58b4ff"), new("d77be0"), new("feae34") };

    /// <summary>Gear glows in the colour of its rarity; other items in the colour of their tier.</summary>
    private Node3D BuildItemDrop(ItemDef def, byte rarity)
    {
        var root = new Node3D();
        var tier = rarity == LootView.NoRarity ? def.Tier : rarity;
        var color = TierGlow[Math.Clamp(tier, 0, 3)];
        root.AddChild(new Sprite3D
        {
            Texture = GD.Load<Texture2D>($"res://assets/ui/icons/{def.Icon}.png"),
            PixelSize = 0.032f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            Shaded = false,
        });
        var ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.26f, OuterRadius = 0.32f, Rings = 18, RingSegments = 4 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color, EmissionEnabled = true, Emission = color, EmissionEnergyMultiplier = 0.8f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
            Position = new Vector3(0, -0.5f, 0),
        };
        root.AddChild(ring);
        if (tier > 0 || def.IsEquipment)
        {
            // The rarer the find, the taller its beam of light.
            var height = 4f + tier * 2f;
            root.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.26f, Height = height, RadialSegments = 8 },
                MaterialOverride = _fx.Stipple(color, 0.25f + tier * 0.05f),
                Position = new Vector3(0, height / 2f - 0.5f, 0),
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
                // The orcs' slash and spear already sounded with the swing that sent them.
                if (spit.Kind == ProjectileKind.SummonedSpear)
                {
                    _sfx.Play("spear_summon", ball.Position, -8f);
                }
                else if (spit.Kind is not (ProjectileKind.PlayerArrow or ProjectileKind.AirSlash or ProjectileKind.AirSlashWide or ProjectileKind.ThrownSpear
                    or ProjectileKind.FangSlash or ProjectileKind.Knife or ProjectileKind.ThrownAxe or ProjectileKind.WispBolt))
                {
                    _sfx.Play(spit.Kind switch
                    {
                        ProjectileKind.Spit => "slime_spit",
                        ProjectileKind.Arrow => "bow_shot",
                        ProjectileKind.PowerArrow => "bow_power",
                        ProjectileKind.ChainBolt => "lightning_zap",
                        _ => "fireball",
                    }, ball.Position);
                }
            }

            var motion = target - ball.Position;
            ball.Position = ball.Position.Lerp(target, spit.Kind == ProjectileKind.PowerArrow ? 1f : blend);
            if (spit.Kind == ProjectileKind.ThrownAxe)
            {
                // End over end along its way: turned to face where it flies, then tumbling about its own axis.
                var flat = new Vector2(motion.X, motion.Z);
                var yaw = flat.LengthSquared() > 0.0001f ? Mathf.Atan2(-flat.X, -flat.Y) : ball.Rotation.Y;
                ball.Rotation = new Vector3(-(float)(Time.GetTicksMsec() % 100000) / 1000f * AxeTumble, yaw, 0);
            }
            if (spit.Kind is ProjectileKind.Arrow or ProjectileKind.PowerArrow or ProjectileKind.PlayerArrow or ProjectileKind.AirSlash
                    or ProjectileKind.AirSlashWide or ProjectileKind.ThrownSpear or ProjectileKind.SummonedSpear or ProjectileKind.FangSlash
                    or ProjectileKind.Knife or ProjectileKind.Meteor
                && motion.LengthSquared() > 0.0001f)
            {
                // A shot falling straight down (the rain of arrows) needs another up, or LookAt
                // warns every frame, and a flood of warnings stalls the game.
                var up = Mathf.Abs(motion.Normalized().Dot(Vector3.Up)) > 0.98f ? Vector3.Forward : Vector3.Up;
                ball.LookAt(ball.GlobalPosition + motion, up);
            }
            else if (spit.Kind == ProjectileKind.SummonedSpear && ball.GlobalPosition.DistanceTo(_shownNow) < 4f)
            {
                // The player's own conjured spears hang over their head pointing where they look.
                var look = new Vector3(-Mathf.Sin(_yaw), 0, -Mathf.Cos(_yaw));
                ball.LookAt(ball.GlobalPosition + look, Vector3.Up);
            }
            else if (spit.Kind == ProjectileKind.SummonedSpear && ball.GlobalPosition.DistanceSquaredTo(_shownNow) > 0.01f)
            {
                // A conjured spear hangs over the orc's head with its point already on the player.
                var aim = (_shownNow + Vector3.Up - ball.GlobalPosition).Normalized();
                ball.LookAt(_shownNow + Vector3.Up, Mathf.Abs(aim.Dot(Vector3.Up)) > 0.98f ? Vector3.Forward : Vector3.Up);
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
            case ProjectileKind.Fireball or ProjectileKind.PlayerFireball:
                _sfx.Play("explosion", last);
                _fx.Burst(last, new Color("f77622"), 34, 9f, 0.6f, 0.13f, -5f, 90f);
                _fx.Burst(last, new Color("fee761"), 18, 5f, 0.45f, 0.09f, -3f, 90f);
                _fx.Puddle(ground, new Color("3e2731"), 1.8f, 1.2f);
                break;
            case ProjectileKind.FangSlash:
                _fx.Burst(last, new Color("e43b44"), 12, 4f, 0.3f, 0.07f, 0f, 120f);
                break;
            case ProjectileKind.WispBolt:
                _fx.Burst(last, new Color("2ce8f5"), 14, 3f, 0.5f, 0.06f, 1f, 120f);
                _fx.Burst(last, new Color("ffffff"), 6, 2f, 0.3f, 0.05f, 0f, 120f);
                break;
            case ProjectileKind.Meteor:
                _sfx.Play("explosion", last);
                _sfx.Play("earth_crack", last, -4f);
                _fx.Burst(last, new Color("f77622"), 60, 12f, 0.7f, 0.16f, -6f, 90f);
                _fx.Burst(last, new Color("fee761"), 30, 7f, 0.5f, 0.11f, -4f, 90f);
                _fx.Ring(ground, new Color("f77622"), Tuning.MeteorSplash, 0.45f);
                _fx.Puddle(ground, new Color("3e2731"), Tuning.MeteorSplash, 2f);
                break;
            case ProjectileKind.Knife:
                _fx.Burst(last, new Color("c0cbdc"), 6, 3f, 0.2f, 0.05f, -6f, 120f);
                break;
            case ProjectileKind.ChainBolt:
                _fx.Burst(last, new Color("2ce8f5"), 18, 6f, 0.3f, 0.06f, 0f, 180f);
                _fx.Burst(last, new Color("ffffff"), 8, 4f, 0.2f, 0.05f, 0f, 180f);
                break;
            case ProjectileKind.AirSlash or ProjectileKind.AirSlashWide:
                // The crescent frays into a puff of pale wind where it dies.
                _fx.Burst(last, new Color("c0cbdc"), 16, 5f, 0.35f, 0.07f, 0f, 120f);
                _fx.Burst(last, new Color("ffffff"), 8, 3f, 0.25f, 0.05f, 0f, 120f);
                break;
            case ProjectileKind.ThrownSpear or ProjectileKind.SummonedSpear or ProjectileKind.ThrownAxe:
                _sfx.Play("arrow_hit", last);
                _fx.Burst(last, new Color("b86f50"), 10, 4f, 0.35f, 0.07f);
                break;
            default:
                _sfx.Play("arrow_hit", last, -5f);
                _fx.Burst(last, new Color("b8a58a"), 5, 3f, 0.25f, 0.05f);
                break;
        }
    }

    private MeshInstance3D BuildProjectile(ProjectileKind kind, Vector3 position)
    {
        if (kind == ProjectileKind.WispBolt)
        {
            // A barrow wisp's bolt: a small cold light trailing pale motes, slow enough to watch it turn.
            var light = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f, RadialSegments = 8, Rings = 5 },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = Colors.White, EmissionEnabled = true, Emission = new Color("2ce8f5"), EmissionEnergyMultiplier = 2.5f,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
                Position = position,
            };
            light.AddChild(Trail(new Color("2ce8f5"), 16, 0.5f, 0.07f));
            light.AddChild(new OmniLight3D { LightColor = new Color("2ce8f5"), OmniRange = 3.5f, LightEnergy = 1.2f });
            return light;
        }

        if (kind == ProjectileKind.Spit)
        {
            return BuildSpit(position);
        }

        if (kind == ProjectileKind.Knife)
        {
            // Fan of Knives: a small steel blade with a thin pale streak.
            var knife = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.03f, 0.38f) },
                MaterialOverride = _fx.Stipple(new Color("c0cbdc"), 1f),
                Position = position,
            };
            knife.AddChild(Trail(new Color("ffffff"), 8, 0.12f, 0.03f));
            return knife;
        }

        if (kind == ProjectileKind.Meteor)
        {
            // Meteor: a dark burning rock trailing fire and smoke.
            var rock = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.55f, Height = 1.1f, RadialSegments = 8, Rings = 5 },
                MaterialOverride = _fx.Stipple(new Color("733e39"), 1f),
                Position = position,
            };
            rock.AddChild(Trail(new Color("f77622"), 30, 0.45f, 0.18f));
            rock.AddChild(Trail(new Color("3e2731"), 16, 0.7f, 0.22f, 1f));
            return rock;
        }

        if (kind == ProjectileKind.ChainBolt)
        {
            // Chain Lightning: a white-hot spark trailing crackling blue.
            var spark = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.14f, 0.14f, 0.14f) },
                MaterialOverride = _fx.Stipple(new Color("ffffff"), 1f),
                Position = position,
            };
            spark.AddChild(Trail(new Color("2ce8f5"), 10, 0.22f, 0.07f));
            return spark;
        }

        if (kind == ProjectileKind.PlayerArrow)
        {
            // The player's arrow: a pale shaft with a white fletching streak behind it.
            var shaft = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.05f, 1.0f) },
                MaterialOverride = _fx.Stipple(new Color("e4a672"), 1f),
                Position = position,
            };
            shaft.AddChild(Trail(new Color("ffffff"), 14, 0.18f, 0.04f));
            return shaft;
        }

        if (kind is ProjectileKind.AirSlash or ProjectileKind.AirSlashWide)
        {
            return BuildAirSlash(position, kind == ProjectileKind.AirSlashWide);
        }

        if (kind == ProjectileKind.FangSlash)
        {
            return BuildAirSlash(position, false, true);
        }

        if (kind is ProjectileKind.ThrownSpear or ProjectileKind.SummonedSpear)
        {
            return BuildThrownSpear(position, kind == ProjectileKind.SummonedSpear);
        }

        if (kind == ProjectileKind.ThrownAxe)
        {
            return BuildThrownAxe(position);
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

    /// <summary>
    /// A crescent of cut air bulging forward along its flight (-Z) with thin tips; a white core inside
    /// a pale blue edge, shedding wisps behind it. It stands taller than a man, or for the
    /// swordmaster's wide slash lies level and wider still.
    /// </summary>
    private MeshInstance3D BuildAirSlash(Vector3 position, bool wide, bool fang = false)
    {
        static ArrayMesh Crescent(float radius, float thickness)
        {
            var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            const int steps = 14;
            const float sweep = 1.25f;
            for (var i = 0; i < steps; i++)
            {
                Vector3 Outer(float t) => new(0, Mathf.Sin(t) * radius, -Mathf.Cos(t) * radius + radius * 0.6f);
                Vector3 Inner(float t)
                {
                    var r = radius - thickness * Mathf.Cos(t / sweep * Mathf.Pi / 2f);
                    return new Vector3(0, Mathf.Sin(t) * r, -Mathf.Cos(t) * r + radius * 0.6f);
                }

                var a = Mathf.Lerp(-sweep, sweep, i / (float)steps);
                var b = Mathf.Lerp(-sweep, sweep, (i + 1) / (float)steps);
                // Both faces, so the crescent shows from either side.
                foreach (var flip in new[] { false, true })
                {
                    var quad = new[] { Outer(a), Inner(a), Outer(b), Inner(b) };
                    var order = flip ? new[] { 0, 2, 1, 1, 2, 3 } : new[] { 0, 1, 2, 1, 3, 2 };
                    foreach (var index in order)
                    {
                        surface.AddVertex(quad[index]);
                    }
                }
            }

            var mesh = new ArrayMesh();
            surface.Commit(mesh);
            return mesh;
        }

        // The root only follows the flight; the crescent inside it is turned level for a wide slash.
        var slash = new MeshInstance3D { Position = position };
        // A fang slash is smaller, red, and slanted like a claw mark.
        var size = wide ? 2.1f : fang ? 0.95f : 1.45f;
        var slant = wide ? Mathf.Pi / 2f : fang ? Mathf.Pi / 4f : 0f;
        var blade = new Node3D { Rotation = new Vector3(0, 0, slant), Scale = Vector3.One * size };
        slash.AddChild(blade);
        blade.AddChild(new MeshInstance3D
        {
            Mesh = Crescent(0.95f, 0.3f),
            MaterialOverride = _fx.Stipple(new Color(fang ? "a22633" : "8b9bb4"), 0.9f),
        });
        blade.AddChild(new MeshInstance3D
        {
            Mesh = Crescent(0.9f, 0.14f),
            MaterialOverride = _fx.Stipple(new Color(fang ? "f6757a" : "ffffff"), 1f),
            Position = new Vector3(0, 0, -0.04f),
        });
        slash.AddChild(Trail(new Color(fang ? "e43b44" : "c0cbdc"), wide ? 40 : fang ? 16 : 30, 0.3f, 0.08f));
        return slash;
    }

    /// <summary>
    /// The spearwoman's spear in flight: a long dark shaft with an iron head, point first. A conjured
    /// spear glows: its head burns orange and it sheds embers.
    /// </summary>
    private MeshInstance3D BuildThrownAxe(Vector3 position)
    {
        // The haft runs along the axe's own Y, so tumbling about X turns it end over end.
        var haft = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.62f, 0.05f) },
            MaterialOverride = _fx.Stipple(new Color("4a3028"), 1f),
            Position = position,
        };
        haft.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.04f, 0.2f, 0.26f) },
            MaterialOverride = _fx.Stipple(new Color("8b9bb4"), 1f),
            Position = new Vector3(0, 0.24f, -0.12f),
        });
        haft.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.22f, 0.04f) },
            MaterialOverride = _fx.Stipple(new Color("c0cbdc"), 1f),
            Position = new Vector3(0, 0.24f, -0.25f),
        });
        haft.AddChild(Trail(new Color("c0cbdc"), 10, 0.16f, 0.04f));
        return haft;
    }

    /// <summary>How fast a thrown axe tumbles, in radians a second.</summary>
    private const float AxeTumble = 20f;

    private MeshInstance3D BuildThrownSpear(Vector3 position, bool conjured)
    {
        var shaft = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.06f, 0.06f, 2.1f) },
            MaterialOverride = _fx.Stipple(new Color("733e39"), 1f),
            Position = position,
        };
        shaft.AddChild(new MeshInstance3D
        {
            Mesh = new PrismMesh { Size = new Vector3(0.14f, 0.34f, 0.04f) },
            MaterialOverride = _fx.Stipple(new Color(conjured ? "feae34" : "c0cbdc"), 1f),
            Position = new Vector3(0, 0, -1.2f),
            Rotation = new Vector3(-Mathf.Pi / 2f, 0, 0),
        });
        shaft.AddChild(conjured ? Trail(new Color("f77622"), 26, 0.35f, 0.06f, 1.5f) : Trail(new Color("ead4aa"), 10, 0.18f, 0.04f));
        return shaft;
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

    /// <summary>
    /// A third-person camera that never jerks. It works out where it would like to stand, then
    /// moves there smoothly: it pulls in quickly when something comes between it and the player,
    /// so it never sits in a wall, and eases back out slowly once the way is clear. Posts, trunks
    /// and fences are thin enough to see past, so they do not pull it in at all. Rising over a
    /// hill and looking down in a tight spot are eased too.
    /// </summary>
    private void PlaceCamera(Vector3 shown, float delta)
    {
        if (_sceneCamera is { } shot)
        {
            _stage.Camera.Fov = BaseFov;
            _stage.Camera.LookAtFromPosition(shot[0], shot[1]);
            return;
        }

        if (_hud.CreatorOpen && _player.FaceAnchor is { } face)
        {
            // The creator looks the player in the face, a little from the right, with the panel on the left.
            var facing = _player.Root.GlobalBasis.Z;
            var side = _player.Root.GlobalBasis.X;
            var head = face.GlobalPosition;
            _stage.Camera.Fov = 20f;
            _stage.Camera.LookAtFromPosition(head - facing * 2.6f + side * 0.75f + Vector3.Up * 0.02f, head - Vector3.Up * 0.32f + side * 0.42f);
            return;
        }

        var aim = _aimBlend * _aimBlend * (3f - 2f * _aimBlend);
        var right = new Vector3(Mathf.Cos(_yaw), 0, -Mathf.Sin(_yaw));
        var target = shown + new Vector3(0, 1.75f - 0.12f * aim, 0) + right * (AimShoulder * aim);
        var jumped = _cameraTarget is { } last && last.DistanceTo(target) > 6f;
        _cameraTarget = target;

        // Against a wall there is no room behind the player, so the camera rises and looks down
        // over the shoulder instead of pressing into the back of the head.
        var raise = 0f;
        var reach = CameraReach(target, _pitch);
        for (var extra = 0.2f; reach < CameraComfort && _pitch - extra >= MinPitch - 0.6f; extra += 0.2f)
        {
            var higher = CameraReach(target, _pitch - extra);
            if (higher > reach)
            {
                (raise, reach) = (extra, higher);
            }
        }

        raise *= 1f - aim;
        if (jumped || _cameraReach < 0f)
        {
            _cameraRaise = raise;
            _cameraReach = reach;
        }

        _cameraRaise = Mathf.Lerp(_cameraRaise, raise, 1f - Mathf.Exp(-5f * delta));
        var pitch = _pitch - _cameraRaise;
        reach = CameraReach(target, pitch);
        reach = Mathf.Lerp(reach, Mathf.Min(reach, AimReach), aim);
        var rate = reach < _cameraReach ? CameraPullIn : Mathf.Lerp(CameraEaseOut, CameraPullIn, aim);
        _cameraReach = Mathf.Lerp(_cameraReach, reach, 1f - Mathf.Exp(-rate * delta));

        // However smooth, the camera must never end up inside something.
        var back = CameraBack(pitch);
        if (!CameraFree(target + back * _cameraReach))
        {
            _cameraReach = reach;
        }

        var position = target + back * _cameraReach;
        var lift = CameraLift(target, position);
        _cameraLift = jumped ? lift : Mathf.Lerp(_cameraLift, lift, 1f - Mathf.Exp(-(lift > _cameraLift ? 10f : 4f) * delta));
        position.Y += _cameraLift;
        _stage.Camera.LookAtFromPosition(position, target);
        NoteCameraJerk(position, target, jumped);
    }

    private Vector3? _lastCameraOffset;

    /// <summary>The biggest jump of the camera relative to the player in one frame, for STATS.</summary>
    private float _cameraJerk;

    /// <summary>
    /// Records how far the camera lurched toward or away from the player, or up and down, in one
    /// frame. Turning the camera is the player's own doing and does not count.
    /// </summary>
    private void NoteCameraJerk(Vector3 position, Vector3 target, bool jumped)
    {
        var offset = position - target;
        if (_lastCameraOffset is { } last && !jumped && _client.Tick > 30)
        {
            var lurch = Mathf.Abs(offset.Length() - last.Length()) + Mathf.Abs(offset.Y - last.Y);
            _cameraJerk = Mathf.Max(_cameraJerk, lurch);
        }

        _lastCameraOffset = offset;
    }

    private Vector3 CameraBack(float pitch)
    {
        var flat = Mathf.Cos(pitch);
        return new Vector3(Mathf.Sin(_yaw) * flat, -Mathf.Sin(pitch), Mathf.Cos(_yaw) * flat);
    }

    private bool CameraFree(Vector3 point) => !_world.IsInsideBox(new System.Numerics.Vector3(point.X, point.Y, point.Z));

    /// <summary>
    /// How far behind the target the camera can stand at a pitch. A run of blocked points shorter
    /// than a thin obstacle (a trunk, a post, a fence) is looked past; the camera itself still
    /// keeps a margin from every box, because its lens sits a little in front of it.
    /// </summary>
    private float CameraReach(Vector3 target, float pitch)
    {
        var back = CameraBack(pitch);
        var reach = CameraStep;
        var blockedFrom = -1f;
        var clearSince = 0f;
        for (var distance = CameraStep * 2f; distance <= CameraDistance + CameraMargin; distance += CameraStep)
        {
            var point = target + back * distance;
            point.Y += CameraLift(target, point);
            var footprint = _world.FootprintAt(new System.Numerics.Vector3(point.X, point.Y, point.Z));
            if (footprint > 0f)
            {
                if (blockedFrom < 0f)
                {
                    blockedFrom = distance;
                }

                // A wall or a building stops the camera; only something narrow seen from above
                // and short along the line of sight, a post or a trunk, is looked past.
                if (footprint > ThinFootprint || distance - blockedFrom > ThinObstacle)
                {
                    break;
                }

                continue;
            }

            if (blockedFrom >= 0f)
            {
                blockedFrom = -1f;
                clearSince = distance;
            }

            // A spot counts once there is a margin of clear space on both sides of it.
            var spot = distance - CameraMargin;
            if (spot >= CameraStep && spot - clearSince >= CameraMargin)
            {
                reach = Mathf.Max(reach, Mathf.Min(spot, CameraDistance));
            }
        }

        return reach;
    }

    /// <summary>
    /// How far a camera point must rise to stay above the ground. Under a bridge the camera keeps
    /// below its deck instead.
    /// </summary>
    private float CameraLift(Vector3 target, Vector3 point)
    {
        var ground = _world.GroundHeight(point.X, point.Z);
        var y = point.Y;
        var below = target.Y < ground;
        y = below ? Mathf.Min(y, ground - CameraClearance) : y;
        var floor = below ? _world.Terrain?.At(point.X, point.Z) ?? 0f : ground;
        y = Mathf.Max(y, floor + CameraClearance);
        return y - point.Y;
    }

    private void Capture()
    {
        GetViewport().GetTexture().GetImage().SavePng($"{_captureDir}/window-{_captured}.png");
        _sinceCapture = 0;
        if (++_captured < _captureFrames)
        {
            return;
        }

        var stats = _client.Stats;
        var enemies = _client.Latest?.Enemies ?? Array.Empty<EnemyView>();
        GD.Print($"STATS snapshots={stats.Snapshots} corrections={stats.Corrections} " +
                 $"maxError={stats.MaxError:F3} buffered={_client.Latest?.BufferedInputs} " +
                 $"tickScale={_client.TickScale:F3} enemies={enemies.Length} rare={enemies.Count(e => e.Type == 1)} " +
                 $"fps={(_measuredFrames > 0 ? _measuredFrames / _frameSeconds : 0):F0} draws={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} objects={Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)} tris={Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame) / 1000:F0}k nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)} frameMs={_frameSeconds * 1000 / Math.Max(_measuredFrames, 1):F1} sounds={_sfx.Played} level={_client.Latest?.Level} xp={_client.Latest?.Xp} hp={_client.State.Health} stamina={_client.State.Stamina:F0} maxHeight={_maxHeight:F2} travelled={_travelled:F0} cameraJerk={_cameraJerk:F2}");
        GetTree().Quit();
    }

    private static Vector3 ToGodot(System.Numerics.Vector3 value) => new(value.X, value.Y, value.Z);

    private static System.Numerics.Vector3 FromGodot(Vector3 value) => new(value.X, value.Y, value.Z);
}
