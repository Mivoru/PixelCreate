using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// A carrion crow, built from a few shapes: a black body, a head with a grey beak and a pale eye, a fanned
/// tail and two wings that flap from the shoulders. The server keeps crows on the ground; this view lifts a
/// circling crow high over the grass, raises it higher as it gathers for a dive, brings it down in a steep
/// swoop, and lets it peck on the ground afterwards, which is when it can be struck. A small shadow on the
/// ground shows where it is while it flies.
/// </summary>
public sealed class CrowView : IEnemyView
{
    private float _cawIn;
    private bool _wasFighting;

    private static readonly Color Black = new("181425");
    private static readonly Color Sheen = new("262b44");

    private readonly EnemyDef _def;
    private readonly Node3D _body = new();
    private readonly Node3D _head = new();
    private readonly Node3D _leftWing = new();
    private readonly Node3D _rightWing = new();
    private readonly MeshInstance3D _shadow;
    private readonly StandardMaterial3D _feathers;
    private readonly float _phase = GD.Randf() * Mathf.Tau;
    private float _height;
    private float _pitch;
    private float _roll;
    private float _deadTime;
    private float _lastYaw;
    private EnemyAction _lastAction;
    private short _lastHealth;
    private bool _first = true;

    public Node3D Root { get; } = new();
    public int DamageTaken { get; private set; }

    /// <summary>How high a circling crow flies, in metres.</summary>
    private const float Aloft = 3.6f;

    /// <summary>How high it climbs while it gathers for a dive.</summary>
    private const float Gathering = 4.6f;

    public CrowView(EnemyDef def)
    {
        _def = def;
        _feathers = new StandardMaterial3D { AlbedoColor = Black, Roughness = 0.4f, Metallic = 0.2f };
        var sheen = new StandardMaterial3D { AlbedoColor = Sheen, Roughness = 0.3f };
        var beak = new StandardMaterial3D { AlbedoColor = new Color("5a6988") };
        var eye = new StandardMaterial3D { AlbedoColor = def.Rare ? new Color("e43b44") : new Color("c0cbdc"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        Root.AddChild(_body);

        // The body: a fat teardrop along -Z.
        _body.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f, RadialSegments = 10, Rings = 6 },
            Scale = new Vector3(0.95f, 0.85f, 1.6f),
            MaterialOverride = _feathers,
        });

        _head.Position = new Vector3(0, 0.07f, -0.26f);
        _body.AddChild(_head);
        _head.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.09f, Height = 0.18f, RadialSegments = 8, Rings = 5 },
            MaterialOverride = sheen,
        });
        _head.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.035f, Height = 0.14f, RadialSegments = 6 },
            Position = new Vector3(0, -0.01f, -0.12f),
            Rotation = new Vector3(-Mathf.Pi / 2f - 0.15f, 0, 0),
            MaterialOverride = beak,
        });
        foreach (var side in new[] { -1f, 1f })
        {
            _head.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.018f, Height = 0.036f, RadialSegments = 6, Rings = 3 },
                Position = new Vector3(side * 0.065f, 0.025f, -0.05f),
                MaterialOverride = eye,
            });
        }

        // A fanned tail.
        _body.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.16f, 0.02f, 0.22f) },
            Position = new Vector3(0, 0.01f, 0.3f),
            MaterialOverride = _feathers,
        });

        // Wings that pivot at the shoulder: a broad inner part and a fingered outer part.
        foreach (var (wing, side) in new[] { (_leftWing, -1f), (_rightWing, 1f) })
        {
            wing.Position = new Vector3(side * 0.12f, 0.05f, -0.04f);
            _body.AddChild(wing);
            wing.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.3f, 0.025f, 0.22f) },
                Position = new Vector3(side * 0.15f, 0, 0.02f),
                MaterialOverride = _feathers,
            });
            for (var finger = 0; finger < 3; finger++)
            {
                wing.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.2f, 0.02f, 0.05f) },
                    Position = new Vector3(side * (0.36f + finger * 0.015f), 0, 0.07f - finger * 0.06f),
                    Rotation = new Vector3(0, side * (0.25f - finger * 0.15f), 0),
                    MaterialOverride = finger == 1 ? sheen : _feathers,
                });
            }
        }

        _shadow = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.28f, BottomRadius = 0.28f, Height = 0.02f, RadialSegments = 10 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Black, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, 0.04f, 0),
        };
        Root.AddChild(_shadow);

        Root.Scale = Vector3.One * def.Size * 1.6f;
        _height = Aloft;
    }

    public void Update(in EnemyView enemy, Vector3 position, float delta, Sfx sfx)
    {
        PlaySounds(enemy, position, sfx, delta);
        Root.Position = position;
        var time = (float)Time.GetTicksMsec() / 1000f + _phase;
        var scale = _def.Size * 1.6f;
        var dead = enemy.Action == EnemyAction.Dead;
        var diving = enemy.Action == EnemyAction.Leap;
        var grounded = enemy.Action is EnemyAction.Strike or EnemyAction.Stagger || dead;
        var gathering = enemy.Action == EnemyAction.Cast;

        // Height, in metres over the ground: the scale of the figure must not lift it further.
        var wanted = grounded ? 0.12f : diving ? 0.3f : gathering ? Gathering : Aloft;
        var rate = diving ? 14f : grounded ? 10f : 2.5f;
        _height = Mathf.Lerp(_height, wanted, 1f - Mathf.Exp(-rate * delta));
        var bob = grounded ? 0f : Mathf.Sin(time * 2.3f) * 0.15f;
        _body.Position = new Vector3(0, (_height + bob) / scale + 0.12f, 0);

        // Facing, a lean into the turn while circling, nose down in the dive.
        var turn = Mathf.Wrap(enemy.Facing - _lastYaw, -Mathf.Pi, Mathf.Pi) / Mathf.Max(delta, 0.001f);
        _lastYaw = enemy.Facing;
        _roll = Mathf.Lerp(_roll, grounded ? 0f : Mathf.Clamp(-turn * 0.15f, -0.6f, 0.6f), 1f - Mathf.Exp(-4f * delta));
        _pitch = Mathf.Lerp(_pitch, diving ? -0.9f : gathering ? 0.35f : 0f, 1f - Mathf.Exp(-10f * delta));
        _body.Rotation = new Vector3(_pitch, enemy.Facing, _roll);

        // Wings: a slow flap while circling, fast beats while it hangs gathering, swept back in the dive,
        // folded on the ground with a flutter now and then, and spread limp when dead.
        float flap;
        if (dead)
        {
            flap = -0.2f;
        }
        else if (grounded)
        {
            flap = Mathf.Sin(time * 3f) > 0.95f ? Mathf.Sin(time * 40f) * 0.5f + 0.4f : 1.25f;
        }
        else if (diving)
        {
            flap = 1.0f;
        }
        else if (gathering)
        {
            flap = Mathf.Sin(time * 22f) * 0.7f + 0.1f;
        }
        else
        {
            flap = Mathf.Sin(time * 7f) * 0.55f + 0.1f;
        }

        // Folded on the ground, the wings sweep back along the body.
        var folded = grounded && flap > 1f;
        var sweep = diving ? 0.7f : folded ? 1.35f : 0f;
        _leftWing.Rotation = new Vector3(0, sweep, folded ? 0.1f : -flap);
        _rightWing.Rotation = new Vector3(0, -sweep, folded ? -0.1f : flap);

        // Pecking on the ground.
        _head.Rotation = new Vector3(grounded && !dead ? Mathf.Max(0f, Mathf.Sin(time * 9f)) * -0.7f : 0f, 0, 0);

        // A dead crow lies on its side, then is gone.
        if (dead)
        {
            _deadTime += delta;
            _body.Rotation = new Vector3(0, enemy.Facing, 1.4f);
        }
        else
        {
            _deadTime = 0f;
        }

        Root.Visible = _deadTime < 4f;
        _feathers.AlbedoColor = enemy.HitFlash > 0 ? Colors.White : Black;
        var spread = 1f - Mathf.Clamp(_height / 6f, 0f, 0.6f);
        _shadow.Scale = new Vector3(spread, 1f, spread);
        _shadow.Visible = !dead;
    }

    private void PlaySounds(in EnemyView enemy, Vector3 position, Sfx sfx, float delta)
    {
        DamageTaken = 0;
        if (!_first)
        {
            if (enemy.Health < _lastHealth)
            {
                DamageTaken = _lastHealth - enemy.Health;
                sfx.Play("crow_hurt", position + Vector3.Up * _height, -3f);
            }

            if (enemy.Action != _lastAction)
            {
                var sound = enemy.Action switch
                {
                    EnemyAction.Leap => "crow_dive",
                    EnemyAction.Cast when _def.Abilities is { } list && enemy.Ability < list.Length && list[enemy.Ability].Effect == "rookCall" => "crow_caw",
                    EnemyAction.Dead => "animal_fall",
                    _ => null,
                };
                if (sound is not null)
                {
                    sfx.Play(sound, position + Vector3.Up * _height, enemy.Action == EnemyAction.Leap ? -3f : -2f);
                }
            }

            // A flock caws as it gathers, and now and then while it circles; one bird of every few, not all at once.
            _cawIn -= delta;
            if (enemy.Action != EnemyAction.Dead && enemy.Fighting && (_cawIn <= 0f || !_wasFighting))
            {
                _cawIn = 5f + (float)GD.RandRange(0.0, 7.0);
                if (!_wasFighting || enemy.Id % 3 == 0)
                {
                    sfx.Play("crow_caw", position + Vector3.Up * _height, -5f);
                }
            }

            _wasFighting = enemy.Fighting;
        }

        _first = false;
        _lastAction = enemy.Action;
        _lastHealth = enemy.Health;
    }
}
