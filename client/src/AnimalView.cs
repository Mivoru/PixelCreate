using System.Linq;
using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// An animal from a jointed Blender model (beast_*.glb): body, head, ears, tail and legs or wings.
/// Legs swing in a diagonal gait that quickens into a gallop. What the server says the animal is
/// doing sets the pose: grazing puts the head down to chew, alert lifts it with the ears pricked,
/// idle lets it look around, and a running hen flaps. Hens peck instead of grazing.
/// A wolf is a hunter: it lopes with its head low and its tail out, crouches and bares its teeth
/// before it strikes, and snaps its jaw shut on the bite.
/// </summary>
public sealed class AnimalView : IEnemyView
{
    private static readonly Dictionary<Color, ShaderMaterial> Materials = new();

    private readonly Node3D _body;
    private readonly Node3D _head;
    private readonly Node3D? _neck;
    private readonly Node3D? _tail;
    private readonly Node3D? _jaw;
    private readonly bool _hunter;
    private float _snarl;
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

    /// <summary>A shadow clone is drawn as a cold, see-through copy of its wolf.</summary>
    private readonly bool _ghost;

    /// <summary>A wolf that can sink into the ground: smoke rises off it, and in the ground it is only a shadow.</summary>
    private readonly bool _shade;
    private readonly Node3D _model;
    private readonly MeshInstance3D? _puddle;
    private readonly CpuParticles3D? _aura;
    private readonly CpuParticles3D _puff;
    private EnemyAction _lastAction;
    private float _lastY;
    private float _airPitch;

    public Node3D Root { get; } = new();
    public int DamageTaken { get; private set; }

    public AnimalView(EnemyDef def, int id)
    {
        _kind = def.Id;
        _bird = def.Id == "chicken";
        var model = def.Id switch
        {
            "deer" when id % 2 == 1 => "doe",
            "shadow_clone" => "mirror_wolf",
            _ => def.Id,
        };
        var scene = GD.Load<PackedScene>($"res://assets/beast_{model}.glb").Instantiate<Node3D>();
        Root.AddChild(scene);
        _model = scene;
        _ghost = def.Id == "shadow_clone";
        _shade = def.Abilities?.Any(a => a.Effect == "shadowRun") == true;
        if (_ghost)
        {
            Ghost(scene, Ghostly());
        }
        else
        {
            Tint(scene);
        }

        _puff = new CpuParticles3D
        {
            Emitting = false, OneShot = true, Amount = 28, Lifetime = 0.7f, Explosiveness = 0.9f, LocalCoords = false,
            Direction = Vector3.Up, Spread = 70f, InitialVelocityMin = 1.2f, InitialVelocityMax = 3.2f, Gravity = new Vector3(0, 1.5f, 0),
            ScaleAmountMin = 0.6f, ScaleAmountMax = 1.4f, EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.45f,
            Mesh = new BoxMesh { Size = Vector3.One * 0.11f },
            MaterialOverride = Flat(_ghost ? new Color("8bd3f7") : new Color("181425")),
            Position = new Vector3(0, 0.5f, 0),
        };
        Root.AddChild(_puff);
        if (_ghost || _shade)
        {
            // Wisps that rise off the body all the time: black smoke for the shadow, cold sparks for a clone.
            _aura = new CpuParticles3D
            {
                Amount = _ghost ? 14 : 70, Lifetime = _ghost ? 1.1f : 0.8f, LocalCoords = false,
                Direction = Vector3.Up, Spread = 25f, InitialVelocityMin = 0.3f, InitialVelocityMax = 0.9f, Gravity = new Vector3(0, 0.8f, 0),
                ScaleAmountMin = 0.5f, ScaleAmountMax = 1.6f, EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
                EmissionBoxExtents = _ghost ? new Vector3(0.14f, 0.12f, 0.5f) : new Vector3(0.16f, 0.22f, 0.55f),
                Mesh = new BoxMesh { Size = Vector3.One * (_ghost ? 0.05f : 0.08f) },
                MaterialOverride = Flat(_ghost ? new Color("c0f0ff") : new Color("181425")),
                Position = new Vector3(0, _ghost ? 0.85f : 0.75f, 0),
            };
            Root.AddChild(_aura);
        }

        if (_shade)
        {
            // In the ground the wolf is only this: a dark patch that slides over the grass.
            _puddle = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.55f, Height = 0.02f, RadialSegments = 14, Rings = 1 },
                MaterialOverride = Flat(new Color("101018")),
                Scale = new Vector3(0.9f, 1f, 1.7f),
                Position = new Vector3(0, 0.04f, 0),
                Visible = false,
            };
            Root.AddChild(_puddle);
        }
        _body = Part(scene, "body")!;
        _head = Part(scene, "head")!;
        _neck = Part(scene, "neck");
        _tail = Part(scene, "tail");
        _jaw = Part(scene, "jaw");
        _hunter = def.Kind == "wolf";
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

    private static ShaderMaterial Flat(Color color)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/stipple.gdshader") };
        material.SetShaderParameter("color", color);
        material.SetShaderParameter("strength", 1f);
        return material;
    }

    private static ShaderMaterial? _ghostly;

    private static ShaderMaterial Ghostly()
    {
        if (_ghostly is null)
        {
            _ghostly = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/ghost.gdshader") };
            _ghostly.SetShaderParameter("body", new Color("4f8fba"));
            _ghostly.SetShaderParameter("rim", new Color("c0f0ff"));
            _ghostly.SetShaderParameter("strength", 0.62f);
        }

        return _ghostly;
    }

    /// <summary>Gives every surface of the model the ghost material.</summary>
    private static void Ghost(Node node, ShaderMaterial material)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh is not null)
        {
            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                mesh.SetSurfaceOverrideMaterial(surface, material);
            }
        }

        foreach (var child in node.GetChildren())
        {
            Ghost(child, material);
        }
    }

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
        // A clone does not fall over: it bursts into sparks and is gone.
        Root.Visible = !(dead && _ghost);
        ShadowShow(enemy, position, sfx);

        // In a pounce the body tips up as it rises and down as it drops, legs reaching out.
        var climb = (position.Y - _lastY) / Mathf.Max(delta, 0.001f);
        _lastY = position.Y;
        var leaping = enemy.Action == EnemyAction.Leap && _hunter;
        _airPitch = Mathf.Lerp(_airPitch, leaping ? Mathf.Clamp(-climb * 0.08f, -0.45f, 0.45f) : 0f, 1f - Mathf.Exp(-12f * delta));
        Root.Position = position;
        Root.Rotation = new Vector3(_airPitch, enemy.Facing, _fall * Mathf.Pi / 2f);

        var gallop = Mathf.Clamp(_speed / 5f, 0f, 1f);
        var moving = _speed > 0.3f && !dead;
        _phase += delta * (_bird ? 9f + _speed * 3f : 4f + _speed * 2.2f);
        var swing = moving ? (_bird ? 0.6f : 0.35f + gallop * 0.45f) : 0f;
        var follow = 1f - Mathf.Exp(-20f * delta);
        foreach (var (leg, shin, offset, front) in _legs)
        {
            var target = leaping ? (front ? 0.9f : -0.8f) : Mathf.Sin(_phase + offset) * swing;
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
        if (enemy.Action is EnemyAction.Windup or EnemyAction.Strike && !_hunter)
        {
            _alert = 0f;
            _graze = Mathf.Lerp(_graze, 0.45f, 1f - Mathf.Exp(-12f * delta));
        }

        // A wolf winding up crouches with its teeth bared; on the strike its jaw snaps shut.
        var crouch = 0f;
        if (_hunter)
        {
            var winding = enemy.Action is EnemyAction.Windup or EnemyAction.Cast;
            var striking = enemy.Action is EnemyAction.Strike or EnemyAction.Leap;
            _snarl = Mathf.Lerp(_snarl, winding ? 1f : striking ? 0.2f : enemy.Fighting ? 0.35f : 0f, 1f - Mathf.Exp(-14f * delta));
            crouch = winding ? 1f : 0f;
            if (_jaw is not null)
            {
                _jaw.Rotation = new Vector3(_snarl * 0.55f + (striking ? 0f : 0f), 0, 0);
            }
        }

        var bob = moving ? Mathf.Abs(Mathf.Sin(_phase)) * (_bird ? 0.03f : 0.03f + gallop * 0.08f) : Mathf.Sin(_clock * 1.6f) * 0.006f;
        var shake = enemy.HitFlash > 0 ? (float)GD.RandRange(-0.04, 0.04) : 0f;
        // The crouch tips the shoulders down a little; legs keep their length, so the body barely sinks.
        _body.Position = _bodyRest + new Vector3(shake, bob - crouch * 0.03f * _snarl, 0);
        _body.Rotation = new Vector3(gallop * Mathf.Sin(_phase * 2f) * 0.05f - crouch * _snarl * 0.06f, 0, 0);

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
        if (sfx is null)
        {
            return;
        }

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
            else if (_id % 3 == 0 && !_hunter)
            {
                sfx.Play("hooves", position, -3f);
            }
        }

        if (_kind == "pig" && enemy.Fighting && !_wasFighting)
        {
            sfx.Play("pig_angry", at);
        }

        if (_hunter && enemy.Action is EnemyAction.Windup or EnemyAction.Cast && _snarl < 0.3f && !_ghost)
        {
            sfx.Play("wolf_growl", at, _id % 2 == 0 ? 0f : -3f);
        }

        // Calm animals call out every so often, each on its own clock.
        _calmFor = bolting || alert || dead ? 0f : _calmFor + delta;
        _callTimer -= delta;
        if (_callTimer <= 0f)
        {
            _callTimer = 9f + (float)GD.RandRange(0.0, 18.0);
            if (_calmFor > 4f && !dead && _kind is not ("deer") && (!_hunter || _id % 3 == 0))
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
        (_, "call") when _hunter => "wolf_howl",
        (_, _) when _hunter => "wolf_hurt",
        (_, _) => $"{_kind}_hurt",
    };

    /// <summary>
    /// The shadow wolf sinks into a dark patch that slides over the ground and rises out of it in a
    /// puff of black smoke. Mirrorfang's call bursts cold light; each clone appears in a spray of it.
    /// </summary>
    private void ShadowShow(in EnemyView enemy, Vector3 position, Sfx sfx)
    {
        var action = enemy.Action;
        var hidden = action == EnemyAction.Shadow;
        _model.Visible = !hidden;
        if (_puddle is not null)
        {
            _puddle.Visible = hidden;
        }

        if (_aura is not null)
        {
            _aura.Emitting = action != EnemyAction.Dead;
        }

        var changed = action != _lastAction;
        if (changed && !_first)
        {
            if (_shade && (action == EnemyAction.Shadow || _lastAction == EnemyAction.Shadow))
            {
                _puff.Restart();
                sfx?.Play(action == EnemyAction.Shadow ? "shadow_sink" : "shadow_rise", position + Vector3.Up * 0.5f);
            }

            if (action == EnemyAction.Dead && _ghost)
            {
                _puff.Restart();
            }

            if (action == EnemyAction.Strike && EnemyDef.All[enemy.Type].Abilities is { } list && enemy.Ability < list.Length
                && list[enemy.Ability].Effect == "shadowClone")
            {
                sfx?.Play("clone_summon", position + Vector3.Up);
            }
        }

        if (_ghost && _lastAction == default && _clockStart)
        {
            _puff.Restart();
        }

        _clockStart = false;
        _lastAction = action;
    }

    private bool _clockStart = true;

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
            if (_hunter)
            {
                // A hunter carries its head low and level, lower still when it snarls.
                pitch = -0.25f - gallop * 0.2f - _snarl * 0.35f + _alert * 0.3f;
            }
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

        if (_hunter)
        {
            // Out level behind while running, raised stiff when it snarls, hanging when calm.
            var stiff = Mathf.Max(_snarl, moving ? 0.6f : 0f);
            _tail.Rotation = new Vector3(-stiff * 0.9f, 0, Mathf.Sin(_clock * 2.2f) * 0.12f * (1f - stiff));
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
