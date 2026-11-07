using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// An animal from a jointed Blender model (beast_*.glb): body, head, ears, tail and legs or wings.
/// Legs swing in a diagonal gait that quickens into a gallop. What the server says the animal is
/// doing sets the pose: grazing puts the head down to chew, alert lifts it with the ears pricked,
/// idle lets it look around, and a running hen flaps. Hens peck instead of grazing.
/// </summary>
public sealed class AnimalView : IEnemyView
{
    private static readonly Dictionary<Color, ShaderMaterial> Materials = new();

    private readonly Node3D _body;
    private readonly Node3D _head;
    private readonly Node3D? _neck;
    private readonly Node3D? _tail;
    private readonly List<(Node3D Leg, Node3D? Shin, float Phase, bool Front)> _legs = new();
    private readonly List<(Node3D Ear, float Side)> _ears = new();
    private readonly List<(Node3D Wing, float Side)> _wings = new();
    private readonly Vector3 _bodyRest;
    private readonly bool _bird;
    private readonly string _kind;
    private short _lastHealth;
    private bool _first = true;
    private float _phase;
    private float _clock;
    private float _graze;
    private float _alert;
    private float _speed;
    private float _fall;
    private Vector3 _lastPosition;
    private readonly int _id;
    private float _callTimer;
    private float _calmFor;
    private bool _wasDead;
    private bool _wasAlert;
    private bool _wasBolting;
    private bool _wasFighting;

    public Node3D Root { get; } = new();
    public int DamageTaken { get; private set; }

    public AnimalView(EnemyDef def, int id)
    {
        _kind = def.Id;
        _bird = def.Id == "chicken";
        var model = def.Id == "deer" && id % 2 == 1 ? "doe" : def.Id;
        var scene = GD.Load<PackedScene>($"res://assets/beast_{model}.glb").Instantiate<Node3D>();
        Root.AddChild(scene);
        Tint(scene);
        _body = Part(scene, "body")!;
        _head = Part(scene, "head")!;
        _neck = Part(scene, "neck");
        _tail = Part(scene, "tail");
        _bodyRest = _body.Position;
        foreach (var (name, phase) in new[] { ("fl", 0f), ("br", 0f), ("fr", Mathf.Pi), ("bl", Mathf.Pi) })
        {
            if (Part(scene, "leg_" + name) is { } leg)
            {
                _legs.Add((leg, Part(scene, "shin_" + name), phase, name[0] == 'f'));
            }
        }

        foreach (var (name, side) in new[] { ("ear_l", 1f), ("ear_r", -1f) })
        {
            if (Part(scene, name) is { } ear)
            {
                _ears.Add((ear, side));
            }
        }

        foreach (var (name, side) in new[] { ("wing_l", 1f), ("wing_r", -1f) })
        {
            if (Part(scene, name) is { } wing)
            {
                _wings.Add((wing, side));
            }
        }

        // Each animal starts its little routines at its own moment.
        _clock = id * 1.37f;
        _id = id;
        _callTimer = 3f + id % 7 * 2.3f;
        Root.Scale = Vector3.One * def.Size;
    }

    private static Node3D? Part(Node scene, string name) => scene.FindChild(name, true, false) as Node3D;

    /// <summary>Swaps Blender's flat materials for the cel shader, one shared material per colour.</summary>
    private static void Tint(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh is not null)
        {
            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.Mesh.SurfaceGetMaterial(surface) is BaseMaterial3D original)
                {
                    var color = original.AlbedoColor;
                    if (!Materials.TryGetValue(color, out var toon))
                    {
                        toon = PlayerRig.ToonMaterial(color);
                        Materials[color] = toon;
                    }

                    mesh.SetSurfaceOverrideMaterial(surface, toon);
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            Tint(child);
        }
    }

    public void Update(in EnemyView enemy, Vector3 position, float delta, Sfx sfx)
    {
        var moved = (position - _lastPosition).Length() / Mathf.Max(delta, 0.001f);
        _speed = Mathf.Lerp(_speed, moved > 20f ? 0f : moved, 1f - Mathf.Exp(-10f * delta));
        _lastPosition = position;
        _clock += delta;
        DamageTaken = !_first && enemy.Health < _lastHealth ? _lastHealth - enemy.Health : 0;
        _first = false;
        _lastHealth = enemy.Health;

        var dead = enemy.Action == EnemyAction.Dead;
        _fall = Mathf.MoveToward(_fall, dead ? 1f : 0f, delta * 3f);
        // The client hides an animal that leaves the player's area; back in the snapshot, it shows again.
        Root.Visible = true;
        Root.Position = position;
        Root.Rotation = new Vector3(0, enemy.Facing, _fall * Mathf.Pi / 2f);

        var gallop = Mathf.Clamp(_speed / 5f, 0f, 1f);
        var moving = _speed > 0.3f && !dead;
        _phase += delta * (_bird ? 9f + _speed * 3f : 4f + _speed * 2.2f);
        var swing = moving ? (_bird ? 0.6f : 0.35f + gallop * 0.45f) : 0f;
        var follow = 1f - Mathf.Exp(-20f * delta);
        foreach (var (leg, shin, offset, front) in _legs)
        {
            var target = Mathf.Sin(_phase + offset) * swing;
            leg.Rotation = new Vector3(Mathf.Lerp(leg.Rotation.X, target, follow), 0, 0);
            if (shin is null)
            {
                continue;
            }

            // While a leg swings forward its lower part folds: a knee bends back, a hock forward.
            var lift = moving ? Mathf.Max(0f, Mathf.Cos(_phase + offset)) * (0.5f + gallop * 0.6f) : 0f;
            var fold = front ? -lift * 1.3f : lift * 0.9f;
            shin.Rotation = new Vector3(Mathf.Lerp(shin.Rotation.X, fold - target * (front ? 0.2f : 0.5f), follow), 0, 0);
        }

        // A boar about to strike drops its head and lowers its tusks.
        if (enemy.Action is EnemyAction.Windup or EnemyAction.Strike)
        {
            _alert = 0f;
            _graze = Mathf.Lerp(_graze, 0.45f, 1f - Mathf.Exp(-12f * delta));
        }

        var bob = moving ? Mathf.Abs(Mathf.Sin(_phase)) * (_bird ? 0.03f : 0.03f + gallop * 0.08f) : Mathf.Sin(_clock * 1.6f) * 0.006f;
        var shake = enemy.HitFlash > 0 ? (float)GD.RandRange(-0.04, 0.04) : 0f;
        _body.Position = _bodyRest + new Vector3(shake, bob, 0);
        _body.Rotation = new Vector3(gallop * Mathf.Sin(_phase * 2f) * 0.05f, 0, 0);

        var grazing = !moving && enemy.Action == EnemyAction.Graze;
        var alert = !moving && enemy.Action == EnemyAction.Alert;
        _graze = Mathf.Lerp(_graze, grazing ? 1f : 0f, 1f - Mathf.Exp(-3f * delta));
        _alert = Mathf.Lerp(_alert, alert ? 1f : 0f, 1f - Mathf.Exp(-6f * delta));
        PoseHead(moving, gallop);
        PoseEars();
        PoseTail(moving);
        PoseWings(moving);

        Voice(enemy, position, delta, moving, sfx);
    }

    /// <summary>
    /// The animal's own sounds: a call now and then while it is calm, a cry when it is hurt, an
    /// alarm when it notices someone, hoofbeats when a herd bolts, a boar's snort as it turns to
    /// fight, and the thud of a body falling.
    /// </summary>
    private void Voice(in EnemyView enemy, Vector3 position, float delta, bool moving, Sfx sfx)
    {
        var at = position + Vector3.Up * 0.8f;
        var dead = enemy.Action == EnemyAction.Dead;
        if (DamageTaken > 0)
        {
            sfx.Play(Sound("hurt"), at);
        }

        if (dead && !_wasDead)
        {
            sfx.Play("animal_fall", position, -2f);
        }

        var alert = enemy.Action == EnemyAction.Alert;
        if (alert && !_wasAlert && _kind is "deer")
        {
            sfx.Play("deer_alarm", at);
        }

        var bolting = moving && _speed > 3f;
        if (bolting && !_wasBolting && _calmFor > 1f)
        {
            // One of every few animals sounds for the herd, so a stampede is not a wall of noise.
            if (_kind == "chicken")
            {
                sfx.Play("chicken_squawk", at, -2f);
            }
            else if (_id % 3 == 0)
            {
                sfx.Play("hooves", position, -3f);
            }
        }

        if (_kind == "pig" && enemy.Fighting && !_wasFighting)
        {
            sfx.Play("pig_angry", at);
        }

        // Calm animals call out every so often, each on its own clock.
        _calmFor = bolting || alert || dead ? 0f : _calmFor + delta;
        _callTimer -= delta;
        if (_callTimer <= 0f)
        {
            _callTimer = 9f + (float)GD.RandRange(0.0, 18.0);
            if (_calmFor > 4f && !dead && _kind is not ("deer"))
            {
                sfx.Play(Sound("call"), at, -5f);
            }
        }

        _wasDead = dead;
        _wasAlert = alert;
        _wasBolting = bolting;
        _wasFighting = enemy.Fighting;
    }

    /// <summary>The file for one of this animal's sounds: its call or its cry of pain.</summary>
    private string Sound(string what) => (_kind, what) switch
    {
        ("cow", "call") => "cow_moo",
        ("sheep", "call") => "sheep_baa",
        ("goat", "call") => "goat_bleat",
        ("pig", "call") => "pig_grunt",
        ("chicken", "call") => "chicken_cluck",
        ("chicken", _) => "chicken_squawk",
        ("deer", _) => "deer_hurt",
        (_, _) => $"{_kind}_hurt",
    };

    private void PoseHead(bool moving, float gallop)
    {
        float pitch;
        if (_bird)
        {
            // A hen pecks in quick dips, then looks up.
            var peck = Mathf.Max(0f, Mathf.Sin(_clock * 7f)) * (Mathf.Sin(_clock * 0.9f) > 0f ? 1f : 0f);
            pitch = -_graze * (0.4f + peck * 0.6f) + _alert * 0.2f;
        }
        else
        {
            // Grazing animals chew with small nods; alert ones raise the head high.
            var chew = Mathf.Sin(_clock * 6f) * 0.04f * _graze;
            pitch = -_graze * 1.35f + chew + _alert * 0.3f - gallop * 0.15f;
        }

        var look = moving || _graze > 0.5f ? 0f : Mathf.Sin(_clock * 0.5f) * 0.35f * (1f - _alert);
        var bob = moving ? Mathf.Sin(_phase * 2f) * 0.05f : Mathf.Sin(_clock * 2.1f) * 0.02f;
        if (_neck is null)
        {
            _head.Rotation = new Vector3(pitch + bob, look, 0);
            return;
        }

        // The neck carries most of a bow, the head the rest, so the muzzle reaches the grass.
        _neck.Rotation = new Vector3(pitch * 0.85f + bob * 0.5f, look * 0.6f, 0);
        _head.Rotation = new Vector3(pitch * 0.45f + bob * 0.5f, look * 0.4f, 0);
    }

    private void PoseEars()
    {
        foreach (var (ear, side) in _ears)
        {
            // Alert ears turn forward and stand; now and then an ear flicks a fly away.
            var flick = Mathf.Sin(_clock * 0.9f + side * 2f) > 0.97f ? 0.5f : 0f;
            ear.Rotation = new Vector3(flick, -side * _alert * 0.5f, side * _alert * 0.35f);
        }
    }

    private void PoseTail(bool moving)
    {
        if (_tail is null)
        {
            return;
        }

        var swish = _kind switch
        {
            "cow" => Mathf.Sin(_clock * 1.6f) * 0.45f,
            "pig" => Mathf.Sin(_clock * 9f) * 0.3f,
            _ => Mathf.Sin(_clock * 3.2f) * 0.2f,
        };

        // A deer flashes the white of its tail when it is alarmed or running.
        var lift = _kind == "deer" ? Mathf.Max(_alert, moving ? 0.8f : 0f) * -1.6f : 0f;
        _tail.Rotation = new Vector3(lift, 0, swish * (1f - _alert));
    }

    private void PoseWings(bool moving)
    {
        foreach (var (wing, side) in _wings)
        {
            var flap = moving && _speed > 2f ? Mathf.Sin(_clock * 28f) * 0.9f : 0f;
            wing.Rotation = new Vector3(0, 0, side * (Mathf.Max(0f, flap) + 0.05f));
        }
    }
}
